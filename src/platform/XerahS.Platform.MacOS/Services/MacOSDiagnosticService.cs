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

using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using XerahS.Platform.Abstractions;
using XerahS.Platform.MacOS.Native;
using DebugHelper = XerahS.Common.DebugHelper;

namespace XerahS.Platform.MacOS.Services
{
    /// <summary>
    /// XIP0078 P3: macOS capture/recording diagnostics. Reports the states users most often
    /// misconfigure (Screen Recording TCC, Accessibility, bridge availability, bundle vs
    /// bare-binary execution) so issue reports carry actionable data.
    /// </summary>
    public class MacOSDiagnosticService : IDiagnosticService
    {
        private const string FolderName = "CaptureTroubleshooting";

        public string WriteRegionCaptureDiagnostics(string personalFolder)
        {
            return WriteReport(personalFolder, "macos-capture-diagnostics", includeRecording: false);
        }

        public string WriteRecordingDiagnostics(string personalFolder)
        {
            return WriteReport(personalFolder, "macos-recording-diagnostics", includeRecording: true);
        }

        private static string WriteReport(string personalFolder, string filePrefix, bool includeRecording)
        {
            if (!OperatingSystem.IsMacOS())
            {
                return string.Empty;
            }

            try
            {
                string folder = Path.Combine(personalFolder, FolderName);
                Directory.CreateDirectory(folder);

                string fileName = $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.log";
                string filePath = Path.Combine(folder, fileName);

                File.WriteAllText(filePath, BuildReport(includeRecording), Encoding.UTF8);
                DebugHelper.WriteLine($"[MacOSDiagnostics] Wrote diagnostics to {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "[MacOSDiagnostics] Failed to write diagnostics");
                return string.Empty;
            }
        }

        private static string BuildReport(bool includeRecording)
        {
            var sb = new StringBuilder();
            sb.AppendLine("XerahS macOS diagnostics");
            sb.AppendLine("========================");
            sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
            sb.AppendLine($"App version: {Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown"}");
            sb.AppendLine($"OS: {RuntimeInformation.OSDescription}");
            sb.AppendLine($"Architecture: {RuntimeInformation.ProcessArchitecture}");
            sb.AppendLine();

            string baseDirectory = AppContext.BaseDirectory;
            bool runningFromBundle = baseDirectory.Contains(".app/Contents/MacOS", StringComparison.Ordinal);
            sb.AppendLine("Execution");
            sb.AppendLine("---------");
            sb.AppendLine($"Base directory: {baseDirectory}");
            sb.AppendLine($"Process path: {Environment.ProcessPath ?? "unknown"}");
            sb.AppendLine($"Running from .app bundle: {runningFromBundle}");
            if (!runningFromBundle)
            {
                sb.AppendLine("NOTE: When running a bare binary (e.g. dotnet run), macOS attributes TCC permissions");
                sb.AppendLine("      to the terminal/dotnet host, not XerahS. Test permissions from the published bundle.");
            }
            sb.AppendLine();

            sb.AppendLine("Permissions (TCC)");
            sb.AppendLine("-----------------");
            sb.AppendLine($"Screen Recording granted: {SafeProbe(() => ScreenRecordingPermission.IsGranted())}");
            sb.AppendLine($"Accessibility trusted: {SafeProbe(() => Accessibility.IsProcessTrusted(prompt: false))}");
            sb.AppendLine();

            sb.AppendLine("ScreenCaptureKit bridge");
            sb.AppendLine("-----------------------");
            string dylibPath = Path.Combine(baseDirectory, ScreenCaptureKitInterop.LibraryName + ".dylib");
            sb.AppendLine($"Bridge file present: {File.Exists(dylibPath)} ({dylibPath})");
            sb.AppendLine($"Bridge loadable: {SafeProbe(() => ScreenCaptureKitInterop.TryLoad())}");
            sb.AppendLine($"ScreenCaptureKit available (macOS 12.3+): {SafeProbe(() => ScreenCaptureKitInterop.IsAvailable() == 1)}");
            sb.AppendLine();

            if (includeRecording)
            {
                sb.AppendLine("Recording");
                sb.AppendLine("---------");
                sb.AppendLine($"Native ScreenCaptureKit recording available: {SafeProbe(MacOSNativeRecordingService.IsAvailable)}");
                sb.AppendLine($"ffmpeg on PATH: {FindOnPath("ffmpeg") ?? "not found"}");
                sb.AppendLine();
            }

            string tmpDir = Environment.GetEnvironmentVariable("TMPDIR") ?? "<not set>";
            sb.AppendLine("Environment");
            sb.AppendLine("-----------");
            sb.AppendLine($"TMPDIR: {tmpDir} (length {tmpDir.Length})");
            sb.AppendLine("NOTE: TMPDIR paths near the 104-char Unix socket limit can break single-instance detection.");

            return sb.ToString();
        }

        private static string SafeProbe(Func<bool> probe)
        {
            try
            {
                return probe().ToString();
            }
            catch (Exception ex)
            {
                return $"probe failed ({ex.GetType().Name})";
            }
        }

        private static string? FindOnPath(string fileName)
        {
            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv))
            {
                return null;
            }

            foreach (string dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string candidate = Path.Combine(dir, fileName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                    // Ignore malformed PATH entries.
                }
            }

            return null;
        }
    }
}
