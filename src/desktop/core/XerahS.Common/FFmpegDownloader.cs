#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System;
using System.IO;
using System.Text.Json;

namespace XerahS.Common
{
    public sealed class FFmpegDownloadResult
    {
        public bool Success { get; }
        public string? FFmpegPath { get; }
        public string? ErrorMessage { get; }
        public string? ExpectedDownloadUrl { get; }

        private FFmpegDownloadResult(bool success, string? ffmpegPath, string? errorMessage, string? expectedDownloadUrl)
        {
            Success = success;
            FFmpegPath = ffmpegPath;
            ErrorMessage = errorMessage;
            ExpectedDownloadUrl = expectedDownloadUrl;
        }

        public static FFmpegDownloadResult CreateSuccess(string ffmpegPath) => new FFmpegDownloadResult(true, ffmpegPath, null, null);

        public static FFmpegDownloadResult CreateFailure(string errorMessage, string? expectedDownloadUrl = null)
            => new FFmpegDownloadResult(false, null, errorMessage, expectedDownloadUrl);
    }

    public static class FFmpegDownloader
    {
        public const string DefaultOwner = "ShareX";
        public const string DefaultRepo = "FFmpeg";
        private const string FFprobeFallbackOwner = "System233";
        private const string FFprobeFallbackRepo = "ffmpeg-msvc-prebuilt";

        public static Task<FFmpegDownloadResult> DownloadLatestToToolsAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            return DownloadLatestAsync(PathsManager.ToolsArchitectureFolder, progress, cancellationToken);
        }

        public static async Task<FFmpegDownloadResult> DownloadLatestAsync(string destinationFolder, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(destinationFolder))
            {
                return FFmpegDownloadResult.CreateFailure("Destination folder was not provided.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return FFmpegDownloadResult.CreateFailure("FFmpeg download was canceled.");
            }

            Directory.CreateDirectory(destinationFolder);

            if (OperatingSystem.IsLinux())
            {
                return await DownloadLinuxAsync(destinationFolder, progress, cancellationToken).ConfigureAwait(false);
            }

            string? downloadedArchive = null;
            FileDownloader? downloader = null;
            Action? detachProgressHandlers = null;

            try
            {
                FFmpegUpdateChecker updateChecker = new FFmpegUpdateChecker(DefaultOwner, DefaultRepo, FFmpegUpdateChecker.ResolveArchitecture());
                // Propagate cancellation into GitHub URL discovery so a cancel
                // mid-lookup does not continue into the archive download.
                string? downloadUrl = await updateChecker.GetLatestDownloadURL(true, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    return FFmpegDownloadResult.CreateFailure("FFmpeg download was canceled.");
                }

                if (string.IsNullOrWhiteSpace(downloadUrl))
                {
                    string expectedUrl = BuildExpectedDownloadUrl(updateChecker);
                    return FFmpegDownloadResult.CreateFailure("FFmpeg download URL could not be resolved.", expectedUrl);
                }

                string versionToken = updateChecker.LatestVersion != null ? updateChecker.LatestVersion.ToString() : "latest";
                string fileName = updateChecker.FileName ?? $"ffmpeg-{versionToken}-{updateChecker.GetExpectedAssetSuffix()}";
                downloadedArchive = Path.Combine(Path.GetTempPath(), fileName);

                downloader = new FileDownloader(downloadUrl, downloadedArchive);
                detachProgressHandlers = AttachProgressHandlers(downloader, progress);
                bool downloadSuccess = await downloader.StartDownload(cancellationToken);
                progress?.Report(100);

                if (cancellationToken.IsCancellationRequested)
                {
                    return FFmpegDownloadResult.CreateFailure("FFmpeg download was canceled.");
                }

                if (!downloadSuccess || !File.Exists(downloadedArchive))
                {
                    return FFmpegDownloadResult.CreateFailure("FFmpeg download failed.");
                }

                ExtractFFmpegBinaries(downloadedArchive, destinationFolder);

                string ffmpegPath = PathsManager.GetFFmpegPath();

                if (string.IsNullOrWhiteSpace(ffmpegPath))
                {
                    return FFmpegDownloadResult.CreateFailure("FFmpeg was downloaded but could not be located.");
                }

                return FFmpegDownloadResult.CreateSuccess(ffmpegPath);
            }
            catch (OperationCanceledException)
            {
                return FFmpegDownloadResult.CreateFailure("FFmpeg download was canceled.");
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return FFmpegDownloadResult.CreateFailure("FFmpeg download was canceled.");
                }
                DebugHelper.WriteException(ex, "FFmpeg download failed.");
                return FFmpegDownloadResult.CreateFailure("FFmpeg download failed.");
            }
            finally
            {
                detachProgressHandlers?.Invoke();

                if (!string.IsNullOrWhiteSpace(downloadedArchive) && File.Exists(downloadedArchive))
                {
                    try
                    {
                        File.Delete(downloadedArchive);
                    }
                    catch
                    {
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    DebugHelper.WriteLine("FFmpeg download was canceled.");
                }
            }
        }

