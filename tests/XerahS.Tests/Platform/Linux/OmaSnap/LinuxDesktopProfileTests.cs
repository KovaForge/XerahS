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
using XerahS.Platform.Linux.Capture.Contracts;
using XerahS.Platform.Linux.Capture.Detection;
using XerahS.Platform.Linux.Capture.OmaSnap;
using XerahS.Platform.Linux.Capture.Orchestration;
using XerahS.Platform.Linux.Services;

namespace XerahS.Tests.Platform.Linux.OmaSnap;

/// <summary>
/// XIP0088 profile matrix. The golden part: on every non Omarchy-like session the capture
/// order and the Automatic selector are exactly what they were before OmaSnap existed.
/// </summary>
[TestFixture]
public sealed class LinuxDesktopProfileTests
{
    private static readonly OmaSnapCapabilities ReadyProbe = new(true, "1.22.0", true, true, true, true, 1, "/usr/lib/xerahs/omasnap/omasnap");

    public sealed record Session(
        string Name,
        Dictionary<string, string?> Environment,
        bool DirectoryOmarchy,
        bool IsSandboxed,
        bool IsWayland,
        string Desktop,
        bool HasPortal,
        bool ProbePasses);

    private static IEnumerable<Session> Sessions()
    {
        yield return new Session("Omarchy", new() { ["HYPRLAND_INSTANCE_SIGNATURE"] = "abc", ["OMARCHY_PATH"] = "/home/u/.local/share/omarchy" }, true, false, true, "Hyprland", true, true);
        yield return new Session("HyprlandWithoutOmarchy", new() { ["HYPRLAND_INSTANCE_SIGNATURE"] = "abc" }, false, false, true, "Hyprland", true, true);
        yield return new Session("GNOME", new(), false, false, true, "GNOME", true, false);
        yield return new Session("KDE", new(), false, false, true, "KDE", true, false);
        yield return new Session("Sway", new(), false, false, true, "sway", true, false);
        yield return new Session("X11", new(), false, false, false, "XFCE", false, false);
        yield return new Session("Flatpak", new() { ["HYPRLAND_INSTANCE_SIGNATURE"] = "abc" }, false, true, true, "Hyprland", true, true);
        yield return new Session("HyprlandWithoutOmaSnap", new() { ["HYPRLAND_INSTANCE_SIGNATURE"] = "abc", ["OMARCHY_PATH"] = "/usr/share/omarchy" }, true, false, true, "Hyprland", true, false);
    }

    private static LinuxDesktopProfile Profile(Session session)
    {
        var profile = LinuxDesktopProfile.Detect(
            name => session.Environment.TryGetValue(name, out var value) ? value : null,
            path => session.DirectoryOmarchy && path == "/usr/share/omarchy",
            distroId: null,
            isSandboxed: session.IsSandboxed);
        profile.SetOmaSnapProbeResult(session.ProbePasses ? ReadyProbe : null);
        return profile;
    }

    private static bool OmarchyLike(Session s) => s.Name is "Omarchy" or "HyprlandWithoutOmarchy";

    [TestCaseSource(nameof(Sessions))]
    public void Profile_Matrix(Session session)
    {
        LinuxDesktopProfile profile = Profile(session);
        Assert.That(profile.IsOmarchy, Is.EqualTo(session.Name is "Omarchy" or "HyprlandWithoutOmaSnap"));
        Assert.That(profile.IsHyprland, Is.EqualTo(session.Environment.ContainsKey("HYPRLAND_INSTANCE_SIGNATURE")));
        Assert.That(profile.IsOmarchyLike, Is.EqualTo(OmarchyLike(session)), "a probe, not a distro string, decides");
    }

