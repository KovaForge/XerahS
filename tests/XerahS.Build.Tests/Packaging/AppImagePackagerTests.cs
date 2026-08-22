#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using NUnit.Framework;
using XerahS.Packaging;

namespace XerahS.Tests.Build;

[TestFixture]
public class AppImagePackagerTests
{
    [Test]
    public void MapAppImageArch_SupportsLinuxReleaseRids()
    {
        Assert.That(AppImagePackager.MapAppImageArch("linux-x64"), Is.EqualTo("x86_64"));
        Assert.That(AppImagePackager.MapAppImageArch("linux-arm64"), Is.EqualTo("aarch64"));
    }

    [Test]
    public void MapAppImageArch_RejectsUnknownRid()
    {
        Assert.That(
            () => AppImagePackager.MapAppImageArch("win-x64"),
            Throws.ArgumentException);
    }

    [Test]
    public void BuildDesktopEntry_UsesExistingXerahSMetadata()
    {
        string desktop = AppImagePackager.BuildDesktopEntry();

        Assert.That(desktop, Does.Contain("[Desktop Entry]"));
        Assert.That(desktop, Does.Contain("Name=XerahS"));
        Assert.That(desktop, Does.Contain("Exec=xerahs %U"));
        Assert.That(desktop, Does.Contain("Icon=xerahs"));
        Assert.That(desktop, Does.Contain("StartupWMClass=xerahs"));
        Assert.That(desktop, Does.Not.Contain("\r"));
    }

    [Test]
    public void BuildAppRunScript_ExecsBundledXerahSBinary()
    {
        string appRun = AppImagePackager.BuildAppRunScript();

        Assert.That(appRun, Does.StartWith("#!/bin/sh"));
        Assert.That(appRun, Does.Contain("usr/lib/xerahs/XerahS"));
        Assert.That(appRun, Does.Contain("exec "));
        Assert.That(appRun, Does.Not.Contain("\r"));
    }

    [Test]
    public void StageAppDir_CopiesPayloadDesktopIconAndAppRun()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "xerahs-appimage-test-" + Guid.NewGuid().ToString("N"));
        string publishDir = Path.Combine(tempRoot, "publish");
        string appDir = Path.Combine(tempRoot, "AppDir");
        string iconPath = Path.Combine(tempRoot, "icon.png");

        try
        {
            Directory.CreateDirectory(Path.Combine(publishDir, "Plugins", "bitly"));
            File.WriteAllText(Path.Combine(publishDir, "XerahS"), "fake-binary");
            File.WriteAllText(Path.Combine(publishDir, "xerahs-watchfolder-daemon"), "fake-daemon");
            File.WriteAllText(Path.Combine(publishDir, "xerahs-watchfolder-daemon.runtimeconfig.json"), "{}");
            File.WriteAllText(Path.Combine(publishDir, "Plugins", "bitly", "plugin.json"), "{\"pluginId\":\"bitly\"}");
            File.WriteAllBytes(iconPath, [0x89, 0x50, 0x4E, 0x47]);

            AppImagePackager.StageAppDir(publishDir, appDir, iconPath);

            Assert.That(File.Exists(Path.Combine(appDir, "AppRun")), Is.True);
            Assert.That(File.Exists(Path.Combine(appDir, "xerahs.desktop")), Is.True);
            Assert.That(File.Exists(Path.Combine(appDir, "xerahs.png")), Is.True);
            Assert.That(File.Exists(Path.Combine(appDir, "usr", "lib", "xerahs", "XerahS")), Is.True);
            Assert.That(File.Exists(Path.Combine(appDir, "usr", "lib", "xerahs", "Plugins", "bitly", "plugin.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(appDir, "usr", "share", "applications", "xerahs.desktop")), Is.True);
            Assert.That(File.Exists(Path.Combine(appDir, "usr", "bin", "xerahs")), Is.True);

            string desktop = File.ReadAllText(Path.Combine(appDir, "xerahs.desktop"));
            Assert.That(desktop, Does.Contain("Name=XerahS"));
            Assert.That(File.ReadAllText(Path.Combine(appDir, "AppRun")), Does.Contain("usr/lib/xerahs/XerahS"));
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }
}
