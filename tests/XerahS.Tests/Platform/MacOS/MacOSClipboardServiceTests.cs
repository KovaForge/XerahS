using NUnit.Framework;
using XerahS.Platform.MacOS;
using System.IO;
using System.Linq;

namespace XerahS.Tests.Platform.MacOS;

[TestFixture]
public class MacOSClipboardServiceTests
{
    [Test]
    public void ParseFileDropList_SplitsAppleScriptCarriageReturnSeparatedPaths()
    {
        var files = MacOSClipboardService.ParseFileDropList("/tmp/first.png\r/tmp/second.png\r");

        Assert.That(files, Is.EqualTo(new[] { "/tmp/first.png", "/tmp/second.png" }));
    }

    [Test]
    public void ParseFileDropList_SkipsBlankLinesAcrossLineEndings()
    {
        var files = MacOSClipboardService.ParseFileDropList("/tmp/first.png\r\n\n/tmp/second.png\r\r");

        Assert.That(files, Is.EqualTo(new[] { "/tmp/first.png", "/tmp/second.png" }));
    }

    [Test]
    public void BuildPosixFileList_NormalizesRelativePathsBeforeAppleScriptClipboardSet()
    {
        var relativePath = Path.Combine("relative", "capture.png");

        var specifier = MacOSClipboardService.BuildPosixFileList(new[] { $" {relativePath} " }).Single();

        Assert.That(specifier, Is.EqualTo($"POSIX file \\\"{Path.GetFullPath(relativePath)}\\\""));
    }

    [Test]
    public void BuildPosixFileList_SkipsInvalidOrBlankPaths()
    {
        var specifiers = MacOSClipboardService.BuildPosixFileList(new[] { "", "   ", "\0invalid" });

        Assert.That(specifiers, Is.Empty);
    }
}