        // Linux builds are published on ShareX/FFmpeg next to the Windows zips, as separate
        // v<version>-linux releases that are never marked latest (so ShareX and Windows XerahS,
        // which read /releases/latest, are unaffected). Each holds ffmpeg-<version>-linux-x64.zip,
        // ffmpeg-<version>-linux-arm64.zip and a signed SHA256SUMS. Everything installs into the
        // user's own tools folder: no package manager, no administrator rights.
        private const string LinuxReleasesApi = "https://api.github.com/repos/" + DefaultOwner + "/" + DefaultRepo + "/releases?per_page=50";

        private static readonly System.Text.RegularExpressions.Regex LinuxReleaseTag = new(
            @"^v(?<version>\d+\.\d+)-linux(\.\d+)?$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        public sealed record GitHubReleaseAsset(string Name, string Url);

        public sealed record GitHubReleaseSummary(
            string Tag, bool Draft, bool Prerelease, DateTimeOffset PublishedAt, IReadOnlyList<GitHubReleaseAsset> Assets);

        public sealed record FFmpegLinuxBuild(
            string Tag, string ZipName, string ZipUrl, string ChecksumsUrl, string SignatureUrl);

        /// <summary>Asset suffix for this CPU, matching the Windows win-x64 / win-arm64 scheme.</summary>
        public static string? GetLinuxRid(System.Runtime.InteropServices.Architecture architecture) => architecture switch
        {
            System.Runtime.InteropServices.Architecture.X64 => "linux-x64",
            System.Runtime.InteropServices.Architecture.Arm64 => "linux-arm64",
            _ => null,
        };

        /// <summary>Newest published Linux release that has a zip for this CPU and a signed checksum file.</summary>
        public static FFmpegLinuxBuild? SelectLinuxRelease(
            IEnumerable<GitHubReleaseSummary> releases,
            System.Runtime.InteropServices.Architecture architecture)
        {
            string? rid = GetLinuxRid(architecture);
            if (rid == null) return null;

            foreach (GitHubReleaseSummary release in releases
                         .Where(r => !r.Draft && !r.Prerelease)
                         .OrderByDescending(r => r.PublishedAt))
            {
                var tag = LinuxReleaseTag.Match(release.Tag);
                if (!tag.Success) continue;

                string zipName = $"ffmpeg-{tag.Groups["version"].Value}-{rid}.zip";
                GitHubReleaseAsset? zip = release.Assets.FirstOrDefault(a => a.Name == zipName);
                GitHubReleaseAsset? sums = release.Assets.FirstOrDefault(a => a.Name == FFmpegReleaseVerifier.ChecksumsFileName);
                GitHubReleaseAsset? sig = release.Assets.FirstOrDefault(a => a.Name == FFmpegReleaseVerifier.SignatureFileName);
                if (zip != null && sums != null && sig != null)
                {
                    return new FFmpegLinuxBuild(release.Tag, zip.Name, zip.Url, sums.Url, sig.Url);
                }
            }

            return null;
        }

        private static async Task<List<GitHubReleaseSummary>?> GetLinuxReleasesAsync(CancellationToken cancellationToken)
        {
            using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, LinuxReleasesApi);
            request.Headers.UserAgent.ParseAdd("XerahS");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await HttpClientFactory.Create().SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                DebugHelper.WriteLine($"[FFmpeg] Listing {DefaultOwner}/{DefaultRepo} releases failed: HTTP {(int)response.StatusCode}");
                return null;
            }

            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            var releases = new List<GitHubReleaseSummary>();
            foreach (JsonElement release in document.RootElement.EnumerateArray())
            {
                var assets = new List<GitHubReleaseAsset>();
                foreach (JsonElement asset in release.GetProperty("assets").EnumerateArray())
                {
                    string? name = asset.GetProperty("name").GetString();
                    string? url = asset.GetProperty("browser_download_url").GetString();
                    if (name != null && url != null) assets.Add(new GitHubReleaseAsset(name, url));
                }

                DateTimeOffset publishedAt = release.TryGetProperty("published_at", out JsonElement published) &&
                                             published.ValueKind == JsonValueKind.String &&
                                             published.TryGetDateTimeOffset(out DateTimeOffset value)
                    ? value
                    : DateTimeOffset.MinValue;
                releases.Add(new GitHubReleaseSummary(
                    release.GetProperty("tag_name").GetString() ?? "",
                    release.GetProperty("draft").GetBoolean(),
                    release.GetProperty("prerelease").GetBoolean(),
                    publishedAt,
                    assets));
            }

