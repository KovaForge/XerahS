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

using System.Diagnostics;
using NUnit.Framework;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Capture.OmaSnap;
using XerahS.Platform.Linux.Services;

namespace XerahS.Tests.Platform.Linux;

/// <summary>
/// OmaSnap host-mode client against tests/fixtures/fake-omasnap (XIP0088 Phase 7). Runs without a
/// display: the fake implements the contract and records its argv.
/// </summary>
[TestFixture]
[NonParallelizable]
public class OmaSnapClientTests
{
    private string _workDir = string.Empty;
    private string _runtimeDir = string.Empty;
    private string _argvFile = string.Empty;

    internal static string FakeOmaSnapPath
    {
        get
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "tests", "fixtures", "fake-omasnap", "omasnap");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new FileNotFoundException("tests/fixtures/fake-omasnap/omasnap not found above the test directory.");
        }
    }

    [SetUp]
    public void SetUp()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Ignore("OmaSnap host mode is Linux-only.");
        }

        _workDir = Path.Combine(Path.GetTempPath(), $"xerahs-omasnap-test-{Guid.NewGuid():N}");
        _runtimeDir = Path.Combine(_workDir, "runtime");
        Directory.CreateDirectory(_runtimeDir);
        _argvFile = Path.Combine(_workDir, "argv.txt");
        Environment.SetEnvironmentVariable("FAKE_OMASNAP_ARGV_FILE", _argvFile);
        Environment.SetEnvironmentVariable("FAKE_OMASNAP_MODE", null);
        Environment.SetEnvironmentVariable("FAKE_OMASNAP_PID_FILE", null);
        Environment.SetEnvironmentVariable("FAKE_OMASNAP_CAPS", null);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("FAKE_OMASNAP_ARGV_FILE", null);
        Environment.SetEnvironmentVariable("FAKE_OMASNAP_MODE", null);
        Environment.SetEnvironmentVariable("FAKE_OMASNAP_PID_FILE", null);
        Environment.SetEnvironmentVariable("FAKE_OMASNAP_CAPS", null);
        try
        {
            Directory.Delete(_workDir, recursive: true);
        }
        catch
        {
        }
    }

    private OmaSnapClient CreateClient() => new(FakeOmaSnapPath, () => _runtimeDir);

    private static void SetMode(string mode) => Environment.SetEnvironmentVariable("FAKE_OMASNAP_MODE", mode);

    private List<string[]> ReadInvocations()
    {
        var invocations = new List<string[]>();
        var current = new List<string>();
        foreach (string line in File.ReadAllLines(_argvFile))
        {
            if (line == "--")
            {
                invocations.Add(current.ToArray());
                current.Clear();
            }
            else
            {
                current.Add(line);
            }
        }

        return invocations;
    }

    [Test]
    public async Task Probe_HostModeBuild_IsUsable()
    {
        OmaSnapCapabilities caps = await CreateClient().ProbeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(caps.IsUsable, Is.True, caps.Describe());
            Assert.That(caps.Version, Is.EqualTo("1.22.0"));
            Assert.That(caps.Describe(), Is.EqualTo("OmaSnap 1.22.0 · Hyprland · ready"));
            Assert.That(caps.SupportsTarget(HostedCaptureTarget.Scroll), Is.True);
            Assert.That(ReadInvocations().Single(), Is.EqualTo(new[] { "--host-capabilities" }));
        });
    }

    [Test]
    public async Task Probe_StandaloneBuildWithoutHostMode_IsRejected()
    {
        SetMode("nohost");

        OmaSnapCapabilities caps = await CreateClient().ProbeAsync();

        Assert.That(caps.IsUsable, Is.False);
        Assert.That(caps.Describe(), Does.Contain("host mode"));
    }

    [TestCase("{\"schemaVersion\":1,\"ok\":true,\"version\":\"1.22.0\",\"hyprland\":true,\"extImageCopyCapture\":true,\"layerShell\":true,\"hostMode\":0}", "host mode 0")]
    [TestCase("{\"schemaVersion\":1,\"ok\":true,\"version\":\"1.22.0\",\"hyprland\":true,\"extImageCopyCapture\":false,\"layerShell\":true,\"hostMode\":1}", "ext-image-copy-capture")]
    [TestCase("{\"schemaVersion\":1,\"ok\":false,\"version\":\"1.22.0\",\"hyprland\":false,\"extImageCopyCapture\":true,\"layerShell\":true,\"hostMode\":1}", "not Hyprland")]
    public async Task Probe_MissingCapability_IsRejected(string capsJson, string expectedReason)
    {
        Environment.SetEnvironmentVariable("FAKE_OMASNAP_CAPS", capsJson);

        OmaSnapCapabilities caps = await CreateClient().ProbeAsync();

        Assert.That(caps.IsUsable, Is.False);
        Assert.That(caps.Describe(), Does.Contain(expectedReason));
    }

    [Test]
    public async Task Probe_GarbageOutput_IsRejected()
    {
        SetMode("garbage");

        OmaSnapCapabilities caps = await CreateClient().ProbeAsync();

        Assert.That(caps.IsUsable, Is.False);
        Assert.That(caps.Describe(), Does.Contain("not valid JSON"));
    }

    [Test]
    public async Task Probe_Hang_TimesOut()
    {
        SetMode("timeout");
        var stopwatch = Stopwatch.StartNew();

        OmaSnapCapabilities caps = await CreateClient().ProbeAsync();

        Assert.That(caps.IsUsable, Is.False);
        Assert.That(caps.Describe(), Does.Contain("timed out"));
        Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(15)));
    }

    [Test]
    public async Task Capture_Ok_ReturnsPngAndMetadata_AndReleaseDeletesIt()
    {
        HostedCaptureResult result = await CreateClient().CaptureAsync(new HostedCaptureRequest(HostedCaptureTarget.Smart));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsOk, Is.True, result.Error);
            Assert.That(File.Exists(result.ImagePath), Is.True);
            Assert.That(Path.GetDirectoryName(result.ImagePath), Is.EqualTo(_runtimeDir));
            Assert.That(result.WindowClass, Is.EqualTo("firefox"));
            Assert.That(result.WindowTitle, Is.EqualTo("Example page"));
            Assert.That(result.Region, Is.EqualTo(new System.Drawing.Rectangle(120, 80, 2, 1)));
            Assert.That(result.EngineVersion, Is.EqualTo("1.22.0"));
            Assert.That(SkiaSharp.SKBitmap.Decode(result.ImagePath)?.Width, Is.EqualTo(2));
        });

        string[] argv = ReadInvocations().Single();
        Assert.That(argv[..2], Is.EqualTo(new[] { "--host", "xerahs" }));
        Assert.That(argv, Does.Contain("--output"));
        Assert.That(argv, Does.Contain("--result-json"));
        Assert.That(argv.Last(), Is.EqualTo("smart"));

        CreateClient().Release(result);
        Assert.That(Directory.GetFiles(_runtimeDir), Is.Empty, "result JSON and PNG must be cleaned up");
    }

    [Test]
    public async Task Capture_ResultOnStdout_IsAccepted()
    {
        SetMode("stdout");

        HostedCaptureResult result = await CreateClient().CaptureAsync(new HostedCaptureRequest(HostedCaptureTarget.Region));

        Assert.That(result.IsOk, Is.True, result.Error);
    }

    [Test]
    public async Task Capture_Cancelled_ReportsCancelAndLeavesNoFiles()
    {
        SetMode("cancelled");

        HostedCaptureResult result = await CreateClient().CaptureAsync(new HostedCaptureRequest(HostedCaptureTarget.Smart));

        Assert.That(result.Status, Is.EqualTo(HostedCaptureStatus.Cancelled));
        Assert.That(Directory.GetFiles(_runtimeDir), Is.Empty);
    }

    [TestCase("error", "no focused output")]
    [TestCase("garbage", "not valid JSON")]
    public async Task Capture_Failure_ReportsError(string mode, string expected)
    {
        SetMode(mode);

        HostedCaptureResult result = await CreateClient().CaptureAsync(new HostedCaptureRequest(HostedCaptureTarget.Smart));

        Assert.That(result.Status, Is.EqualTo(HostedCaptureStatus.Failed));
        Assert.That(result.Error, Does.Contain(expected));
        Assert.That(Directory.GetFiles(_runtimeDir), Is.Empty);
    }

    [Test]
    public void Capture_Cancellation_KillsProcessAndCleansUp()
    {
        SetMode("timeout");
        string pidFile = Path.Combine(_workDir, "pid");
        Environment.SetEnvironmentVariable("FAKE_OMASNAP_PID_FILE", pidFile);
        using var cts = new CancellationTokenSource();

        Task<HostedCaptureResult> capture = CreateClient().CaptureAsync(new HostedCaptureRequest(HostedCaptureTarget.Smart), cts.Token);
        SpinWait.SpinUntil(() => File.Exists(pidFile) && new FileInfo(pidFile).Length > 0, TimeSpan.FromSeconds(10));
        int pid = int.Parse(File.ReadAllText(pidFile).Trim());
        cts.Cancel();

        Assert.That(async () => await capture, Throws.InstanceOf<OperationCanceledException>());
        Assert.That(SpinWait.SpinUntil(() => !ProcessExists(pid), TimeSpan.FromSeconds(5)), Is.True, "fake omasnap must be killed");
        Assert.That(Directory.GetFiles(_runtimeDir), Is.Empty);
    }

    [Test]
    public async Task Annotate_PassesFileEditorAndOutput()
    {
        string input = Path.Combine(_workDir, "input.png");
        await File.WriteAllBytesAsync(input, [1, 2, 3]);

        HostedCaptureResult result = await CreateClient().AnnotateAsync(input, "window");

        Assert.That(result.IsOk, Is.True, result.Error);
        string[] argv = ReadInvocations().Single();
        Assert.That(argv, Is.EqualTo(new[]
        {
            "--host", "xerahs", "--file", input, "--output", result.ImagePath!,
            "--result-json", argv[7], "--editor", "window"
        }));
    }

    private static bool ProcessExists(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

[TestFixture]
public class OmaSnapArgumentsTests
{
    [TestCase(HostedCaptureTarget.Smart, "smart")]
    [TestCase(HostedCaptureTarget.Region, "--capture-region")]
    [TestCase(HostedCaptureTarget.Window, "--capture-window")]
    [TestCase(HostedCaptureTarget.Fullscreen, "--capture-fullscreen")]
    [TestCase(HostedCaptureTarget.Scroll, "--scroll")]
    public void Capture_MapsTargetToFlag(HostedCaptureTarget target, string flag)
    {
        var args = OmaSnapArguments.Capture(new HostedCaptureRequest(target), "/r/out.png", "/r/out.json");

        Assert.That(args, Is.EqualTo(new[] { "--host", "xerahs", "--output", "/r/out.png", "--result-json", "/r/out.json", flag }));
    }

    [Test]
    public void Capture_PreselectedRegionEditorAndRecents()
    {
        var request = new HostedCaptureRequest(HostedCaptureTarget.Region)
        {
            Region = new System.Drawing.Rectangle(10, 20, 300, 200),
            Editor = "overlay",
            DisableRecents = true
        };

        var args = OmaSnapArguments.Capture(request, "/o.png", "/o.json");

        Assert.That(args.Skip(6), Is.EqualTo(new[] { "--capture-region", "--region", "10,20,300,200", "--editor", "overlay", "--no-recents" }));
    }

    [Test]
    public void Paths_WithSpacesAndDashes_StaySingleArguments()
    {
        var args = OmaSnapArguments.Capture(new HostedCaptureRequest(HostedCaptureTarget.Smart), "/tmp/a b/-x.png", "/tmp/a b/r.json");

        Assert.That(args, Does.Contain("/tmp/a b/-x.png"));
    }

    [Test]
    public void HostUploadCommand_QuotesOnlyWhenNeeded()
    {
        Assert.That(OmaSnapClient.FormatCommand(["/usr/lib/xerahs/omaxerahs", "upload"]), Is.EqualTo("/usr/lib/xerahs/omaxerahs upload"));
        Assert.That(OmaSnapClient.FormatCommand(["/opt/my apps/omaxerahs", "upload", "a\"b"]), Is.EqualTo("\"/opt/my apps/omaxerahs\" upload \"a\\\"b\""));
    }

    [Test]
    public void ResultParser_RejectsOutputOutsideTheRequestedPath()
    {
        string expected = Path.Combine(Path.GetTempPath(), "expected.png");
        string json = "{\"schemaVersion\":1,\"status\":\"ok\",\"path\":\"/etc/passwd\"}";

        HostedCaptureResult result = OmaSnapResultParser.Parse(json, 0, expected);

        Assert.That(result.Status, Is.EqualTo(HostedCaptureStatus.Failed));
        Assert.That(result.Error, Does.Contain("unexpected path"));
    }

    [TestCase(3, "{\"status\":\"ok\"}", HostedCaptureStatus.Cancelled)]
    [TestCase(2, "", HostedCaptureStatus.Failed)]
    [TestCase(1, "", HostedCaptureStatus.Failed)]
    public void ResultParser_ExitCodes(int exitCode, string json, HostedCaptureStatus expected)
    {
        Assert.That(OmaSnapResultParser.Parse(json, exitCode, "/tmp/x.png").Status, Is.EqualTo(expected));
    }

    [Test]
    public void Locator_PrefersOverrideThenEnvThenBundledThenPackagedThenPath()
    {
        var env = new Dictionary<string, string?>
        {
            [OmaSnapLocator.PathOverrideVariable] = "/env/omasnap",
            ["PATH"] = "/usr/local/bin:/home/u/.local/bin"
        };

        var candidates = OmaSnapLocator.GetCandidates(key => env.GetValueOrDefault(key), "/opt/xerahs", "/dev/omasnap");

        Assert.That(candidates, Is.EqualTo(new[]
        {
            "/dev/omasnap", "/env/omasnap", "/opt/xerahs/omasnap/omasnap", OmaSnapLocator.PackagedPath,
            "/usr/local/bin/omasnap", "/home/u/.local/bin/omasnap"
        }));
    }

    [Test]
    public void RuntimeFolder_UsesXdgRuntimeDirOrPrivateTmp()
    {
        Assert.That(OmaSnapRuntimeFolder.Resolve(k => k == "XDG_RUNTIME_DIR" ? "/run/user/1000" : null, () => "1000"),
            Is.EqualTo("/run/user/1000/xerahs/omasnap"));
        Assert.That(OmaSnapRuntimeFolder.Resolve(_ => null, () => "1000"),
            Is.EqualTo(Path.Combine(Path.GetTempPath(), "xerahs-1000", "omasnap")));
    }
}
