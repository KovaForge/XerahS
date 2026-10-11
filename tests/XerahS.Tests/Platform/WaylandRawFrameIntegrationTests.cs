using Win32Exception = System.ComponentModel.Win32Exception;
using System.Diagnostics;
using NUnit.Framework;
using XerahS.Platform.Linux.Recording;
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.Tests.Platform;

/// <summary>Verify the raw pipe contract against real GStreamer buffers, including row padding.</summary>
[TestFixture]
[Category("Integration")]
public class WaylandRawFrameIntegrationTests
{
    [TestCase(1422, 1141, 1416, 1140)]
    [TestCase(1610, 983, 1608, 982)]
    [TestCase(1366, 768, 1360, 768)]
    [TestCase(1935, 1208, 1928, 1208)]
    [TestCase(1920, 1080, 1920, 1080)]
    [TestCase(3840, 2160, 3840, 2160)]
    public async Task GStreamerFramesMatchFFmpegPackedLayout(int sourceWidth, int sourceHeight, int outputWidth, int outputHeight)
    {
        RequireLinux();
        string source = $"videotestsrc num-buffers=3 pattern=checkers-8 ! video/x-raw,format=RGB,width={sourceWidth},height={sourceHeight}";
        byte[] captured = await RunToolAsync("gst-launch-1.0", FFmpegEncodingBridge.BuildCaptureArgs(source, sourceWidth, sourceHeight));
        int packedFrameBytes = outputWidth * outputHeight * 3 / 2;
        // The production queue can drop whole frames under load, particularly at 4K. It must
        // still deliver an integral number of correctly packed frames, without plane padding.
        Assert.That(captured.Length, Is.InRange(packedFrameBytes, 3 * packedFrameBytes));
        Assert.That(captured.Length % packedFrameBytes, Is.Zero, "GStreamer must add no plane padding or partial frame bytes.");

        using var input = new MemoryStream(captured);
        using var output = new MemoryStream();
        Assert.That(FFmpegEncodingBridge.CopyRawFrames(input, output, sourceWidth, sourceHeight), Is.EqualTo(captured.Length / packedFrameBytes));
        Assert.That(output.ToArray(), Is.EqualTo(captured));

        // FFmpeg's independent rawvideo parser must find exactly the same frame boundaries and
        // bytes. RGB source caps above prevent alignment from changing the source's own size.
        byte[] decoded = await RunToolAsync("ffmpeg",
            $"-v error -f rawvideo -pix_fmt yuv420p -video_size {outputWidth}x{outputHeight} -framerate 30 -i pipe:0 -f rawvideo -pix_fmt yuv420p pipe:1", captured);
        Assert.That(decoded, Is.EqualTo(captured));
    }

    [Test]
    public async Task ReproducesTheUnalignedWindowPadding()
    {
        RequireLinux();
        byte[] captured = await RunToolAsync("gst-launch-1.0",
            "-q videotestsrc num-buffers=3 ! video/x-raw,format=I420,width=1422,height=1140 ! fdsink fd=1 sync=false");
        Assert.That(captured, Has.Length.EqualTo(7305120));
        Assert.That(captured, Has.Length.Not.EqualTo(3 * 1422 * 1140 * 3 / 2));
    }

