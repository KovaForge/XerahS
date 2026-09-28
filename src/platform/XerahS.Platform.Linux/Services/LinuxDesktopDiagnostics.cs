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

using System.Text;
using System.Text.Json;
using XerahS.Platform.Linux.Capture.OmaSnap;
using XerahS.Platform.Linux.Hyprland;

namespace XerahS.Platform.Linux.Services;

/// <summary>
/// XIP0088: <c>xerahs doctor --linux-desktop</c>. Prints the desktop profile, the OmaSnap probe and
/// the Hyprland keybinding state. Read only; nothing is written or reloaded.
/// </summary>
public static class LinuxDesktopDiagnostics
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static async Task<(string Report, int ExitCode)> BuildReportAsync(bool json)
    {
        LinuxDesktopProfile profile = LinuxDesktopProfile.Detect();
        var omaSnap = new OmaSnapService(profile);
        await omaSnap.EnsureProbedAsync().ConfigureAwait(false);
        var keybindings = profile.IsHyprland && !profile.IsSandboxed ? new HyprlandKeybindingService(profile.IsOmarchy) : null;

        var report = new DesktopReport(
            new ProfileReport(profile.IsOmarchy, profile.IsHyprland, profile.IsSandboxed, profile.IsOmarchyLike, profile.DistroId, profile.OmarchyPath),
            new OmaSnapReport(
                omaSnap.Status.IsAvailable,
                omaSnap.Status.Summary,
                OmaSnapLocator.GetCandidates(),
                profile.OmaSnap),
            profile.IsOmarchyLike ? "OmaSnap" : "existing chain (wlroots, portal, slurp, overlay)",
            keybindings == null
                ? null
                : new KeybindingReport(
                    keybindings.ManagedFilePath,
                    File.Exists(keybindings.ManagedFilePath),
                    keybindings.IncludeTargetPath,
                    keybindings.IsIncluded));

        return json ? (JsonSerializer.Serialize(report, JsonOptions), 0) : (BuildText(report), 0);
    }

    /// <summary>True when an OmaSnap with host mode is installed and this is a session it can capture.</summary>
    public static async Task<bool> IsOmaSnapUsableAsync()
    {
        var omaSnap = new OmaSnapService(LinuxDesktopProfile.Detect());
        await omaSnap.EnsureProbedAsync().ConfigureAwait(false);
        return omaSnap.Status.IsAvailable && !omaSnap.Profile.IsSandboxed;
    }

    internal static string BuildText(DesktopReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("XerahS Linux desktop doctor (XIP0088)");
        sb.AppendLine();
        sb.AppendLine($"Omarchy           : {YesNo(report.Profile.IsOmarchy)}{(report.Profile.OmarchyPath is { } path ? $" ({path})" : string.Empty)}");
        sb.AppendLine($"Hyprland session  : {YesNo(report.Profile.IsHyprland)}");
        sb.AppendLine($"Sandboxed         : {YesNo(report.Profile.IsSandboxed)}");
        sb.AppendLine($"Distro id         : {report.Profile.DistroId ?? "unknown"}");
        sb.AppendLine($"Omarchy-like      : {YesNo(report.Profile.IsOmarchyLike)}");
        sb.AppendLine($"Automatic capture : {report.AutomaticEngine}");
        sb.AppendLine();
        sb.AppendLine($"OmaSnap           : {report.OmaSnap.Summary}");
        sb.AppendLine("Search order      :");
        foreach (string candidate in report.OmaSnap.Candidates)
        {
            sb.AppendLine($"  - {candidate}");
        }

        if (report.OmaSnap.Probe != null)
        {
            sb.AppendLine("Probe             :");
            sb.AppendLine(JsonSerializer.Serialize(report.OmaSnap.Probe, JsonOptions));
        }

        if (report.HyprlandKeybindings is { } keybindings)
        {
            sb.AppendLine();
            sb.AppendLine($"Managed keybindings : {keybindings.ManagedFile} ({(keybindings.ManagedFileExists ? "present" : "absent")})");
            sb.AppendLine($"Loaded from         : {keybindings.IncludeTarget} ({(keybindings.Included ? "included" : "not included")})");
        }

        return sb.ToString();
    }

    private static string YesNo(bool value) => value ? "yes" : "no";

    internal sealed record DesktopReport(ProfileReport Profile, OmaSnapReport OmaSnap, string AutomaticEngine, KeybindingReport? HyprlandKeybindings);

    internal sealed record ProfileReport(bool IsOmarchy, bool IsHyprland, bool IsSandboxed, bool IsOmarchyLike, string? DistroId, string? OmarchyPath);

    internal sealed record OmaSnapReport(bool Available, string Summary, IReadOnlyList<string> Candidates, OmaSnapCapabilities? Probe);

    internal sealed record KeybindingReport(string ManagedFile, bool ManagedFileExists, string IncludeTarget, bool Included);
}
