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

using XerahS.Platform.Linux.Capture.OmaSnap;

namespace XerahS.Platform.Linux.Services;

/// <summary>
/// The one source of truth for "what kind of Linux desktop is this" (XIP0088): Omarchy,
/// Hyprland, sandboxing and the cached OmaSnap capability probe. Omarchy detection used to live
/// in three places (os-release, the agent skill bootstrapper and the Omarchy plugin).
/// A capability probe, never a distro string, decides whether OmaSnap is used:
/// <see cref="IsOmarchyLike"/> needs a live Hyprland session and a passing probe.
/// </summary>
public sealed class LinuxDesktopProfile
{
    private static readonly Lazy<LinuxDesktopProfile> CurrentProfile = new(Detect);
    private volatile OmaSnapCapabilities? _omaSnap;
    private volatile bool _omaSnapProbed;

    private LinuxDesktopProfile(bool isOmarchy, bool isHyprland, bool isSandboxed, string? distroId, string? omarchyPath)
    {
        IsOmarchy = isOmarchy;
        IsHyprland = isHyprland;
        IsSandboxed = isSandboxed;
        DistroId = distroId;
        OmarchyPath = omarchyPath;
    }

    /// <summary>Profile of the running session, detected once.</summary>
    public static LinuxDesktopProfile Current => CurrentProfile.Value;

    /// <summary>Omarchy is installed: <c>OMARCHY_PATH</c> is set or <c>/usr/share/omarchy</c> exists.</summary>
    public bool IsOmarchy { get; }

    /// <summary>A live Hyprland session (<c>HYPRLAND_INSTANCE_SIGNATURE</c>).</summary>
    public bool IsHyprland { get; }

    /// <summary>Flatpak, Snap or another container: OmaSnap is never used there.</summary>
    public bool IsSandboxed { get; }

    public string? DistroId { get; }

    /// <summary>Omarchy's install folder, when known. Read only; XerahS never writes there.</summary>
    public string? OmarchyPath { get; }

    /// <summary>The probe result, or null before the probe finished or when OmaSnap is absent.</summary>
    public OmaSnapCapabilities? OmaSnap => _omaSnap;

    public bool OmaSnapProbed => _omaSnapProbed;

    /// <summary>Hyprland plus a passing OmaSnap probe (whether or not Omarchy is installed).</summary>
    public bool IsOmarchyLike => IsHyprland && !IsSandboxed && _omaSnap?.IsUsable == true;

    public void SetOmaSnapProbeResult(OmaSnapCapabilities? capabilities)
    {
        _omaSnap = capabilities;
        _omaSnapProbed = true;
    }

    public static LinuxDesktopProfile Detect()
    {
        var environment = LinuxRuntimeEnvironment.Detect();
        return Detect(Environment.GetEnvironmentVariable, Directory.Exists, LinuxOsRelease.DistroId, environment.IsSandboxed);
    }

    internal static LinuxDesktopProfile Detect(
        Func<string, string?> getEnvironmentVariable,
        Func<string, bool> directoryExists,
        string? distroId,
        bool isSandboxed)
    {
        string? omarchyPath = getEnvironmentVariable("OMARCHY_PATH");
        if (string.IsNullOrWhiteSpace(omarchyPath))
        {
            omarchyPath = SafeDirectoryExists(directoryExists, "/usr/share/omarchy") ? "/usr/share/omarchy" : null;
        }

        bool isOmarchy = !string.IsNullOrWhiteSpace(omarchyPath) ||
                         string.Equals(distroId, "omarchy", StringComparison.OrdinalIgnoreCase);

        bool isHyprland = !string.IsNullOrWhiteSpace(getEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE"));

        return new LinuxDesktopProfile(isOmarchy, isHyprland, isSandboxed, distroId, omarchyPath);
    }

    /// <summary>Test factory with explicit values.</summary>
    internal static LinuxDesktopProfile Create(bool isOmarchy, bool isHyprland, bool isSandboxed = false, OmaSnapCapabilities? omaSnap = null)
    {
        var profile = new LinuxDesktopProfile(isOmarchy, isHyprland, isSandboxed, null, isOmarchy ? "/usr/share/omarchy" : null);
        if (omaSnap != null)
        {
            profile.SetOmaSnapProbeResult(omaSnap);
        }

        return profile;
    }

    public string ToDiagnosticString()
    {
        string omaSnap = !_omaSnapProbed
            ? "not probed"
            : _omaSnap == null ? "absent" : _omaSnap.Summary;
        return $"Omarchy={IsOmarchy}, Hyprland={IsHyprland}, Sandboxed={IsSandboxed}, OmarchyLike={IsOmarchyLike}, OmaSnap={omaSnap}";
    }

    private static bool SafeDirectoryExists(Func<string, bool> directoryExists, string path)
    {
        try
        {
            return directoryExists(path);
        }
        catch
        {
            return false;
        }
    }
}
