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
using XerahS.Platform.Linux.Capture.OmaSnap;
using XerahS.Platform.Linux.Services;

namespace XerahS.Tests.Platform.Linux.OmaSnap;

[TestFixture]
[NonParallelizable]
[Platform("Linux,MacOsX")]
public sealed class OmaSnapServiceTests
{
    private string _root = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "xerahs-omasnap-service-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_root, recursive: true);
    }

    private string Wrapper(string name, string caps)
    {
        // A second "binary" that behaves like a standalone omasnap without host mode.
        string path = Path.Combine(_root, name);
        File.WriteAllText(path, $"#!/bin/bash\nFAKE_OMASNAP_CAPS={caps} exec \"{FakeOmaSnap.BinaryPath}\" \"$@\"\n");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }

    private OmaSnapService Service(LinuxDesktopProfile profile, params string[] candidates) =>
        new(profile, () => candidates, () => ["/usr/lib/xerahs/omaxerahs", "upload"], Path.Combine(_root, "runtime"));

    [Test]
    public async Task Probe_SkipsStandaloneWithoutHostMode()
    {
        using var _ = FakeOmaSnap.Use();
        var profile = LinuxDesktopProfile.Create(isOmarchy: true, isHyprland: true);
        string standalone = Wrapper("standalone-omasnap", "nohost");
        var service = Service(profile, standalone, FakeOmaSnap.BinaryPath);

        Assert.That(service.ShouldHandle(LinuxInteractiveRegionSelectorPreference.Automatic), Is.False, "nothing routes to OmaSnap before the probe");
        await service.EnsureProbedAsync();

        Assert.That(service.Status.IsAvailable, Is.True);
        Assert.That(service.Status.BinaryPath, Is.EqualTo(FakeOmaSnap.BinaryPath));
        Assert.That(profile.IsOmarchyLike, Is.True);
        Assert.That(service.ShouldHandle(LinuxInteractiveRegionSelectorPreference.Automatic), Is.True);
        Assert.That(service.ShouldHandle(LinuxInteractiveRegionSelectorPreference.PortalDialog), Is.False);
    }

    [Test]
    public async Task Probe_WithoutHyprland_DoesNotRunOmaSnap()
    {
        string argv = Path.Combine(_root, "argv.txt");
        using var _ = FakeOmaSnap.Use(argvFile: argv);
        var service = Service(LinuxDesktopProfile.Create(isOmarchy: false, isHyprland: false), FakeOmaSnap.BinaryPath);
        await service.EnsureProbedAsync();

        Assert.That(service.Status.IsAvailable, Is.False);
        Assert.That(File.Exists(argv), Is.False);
        Assert.That(service.ShouldHandle(LinuxInteractiveRegionSelectorPreference.OmaSnap), Is.False);
        var result = await service.CaptureAsync(new OmaSnapCaptureRequest(OmaSnapCaptureTarget.Smart));
        Assert.That(result.Outcome, Is.EqualTo(OmaSnapOutcome.Unavailable));
    }

    [Test]
    public async Task SecondCapture_ClosesTheOpenOverlay()
    {
        using var _ = FakeOmaSnap.Use(mode: "timeout");
        var service = Service(LinuxDesktopProfile.Create(isOmarchy: true, isHyprland: true), FakeOmaSnap.BinaryPath);
        await service.EnsureProbedAsync();

        Task<OmaSnapCaptureResult> first = service.CaptureAsync(new OmaSnapCaptureRequest(OmaSnapCaptureTarget.Smart));
        await Task.Delay(300);
        OmaSnapCaptureResult second = await service.CaptureAsync(new OmaSnapCaptureRequest(OmaSnapCaptureTarget.Smart));
        OmaSnapCaptureResult firstResult = await first.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.That(second.Outcome, Is.EqualTo(OmaSnapOutcome.Cancelled));
        Assert.That(firstResult.Outcome, Is.EqualTo(OmaSnapOutcome.Cancelled));
    }

    [Test]
    public async Task Pin_SetsTheHostUploadCommand()
    {
        string argv = Path.Combine(_root, "argv.txt");
        string env = Path.Combine(_root, "env.txt");
        using var _ = FakeOmaSnap.Use(argvFile: argv, envFile: env);
        var service = Service(LinuxDesktopProfile.Create(isOmarchy: true, isHyprland: true), FakeOmaSnap.BinaryPath);
        await service.EnsureProbedAsync();
        File.Delete(argv);
        File.Delete(env);
        string image = Path.Combine(_root, "pin.png");
        File.WriteAllBytes(image, [1]);

        Assert.That(await service.PinAsync(image), Is.True);
        for (int i = 0; i < 50 && (!File.Exists(env) || !File.Exists(argv) || File.ReadAllText(env).Length == 0); i++)
        {
            await Task.Delay(100);
        }

        Assert.That(File.ReadAllLines(argv), Is.EqualTo(new[] { "--pin", image }));
        Assert.That(File.ReadAllText(env).Trim(), Is.EqualTo("/usr/lib/xerahs/omaxerahs upload"));
    }
}
