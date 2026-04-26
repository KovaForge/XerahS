using NUnit.Framework;
using XerahS.Platform.Linux.Services;

namespace XerahS.Tests.Platform;

public class LinuxStartupServiceTests
{
    [Test]
    public void BuildDesktopEntry_EscapesQuotedExecutablePathCharacters()
    {
        const string executablePath = "/opt/XerahS \"nightly\"/xerahs\\launcher";

        string desktopEntry = LinuxStartupService.BuildDesktopEntry(executablePath);

        Assert.That(desktopEntry, Does.Contain("Exec=\"/opt/XerahS \\\"nightly\\\"/xerahs\\\\launcher\""));
    }

    [Test]
    public void EscapeQuotedDesktopEntryArgument_EscapesBackslashesAndDoubleQuotes()
    {
        const string value = "/tmp/XerahS \"beta\"/app\\binary";

        string escaped = LinuxStartupService.EscapeQuotedDesktopEntryArgument(value);

        Assert.That(escaped, Is.EqualTo("/tmp/XerahS \\\"beta\\\"/app\\\\binary"));
    }
}
