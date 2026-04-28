using NUnit.Framework;
using XerahS.Platform.MacOS;

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
}
