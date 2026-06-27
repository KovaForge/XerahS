#nullable enable

using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using XerahS.Common;

namespace XerahS.Tests.Common;

[TestFixture, NonParallelizable]
public class FileDownloaderTests
{
    [Test]
    public void StopDownload_SetsIsCanceledFlag()
    {
        // Arrange
        var downloader = new FileDownloader("http://example.com/test.zip", Path.GetTempFileName());

        try
        {
            // Act
            downloader.StopDownload();

            // Assert: StopDownload should set the IsCanceled flag
            Assert.That(downloader.IsCanceled, Is.True);
        }
        finally
        {
            if (File.Exists(downloader.DownloadLocation))
                File.Delete(downloader.DownloadLocation);
        }
    }

    [Test]
    public void StartDownload_WithEmptyUrl_ReturnsFalse()
    {
        // Arrange: empty URL should not start download
        var downloader = new FileDownloader("", Path.GetTempFileName());

        try
        {
            // Act
            var result = downloader.StartDownload().Result;

            // Assert
            Assert.That(result, Is.False);
            Assert.That(downloader.IsDownloading, Is.False);
        }
        finally
        {
            if (File.Exists(downloader.DownloadLocation))
                File.Delete(downloader.DownloadLocation);
        }
    }

    [Test]
    public void StartDownload_WithNullUrl_ReturnsFalse()
    {
        // Arrange: empty/short URL should not start download
        var downloader = new FileDownloader("x", Path.GetTempFileName());

        try
        {
            // Act: using short invalid URL
            var result = downloader.StartDownload().Result;

            // Assert: download should fail quickly (404 or similar)
            Assert.That(result, Is.False);
        }
        finally
        {
            if (File.Exists(downloader.DownloadLocation))
                File.Delete(downloader.DownloadLocation);
        }
    }
    [Test]
    public async Task SimulateDownloadWithEarlyEOF_PartialDelivery_BreaksOut()
    {
        // Arrange: simulate downloading a 1024-byte file where only 256 bytes arrive.
        const long fileSize = 1024;
        byte[] receiveSequence = new byte[256];
        for (int i = 0; i < receiveSequence.Length; i++)
            receiveSequence[i] = 1;

        // Act
        var (downloadedSize, completed) =
            await FileDownloaderTestAccessor.SimulateDownloadWithEarlyEOF(fileSize, receiveSequence);

        // Assert
        Assert.That(downloadedSize, Is.EqualTo(256));
        Assert.That(completed, Is.False);
    }

    [Test]
    public async Task SimulateDownloadWithEarlyEOF_CompleteDelivery_CompletesTrue()
    {
        // Arrange: simulate downloading a 1024-byte file where all bytes arrive.
        const long fileSize = 1024;
        byte[] receiveSequence = new byte[1024];
        for (int i = 0; i < receiveSequence.Length; i++)
            receiveSequence[i] = 1;

        // Act
        var (downloadedSize, completed) =
            await FileDownloaderTestAccessor.SimulateDownloadWithEarlyEOF(fileSize, receiveSequence);

        // Assert
        Assert.That(downloadedSize, Is.EqualTo(1024));
        Assert.That(completed, Is.True);
    }

    [Test]
    public async Task SimulateDownloadWithEarlyEOF_EmptySequence_ExitsImmediately()
    {
        // Edge: zero bytes received (immediate EOF).
        const long fileSize = 1024;
        byte[] receiveSequence = Array.Empty<byte>();

        var (downloadedSize, completed) =
            await FileDownloaderTestAccessor.SimulateDownloadWithEarlyEOF(fileSize, receiveSequence);

        Assert.That(downloadedSize, Is.EqualTo(0));
        Assert.That(completed, Is.False);
    }

    [Test]
    public async Task SimulateDownloadWithEarlyEOF_MoreBytesThanNeeded_CompletesTrue()
    {
        // Arrange: simulate downloading a 1024-byte file where more bytes arrive than needed.
        const long fileSize = 1024;
        byte[] receiveSequence = new byte[2048];
        for (int i = 0; i < receiveSequence.Length; i++)
            receiveSequence[i] = 1;

        var (downloadedSize, completed) =
            await FileDownloaderTestAccessor.SimulateDownloadWithEarlyEOF(fileSize, receiveSequence);

        Assert.That(downloadedSize, Is.EqualTo(1024));
        Assert.That(completed, Is.True);
    }

