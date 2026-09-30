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
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using XerahS.Common;
using static XerahS.Common.FFmpegDownloader;

namespace XerahS.Tests.Common;

public class FFmpegLinuxDownloadTests
{
    private static readonly DateTimeOffset Day = new(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);

    private static GitHubReleaseSummary Release(string tag, int day, bool prerelease = false, bool draft = false, params string[] names) =>
        new(tag, draft, prerelease, Day.AddDays(day), names.Select(n => new GitHubReleaseAsset(n, $"u/{tag}/{n}")).ToList());

    private static readonly string[] Signed81 =
        ["ffmpeg-8.1-linux-x64.zip", "ffmpeg-8.1-linux-arm64.zip", "SHA256SUMS", "SHA256SUMS.sig"];

    [Test]
    public void SelectsTheNewestSignedLinuxReleaseForTheCpu()
    {
        GitHubReleaseSummary[] releases =
        [
            Release("v8.1", 0, names: ["ffmpeg-8.1-win-x64.zip", "ffmpeg-8.1-win-arm64.zip"]),
            Release("v8.1-linux", 1, names: Signed81),
            Release("v8.1-linux.2", 2, names: Signed81),
            Release("v9.0-linux", 3, prerelease: true, names: ["ffmpeg-9.0-linux-x64.zip", "SHA256SUMS", "SHA256SUMS.sig"]),
            Release("v9.1-linux", 4, draft: true, names: ["ffmpeg-9.1-linux-x64.zip", "SHA256SUMS", "SHA256SUMS.sig"]),
            Release("v9.2-linux", 5, names: ["ffmpeg-9.2-linux-x64.zip", "SHA256SUMS"]), // unsigned
        ];

        FFmpegLinuxBuild? x64 = SelectLinuxRelease(releases, Architecture.X64);
        Assert.That(x64?.Tag, Is.EqualTo("v8.1-linux.2"));
        Assert.That(x64?.ZipName, Is.EqualTo("ffmpeg-8.1-linux-x64.zip"));
        Assert.That(x64?.SignatureUrl, Is.EqualTo("u/v8.1-linux.2/SHA256SUMS.sig"));
        Assert.That(SelectLinuxRelease(releases, Architecture.Arm64)?.ZipName, Is.EqualTo("ffmpeg-8.1-linux-arm64.zip"));
        Assert.That(SelectLinuxRelease(releases, Architecture.X86), Is.Null);
    }

    [Test]
    public void IgnoresTheWindowsReleases()
    {
        GitHubReleaseSummary[] releases = [Release("v8.1", 0, names: ["ffmpeg-8.1-win-x64.zip", "SHA256SUMS", "SHA256SUMS.sig"])];
        Assert.That(SelectLinuxRelease(releases, Architecture.X64), Is.Null);
    }

    [Test]
    public void AcceptsOnlyChecksumsSignedByATrustedKey()
    {
        using ECDsa trusted = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string[] keys = [trusted.ExportSubjectPublicKeyInfoPem()];
        byte[] sums = Encoding.UTF8.GetBytes($"{new string('a', 64)}  ffmpeg-8.1-linux-x64.zip\n");
        byte[] signature = trusted.SignData(sums, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        Assert.That(FFmpegReleaseVerifier.IsSignatureValid(sums, signature, keys), Is.True);
        Assert.That(FFmpegReleaseVerifier.IsSignatureValid(sums, other.SignData(sums, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence), keys), Is.False);

        byte[] tampered = (byte[])sums.Clone();
        tampered[0] = (byte)'b';
        Assert.That(FFmpegReleaseVerifier.IsSignatureValid(tampered, signature, keys), Is.False);
    }

    [Test]
    public void PinnedKeyIsAValidP256PublicKey()
    {
        Assert.That(FFmpegReleaseVerifier.TrustedKeys, Is.Not.Empty);
        foreach (string pem in FFmpegReleaseVerifier.TrustedKeys)
        {
            using ECDsa key = ECDsa.Create();
            key.ImportFromPem(pem);
            Assert.That(key.KeySize, Is.EqualTo(256));
        }
    }

    [Test]
    public void FindsTheHashForTheExactFileName()
    {
        string x64 = new('A', 64), arm = new('b', 64);
        byte[] sums = Encoding.UTF8.GetBytes($"{x64}  ffmpeg-8.1-linux-x64.zip\r\n{arm} *ffmpeg-8.1-linux-arm64.zip\n");

        Assert.That(FFmpegReleaseVerifier.FindHash(sums, "ffmpeg-8.1-linux-x64.zip"), Is.EqualTo(new string('a', 64)));
        Assert.That(FFmpegReleaseVerifier.FindHash(sums, "ffmpeg-8.1-linux-arm64.zip"), Is.EqualTo(arm));
        Assert.That(FFmpegReleaseVerifier.FindHash(sums, "ffmpeg-8.1-linux"), Is.Null);
    }

    [Test]
    public async Task ChecksTheDownloadedFileHash()
    {
        string path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "ffmpeg");
            string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("ffmpeg")));
            Assert.That(await FFmpegReleaseVerifier.IsFileHashValidAsync(path, hash), Is.True);
            Assert.That(await FFmpegReleaseVerifier.IsFileHashValidAsync(path, new string('0', 64)), Is.False);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void LinuxToolsFolderFollowsTheAssetNames()
    {
        if (!OperatingSystem.IsLinux()) Assert.Ignore("Linux only.");
        Assert.That(PathsManager.GetToolsArchitectureFolderName(Architecture.X64), Is.EqualTo("linux-x64"));
        Assert.That(PathsManager.GetToolsArchitectureFolderName(Architecture.Arm64), Is.EqualTo("linux-arm64"));
        Assert.That(PathsManager.CurrentArchitectureFolderName, Is.EqualTo("linux64"), "plugins keep their folder");
    }

    [Test]
    [Explicit("Downloads about 60 MB from GitHub.")]
    [Category("Network")]
    public async Task DownloadsAVerifiedFFmpegIntoAUserFolder()
    {
        if (!OperatingSystem.IsLinux()) Assert.Ignore("Linux only.");
        string folder = Path.Combine(Path.GetTempPath(), "xerahs-ffmpeg-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            FFmpegDownloadResult result = await DownloadLatestAsync(folder);
            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.FFmpegPath, Is.EqualTo(Path.Combine(folder, "ffmpeg")));

            using var process = Process.Start(new ProcessStartInfo(result.FFmpegPath!, "-hide_banner -encoders")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
            })!;
            string encoders = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            Assert.That(encoders, Does.Contain("libx264").And.Contain("libvpx-vp9"));
            Assert.That(File.Exists(Path.Combine(folder, "ffprobe")), Is.True);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }
}