    [TestCaseSource(nameof(Sessions))]
    public void ServiceSelection_Matrix(Session session)
    {
        LinuxDesktopProfile profile = Profile(session);
        bool available = session.ProbePasses && !session.IsSandboxed;
        bool automatic = available && profile.IsOmarchyLike;
        Assert.That(ShouldHandle(profile, available, LinuxInteractiveRegionSelectorPreference.Automatic), Is.EqualTo(automatic));
        Assert.That(ShouldHandle(profile, available, LinuxInteractiveRegionSelectorPreference.OmaSnap), Is.EqualTo(available));
        Assert.That(ShouldHandle(profile, available, LinuxInteractiveRegionSelectorPreference.Slurp), Is.False);
    }

    private static bool ShouldHandle(LinuxDesktopProfile profile, bool available, LinuxInteractiveRegionSelectorPreference preference)
    {
        // Mirrors OmaSnapService.ShouldHandle with the probe outcome injected.
        if (!available || profile.IsSandboxed)
        {
            return false;
        }

        return preference switch
        {
            LinuxInteractiveRegionSelectorPreference.OmaSnap => true,
            LinuxInteractiveRegionSelectorPreference.Automatic => profile.IsOmarchyLike,
            _ => false
        };
    }

    private static readonly LinuxCaptureKind[] Kinds = [LinuxCaptureKind.Region, LinuxCaptureKind.FullScreen, LinuxCaptureKind.ActiveWindow];

    private static readonly LinuxInteractiveRegionSelectorPreference[] ExistingPreferences =
    [
        LinuxInteractiveRegionSelectorPreference.Automatic,
        LinuxInteractiveRegionSelectorPreference.XerahSOverlay,
        LinuxInteractiveRegionSelectorPreference.DesktopNative,
        LinuxInteractiveRegionSelectorPreference.PortalDialog,
        LinuxInteractiveRegionSelectorPreference.Slurp
    ];

    [TestCaseSource(nameof(Sessions))]
    public void CaptureOrder_UnchangedUnlessOmarchyLike(Session session)
    {
        LinuxDesktopProfile profile = Profile(session);
        var context = new LinuxCaptureContext(session.IsWayland, session.Desktop, session.IsWayland ? "WAYLAND" : "X11", session.IsSandboxed, session.HasPortal);
        var golden = new WaterfallCapturePolicy();
        var withOmaSnap = new WaterfallCapturePolicy(preference => ShouldHandle(profile, session.ProbePasses && !session.IsSandboxed, preference));

        foreach (LinuxCaptureKind kind in Kinds)
        {
            foreach (var preference in ExistingPreferences)
            {
                var request = new LinuxCaptureRequest(kind, new CaptureOptions { LinuxRegionSelectorPreference = preference });
                var expected = golden.GetStageOrder(request, context).ToList();
                var actual = withOmaSnap.GetStageOrder(request, context).ToList();
                bool omaSnapFirst = OmarchyLike(session) && kind == LinuxCaptureKind.Region && preference == LinuxInteractiveRegionSelectorPreference.Automatic;
                if (omaSnapFirst)
                {
                    expected.Insert(0, LinuxCaptureStage.OmaSnap);
                }

                Assert.That(actual, Is.EqualTo(expected), $"{session.Name} {kind} {preference}");
            }
        }
    }

    [Test]
    public void GoldenOrder_WaylandAutomaticRegion()
    {
        var context = new LinuxCaptureContext(isWayland: true, desktop: "GNOME", compositor: "WAYLAND", isSandboxed: false, hasScreenshotPortal: true);
        var request = new LinuxCaptureRequest(LinuxCaptureKind.Region, new CaptureOptions());
        Assert.That(new WaterfallCapturePolicy().GetStageOrder(request, context), Is.EqualTo(new[]
        {
            LinuxCaptureStage.Portal,
            LinuxCaptureStage.DesktopDbus,
            LinuxCaptureStage.WaylandProtocol,
            LinuxCaptureStage.X11
        }));
    }

