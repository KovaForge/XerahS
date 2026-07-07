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

using XerahS.Common;

namespace XerahS.Platform.Linux.Services;

/// <summary>
/// Keeps clipboard ownership after app exit via wl-copy owner process (XIP0079 P3).
/// </summary>
public static class LinuxClipboardExitPersistence
{
    private static readonly Lazy<LinuxClipboardService> OwnerService = new(() => new LinuxClipboardService());

    public static bool CanPersist()
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        var environment = LinuxRuntimeEnvironment.Detect();
        if (environment.IsSandboxed)
        {
            return false;
        }

        return LinuxClipboardCapabilities.HasWlCopy;
    }

    public static void PersistText(string text)
    {
        if (!CanPersist() || string.IsNullOrEmpty(text))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await OwnerService.Value.SetTextAsync(text).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "LinuxClipboardExitPersistence: text persist failed");
            }
        });
    }

    public static void PersistImage(byte[] pngBytes)
    {
        if (!CanPersist() || pngBytes == null || pngBytes.Length == 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await OwnerService.Value.SetImageAsync(pngBytes).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "LinuxClipboardExitPersistence: image persist failed");
            }
        });
    }
}
