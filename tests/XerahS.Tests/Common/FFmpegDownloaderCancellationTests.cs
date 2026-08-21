#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using XerahS.Common;

namespace XerahS.Tests.Common;

[TestFixture]
public class FFmpegDownloaderCancellationTests
{
    [Test]
    public async Task DownloadLatestAsync_PreCancelledToken_ReturnsCanceledFailureWithoutCreatingDestination()
    {
        // Arrange: use a temp folder that should NOT be created when cancellation is honoured up front.
        string destinationFolder = Path.Combine(Path.GetTempPath(), "xerahs-ffmpeg-cancel-" + Guid.NewGuid().ToString("N"));
        Assert.That(Directory.Exists(destinationFolder), Is.False, "Test precondition: destination must not exist.");

        using CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            // Act
            FFmpegDownloadResult result = await FFmpegDownloader.DownloadLatestAsync(destinationFolder, progress: null, cts.Token);

            // Assert
            Assert.That(result.Success, Is.False, "Pre-cancelled token must surface a failure result.");
            Assert.That(result.ErrorMessage, Is.EqualTo("FFmpeg download was canceled."));
            Assert.That(result.FFmpegPath, Is.Null);
            Assert.That(Directory.Exists(destinationFolder), Is.False, "Early-cancel path must not create the destination folder.");
        }
        finally
        {
            if (Directory.Exists(destinationFolder))
            {
                try { Directory.Delete(destinationFolder, recursive: true); } catch { }
            }
        }
    }

    [Test]
    public void DownloadLatestAsync_EmptyDestination_ReturnsDestinationFailure()
    {
        // Sanity guard: empty/whitespace destination is a different failure path from cancellation.
        // This test exists primarily to lock in that empty-destination failures still surface
        // and do not accidentally regress to "FFmpeg download was canceled." messages.
        FFmpegDownloadResult result = FFmpegDownloader.DownloadLatestAsync("", progress: null, CancellationToken.None).Result;
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.EqualTo("Destination folder was not provided."));
    }

    [Test]
    public async Task DownloadLatestAsync_WhitespaceDestination_ReturnsDestinationFailure()
    {
        FFmpegDownloadResult result = await FFmpegDownloader.DownloadLatestAsync("   ", progress: null, CancellationToken.None);
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.EqualTo("Destination folder was not provided."));
    }

    [Test]
    public async Task DownloadFFprobeFallbackAsync_PreCancelledToken_ReturnsNullWithoutCreatingDestination()
    {
        // Arrange: use a temp folder that should NOT be created when cancellation is honoured up front.
        string destinationFolder = Path.Combine(Path.GetTempPath(), "xerahs-ffprobe-cancel-" + Guid.NewGuid().ToString("N"));
        Assert.That(Directory.Exists(destinationFolder), Is.False, "Test precondition: destination must not exist.");

        using CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            // Act
            string? result = await FFmpegDownloader.DownloadFFprobeFallbackAsync(destinationFolder, progress: null, cts.Token);

            // Assert
            Assert.That(result, Is.Null, "Pre-cancelled token must return null without doing any I/O.");
            Assert.That(Directory.Exists(destinationFolder), Is.False, "Early-cancel path must not create the destination folder.");
        }
        finally
        {
            if (Directory.Exists(destinationFolder))
            {
                try { Directory.Delete(destinationFolder, recursive: true); } catch { }
            }
        }
    }

    [Test]
    public async Task DownloadFFprobeFallbackAsync_EmptyDestination_ReturnsNull()
    {
        string? result = await FFmpegDownloader.DownloadFFprobeFallbackAsync("", progress: null, CancellationToken.None);
        Assert.That(result, Is.Null);
    }
}
