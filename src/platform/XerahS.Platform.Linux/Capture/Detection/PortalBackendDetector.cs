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

using System.Collections.Generic;
using System.Diagnostics;

namespace XerahS.Platform.Linux.Capture.Detection;

internal static class PortalBackendDetector
{
    public static string GetRunningBackendsSummary()
    {
        var running = new List<string>();
        var snapshot = ProbeRunningBackends();

        if (snapshot.HasKde) running.Add("kde");
        if (snapshot.HasGnome) running.Add("gnome");
        if (snapshot.HasGtk) running.Add("gtk");
        if (snapshot.HasWlr) running.Add("wlr");
        if (snapshot.HasHyprland) running.Add("hyprland");
        if (snapshot.HasLxqt) running.Add("lxqt");
        if (snapshot.HasXapp) running.Add("xapp");

        return running.Count > 0 ? string.Join(", ", running) : "none detected";
    }

    public static string GetRoutingHint()
    {
        return DesktopEnvironmentDetector.Detect() switch
        {
            "KDE" => "kde",
            "GNOME" => "gnome",
            "CINNAMON" or "MATE" or "XFCE" => "xapp",
            "LXQT" => "lxqt",
            "HYPRLAND" => "hyprland",
            "SWAY" => "wlr",
            _ => "unknown"
        };
    }

    public static X11PortalRegionSupport DetectX11RegionSupport(
        string? desktop,
        bool hasScreenshotPortal,
        bool hasGnomeShellScreenshot,
        bool hasKdeScreenShot2)
    {
        return DetectX11RegionSupport(
            desktop,
            hasScreenshotPortal,
            hasGnomeShellScreenshot,
            hasKdeScreenShot2,
            ProbeRunningBackends());
    }

    internal static X11PortalRegionSupport DetectX11RegionSupport(
        string? desktop,
        bool hasScreenshotPortal,
        bool hasGnomeShellScreenshot,
        bool hasKdeScreenShot2,
        PortalBackendSnapshot backends)
    {
        if (!hasScreenshotPortal)
        {
            return default;
        }

        var preferredBackend = ResolvePreferredBackend(desktop, backends);
        if (!IsKnownGoodX11PortalBackend(preferredBackend))
        {
            return default;
        }

        bool shouldPreferPortal = preferredBackend switch
        {
            PortalBackendKind.Gnome => !hasGnomeShellScreenshot,
            PortalBackendKind.Kde or PortalBackendKind.Lxqt => !hasKdeScreenShot2,
            _ => true
        };

        return new X11PortalRegionSupport(
            HasKnownGoodX11PortalBackend: true,
            PrefersPortalForRegionCaptureOnX11: shouldPreferPortal,
            BackendLabel: GetBackendLabel(preferredBackend));
    }

    private static PortalBackendSnapshot ProbeRunningBackends()
    {
        return new PortalBackendSnapshot(
            HasKde: IsProcessRunning("xdg-desktop-portal-kde"),
            HasGnome: IsProcessRunning("xdg-desktop-portal-gnome"),
            HasGtk: IsProcessRunning("xdg-desktop-portal-gtk"),
            HasWlr: IsProcessRunning("xdg-desktop-portal-wlr"),
            HasHyprland: IsProcessRunning("xdg-desktop-portal-hyprland"),
            HasLxqt: IsProcessRunning("xdg-desktop-portal-lxqt"),
            HasXapp: IsProcessRunning("xdg-desktop-portal-xapp"));
    }

    private static bool IsProcessRunning(string processName)
    {
        try
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static PortalBackendKind ResolvePreferredBackend(string? desktop, PortalBackendSnapshot backends)
    {
        switch (desktop)
        {
            case "GNOME":
                if (backends.HasGnome) return PortalBackendKind.Gnome;
                if (backends.HasGtk) return PortalBackendKind.Gtk;
                break;
            case "CINNAMON":
            case "MATE":
            case "XFCE":
                if (backends.HasXapp) return PortalBackendKind.Xapp;
                if (backends.HasGnome) return PortalBackendKind.Gnome;
                if (backends.HasGtk) return PortalBackendKind.Gtk;
                break;
            case "KDE":
                if (backends.HasKde) return PortalBackendKind.Kde;
                if (backends.HasGtk) return PortalBackendKind.Gtk;
                break;
            case "LXQT":
                if (backends.HasLxqt) return PortalBackendKind.Lxqt;
                if (backends.HasKde) return PortalBackendKind.Kde;
                if (backends.HasGtk) return PortalBackendKind.Gtk;
                break;
        }

        var specializedBackends = new List<PortalBackendKind>();
        if (backends.HasGnome) specializedBackends.Add(PortalBackendKind.Gnome);
        if (backends.HasKde) specializedBackends.Add(PortalBackendKind.Kde);
        if (backends.HasLxqt) specializedBackends.Add(PortalBackendKind.Lxqt);
        if (backends.HasXapp) specializedBackends.Add(PortalBackendKind.Xapp);

        if (specializedBackends.Count == 1)
        {
            return specializedBackends[0];
        }

        return specializedBackends.Count == 0 && backends.HasGtk
            ? PortalBackendKind.Gtk
            : PortalBackendKind.Unknown;
    }

    private static bool IsKnownGoodX11PortalBackend(PortalBackendKind backend)
    {
        return backend is PortalBackendKind.Gnome or
               PortalBackendKind.Kde or
               PortalBackendKind.Lxqt or
               PortalBackendKind.Xapp;
    }

    private static string? GetBackendLabel(PortalBackendKind backend)
    {
        return backend switch
        {
            PortalBackendKind.Gnome => "gnome",
            PortalBackendKind.Kde => "kde",
            PortalBackendKind.Lxqt => "lxqt",
            PortalBackendKind.Xapp => "xapp",
            PortalBackendKind.Gtk => "gtk",
            _ => null
        };
    }
}

internal readonly record struct PortalBackendSnapshot(
    bool HasKde,
    bool HasGnome,
    bool HasGtk,
    bool HasWlr,
    bool HasHyprland,
    bool HasLxqt,
    bool HasXapp);

internal readonly record struct X11PortalRegionSupport(
    bool HasKnownGoodX11PortalBackend,
    bool PrefersPortalForRegionCaptureOnX11,
    string? BackendLabel);

internal enum PortalBackendKind
{
    Unknown,
    Gtk,
    Gnome,
    Kde,
    Lxqt,
    Xapp
}