    [Test]
    public void GoldenOrder_OmaSnapPreferenceFallsBackToExistingChain()
    {
        var context = new LinuxCaptureContext(isWayland: true, desktop: "Hyprland", compositor: "WAYLAND", isSandboxed: false, hasScreenshotPortal: true);
        var request = new LinuxCaptureRequest(LinuxCaptureKind.Region, new CaptureOptions { LinuxRegionSelectorPreference = LinuxInteractiveRegionSelectorPreference.OmaSnap });
        Assert.That(new WaterfallCapturePolicy(_ => true).GetStageOrder(request, context), Is.EqualTo(new[]
        {
            LinuxCaptureStage.OmaSnap,
            LinuxCaptureStage.Portal,
            LinuxCaptureStage.DesktopDbus,
            LinuxCaptureStage.WaylandProtocol,
            LinuxCaptureStage.X11
        }));
    }

    [TestCaseSource(nameof(Sessions))]
    public void AutomaticSelector_UnchangedUnlessOmarchyLike(Session session)
    {
        var context = new LinuxCaptureContext(session.IsWayland, session.Desktop, session.IsWayland ? "WAYLAND" : "X11", session.IsSandboxed, session.HasPortal);
        var support = new LinuxRegionCaptureSupportSnapshot(HasGnomeShellScreenshot: session.Desktop == "GNOME", HasKdeScreenShot2: session.Desktop == "KDE", HasSlurp: true);
        var capability = new LinuxRegionCaptureCapability(SupportsNativeRegionCapture: true, SupportsLegacyOverlayCapture: !session.IsWayland, Reason: "test");
        LinuxDesktopProfile profile = Profile(session);
        bool available = session.ProbePasses && !session.IsSandboxed;

        var golden = LinuxRegionSelectorDiagnosticsDetector.Detect(context, support, capability);
        var actual = LinuxRegionSelectorDiagnosticsDetector.Detect(
            context, support, capability,
            omaSnapAvailable: available,
            omaSnapPreferredByAutomatic: available && profile.IsOmarchyLike);

        if (OmarchyLike(session))
        {
            Assert.That(actual.AutomaticPreference, Is.EqualTo(LinuxInteractiveRegionSelectorPreference.OmaSnap));
        }
        else
        {
            Assert.That(actual.AutomaticPreference, Is.EqualTo(golden.AutomaticPreference), session.Name);
        }

        Assert.That(actual.AvailablePreferences.Contains(LinuxInteractiveRegionSelectorPreference.OmaSnap), Is.EqualTo(available));
        Assert.That(actual.AvailablePreferences.Where(p => p != LinuxInteractiveRegionSelectorPreference.OmaSnap), Is.EqualTo(golden.AvailablePreferences));
    }

    [Test]
    public void EnumValues_AreStable()
    {
        Assert.That((int)LinuxInteractiveRegionSelectorPreference.Automatic, Is.EqualTo(0));
        Assert.That((int)LinuxInteractiveRegionSelectorPreference.XerahSOverlay, Is.EqualTo(1));
        Assert.That((int)LinuxInteractiveRegionSelectorPreference.DesktopNative, Is.EqualTo(2));
        Assert.That((int)LinuxInteractiveRegionSelectorPreference.PortalDialog, Is.EqualTo(3));
        Assert.That((int)LinuxInteractiveRegionSelectorPreference.Slurp, Is.EqualTo(4));
        Assert.That((int)LinuxInteractiveRegionSelectorPreference.OmaSnap, Is.EqualTo(5));
    }

    [Test]
    public void Locator_PrefersOverrideThenBundledThenPackagedThenPath()
    {
        var existing = new HashSet<string>
        {
            "/dev/omasnap",
            "/opt/xerahs/omasnap/omasnap",
            OmaSnapLocator.PackagedPath,
            "/home/u/.local/bin/omasnap"
        };
        var candidates = OmaSnapLocator.GetCandidates("/dev/omasnap", "/opt/xerahs/", "/home/u/.local/bin:/usr/bin", existing.Contains);
        Assert.That(candidates, Is.EqualTo(new[] { "/dev/omasnap", "/opt/xerahs/omasnap/omasnap", OmaSnapLocator.PackagedPath, "/home/u/.local/bin/omasnap" }));
    }
}
