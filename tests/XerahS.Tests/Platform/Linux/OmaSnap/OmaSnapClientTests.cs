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

namespace XerahS.Tests.Platform.Linux.OmaSnap;

[TestFixture]
[NonParallelizable]
[Platform("Linux,MacOsX")]
public sealed class OmaSnapClientTests
{
    private string _root = string.Empty;
    private string _argv = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "xerahs-omasnap-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _argv = Path.Combine(_root, "argv.txt");
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_root, recursive: true);
    }

    private OmaSnapClient CreateClient() => new(FakeOmaSnap.BinaryPath, Path.Combine(_root, "runtime"));

    private string[] RecordedArguments() => File.ReadAllLines(_argv);

    private string RuntimeFolder => Path.Combine(_root, "runtime");

    [Test]
    public async Task Probe_ParsesCapabilities()
    {
        using var _ = FakeOmaSnap.Use(caps: "ok");
        OmaSnapCapabilities? caps = await CreateClient().ProbeAsync();
        Assert.That(caps, Is.Not.Null);
        Assert.That(caps!.IsUsable, Is.True);
        Assert.That(caps.Version, Is.EqualTo("1.22.0"));
        Assert.That(caps.Summary, Is.EqualTo("OmaSnap 1.22.0 · Hyprland · ready"));
    }

    [Test]
    public async Task Probe_StandaloneWithoutHostMode_IsNull()
    {
        using var _ = FakeOmaSnap.Use(caps: "nohost");
        Assert.That(await CreateClient().ProbeAsync(), Is.Null);
    }

    [Test]
    public async Task Probe_FailedSession_IsNotUsable()
    {
        using var _ = FakeOmaSnap.Use(caps: "fail");
        OmaSnapCapabilities? caps = await CreateClient().ProbeAsync();
        Assert.That(caps!.IsUsable, Is.False);
        Assert.That(caps.Summary, Does.Contain("missing Hyprland"));
    }

    [Test]
    public async Task Probe_TimesOut()
    {
        using var _ = FakeOmaSnap.Use(caps: "slow");
        var started = DateTime.UtcNow;
        Assert.That(await CreateClient().ProbeAsync(), Is.Null);
        Assert.That(DateTime.UtcNow - started, Is.LessThan(TimeSpan.FromSeconds(15)));
    }

    [Test]
    public async Task Capture_Ok_ReturnsImageAndWindowMetadata()
    {
        using var _ = FakeOmaSnap.Use(mode: "ok", argvFile: _argv);
        var client = CreateClient();
        OmaSnapCaptureResult result = await client.CaptureAsync(new OmaSnapCaptureRequest(OmaSnapCaptureTarget.Window));

        Assert.That(result.Outcome, Is.EqualTo(OmaSnapOutcome.Succeeded));
        Assert.That(File.Exists(result.ImagePath), Is.True);
        Assert.That(result.WindowClass, Is.EqualTo("firefox"));
        Assert.That(result.WindowTitle, Is.EqualTo("XerahS - Mozilla Firefox"));
        Assert.That(result.Region, Is.EqualTo(new OmaSnapRegion(10, 20, 1, 1)));
        Assert.That(result.Scale, Is.EqualTo(2));

        client.Release(result);
        Assert.That(Directory.GetDirectories(RuntimeFolder), Is.Empty, "runtime files are removed after release");
    }

    [Test]
    public async Task Capture_Cancelled_IsCancelAndCleansUp()
    {
        using var _ = FakeOmaSnap.Use(mode: "cancelled");
        OmaSnapCaptureResult result = await CreateClient().CaptureAsync(new OmaSnapCaptureRequest(OmaSnapCaptureTarget.Smart));
        Assert.That(result.Outcome, Is.EqualTo(OmaSnapOutcome.Cancelled));
        Assert.That(Directory.GetDirectories(RuntimeFolder), Is.Empty);
    }

    [TestCase("error", "Screen capture failed")]
    [TestCase("garbage", "exit code 1")]
    [TestCase("crash", "exit code 1")]
    [TestCase("usage", "--region takes x,y,width,height")]
    public async Task Capture_Failures_AreFailures(string mode, string expectedError)
    {
        using var _ = FakeOmaSnap.Use(mode: mode);
        OmaSnapCaptureResult result = await CreateClient().CaptureAsync(new OmaSnapCaptureRequest(OmaSnapCaptureTarget.Smart));
        Assert.That(result.Outcome, Is.EqualTo(OmaSnapOutcome.Failed));
        Assert.That(result.Error, Does.Contain(expectedError));
        Assert.That(Directory.GetDirectories(RuntimeFolder), Is.Empty);
    }

    [Test]
    public async Task Capture_CancellationKillsProcess()
    {
        using var _ = FakeOmaSnap.Use(mode: "timeout");
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var started = DateTime.UtcNow;
        OmaSnapCaptureResult result = await CreateClient().CaptureAsync(new OmaSnapCaptureRequest(OmaSnapCaptureTarget.Smart), cancel.Token);
        Assert.That(result.Outcome, Is.EqualTo(OmaSnapOutcome.Cancelled));
        Assert.That(DateTime.UtcNow - started, Is.LessThan(TimeSpan.FromSeconds(10)));
        Assert.That(Directory.GetDirectories(RuntimeFolder), Is.Empty);
    }

    [Test]
    public async Task Capture_HostedRunClearsUploadCommand()
    {
        string envFile = Path.Combine(_root, "env.txt");
        using var _ = FakeOmaSnap.Use(mode: "cancelled", envFile: envFile);
        Environment.SetEnvironmentVariable(OmaSnapClient.UploadCommandEnvironmentVariable, "curl evil");
        try
        {
            await CreateClient().CaptureAsync(new OmaSnapCaptureRequest(OmaSnapCaptureTarget.Smart));
        }
        finally
        {
            Environment.SetEnvironmentVariable(OmaSnapClient.UploadCommandEnvironmentVariable, null);
        }

        Assert.That(File.ReadAllText(envFile).Trim(), Is.EqualTo("<unset>"));
    }

    [Test]
    public async Task Annotate_PassesFileAndEditor()
    {
        using var _ = FakeOmaSnap.Use(mode: "cancelled", argvFile: _argv);
        await CreateClient().AnnotateAsync("/home/u/My Pictures/shot.png");
        string[] argv = RecordedArguments();
        Assert.That(argv, Does.Contain("--file"));
        Assert.That(argv[Array.IndexOf(argv, "--file") + 1], Is.EqualTo("/home/u/My Pictures/shot.png"));
        Assert.That(argv[Array.IndexOf(argv, "--editor") + 1], Is.EqualTo("overlay"));
    }

    [TestCase(OmaSnapCaptureTarget.Smart, new string[0])]
    [TestCase(OmaSnapCaptureTarget.Region, new[] { "--capture-region" })]
    [TestCase(OmaSnapCaptureTarget.Window, new[] { "--capture-window" })]
    [TestCase(OmaSnapCaptureTarget.Fullscreen, new[] { "--capture-fullscreen" })]
    [TestCase(OmaSnapCaptureTarget.Scroll, new[] { "--scroll" })]
    public void BuildCaptureArguments_MapsTargets(OmaSnapCaptureTarget target, string[] expectedTail)
    {
        var argv = OmaSnapClient.BuildCaptureArguments(new OmaSnapCaptureRequest(target), "/run/x/capture.png", "/run/x/result.json");
        Assert.That(argv.Take(7), Is.EqualTo(new[] { "--host", "xerahs", "--output", "/run/x/capture.png", "--result-json", "/run/x/result.json", "--no-recents" }));
        Assert.That(argv.Skip(7), Is.EqualTo(expectedTail));
    }

    [Test]
    public void BuildCaptureArguments_FixedRegionIsNonInteractive()
    {
        var request = new OmaSnapCaptureRequest(OmaSnapCaptureTarget.Region) { Region = new OmaSnapRegion(-1920, 0, 800, 600), OpenEditor = true };
        var argv = OmaSnapClient.BuildCaptureArguments(request, "o.png", "r.json");
        Assert.That(argv.Skip(7), Is.EqualTo(new[] { "--capture-region", "--region", "-1920,0,800,600" }));
    }

    [Test]
    public void BuildCaptureArguments_EditorOption()
    {
        var argv = OmaSnapClient.BuildCaptureArguments(new OmaSnapCaptureRequest(OmaSnapCaptureTarget.Smart) { OpenEditor = true }, "o.png", "r.json");
        Assert.That(argv.Skip(7), Is.EqualTo(new[] { "--editor", "overlay" }));
    }

    [Test]
    public void ResolveRuntimeRoot_PrefersXdgRuntimeDir()
    {
        Assert.That(OmaSnapClient.ResolveRuntimeRoot(name => name == "XDG_RUNTIME_DIR" ? "/run/user/1000" : null),
            Is.EqualTo("/run/user/1000/xerahs/omasnap"));
        Assert.That(OmaSnapClient.ResolveRuntimeRoot(name => name == "USER" ? "mike" : null),
            Does.EndWith(Path.Combine("xerahs-mike", "omasnap")));
    }

    [Test]
    public void JoinCommand_QuotesForOmaSnapSplitting()
    {
        Assert.That(OmaSnapClient.JoinCommand(["/usr/lib/xerahs/omaxerahs", "upload"]), Is.EqualTo("/usr/lib/xerahs/omaxerahs upload"));
        Assert.That(OmaSnapClient.JoinCommand(["/opt/My Apps/omaxerahs", "it's"]), Is.EqualTo("'/opt/My Apps/omaxerahs' 'it'\\''s'"));
    }
}
