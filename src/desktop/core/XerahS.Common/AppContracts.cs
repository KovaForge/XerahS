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

/// <summary>
/// Central repository for all application-level named contracts: IPC channel names,
/// command-line flags, well-known workflow IDs, and D-Bus/XDG desktop integration keys.
/// Having these in one place lets agents search semantically (grep "SingleInstance")
/// instead of hunting for raw GUIDs or magic strings scattered across entry points.
/// </summary>
public static class AppContracts
{
    /// <summary>
    /// Single-instance enforcement: named mutex and pipe used to coordinate
    /// a single running instance of the application across all entry points.
    /// </summary>
    public static class SingleInstance
    {
        /// <summary>Named mutex ensuring only one instance acquires the primary lock.</summary>
        public const string MutexName = "XerahS-82E6AC09-0FFC-4992-B793-3F79E1F71E70";

        /// <summary>Named pipe used to relay command-line arguments from subsequent instances to the primary instance.</summary>
        public const string PipeName = "XerahS-Pipe-1F42DA49-7B2A-4E6F-8A3C-D56F09E0C481";
    }

    /// <summary>
    /// Command-line flags recognised by the application and CLI entry points.
    /// </summary>
    public static class Cli
    {
        /// <summary>Flag used by helper processes (e.g. screen capture helpers) to forward a capture back to the running instance.</summary>
        public const string SendToFlag = "--send-to";

        /// <summary>
        /// Overrides the app personal/settings folder for the current process.
        /// Intended for isolated debug and tooling scenarios.
        /// </summary>
        public const string SettingsFolderFlag = "--settings-folder";

        /// <summary>Legacy flag for installing a plugin directly from the CLI (superseded by the plugin exporter tool).</summary>
        public const string LegacyInstallPluginFlag = "-InstallPlugin";

        /// <summary>
        /// Default workflow ID used by <c>verify-recording</c> command when no workflow is specified.
        /// This is the built-in "Screen recording" workflow shipped with XerahS.
        /// </summary>
        public const string DefaultRecordingWorkflowId = "67f116dc";
    }

    /// <summary>
    /// Desktop integration constants for Linux (XDG, D-Bus, Thunar, KDE, etc.).
    /// </summary>
    public static class LinuxIntegration
    {
        /// <summary>D-Bus header key used to mark a message as a SendTo relay from a helper process.</summary>
        public const string SendToMarkerKey = "X-XerahS-SendTo";

        /// <summary>Value associated with <see cref="SendToMarkerKey"/> when the relay is active.</summary>
        public const string SendToMarkerValue = "true";
    }
}
