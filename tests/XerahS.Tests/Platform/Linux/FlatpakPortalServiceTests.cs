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

    [Test]
    public void FlatpakPortalStartupService_BuildAutostartCommandLine_TrimsAppId()
    {
        var commandLine = FlatpakPortalStartupService.BuildAutostartCommandLine("  io.github.ShareX.XerahS  ");

        Assert.That(commandLine, Is.EqualTo(new[] { "flatpak", "run", "io.github.ShareX.XerahS" }));
    }

    [Test]
    public void FlatpakPortalStartupService_BuildAutostartCommandLine_UsesDefaultForBlankAppId()
    {
        var commandLine = FlatpakPortalStartupService.BuildAutostartCommandLine("   ");

        Assert.That(commandLine, Is.EqualTo(new[] { "flatpak", "run", "io.github.ShareX.XerahS" }));
    }

    [Test]
    public void FlatpakPortalStartupService_GetStateFilePath_UsesXdgStateDirectory()
    {
        var xdg = XerahS.Common.LinuxXdgDirectories.Resolve(
            name => name switch
            {
                "XDG_CONFIG_HOME" => "/tmp/xdg-config",
                "XDG_STATE_HOME" => "/tmp/xdg-state",
                _ => null
            },
            "/home/alex");

        Assert.Multiple(() =>
        {
            Assert.That(
                Normalize(FlatpakPortalStartupService.GetStateFilePath(xdg)),
                Is.EqualTo("/tmp/xdg-state/xerahs/flatpak-autostart.enabled"));
            Assert.That(
                Normalize(FlatpakPortalStartupService.GetLegacyConfigStateFilePath(xdg)),
                Is.EqualTo("/tmp/xdg-config/xerahs/flatpak-autostart.enabled"));
        });
    }

    private static string Normalize(string path)
    {
        return path.Replace('\\', '/');
    }
}
