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

using XerahS.Platform.Abstractions;
using XerahS.Services.Abstractions;

namespace XerahS.Bootstrap
{
    /// <summary>
    /// Explicit platform service instances owned by a desktop host.
    /// </summary>
    public sealed class DesktopPlatformServices
    {
        public DesktopPlatformServices(
            IPlatformInfo platformInfo,
            IScreenService screen,
            IClipboardService clipboard,
            IClipboardMonitorService clipboardMonitor,
            IWindowService window,
            IInputService input,
            IFontService fonts,
            IHotkeyService hotkey,
            IScreenCaptureService screenCapture,
            IStartupService startup,
            ISystemService system,
            IDiagnosticService diagnostic,
            IWatchFolderDaemonService watchFolderDaemon)
        {
            PlatformInfo = platformInfo ?? throw new ArgumentNullException(nameof(platformInfo));
            Screen = screen ?? throw new ArgumentNullException(nameof(screen));
            Clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
            ClipboardMonitor = clipboardMonitor ?? throw new ArgumentNullException(nameof(clipboardMonitor));
            Window = window ?? throw new ArgumentNullException(nameof(window));
            Input = input ?? throw new ArgumentNullException(nameof(input));
            Fonts = fonts ?? throw new ArgumentNullException(nameof(fonts));
            Hotkey = hotkey ?? throw new ArgumentNullException(nameof(hotkey));
            ScreenCapture = screenCapture ?? throw new ArgumentNullException(nameof(screenCapture));
            Startup = startup ?? throw new ArgumentNullException(nameof(startup));
            System = system ?? throw new ArgumentNullException(nameof(system));
            Diagnostic = diagnostic ?? throw new ArgumentNullException(nameof(diagnostic));
            WatchFolderDaemon = watchFolderDaemon ?? throw new ArgumentNullException(nameof(watchFolderDaemon));
        }

        public IPlatformInfo PlatformInfo { get; }
        public IScreenService Screen { get; }
        public IClipboardService Clipboard { get; }
        public IClipboardMonitorService ClipboardMonitor { get; }
        public IWindowService Window { get; }
        public IInputService Input { get; }
        public IFontService Fonts { get; }
        public IHotkeyService Hotkey { get; }
        public IScreenCaptureService ScreenCapture { get; }
        public IStartupService Startup { get; }
        public ISystemService System { get; }
        public IDiagnosticService Diagnostic { get; }
        public IWatchFolderDaemonService WatchFolderDaemon { get; }
        public IShellIntegrationService? ShellIntegration { get; init; }
        public INotificationService? Notification { get; init; }
        public IThemeService? Theme { get; init; }
        public IScrollingCaptureService? ScrollingCapture { get; init; }
        public IOcrService? Ocr { get; init; }
        public IUIService? UI { get; init; }
        public IToastService? Toast { get; init; }
        public IImageEncoderService? ImageEncoder { get; init; }

        /// <summary>
        /// Captures the legacy process-wide platform registry at a compatibility boundary.
        /// New hosts should construct this class directly from the instances they own.
        /// </summary>
        public static DesktopPlatformServices FromCurrentProcess(
            IUIService? uiService = null,
            IToastService? toastService = null,
            IImageEncoderService? imageEncoderService = null)
        {
            if (!PlatformServices.IsInitialized)
            {
                throw new InvalidOperationException("Platform services must be initialized before composing a desktop host.");
            }

            return new DesktopPlatformServices(
                PlatformServices.PlatformInfo,
                PlatformServices.Screen,
                PlatformServices.Clipboard,
                PlatformServices.ClipboardMonitor,
                PlatformServices.Window,
                PlatformServices.Input,
                PlatformServices.Fonts,
                PlatformServices.Hotkey,
                PlatformServices.ScreenCapture,
                PlatformServices.Startup,
                PlatformServices.System,
                PlatformServices.Diagnostic,
                PlatformServices.WatchFolderDaemon)
            {
                ShellIntegration = PlatformServices.GetShellIntegrationIfAvailable(),
                Notification = PlatformServices.GetNotificationIfAvailable(),
                Theme = PlatformServices.IsThemeServiceInitialized ? PlatformServices.Theme : null,
                ScrollingCapture = PlatformServices.ScrollingCapture,
                Ocr = PlatformServices.Ocr,
                UI = uiService,
                Toast = toastService,
                ImageEncoder = imageEncoderService
            };
        }
    }
}
