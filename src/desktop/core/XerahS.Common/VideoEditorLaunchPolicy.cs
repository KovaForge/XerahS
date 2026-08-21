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

namespace XerahS.Common;

public readonly record struct VideoEditorLaunchPolicy(
    bool AllowInteractiveLaunch,
    bool AllowAutoLaunchAfterCapture,
    bool EnableLinuxWaylandExplicitSyncMitigation,
    string? AutoLaunchBlockedReason = null)
{
    public static VideoEditorLaunchPolicy Default { get; } =
        new(
            AllowInteractiveLaunch: true,
            AllowAutoLaunchAfterCapture: true,
            EnableLinuxWaylandExplicitSyncMitigation: false);
}

public static class VideoEditorLaunchPolicyResolver
{
    private const string WaylandAutoLaunchBlockedReason =
        "Recording saved. Auto-opening the video editor is disabled on Linux Wayland KDE/Plasma because the embedded webview host can terminate the app. Open the editor manually if needed.";

    public static VideoEditorLaunchPolicy GetCurrentPolicy()
    {
        return ResolvePolicy(
            OperatingSystem.IsLinux(),
            Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"),
            Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"),
            Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP"),
            Environment.GetEnvironmentVariable("XDG_SESSION_DESKTOP"),
            Environment.GetEnvironmentVariable("DESKTOP_SESSION"));
    }

    public static VideoEditorLaunchPolicy ResolvePolicy(
        bool isLinux,
        string? sessionType,
        string? waylandDisplay,
        string? currentDesktop,
        string? sessionDesktop,
        string? desktopSession)
    {
        if (!isLinux)
        {
            return VideoEditorLaunchPolicy.Default;
        }

        if (!IsWaylandSession(sessionType, waylandDisplay))
        {
            return VideoEditorLaunchPolicy.Default;
        }

        // Use the GTK explicit-sync mitigation for interactive opens on Wayland.
        // Auto-launch remains blocked on KDE/Plasma because a native crash in the
        // embedded host would still take down the recording process after Stop.
        if (IsKdeLikeDesktop(currentDesktop, sessionDesktop, desktopSession))
        {
            return new VideoEditorLaunchPolicy(
                AllowInteractiveLaunch: true,
                AllowAutoLaunchAfterCapture: false,
                EnableLinuxWaylandExplicitSyncMitigation: true,
                AutoLaunchBlockedReason: WaylandAutoLaunchBlockedReason);
        }

        return new VideoEditorLaunchPolicy(
            AllowInteractiveLaunch: true,
            AllowAutoLaunchAfterCapture: true,
            EnableLinuxWaylandExplicitSyncMitigation: true);
    }

    private static bool IsWaylandSession(string? sessionType, string? waylandDisplay)
    {
        if (string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(waylandDisplay);
    }

    private static bool IsKdeLikeDesktop(string? currentDesktop, string? sessionDesktop, string? desktopSession)
    {
        return ContainsDesktopToken(currentDesktop) ||
               ContainsDesktopToken(sessionDesktop) ||
               ContainsDesktopToken(desktopSession);
    }

    private static bool ContainsDesktopToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains("KDE", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("PLASMA", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("KWIN", StringComparison.OrdinalIgnoreCase);
    }
}
