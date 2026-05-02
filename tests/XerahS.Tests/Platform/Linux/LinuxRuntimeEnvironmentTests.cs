using NUnit.Framework;
using XerahS.Platform.Linux.Services;

namespace XerahS.Tests.Platform.Linux;

[TestFixture]
public class LinuxRuntimeEnvironmentTests
{
    [Test]
    public void Detect_FlatpakId_MarksEnvironmentAsSandboxedFlatpak()
    {
        var environment = LinuxRuntimeEnvironment.Detect(
            name => name switch
            {
                "FLATPAK_ID" => "io.github.ShareX.XerahS",
                "XDG_SESSION_TYPE" => "x11",
                "DISPLAY" => ":99",
                _ => null
            },
            _ => false);

        Assert.Multiple(() =>
        {
            Assert.That(environment.IsFlatpak, Is.True);
            Assert.That(environment.IsSandboxed, Is.True);
            Assert.That(environment.IsX11, Is.True);
            Assert.That(environment.ShouldUsePortalServices(usePortalServices: true), Is.True);
            Assert.That(environment.AppId, Is.EqualTo("io.github.ShareX.XerahS"));
        });
    }

    [Test]
    public void Detect_AppImage_DoesNotMarkEnvironmentAsSandboxed()
    {
        var environment = LinuxRuntimeEnvironment.Detect(
            name => name switch
            {
                "APPIMAGE" => "/tmp/XerahS.AppImage",
                "XDG_SESSION_TYPE" => "x11",
                "DISPLAY" => ":99",
                _ => null
            },
            _ => false);

        Assert.Multiple(() =>
        {
            Assert.That(environment.IsSandboxed, Is.False);
            Assert.That(environment.IsFlatpak, Is.False);
            Assert.That(environment.ShouldUsePortalServices(usePortalServices: true), Is.False);
        });
    }

    [Test]
    public void Detect_WaylandNative_UsesPortalServicesWithoutSandboxing()
    {
        var environment = LinuxRuntimeEnvironment.Detect(
            name => name switch
            {
                "XDG_SESSION_TYPE" => "wayland",
                "WAYLAND_DISPLAY" => "wayland-0",
                "XDG_CURRENT_DESKTOP" => "GNOME",
                _ => null
            },
            _ => false);

        Assert.Multiple(() =>
        {
            Assert.That(environment.IsSandboxed, Is.False);
            Assert.That(environment.IsWayland, Is.True);
            Assert.That(environment.Desktop, Is.EqualTo("GNOME"));
            Assert.That(environment.ShouldUsePortalServices(usePortalServices: true), Is.True);
        });
    }
}
