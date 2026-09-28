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

namespace XerahS.Common
{
    public enum LinuxDisplayState
    {
        /// <summary>An X11 display XerahS can connect to (or a display we cannot verify, such as TCP).</summary>
        Ready,
        /// <summary>DISPLAY names a local X server whose socket does not exist yet (XWayland still starting).</summary>
        WaitingForX11,
        /// <summary>A Wayland session with no DISPLAY at all: XWayland is disabled or not started.</summary>
        WaylandWithoutX11,
        /// <summary>No display environment at all.</summary>
        NoDisplay
    }

    /// <summary>
    /// Decides whether the X11 display XerahS's UI toolkit needs is usable. On a pure Wayland
    /// session (Hyprland/Omarchy) XerahS runs through XWayland; at autostart the X socket can
    /// appear a moment after XerahS starts, which used to crash startup with "XOpenDisplay failed".
    /// </summary>
    public static class LinuxDisplayReadiness
    {
        public static readonly TimeSpan DefaultX11WaitTimeout = TimeSpan.FromSeconds(20);

        public static LinuxDisplayState Evaluate(string? display, string? waylandDisplay, Func<string, bool> socketExists)
        {
            ArgumentNullException.ThrowIfNull(socketExists);
            if (string.IsNullOrWhiteSpace(display))
            {
                return string.IsNullOrWhiteSpace(waylandDisplay) ? LinuxDisplayState.NoDisplay : LinuxDisplayState.WaylandWithoutX11;
            }

            string? socket = GetLocalX11SocketPath(display);
            if (socket == null)
            {
                // Remote/TCP display (e.g. SSH forwarding): nothing local to wait for.
                return LinuxDisplayState.Ready;
            }

            return socketExists(socket) ? LinuxDisplayState.Ready : LinuxDisplayState.WaitingForX11;
        }

        /// <summary>
        /// Socket path for a local display such as ":0", ":1.0" or "unix:0"; null for a remote
        /// display ("host:10.0") or a value that does not parse.
        /// </summary>
        public static string? GetLocalX11SocketPath(string? display)
        {
            if (string.IsNullOrWhiteSpace(display))
            {
                return null;
            }

            string value = display.Trim();
            int colon = value.LastIndexOf(':');
            if (colon < 0 || colon == value.Length - 1)
            {
                return null;
            }

            string host = value[..colon];
            if (host.Length > 0 && !host.Equals("unix", StringComparison.Ordinal))
            {
                return null;
            }

            string token = value[(colon + 1)..];
            int dot = token.IndexOf('.');
            if (dot >= 0)
            {
                token = token[..dot];
            }

            return int.TryParse(token, out int number) && number >= 0 ? $"/tmp/.X11-unix/X{number}" : null;
        }

        /// <summary>Blocks startup (before any UI exists) until the X socket appears or the timeout passes.</summary>
        public static LinuxDisplayState WaitForX11(string? display, string? waylandDisplay, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            LinuxDisplayState state = Evaluate(display, waylandDisplay, File.Exists);
            while (state == LinuxDisplayState.WaitingForX11 && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(250);
                state = Evaluate(display, waylandDisplay, File.Exists);
            }

            return state;
        }

        /// <summary>User-facing explanation for a display XerahS cannot use.</summary>
        public static string DescribeProblem(LinuxDisplayState state, string? display) => state switch
        {
            LinuxDisplayState.WaitingForX11 =>
                $"XerahS could not start: the X server for DISPLAY={display} (XWayland) did not become available. Make sure XWayland is enabled in your compositor, then start XerahS again.",
            LinuxDisplayState.WaylandWithoutX11 =>
                "XerahS could not start: this Wayland session has no XWayland display (DISPLAY is not set). XerahS needs XWayland; enable it in your compositor and start XerahS again.",
            LinuxDisplayState.NoDisplay =>
                "XerahS could not start: no display was found (neither DISPLAY nor WAYLAND_DISPLAY is set).",
            _ => string.Empty
        };
    }
}
