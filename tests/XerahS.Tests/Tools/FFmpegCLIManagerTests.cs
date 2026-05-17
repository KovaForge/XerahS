using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using XerahS.Common;
using XerahS.Media;

namespace XerahS.Tests.Tools;

[TestFixture]
public sealed class FFmpegCLIManagerTests
{
    [Test]
    [Platform(Exclude = "Win")]
    public void Close_AfterGracefulQuitAttempts_KillsEntireProcessTree()
    {
        const string shellPath = "/bin/sh";
        if (!File.Exists(shellPath))
        {
            Assert.Ignore("/bin/sh is required for the process-tree regression test.");
        }

        string tempDirectory = Path.Combine(Path.GetTempPath(), "XerahS-FFmpegCLIManagerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        string childPidPath = Path.Combine(tempDirectory, "child.pid");
        int childPid = 0;

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = shellPath,
                Arguments = $"-c \"sleep 60 & echo $! > {QuoteForShell(childPidPath)}; wait\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        try
        {
            Assert.That(process.Start(), Is.True);
            Assert.That(WaitUntil(() => TryReadPid(childPidPath, out childPid), TimeSpan.FromSeconds(5)), Is.True,
                "Timed out waiting for child process PID.");

            var manager = new FFmpegCLIManager(shellPath);
            SetPrivateField(typeof(ExternalCLIManager), manager, "process", process);
            SetPrivateField(typeof(ExternalCLIManager), manager, "<IsProcessRunning>k__BackingField", true);
            SetPrivateField(typeof(FFmpegCLIManager), manager, "closeTryCount", 2);

            manager.Close();

            Assert.Multiple(() =>
            {
                Assert.That(process.WaitForExit(5000), Is.True, "Parent process should be killed on the forced close attempt.");
                Assert.That(WaitUntil(() => ProcessHasExited(childPid), TimeSpan.FromSeconds(5)), Is.True,
                    "Forced FFmpeg close should terminate child processes as well as the parent process.");
            });
        }
        finally
        {
            TryKillProcessTree(process);
            if (childPid > 0)
            {
                TryKillProcess(childPid);
            }

            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void GetVideoInfo_EscapesEmbeddedQuotesInInputPath()
    {
        var manager = new CapturingFFmpegCLIManager();
        string videoPath = Path.Combine(Path.GetTempPath(), "capture \"quoted\" name.mp4");

        VideoInfo? info = manager.GetVideoInfo(videoPath);

        Assert.Multiple(() =>
        {
            Assert.That(info, Is.Not.Null);
            Assert.That(manager.CapturedArgs, Is.EqualTo("-i \"" + videoPath.Replace("\"", "\\\"") + "\""));
        });
    }

    private sealed class CapturingFFmpegCLIManager : FFmpegCLIManager
    {
        public CapturingFFmpegCLIManager() : base("ffmpeg")
        {
        }

        public string? CapturedArgs { get; private set; }

        public override int Open(string path, string? args = null)
        {
            CapturedArgs = args;
            Output.AppendLine("Input #0, mov,mp4,m4a,3gp,3g2,mj2, from 'capture.mp4':");
            Output.AppendLine("  Duration: 00:00:01.00, start: 0.000000, bitrate: 512 kb/s");
            Output.AppendLine("    Stream #0:0: Video: h264 (High), yuv420p, 1920x1080, 30 fps");
            return 0;
        }
    }

    private static string QuoteForShell(string value) => "'" + value.Replace("'", "'\\''") + "'";

    private static bool TryReadPid(string path, out int pid)
    {
        pid = 0;

        if (!File.Exists(path))
        {
            return false;
        }

        return int.TryParse(File.ReadAllText(path).Trim(), out pid);
    }

    private static bool ProcessHasExited(int pid)
    {
        try
        {
            using Process process = Process.GetProcessById(pid);
            return process.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(50);
        }

        return condition();
    }

    private static void SetPrivateField(Type declaringType, object instance, string fieldName, object? value)
    {
        FieldInfo? field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found on {declaringType.FullName}.");
        field!.SetValue(instance, value);
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void TryKillProcess(int pid)
    {
        try
        {
            using Process process = Process.GetProcessById(pid);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }
}
