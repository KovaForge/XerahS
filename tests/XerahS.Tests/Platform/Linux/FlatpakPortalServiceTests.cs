using NUnit.Framework;
using XerahS.Platform.Linux.Services;
using XerahS.Services.Abstractions;

namespace XerahS.Tests.Platform.Linux;

[TestFixture]
public class FlatpakPortalServiceTests
{
    [Test]
    public void FlatpakPortalStartupService_BuildAutostartCommandLine_UsesFlatpakRunAppId()
    {
        var commandLine = FlatpakPortalStartupService.BuildAutostartCommandLine("io.github.ShareX.XerahS");

        Assert.That(commandLine, Is.EqualTo(new[] { "flatpak", "run", "io.github.ShareX.XerahS" }));
    }

    [Test]
    public void PortalNotificationService_MapPriority_UsesPortalPriorityValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PortalNotificationService.MapPriority(NotificationType.Info), Is.EqualTo("normal"));
            Assert.That(PortalNotificationService.MapPriority(NotificationType.Success), Is.EqualTo("low"));
            Assert.That(PortalNotificationService.MapPriority(NotificationType.Warning), Is.EqualTo("normal"));
            Assert.That(PortalNotificationService.MapPriority(NotificationType.Error), Is.EqualTo("urgent"));
        });
    }
}