    [Test]
    public async Task AlignedWindowSurvivesMp4EncodingWithoutPixelCorruption()
    {
        RequireLinux();
        byte[] listing = await RunToolAsync("ffmpeg", "-hide_banner -encoders");
        if (!FFmpegFeatures.Parse(System.Text.Encoding.UTF8.GetString(listing), string.Empty).Has("libx264"))
        {
            Assert.Ignore("The lossless MP4 regression test requires libx264.");
        }

        byte[] captured = await RunToolAsync("gst-launch-1.0", FFmpegEncodingBridge.BuildCaptureArgs(
            "videotestsrc num-buffers=3 pattern=checkers-8 ! video/x-raw,format=RGB,width=1422,height=1141", 1422, 1141));
        string path = Path.Combine(Path.GetTempPath(), $"xerahs-raw-bridge-{Guid.NewGuid():N}.mp4");
        try
        {
            // Lossless encoding makes every pixel comparable. CFR may repeat/drop identical
            // static frames when this finite fixture is read faster than a live portal stream.
            var plan = new FFmpegEncoderPlan("libx264", "-preset ultrafast -qp 0 -pix_fmt yuv420p", string.Empty, ".mp4");
            await RunToolAsync("ffmpeg", FFmpegEncodingBridge.BuildEncodeArgs(1422, 1141, 30, plan, null, path), captured);
            byte[] decoded = await RunToolAsync("ffmpeg", $"-v error -i \"{path}\" -f rawvideo -pix_fmt yuv420p pipe:1");
            const int frameBytes = 1416 * 1140 * 3 / 2;
            Assert.That(decoded.Length, Is.GreaterThanOrEqualTo(frameBytes));
            Assert.That(decoded.Length % frameBytes, Is.Zero);
            for (int offset = 0; offset < decoded.Length; offset += frameBytes)
            {
                Assert.That(decoded.AsSpan(offset, frameBytes).SequenceEqual(captured.AsSpan(0, frameBytes)), Is.True,
                    "An encoded frame must preserve the original luma/chroma layout and every pixel.");
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task StopWaitsForTheLiveBridgeToFinalizeItsMp4()
    {
        RequireLinux();
        byte[] listing = await RunToolAsync("ffmpeg", "-hide_banner -encoders");
        FFmpegEncoderPlan? plan = FFmpegEncodingBridge.PlanEncoders(VideoCodec.H264, 4000,
            FFmpegFeatures.Parse(System.Text.Encoding.UTF8.GetString(listing), string.Empty));
        if (plan == null)
        {
            Assert.Ignore("The live bridge test requires an FFmpeg video encoder.");
        }

        // Resolve missing GStreamer before starting a live child, so an unavailable tool skips
        // cleanly without leaving an encoder waiting for input.
        await RunToolAsync("gst-launch-1.0", "--version");
        string path = Path.Combine(Path.GetTempPath(), $"xerahs-live-bridge-{Guid.NewGuid():N}.mp4");
        using var service = new WaylandPortalRecordingService();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var error = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.StatusChanged += (_, args) =>
        {
            if (args.Status == RecordingStatus.Recording) started.TrySetResult();
        };
        service.ErrorOccurred += (_, args) => error.TrySetResult(args.Error);
        Task worker = service.StartFFmpegEncodingBridge(
            FFmpegEncodingBridge.BuildCaptureArgs("videotestsrc is-live=true pattern=checkers-8 ! video/x-raw,format=RGB,width=1422,height=1141", 1422, 1141),
            "ffmpeg", FFmpegEncodingBridge.BuildEncodeArgs(1422, 1141, 30, plan!, null, path), path, 1422, 1141);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.Delay(300); // Allow the live source to deliver frames before sending EOS.
            await service.StopRecordingAsync().WaitAsync(TimeSpan.FromSeconds(20));
            Assert.That(worker.IsCompleted, Is.True, "Stop must join the encoder before returning its file.");
            Assert.That(error.Task.IsCompleted, Is.False, error.Task.IsCompleted ? (await error.Task).ToString() : string.Empty);
            byte[] decoded = await RunToolAsync("ffmpeg", $"-v error -i \"{path}\" -f rawvideo -pix_fmt yuv420p pipe:1");
            Assert.That(decoded.Length, Is.GreaterThanOrEqualTo(1416 * 1140 * 3 / 2));
            Assert.That(decoded.Length % (1416 * 1140 * 3 / 2), Is.Zero);
        }
        finally
        {
            await service.StopRecordingAsync();
            await worker;
            File.Delete(path);
        }
    }

    private static void RequireLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Ignore("The Wayland bridge integration tests require Linux, GStreamer and FFmpeg.");
        }
    }

    private static async Task<byte[]> RunToolAsync(string executable, string arguments, byte[]? input = null)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executable, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = input != null,
            }
        };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            Assert.Ignore(executable + " is not installed.");
            throw;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var output = new MemoryStream();
        try
        {
            Task outputTask = process.StandardOutput.BaseStream.CopyToAsync(output, timeout.Token);
            Task<string> errorsTask = process.StandardError.ReadToEndAsync(timeout.Token);
            if (input != null)
            {
                await process.StandardInput.BaseStream.WriteAsync(input, timeout.Token);
                process.StandardInput.Close();
            }

            await process.WaitForExitAsync(timeout.Token);
            await outputTask;
            Assert.That(process.ExitCode, Is.Zero, await errorsTask);
            return output.ToArray();
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }
}
