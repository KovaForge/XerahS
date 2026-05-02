using NUnit.Framework;
using XerahS.Common;

namespace XerahS.Tests.Platform.Linux;

[TestFixture]
public class LinuxXdgDirectoriesTests
{
    [Test]
    public void Resolve_UnsetValues_UsesSpecDefaultsUnderHome()
    {
        var xdg = LinuxXdgDirectories.Resolve(_ => null, "/home/alex");

        Assert.Multiple(() =>
        {
            Assert.That(Normalize(xdg.ConfigHome), Is.EqualTo("/home/alex/.config"));
            Assert.That(Normalize(xdg.DataHome), Is.EqualTo("/home/alex/.local/share"));
            Assert.That(Normalize(xdg.StateHome), Is.EqualTo("/home/alex/.local/state"));
            Assert.That(Normalize(xdg.CacheHome), Is.EqualTo("/home/alex/.cache"));
            Assert.That(xdg.RuntimeDirectory, Is.Null);
            Assert.That(Normalize(xdg.ConfigDirectory), Is.EqualTo("/home/alex/.config/xerahs"));
            Assert.That(Normalize(xdg.DataDirectory), Is.EqualTo("/home/alex/.local/share/xerahs"));
            Assert.That(Normalize(xdg.StateDirectory), Is.EqualTo("/home/alex/.local/state/xerahs"));
            Assert.That(Normalize(xdg.CacheDirectory), Is.EqualTo("/home/alex/.cache/xerahs"));
        });
    }

    [Test]
    public void Resolve_EmptyValues_UsesSpecDefaultsUnderHome()
    {
        var xdg = LinuxXdgDirectories.Resolve(
            name => name.StartsWith("XDG_", StringComparison.Ordinal) ? "   " : null,
            "/home/alex");

        Assert.Multiple(() =>
        {
            Assert.That(Normalize(xdg.ConfigHome), Is.EqualTo("/home/alex/.config"));
            Assert.That(Normalize(xdg.DataHome), Is.EqualTo("/home/alex/.local/share"));
            Assert.That(Normalize(xdg.StateHome), Is.EqualTo("/home/alex/.local/state"));
            Assert.That(Normalize(xdg.CacheHome), Is.EqualTo("/home/alex/.cache"));
            Assert.That(xdg.RuntimeDirectory, Is.Null);
        });
    }

    [Test]
    public void Resolve_AbsoluteValues_RespectsEnvironmentOverrides()
    {
        var xdg = LinuxXdgDirectories.Resolve(
            name => name switch
            {
                "XDG_CONFIG_HOME" => "/tmp/xdg-config",
                "XDG_DATA_HOME" => "/tmp/xdg-data",
                "XDG_STATE_HOME" => "/tmp/xdg-state",
                "XDG_CACHE_HOME" => "/tmp/xdg-cache",
                "XDG_RUNTIME_DIR" => "/run/user/1000",
                _ => null
            },
            "/home/alex");

        Assert.Multiple(() =>
        {
            Assert.That(xdg.ConfigHome, Is.EqualTo("/tmp/xdg-config"));
            Assert.That(xdg.DataHome, Is.EqualTo("/tmp/xdg-data"));
            Assert.That(xdg.StateHome, Is.EqualTo("/tmp/xdg-state"));
            Assert.That(xdg.CacheHome, Is.EqualTo("/tmp/xdg-cache"));
            Assert.That(xdg.RuntimeDirectory, Is.EqualTo("/run/user/1000"));
        });
    }

    [Test]
    public void Resolve_RelativeValues_IgnoresInvalidEnvironmentOverrides()
    {
        var xdg = LinuxXdgDirectories.Resolve(
            name => name switch
            {
                "XDG_CONFIG_HOME" => "relative-config",
                "XDG_DATA_HOME" => "relative-data",
                "XDG_STATE_HOME" => "relative-state",
                "XDG_CACHE_HOME" => "relative-cache",
                "XDG_RUNTIME_DIR" => "relative-runtime",
                _ => null
            },
            "/home/alex");

        Assert.Multiple(() =>
        {
            Assert.That(Normalize(xdg.ConfigHome), Is.EqualTo("/home/alex/.config"));
            Assert.That(Normalize(xdg.DataHome), Is.EqualTo("/home/alex/.local/share"));
            Assert.That(Normalize(xdg.StateHome), Is.EqualTo("/home/alex/.local/state"));
            Assert.That(Normalize(xdg.CacheHome), Is.EqualTo("/home/alex/.cache"));
            Assert.That(xdg.RuntimeDirectory, Is.Null);
        });
    }

    [Test]
    public void Resolve_HomeFromEnvironment_UsesAbsoluteHomeWhenArgumentUnset()
    {
        var xdg = LinuxXdgDirectories.Resolve(
            name => name == "HOME" ? "/home/env-user" : null);

        Assert.That(Normalize(xdg.DataDirectory), Is.EqualTo("/home/env-user/.local/share/xerahs"));
    }

    [Test]
    public void Resolve_NoHomeLitterApplicationDirectories_StayUnderXdgRoots()
    {
        string home = "/tmp/xerahs-home";
        var xdg = LinuxXdgDirectories.Resolve(_ => null, home);
        string[] applicationDirectories =
        [
            xdg.ConfigDirectory,
            xdg.DataDirectory,
            xdg.StateDirectory,
            xdg.CacheDirectory
        ];

        foreach (string directory in applicationDirectories)
        {
            string normalized = Normalize(directory);
            Assert.That(normalized, Does.StartWith(home + "/."));
            Assert.That(normalized, Is.Not.EqualTo(home + "/XerahS"));
            Assert.That(normalized, Is.Not.EqualTo(home + "/.XerahS"));
            Assert.That(normalized, Is.Not.EqualTo(home + "/ShareX"));
            Assert.That(normalized, Is.Not.EqualTo(home + "/Screenshots"));
        }
    }

    private static string Normalize(string path)
    {
        return path.Replace('\\', '/');
    }
}
