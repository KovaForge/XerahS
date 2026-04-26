using NUnit.Framework;
using XerahS.Platform.Abstractions;
using XerahS.UI.Helpers;

namespace XerahS.Tests.UI;

public class LinuxRegionSelectorPreferenceSupportTests
{
    [Test]
    public void GetVisiblePreferences_IncludesOverlayWhenAutomaticPrefersOverlay()
    {
        var diagnostics = CreateDiagnostics(
            LinuxInteractiveRegionSelectorPreference.XerahSOverlay,
            LinuxInteractiveRegionSelectorPreference.Automatic,
            LinuxInteractiveRegionSelectorPreference.PortalDialog);

        var result = LinuxRegionSelectorPreferenceSupport.GetVisiblePreferences(diagnostics);

        Assert.That(result, Is.EqualTo(new[]
        {
            LinuxInteractiveRegionSelectorPreference.Automatic,
            LinuxInteractiveRegionSelectorPreference.XerahSOverlay,
            LinuxInteractiveRegionSelectorPreference.PortalDialog
        }));
    }

    [Test]
    public void NormalizeForCurrentSession_WithOverlayAvailableViaAutomaticPreference_KeepsOverlaySelection()
    {
        var diagnostics = CreateDiagnostics(
            LinuxInteractiveRegionSelectorPreference.XerahSOverlay,
            LinuxInteractiveRegionSelectorPreference.Automatic,
            LinuxInteractiveRegionSelectorPreference.PortalDialog);

        var result = LinuxRegionSelectorPreferenceSupport.NormalizeForCurrentSession(
            LinuxInteractiveRegionSelectorPreference.XerahSOverlay,
            diagnostics);

        Assert.That(result, Is.EqualTo(LinuxInteractiveRegionSelectorPreference.XerahSOverlay));
    }

    [Test]
    public void NormalizeForCurrentSession_WhenPreferenceIsUnavailable_FallsBackToAutomatic()
    {
        var diagnostics = CreateDiagnostics(
            LinuxInteractiveRegionSelectorPreference.PortalDialog,
            LinuxInteractiveRegionSelectorPreference.Automatic,
            LinuxInteractiveRegionSelectorPreference.PortalDialog);

        var result = LinuxRegionSelectorPreferenceSupport.NormalizeForCurrentSession(
            LinuxInteractiveRegionSelectorPreference.Slurp,
            diagnostics);

        Assert.That(result, Is.EqualTo(LinuxInteractiveRegionSelectorPreference.Automatic));
    }

    private static LinuxRegionSelectorDiagnostics CreateDiagnostics(
        LinuxInteractiveRegionSelectorPreference automaticPreference,
        params LinuxInteractiveRegionSelectorPreference[] availablePreferences)
    {
        return new LinuxRegionSelectorDiagnostics(
            SessionType: "wayland",
            Desktop: "GNOME",
            Compositor: "Mutter",
            PortalBackendSummary: "xdg-desktop-portal",
            AutomaticPreference: automaticPreference,
            AvailablePreferences: availablePreferences,
            LastDecision: null);
    }
}
