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
using System.Text;
using XerahS.Common;

namespace XerahS.UI.Services;

internal static class MacOSUploadFilePicker
{
    internal const int UserCanceledExitCode = 1;

    public static async Task<string?> PickFileAsync()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = CreateStartInfo()
            };

            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    output.AppendLine(e.Data);
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    error.AppendLine(e.Data);
                }
            };

            if (!process.Start())
            {
                return null;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync().ConfigureAwait(false);

            string selectedPath = NormalizeOutputPath(output.ToString());
            if (process.ExitCode == 0)
            {
                return File.Exists(selectedPath) ? selectedPath : null;
            }

            if (process.ExitCode != UserCanceledExitCode || !IsUserCanceled(error.ToString()))
            {
                DebugHelper.WriteLine($"macOS upload file picker failed: exit={process.ExitCode}, error={error}");
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "macOS upload file picker failed");
        }

        return null;
    }

    internal static ProcessStartInfo CreateStartInfo()
    {
        var startInfo = new ProcessStartInfo("osascript")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (string expression in CreateChooseFileScript())
        {
            startInfo.ArgumentList.Add("-e");
            startInfo.ArgumentList.Add(expression);
        }

        return startInfo;
    }

    internal static string[] CreateChooseFileScript() =>
    [
        "set selectedFile to choose file with prompt \"Select File to Upload\" default location (path to desktop folder)",
        "POSIX path of selectedFile"
    ];

    internal static string NormalizeOutputPath(string output) => output.Trim();

    internal static bool IsUserCanceled(string errorOutput) =>
        errorOutput.Contains("User canceled.", StringComparison.OrdinalIgnoreCase) ||
        errorOutput.Contains("(-128)", StringComparison.OrdinalIgnoreCase);
}
