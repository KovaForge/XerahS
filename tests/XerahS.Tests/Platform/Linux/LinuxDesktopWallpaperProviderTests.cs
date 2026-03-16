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
using XerahS.Platform.Linux.Services;

namespace XerahS.Tests.Platform.Linux;

public class LinuxDesktopWallpaperProviderTests
{
    [Test]
    public void RequiresWallpaperConversion_JpegXlWallpaper_ReturnsTrue()
    {
        Assert.That(
            LinuxDesktopWallpaperProvider.RequiresWallpaperConversion("/usr/share/backgrounds/gnome/adwaita-l.jxl"),
            Is.True);
    }

    [Test]
    public void RequiresWallpaperConversion_PngWallpaper_ReturnsFalse()
    {
        Assert.That(
            LinuxDesktopWallpaperProvider.RequiresWallpaperConversion("/usr/share/backgrounds/custom.png"),
            Is.False);
    }

    [Test]
    public void AccessiblePathCandidates_AbsolutePath_IncludeSandboxHostMirrors()
    {
        const string path = "/usr/share/backgrounds/gnome/adwaita-l.jxl";

        string[] candidates = LinuxDesktopWallpaperProvider.GetAccessiblePathCandidates(path).ToArray();

        Assert.That(candidates, Is.EqualTo(new[]
        {
            path,
            Path.Combine("/run/host", "usr/share/backgrounds/gnome/adwaita-l.jxl"),
            Path.Combine("/var/run/host", "usr/share/backgrounds/gnome/adwaita-l.jxl")
        }));
    }

    [Test]
    public void AccessiblePathCandidates_AlreadySandboxMirroredPath_DoesNotDoublePrefix()
    {
        const string path = "/run/host/usr/share/backgrounds/gnome/adwaita-l.jxl";

        string[] candidates = LinuxDesktopWallpaperProvider.GetAccessiblePathCandidates(path).ToArray();

        Assert.That(candidates, Is.EqualTo(new[] { path }));
    }

    [Test]
    public void AccessiblePathCandidates_RelativePath_ReturnOriginalOnly()
    {
        const string path = "wallpapers/current.png";

        string[] candidates = LinuxDesktopWallpaperProvider.GetAccessiblePathCandidates(path).ToArray();

        Assert.That(candidates, Is.EqualTo(new[] { path }));
    }

    [Test]
    public void WallpaperConversionCachePath_JpegXlWallpaper_UsesPngCacheFile()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string sourcePath = Path.Combine(tempDirectory, "wallpaper.jxl");
            File.WriteAllBytes(sourcePath, [1, 2, 3, 4]);

            string cachePath = LinuxDesktopWallpaperProvider.GetWallpaperConversionCachePath(sourcePath);

            Assert.That(Path.GetExtension(cachePath), Is.EqualTo(".png"));
            Assert.That(Path.GetDirectoryName(cachePath), Is.EqualTo(Path.Combine(Path.GetTempPath(), "xerahs-wallpaper-cache")));
            Assert.That(Path.GetFileName(cachePath), Does.StartWith("wallpaper-"));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
