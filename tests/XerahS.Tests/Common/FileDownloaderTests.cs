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
}