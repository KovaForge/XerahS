using NUnit.Framework;
using XerahS.Platform.Linux.Services;
using XerahS.Platform.MacOS.Services;

namespace XerahS.Tests.Platform;

public class NotificationServiceProcessStartInfoTests
{
    [Test]
    public void LinuxNotificationService_CreateStartInfo_PreservesQuotedArguments()
    {
        const string title = "Capture \"done\"";
        const string message = "Saved to /tmp/shot\nReady for upload";

        var startInfo = LinuxNotificationService.CreateStartInfo(title, message);

        Assert.Multiple(() =>
        {
            Assert.That(startInfo.FileName, Is.EqualTo("notify-send"));
            Assert.That(startInfo.ArgumentList, Is.EqualTo(new[] { title, message }));
        });
    }

    [Test]
    public void MacOSNotificationService_CreateStartInfo_EscapesAppleScriptStringLiteral()
    {
        const string title = "Capture \"done\"";
        const string message = "Saved to C:\\Shots\\latest\nReady for upload";

        var startInfo = MacOSNotificationService.CreateStartInfo(title, message);

        Assert.Multiple(() =>
        {
            Assert.That(startInfo.FileName, Is.EqualTo("osascript"));
            Assert.That(startInfo.ArgumentList.Count, Is.EqualTo(2));
            Assert.That(startInfo.ArgumentList[0], Is.EqualTo("-e"));
            Assert.That(startInfo.ArgumentList[1], Is.EqualTo("display notification \"Saved to C:\\\\Shots\\\\latest\\nReady for upload\" with title \"Capture \\\"done\\\"\""));
        });
    }
}
