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

using NUnit.Framework;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux;
using XerahS.UI.Services;

namespace XerahS.Tests.Platform.Linux;

public class LinuxRegionSelectorDiagnosticsTests
{
    [Test]
    public void LinuxScreenCaptureService_CreateRuntimeDecision_MapsPortalProvider()
    {
        var timestamp = new DateTimeOffset(2026, 3, 15, 10, 30, 0, TimeSpan.Zero);

        var decision = LinuxScreenCaptureService.CreateRuntimeDecision(
            operation: "Region capture",
            providerId: "portal",
            requestedPreference: LinuxInteractiveRegionSelectorPreference.Automatic,
            outcome: "Succeeded",
            timestampUtc: timestamp);

        Assert.That(decision.ProviderDisplayName, Is.EqualTo("XDG portal dialog"));
        Assert.That(decision.EffectivePreference, Is.EqualTo(LinuxInteractiveRegionSelectorPreference.PortalDialog));
        Assert.That(decision.Outcome, Is.EqualTo("Succeeded"));
        Assert.That(decision.TimestampUtc, Is.EqualTo(timestamp));
    }

    [Test]
    public void LinuxScreenCaptureService_CreateRuntimeDecision_MapsDesktopNativeProviders()
    {
        var decision = LinuxScreenCaptureService.CreateRuntimeDecision(
            operation: "Region capture",
            providerId: "kde-dbus",
            requestedPreference: LinuxInteractiveRegionSelectorPreference.Automatic,
            outcome: "Failed");

        Assert.That(decision.ProviderDisplayName, Is.EqualTo("KDE desktop selector"));
        Assert.That(decision.EffectivePreference, Is.EqualTo(LinuxInteractiveRegionSelectorPreference.DesktopNative));
        Assert.That(decision.Outcome, Is.EqualTo("Failed"));
    }

    [Test]
    public void ScreenCaptureService_MergeLinuxRegionSelectorDiagnostics_PrefersNewestDecision()
    {
        var platformDecision = new LinuxRegionSelectorRuntimeDecision(
            Operation: "Region capture",
            ProviderId: "portal",
            ProviderDisplayName: "XDG portal dialog",
            RequestedPreference: LinuxInteractiveRegionSelectorPreference.Automatic,
            EffectivePreference: LinuxInteractiveRegionSelectorPreference.PortalDialog,
            Outcome: "Succeeded",
            TimestampUtc: new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero));

        var overlayDecision = new LinuxRegionSelectorRuntimeDecision(
            Operation: "Region capture",
            ProviderId: "xerahs-overlay",
            ProviderDisplayName: "XerahS overlay crosshair",
            RequestedPreference: LinuxInteractiveRegionSelectorPreference.PortalDialog,
            EffectivePreference: LinuxInteractiveRegionSelectorPreference.XerahSOverlay,
            Outcome: "Succeeded",
            TimestampUtc: new DateTimeOffset(2026, 3, 15, 10, 1, 0, TimeSpan.Zero));

        var diagnostics = new LinuxRegionSelectorDiagnostics(
            SessionType: "Wayland",
            Desktop: "GNOME",
            Compositor: "Mutter",
            PortalBackendSummary: "gnome",
            AutomaticPreference: LinuxInteractiveRegionSelectorPreference.PortalDialog,
            AvailablePreferences: new[]
            {
                LinuxInteractiveRegionSelectorPreference.Automatic,
                LinuxInteractiveRegionSelectorPreference.PortalDialog
            },
            LastDecision: platformDecision);

        var merged = ScreenCaptureService.MergeLinuxRegionSelectorDiagnostics(diagnostics, overlayDecision);

        Assert.That(merged, Is.Not.Null);
        Assert.That(merged!.LastDecision, Is.EqualTo(overlayDecision));
    }

    [Test]
    public void ScreenCaptureService_MergeLinuxRegionSelectorDiagnostics_PreservesNewerPlatformDecision()
    {
        var platformDecision = new LinuxRegionSelectorRuntimeDecision(
            Operation: "Region capture",
            ProviderId: "portal",
            ProviderDisplayName: "XDG portal dialog",
            RequestedPreference: LinuxInteractiveRegionSelectorPreference.Automatic,
            EffectivePreference: LinuxInteractiveRegionSelectorPreference.PortalDialog,
            Outcome: "Succeeded",
            TimestampUtc: new DateTimeOffset(2026, 3, 15, 10, 2, 0, TimeSpan.Zero));

        var overlayDecision = new LinuxRegionSelectorRuntimeDecision(
            Operation: "Region capture",
            ProviderId: "xerahs-overlay",
            ProviderDisplayName: "XerahS overlay crosshair",
            RequestedPreference: LinuxInteractiveRegionSelectorPreference.PortalDialog,
            EffectivePreference: LinuxInteractiveRegionSelectorPreference.XerahSOverlay,
            Outcome: "Succeeded",
            TimestampUtc: new DateTimeOffset(2026, 3, 15, 10, 1, 0, TimeSpan.Zero));

        var diagnostics = new LinuxRegionSelectorDiagnostics(
            SessionType: "Wayland",
            Desktop: "GNOME",
            Compositor: "Mutter",
            PortalBackendSummary: "gnome",
            AutomaticPreference: LinuxInteractiveRegionSelectorPreference.PortalDialog,
            AvailablePreferences: new[]
            {
                LinuxInteractiveRegionSelectorPreference.Automatic,
                LinuxInteractiveRegionSelectorPreference.PortalDialog
            },
            LastDecision: platformDecision);

        var merged = ScreenCaptureService.MergeLinuxRegionSelectorDiagnostics(diagnostics, overlayDecision);

        Assert.That(merged, Is.Not.Null);
        Assert.That(merged!.LastDecision, Is.EqualTo(platformDecision));
    }
}