            return releases;
        }

        private static async Task<FFmpegDownloadResult> DownloadLinuxAsync(
            string destinationFolder, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            const string releasesPage = "https://github.com/" + DefaultOwner + "/" + DefaultRepo + "/releases";
            string? archive = null;
            FileDownloader? downloader = null;
            Action? detachProgressHandlers = null;
            try
            {
                List<GitHubReleaseSummary>? releases = await GetLinuxReleasesAsync(cancellationToken).ConfigureAwait(false);
                if (releases == null)
                {
                    return FFmpegDownloadResult.CreateFailure("Could not list FFmpeg builds from GitHub.", releasesPage);
                }

                var architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;
                if (SelectLinuxRelease(releases, architecture) is not { } build)
                {
                    return FFmpegDownloadResult.CreateFailure($"No FFmpeg build is available for {architecture} Linux.", releasesPage);
                }

                HttpClient http = HttpClientFactory.Create();
                byte[] checksums = await http.GetByteArrayAsync(build.ChecksumsUrl, cancellationToken).ConfigureAwait(false);
                byte[] signature = await http.GetByteArrayAsync(build.SignatureUrl, cancellationToken).ConfigureAwait(false);
                if (!FFmpegReleaseVerifier.IsSignatureValid(checksums, signature))
                {
                    DebugHelper.WriteLine($"[FFmpeg] {build.Tag}: {FFmpegReleaseVerifier.SignatureFileName} does not match a trusted key.");
                    return FFmpegDownloadResult.CreateFailure("The FFmpeg download could not be verified and was not installed.", build.ZipUrl);
                }

                string? expectedHash = FFmpegReleaseVerifier.FindHash(checksums, build.ZipName);
                if (expectedHash == null)
                {
                    DebugHelper.WriteLine($"[FFmpeg] {build.Tag}: {build.ZipName} is not listed in {FFmpegReleaseVerifier.ChecksumsFileName}.");
                    return FFmpegDownloadResult.CreateFailure("The FFmpeg download could not be verified and was not installed.", build.ZipUrl);
                }

                archive = Path.Combine(Path.GetTempPath(), $"xerahs-{Guid.NewGuid():N}-{build.ZipName}");
                downloader = new FileDownloader(build.ZipUrl, archive);
                detachProgressHandlers = AttachProgressHandlers(downloader, progress);
                if (!await downloader.StartDownload(cancellationToken).ConfigureAwait(false) || !File.Exists(archive))
                {
                    return FFmpegDownloadResult.CreateFailure("FFmpeg download failed.", build.ZipUrl);
                }
                progress?.Report(100);

                if (!await FFmpegReleaseVerifier.IsFileHashValidAsync(archive, expectedHash, cancellationToken).ConfigureAwait(false))
                {
                    DebugHelper.WriteLine($"[FFmpeg] {build.ZipName}: SHA-256 does not match the signed {FFmpegReleaseVerifier.ChecksumsFileName}.");
                    return FFmpegDownloadResult.CreateFailure("The FFmpeg download could not be verified and was not installed.", build.ZipUrl);
                }

                ExtractExecutables(archive, destinationFolder, "ffmpeg", "ffprobe");
                foreach (string binary in new[] { "ffmpeg", "ffprobe" })
                {
                    if (!File.Exists(Path.Combine(destinationFolder, binary)))
                    {
                        return FFmpegDownloadResult.CreateFailure($"The FFmpeg archive did not contain {binary}.", build.ZipUrl);
                    }
                }

                DebugHelper.WriteLine($"[FFmpeg] Installed {build.ZipName} ({build.Tag}, signature verified) to {destinationFolder}");
                return FFmpegDownloadResult.CreateSuccess(Path.Combine(destinationFolder, "ffmpeg"));
            }
            catch (OperationCanceledException)
            {
                return FFmpegDownloadResult.CreateFailure("FFmpeg download was canceled.");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "FFmpeg download failed.");
                return FFmpegDownloadResult.CreateFailure($"FFmpeg download failed: {ex.Message}");
            }
            finally
            {
                detachProgressHandlers?.Invoke();
                try
                {
                    if (archive != null && File.Exists(archive)) File.Delete(archive);
                }
                catch
                {
                    // Temp file; the OS cleans it eventually.
                }
            }
        }

        public static async Task<string?> DownloadFFprobeFallbackAsync(
            string destinationFolder,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(destinationFolder))
            {
                return null;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            string ffprobeBinary = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            Directory.CreateDirectory(destinationFolder);

            string? downloadedArchive = null;
            FileDownloader? downloader = null;
            Action? detachProgressHandlers = null;

            try
            {
                string? downloadUrl = await GetFFprobeFallbackDownloadUrlAsync(cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }

                if (string.IsNullOrWhiteSpace(downloadUrl))
                {
                    return null;
                }

                downloadedArchive = Path.Combine(
                    Path.GetTempPath(),
                    Path.GetFileName(new Uri(downloadUrl).AbsolutePath));

                downloader = new FileDownloader(downloadUrl, downloadedArchive);
                detachProgressHandlers = AttachProgressHandlers(downloader, progress);
                bool downloadSuccess = await downloader.StartDownload(cancellationToken);
                progress?.Report(100);

                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }

                if (!downloadSuccess || !File.Exists(downloadedArchive))
                {
                    return null;
                }

                ExtractExecutables(downloadedArchive, destinationFolder, ffprobeBinary);

                string extractedFFprobePath = Path.Combine(destinationFolder, ffprobeBinary);
                return File.Exists(extractedFFprobePath) ? extractedFFprobePath : null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }
                DebugHelper.WriteException(ex, "FFprobe fallback download failed.");
                return null;
            }
            finally
            {
                detachProgressHandlers?.Invoke();

                if (!string.IsNullOrWhiteSpace(downloadedArchive) && File.Exists(downloadedArchive))
                {
                    try
                    {
                        File.Delete(downloadedArchive);
                    }
                    catch
                    {
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    DebugHelper.WriteLine("FFprobe fallback download was canceled.");
                }
            }
        }

        private static void ExtractFFmpegBinaries(string archivePath, string destinationFolder)
        {
            string ffmpegBinary = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
            string ffprobeBinary = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";

            ExtractExecutables(archivePath, destinationFolder, ffmpegBinary, ffprobeBinary);
        }

        private static void EnsureExecutable(string path)
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            try
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to set FFmpeg executable permissions.");
            }
        }

        private static Action? AttachProgressHandlers(FileDownloader downloader, IProgress<double>? progress)
        {
            if (progress == null)
            {
                return null;
            }

            void ReportProgress()
            {
                if (downloader.FileSize > 0)
                {
                    progress.Report(Math.Clamp(downloader.DownloadPercentage, 0, 100));
                }
            }

            downloader.FileSizeReceived += ReportProgress;
            downloader.ProgressChanged += ReportProgress;

            return () =>
            {
                downloader.FileSizeReceived -= ReportProgress;
                downloader.ProgressChanged -= ReportProgress;
            };
        }

        private static string BuildExpectedDownloadUrl(FFmpegUpdateChecker updateChecker)
        {
            string versionToken = updateChecker.LatestVersion != null ? updateChecker.LatestVersion.ToString() : "{version}";
            string suffix = updateChecker.GetExpectedAssetSuffix();
            string fileName = $"ffmpeg-{versionToken}-{suffix}";
            return $"https://github.com/{updateChecker.Owner}/{updateChecker.Repo}/releases/download/v{versionToken}/{fileName}";
        }

        private static void ExtractExecutables(string archivePath, string destinationFolder, params string[] executableNames)
        {
            HashSet<string> executableSet = new HashSet<string>(executableNames, StringComparer.OrdinalIgnoreCase);

            ZipManager.Extract(
                archivePath,
                destinationFolder,
                retainDirectoryStructure: false,
                filter: entry => executableSet.Contains(entry.Name));

            foreach (string executableName in executableNames)
            {
                string extractedExecutablePath = Path.Combine(destinationFolder, executableName);
                if (File.Exists(extractedExecutablePath))
                {
                    EnsureExecutable(extractedExecutablePath);
                }
            }
        }

        private static Task<string?> GetFFprobeFallbackDownloadUrlAsync()
        {
            return GetFFprobeFallbackDownloadUrlAsync(CancellationToken.None);
        }

        private static async Task<string?> GetFFprobeFallbackDownloadUrlAsync(CancellationToken cancellationToken)
        {
            string response = await WebHelpers.DownloadStringAsync(
                $"https://api.github.com/repos/{FFprobeFallbackOwner}/{FFprobeFallbackRepo}/releases/latest",
                cancellationToken);
            if (string.IsNullOrWhiteSpace(response))
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            using JsonDocument document = JsonDocument.Parse(response);
            if (!document.RootElement.TryGetProperty("assets", out JsonElement assetsElement))
            {
                return null;
            }

            string[] preferredSuffixes = GetFFprobeFallbackAssetSuffixes();

            foreach (string suffix in preferredSuffixes)
            {
                foreach (JsonElement assetElement in assetsElement.EnumerateArray())
                {
                    if (!assetElement.TryGetProperty("name", out JsonElement nameElement) ||
                        !assetElement.TryGetProperty("browser_download_url", out JsonElement urlElement))
                    {
                        continue;
                    }

                    string? assetName = nameElement.GetString();
                    if (!string.IsNullOrWhiteSpace(assetName) &&
                        assetName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        return urlElement.GetString();
                    }
                }
            }

            return null;
        }

        private static string[] GetFFprobeFallbackAssetSuffixes()
        {
            return FFmpegUpdateChecker.ResolveArchitecture() switch
            {
                FFmpegArchitecture.winArm64 =>
                [
                    "-gpl-arm64-static.zip",
                    "-lgpl-arm64-static.zip",
                    "-gpl-arm64-shared.zip",
                    "-lgpl-arm64-shared.zip"
                ],
                FFmpegArchitecture.win64 =>
                [
                    "-gpl-amd64-static.zip",
                    "-lgpl-amd64-static.zip",
                    "-gpl-amd64-shared.zip",
                    "-lgpl-amd64-shared.zip"
                ],
                _ =>
                [
                    "-gpl-x86-static.zip",
                    "-lgpl-x86-static.zip",
                    "-gpl-x86-shared.zip",
                    "-lgpl-x86-shared.zip"
                ]
            };
        }
    }
}
