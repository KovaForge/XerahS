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

using System.Diagnostics;
using System.Runtime.InteropServices;
using NUnit.Framework;
using XerahS.Common;

namespace XerahS.Tests.Common;

public class FFmpegLinuxDownloadTests
{
    private static readonly (string Name, string Url)[] Assets =
    {
        ("ffmpeg-master-latest-linux64-gpl.tar.xz", "u/master"),
        ("ffmpeg-n8.1-latest-linux64-gpl-8.1.tar.xz", "u/8.1-x64"),
        ("ffmpeg-n9.0-latest-linux64-gpl-9.0.tar.xz", "u/9.0-x64"),
        ("ffmpeg-n9.0-latest-linux64-lgpl-9.0.tar.xz", "u/9.0-x64-lgpl"),
        ("ffmpeg-n9.0-latest-linux64-gpl-shared-9.0.tar.xz", "u/9.0-x64-shared"),
        ("ffmpeg-n9.0-latest-linuxarm64-gpl-9.0.tar.xz", "u/9.0-arm64"),
        ("ffmpeg-n10.0-latest-win64-gpl-10.0.zip", "u/win"),
    };

    [Test]
    public void SelectsNewestStableStaticGplBuildForTheCpu()
    {
        Assert.That(FFmpegDownloader.SelectLinuxStaticAsset(Assets, Architecture.X64)?.Url, Is.EqualTo("u/9.0-x64"));
        Assert.That(FFmpegDownloader.SelectLinuxStaticAsset(Assets, Architecture.Arm64)?.Url, Is.EqualTo("u/9.0-arm64"));
        Assert.That(FFmpegDownloader.SelectLinuxStaticAsset(Assets, Architecture.X86), Is.Null);
    }

    [Test]
    [Explicit("Downloads about 150 MB from GitHub.")]
    [Category("Network")]
    public async Task DownloadsARunnableFFmpegIntoAUserFolder()
    {
        if (!OperatingSystem.IsLinux()) Assert.Ignore("Linux only.");
        string folder = Path.Combine(Path.GetTempPath(), "xerahs-ffmpeg-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            FFmpegDownloadResult result = await FFmpegDownloader.DownloadLatestAsync(folder);
            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.FFmpegPath, Is.EqualTo(Path.Combine(folder, "ffmpeg")));

            using var process = Process.Start(new ProcessStartInfo(result.FFmpegPath!, "-hide_banner -encoders")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
            })!;
            string encoders = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            Assert.That(encoders, Does.Contain("libx264").And.Contain("libvpx-vp9"));
            Assert.That(File.Exists(Path.Combine(folder, "ffprobe")), Is.True);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }
}