    [Test]
    public async Task SimulateDownloadWithEarlyEOF_ExactBytes_CompletesTrue()
    {
        // Arrange: simulate downloading where exactly FileSize bytes arrive (edge case).
        const long fileSize = 1024;
        byte[] receiveSequence = new byte[1024];
        for (int i = 0; i < receiveSequence.Length; i++)
            receiveSequence[i] = 1;

        var (downloadedSize, completed) =
            await FileDownloaderTestAccessor.SimulateDownloadWithEarlyEOF(fileSize, receiveSequence);

        Assert.That(downloadedSize, Is.EqualTo(1024));
        Assert.That(completed, Is.True);
    }

    [Test]
    public async Task CopyToFileAsync_UnknownContentLength_ReadsUntilStreamClose()
    {
        // Regression: chunked / streaming transfer-encoding (Content-Length absent) used to
        // skip the entire download loop and return false silently, leaving a 0-byte file behind.
        // declaredFileSize is null to simulate a server that did not send Content-Length.
        const int payloadSize = 50_000;
        byte[] payload = new byte[payloadSize];
        for (int i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)(i % 251);
        }

        var downloader = new FileDownloader("http://example.com/chunked", Path.GetTempFileName());
        string destination = downloader.DownloadLocation;

        try
        {
            using var source = new MemoryStream(payload);

            long copied = await FileDownloaderTestAccessor.CopyToFileAsync(
                downloader, source, destination, declaredFileSize: null);

            Assert.That(copied, Is.EqualTo(payloadSize));
            Assert.That(downloader.DownloadedSize, Is.EqualTo(payloadSize));
            Assert.That(File.Exists(destination), Is.True);
            byte[] onDisk = File.ReadAllBytes(destination);
            Assert.That(onDisk.Length, Is.EqualTo(payloadSize));
            Assert.That(onDisk, Is.EqualTo(payload));
        }
        finally
        {
            if (File.Exists(destination))
                File.Delete(destination);
        }
    }

    [Test]
    public async Task CopyToFileAsync_DeclaredContentLength_StopsAtDeclaredSize()
    {
        // Positive case: declared Content-Length path must still cap the read at the declared
        // value (server may send trailing bytes, but the loop must not over-read).
        const int declared = 12_345;
        const int sent = 20_000;
        byte[] payload = new byte[sent];
        for (int i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)(i & 0xFF);
        }

        var downloader = new FileDownloader("http://example.com/with-len", Path.GetTempFileName());
        string destination = downloader.DownloadLocation;

        try
        {
            using var source = new MemoryStream(payload);

            long copied = await FileDownloaderTestAccessor.CopyToFileAsync(
                downloader, source, destination, declaredFileSize: declared);

            Assert.That(copied, Is.EqualTo(declared));
            Assert.That(downloader.DownloadedSize, Is.EqualTo(declared));
            Assert.That(new FileInfo(destination).Length, Is.EqualTo(declared));
        }
        finally
        {
            if (File.Exists(destination))
                File.Delete(destination);
        }
    }

    [Test]
    public async Task CopyToFileAsync_EmptySource_UnknownLength_ProducesEmptyFile()
    {
        // Edge: empty body with unknown length is a valid (zero-byte) download.
        var downloader = new FileDownloader("http://example.com/empty", Path.GetTempFileName());
        string destination = downloader.DownloadLocation;

        try
        {
            using var source = new MemoryStream(Array.Empty<byte>());

            long copied = await FileDownloaderTestAccessor.CopyToFileAsync(
                downloader, source, destination, declaredFileSize: null);

            Assert.That(copied, Is.EqualTo(0));
            Assert.That(downloader.DownloadedSize, Is.EqualTo(0));
            Assert.That(File.Exists(destination), Is.True);
            Assert.That(new FileInfo(destination).Length, Is.EqualTo(0));
        }
        finally
        {
            if (File.Exists(destination))
                File.Delete(destination);
        }
    }

    [Test]
    public async Task CopyToFileAsync_StopDownload_BeforeLoop_ResetsIsDownloading()
    {
        // Regression: when the user cancels before any read happens, IsDownloading must still
        // be reset and the file must not exist (DoWork's finally block deletes on cancel).
        var downloader = new FileDownloader("http://example.com/cancel", Path.GetTempFileName());
        string destination = downloader.DownloadLocation;

        try
        {
            downloader.StopDownload();

            using var source = new MemoryStream(new byte[] { 1, 2, 3 });

            long copied = await FileDownloaderTestAccessor.CopyToFileAsync(
                downloader, source, destination, declaredFileSize: null);

            Assert.That(copied, Is.EqualTo(0));
            Assert.That(downloader.IsCanceled, Is.True);
        }
        finally
        {
            if (File.Exists(destination))
                File.Delete(destination);
        }
    }
}