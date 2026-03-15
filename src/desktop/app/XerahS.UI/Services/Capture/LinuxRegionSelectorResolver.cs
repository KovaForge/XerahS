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
using XerahS.Platform.Abstractions;

namespace XerahS.UI.Services.Capture;

/// <summary>
/// Resolves the effective Linux region selector preference, manages runtime decisions,
/// and merges diagnostics from platform and overlay layers.
/// Extracted from <see cref="ScreenCaptureService"/> (XIP-0052 §3.2).
/// </summary>
public class LinuxRegionSelectorResolver
{
    private readonly object _decisionLock = new();
    private LinuxRegionSelectorRuntimeDecision? _lastDecision;
    private readonly string _overlayProviderId;

    public LinuxRegionSelectorResolver(string overlayProviderId)
    {
        _overlayProviderId = overlayProviderId;
    }

    public LinuxRegionCaptureCapability GetCapability(
        IScreenCaptureService platformImpl, CaptureOptions? options = null)
    {
        if (platformImpl is ILinuxRegionCaptureCapabilityProvider provider)
        {
            return provider.GetLinuxRegionCaptureCapability(options);
        }

        return new LinuxRegionCaptureCapability(
            SupportsNativeRegionCapture: false,
            SupportsLegacyOverlayCapture: false,
            Reason: "Linux region capture capability provider unavailable.");
    }

    public LinuxRegionSelectorDiagnostics? GetDiagnostics(IScreenCaptureService platformImpl)
    {
        LinuxRegionSelectorDiagnostics? diagnostics = null;
        if (platformImpl is ILinuxRegionSelectorDiagnosticsProvider provider)
        {
            diagnostics = provider.GetLinuxRegionSelectorDiagnostics();
        }

        return MergeDiagnostics(diagnostics, GetLastDecision());
    }

    public LinuxInteractiveRegionSelectorPreference ResolveEffectivePreference(
        CaptureOptions? options,
        LinuxRegionCaptureCapability? linuxCapability,
        IScreenCaptureService platformImpl)
    {
        var preference = LinuxCaptureOptionsResolver.GetLinuxRegionSelectorPreference(options);
        if (!OperatingSystem.IsLinux())
        {
            return preference;
        }

        var diagnostics = GetDiagnostics(platformImpl);
        if (preference != LinuxInteractiveRegionSelectorPreference.Automatic)
        {
            if (diagnostics?.AvailablePreferences is { Count: > 0 } availablePreferences &&
                !availablePreferences.Contains(preference))
            {
                string availableList = string.Join(", ", availablePreferences.Select(x => x.ToString()));
                DebugHelper.WriteLine(
                    $"[RegionCapture] Linux selector '{preference}' is unavailable in the current session. " +
                    $"Available selectors: {availableList}. Falling back to Automatic.");
                preference = LinuxInteractiveRegionSelectorPreference.Automatic;
            }
            else
            {
                return preference;
            }
        }

        if ((options?.UseModernCapture == false) && linuxCapability?.SupportsLegacyOverlayCapture == true)
        {
            return LinuxInteractiveRegionSelectorPreference.XerahSOverlay;
        }

        if (diagnostics?.AutomaticPreference is { } automaticPreference &&
            automaticPreference != LinuxInteractiveRegionSelectorPreference.Automatic)
        {
            return automaticPreference;
        }

        if (linuxCapability?.SupportsLegacyOverlayCapture == true)
        {
            return LinuxInteractiveRegionSelectorPreference.XerahSOverlay;
        }

        return linuxCapability?.SupportsNativeRegionCapture == true
            ? LinuxInteractiveRegionSelectorPreference.PortalDialog
            : LinuxInteractiveRegionSelectorPreference.Automatic;
    }

    public void RecordDecision(LinuxRegionSelectorRuntimeDecision decision)
    {
        lock (_decisionLock)
        {
            _lastDecision = decision;
        }
    }

    public LinuxRegionSelectorRuntimeDecision CreateOverlayDecision(
        string operation,
        LinuxInteractiveRegionSelectorPreference requestedPreference,
        string outcome)
    {
        return new LinuxRegionSelectorRuntimeDecision(
            Operation: operation,
            ProviderId: _overlayProviderId,
            ProviderDisplayName: "XerahS overlay crosshair",
            RequestedPreference: requestedPreference,
            EffectivePreference: LinuxInteractiveRegionSelectorPreference.XerahSOverlay,
            Outcome: outcome,
            TimestampUtc: DateTimeOffset.UtcNow);
    }

    private LinuxRegionSelectorRuntimeDecision? GetLastDecision()
    {
        lock (_decisionLock)
        {
            return _lastDecision;
        }
    }

    internal static LinuxRegionSelectorDiagnostics? MergeDiagnostics(
        LinuxRegionSelectorDiagnostics? diagnostics,
        LinuxRegionSelectorRuntimeDecision? runtimeDecision)
    {
        if (diagnostics == null || runtimeDecision == null)
        {
            return diagnostics;
        }

        if (diagnostics.LastDecision == null ||
            runtimeDecision.TimestampUtc >= diagnostics.LastDecision.TimestampUtc)
        {
            return diagnostics with
            {
                LastDecision = runtimeDecision
            };
        }

        return diagnostics;
    }
}
