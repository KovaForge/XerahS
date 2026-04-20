using NUnit.Framework;
using XerahS.Platform.Linux.Services;

namespace XerahS.Tests.Platform.Linux;

[TestFixture]
public class LinuxClipboardServiceTests
{
    [Test]
    public void ToClipboardFileUri_EscapesSpacesAndSpecialCharacters()
    {
        var uri = LinuxClipboardService.ToClipboardFileUri("/tmp/Capture Folder/#1 %.png");

        Assert.That(uri, Is.EqualTo("file:///tmp/Capture%20Folder/%231%20%25.png"));
    }

    [Test]
    public void ParseClipboardFileUri_DecodesFileUrisBackToPaths()
    {
        var path = LinuxClipboardService.ParseClipboardFileUri("file:///tmp/Capture%20Folder/%231%20%25.png");

        Assert.That(path, Is.EqualTo("/tmp/Capture Folder/#1 %.png"));
    }

    [Test]
    public void ParseFileDropList_SkipsBlankLinesAndPreservesPlainPaths()
    {
        var files = LinuxClipboardService.ParseFileDropList("file:///tmp/first%20shot.png\n\n/tmp/plain.txt\r\n");

        Assert.That(files, Is.EqualTo(new[] { "/tmp/first shot.png", "/tmp/plain.txt" }));
    }
}
