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
using XerahS.Platform.Linux.Capture.Contracts;
using XerahS.Platform.Linux.Capture.Wayland;

namespace XerahS.Platform.Linux.Capture.Detection;

internal static class LinuxRegionCaptureCapabilityDetector
{
    public static LinuxRegionCaptureCapability Detect()
    {
        return Detect(LinuxRuntimeContextDetector.Detect());
    }

    public static LinuxRegionCaptureCapability Detect(ILinuxCaptureContext context)
    {
        return Detect(context, ProbeSupportSnapshot(context));
    }

    internal static LinuxRegionCaptureCapability Detect(
        ILinuxCaptureContext context,
        LinuxRegionCaptureSupportSnapshot support)
    {
        if (context.IsSandboxed)
        {
            return context.HasScreenshotPortal
                ? new LinuxRegionCaptureCapability(
                    SupportsNativeRegionCapture: true,
                    SupportsLegacyOverlayCapture: false,
                    Reason: "Sandboxed Linux session requires the XDG Screenshot portal.")
                : new LinuxRegionCaptureCapability(
                    SupportsNativeRegionCapture: false,
                    SupportsLegacyOverlayCapture: false,
                    Reason: "Sandboxed Linux session has no screenshot portal available.");
        }

        if (context.IsWayland)
        {
            if (context.HasScreenshotPortal)
            {
                return new LinuxRegionCaptureCapability(
                    SupportsNativeRegionCapture: true,
                    SupportsLegacyOverlayCapture: true,
                    Reason: "Wayland region capture can use the XDG Screenshot portal. XerahS overlay selection is available with portal-backed capture.");
            }

            if (support.HasSlurp)
            {
                return new LinuxRegionCaptureCapability(
                    SupportsNativeRegionCapture: true,
                    SupportsLegacyOverlayCapture: false,
                    Reason: "Wayland region capture can use wlroots-compatible CLI tools.");
            }

            return new LinuxRegionCaptureCapability(
                SupportsNativeRegionCapture: false,
                SupportsLegacyOverlayCapture: false,
                Reason: "Wayland session has no supported modern region selector.");
        }

        return context.Desktop switch
        {
            "GNOME" or "CINNAMON" or "MATE" when support.HasGnomeShellScreenshot =>
                new LinuxRegionCaptureCapability(
                    SupportsNativeRegionCapture: true,
                    SupportsLegacyOverlayCapture: true,
                    Reason: "X11 desktop exposes org.gnome.Shell.Screenshot for native region capture."),
            "KDE" or "LXQT" when support.HasKdeScreenShot2 =>
                new LinuxRegionCaptureCapability(
                    SupportsNativeRegionCapture: true,
                    SupportsLegacyOverlayCapture: true,
                    Reason: "X11 desktop exposes org.kde.KWin.ScreenShot2 for native region capture."),
            _ when support.HasKnownGoodX11PortalBackend =>
                new LinuxRegionCaptureCapability(
                    SupportsNativeRegionCapture: true,
                    SupportsLegacyOverlayCapture: true,
                    Reason: $"X11 desktop can use the {support.X11PortalBackendLabel ?? "desktop-matched"} XDG portal backend for native region capture."),
            _ => new LinuxRegionCaptureCapability(
                SupportsNativeRegionCapture: false,
                SupportsLegacyOverlayCapture: true,
                Reason: $"X11 desktop '{context.Desktop ?? "unknown"}' will use the XerahS overlay fallback.")
        };
    }

    internal static LinuxRegionCaptureSupportSnapshot ProbeSupportSnapshot(ILinuxCaptureContext context)
    {
        bool hasGnomeShellScreenshot = false;
        bool hasKdeScreenShot2 = false;
        bool hasSlurp = false;
        X11PortalRegionSupport x11PortalSupport = default;

        if (context.IsWayland)
        {
            hasSlurp = WaylandCliCapture.IsSlurpAvailable();
            return new LinuxRegionCaptureSupportSnapshot(hasGnomeShellScreenshot, hasKdeScreenShot2, hasSlurp);
        }

        switch (context.Desktop)
        {
            case "GNOME":
            case "CINNAMON":
            case "MATE":
                hasGnomeShellScreenshot = DesktopCaptureInterfaceChecker.HasGnomeShellScreenshotInterface();
                break;
            case "KDE":
            case "LXQT":
                hasKdeScreenShot2 = DesktopCaptureInterfaceChecker.HasKdeScreenShot2Interface();
                break;
        }

        x11PortalSupport = PortalBackendDetector.DetectX11RegionSupport(
            context.Desktop,
            context.HasScreenshotPortal,
            hasGnomeShellScreenshot,
            hasKdeScreenShot2);

        return new LinuxRegionCaptureSupportSnapshot(
            hasGnomeShellScreenshot,
            hasKdeScreenShot2,
            hasSlurp,
            HasKnownGoodX11PortalBackend: x11PortalSupport.HasKnownGoodX11PortalBackend,
            PrefersPortalForRegionCaptureOnX11: x11PortalSupport.PrefersPortalForRegionCaptureOnX11,
            X11PortalBackendLabel: x11PortalSupport.BackendLabel);
    }
}

internal readonly record struct LinuxRegionCaptureSupportSnapshot(
    bool HasGnomeShellScreenshot,
    bool HasKdeScreenShot2,
    bool HasSlurp,
    bool HasKnownGoodX11PortalBackend = false,
    bool PrefersPortalForRegionCaptureOnX11 = false,
    string? X11PortalBackendLabel = null);
