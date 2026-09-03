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
using XerahS.Media;

namespace XerahS.Tests.Media;

[TestFixture]
public sealed class VideoConverterOptionsTests
{
    [Test]
    public void GetFFmpegArgs_UndefinedVideoCodec_FallsBackToLibx264AndAac()
    {
        VideoConverterOptions options = new()
        {
            InputFilePath = "/tmp/input.mp4",
            OutputFolderPath = "/tmp",
            OutputFileName = "out.mp4",
            VideoCodec = (ConverterVideoCodecs)9999,
        };

        string args = options.GetFFmpegArgs();

        Assert.That(args, Does.Contain("-c:v libx264"));
        Assert.That(args, Does.Contain("-c:a aac"));
        Assert.That(args, Does.Contain("-i \"/tmp/input.mp4\""));
    }

    [Test]
    public void GetFFmpegArgs_KnownX264Codec_EmitsLibx264AndAac()
    {
        VideoConverterOptions options = new()
        {
            InputFilePath = "/tmp/input.mp4",
            OutputFolderPath = "/tmp",
            OutputFileName = "out.mp4",
            VideoCodec = ConverterVideoCodecs.x264,
        };

        string args = options.GetFFmpegArgs();

        Assert.That(args, Does.Contain("-c:v libx264"));
        Assert.That(args, Does.Contain("-c:a aac"));
    }

    [Test]
    public void GetFFmpegArgs_GifCodec_DoesNotFallBackToLibx264()
    {
        VideoConverterOptions options = new()
        {
            InputFilePath = "/tmp/input.mp4",
            OutputFolderPath = "/tmp",
            OutputFileName = "out.gif",
            VideoCodec = ConverterVideoCodecs.gif,
        };

        string args = options.GetFFmpegArgs();

        Assert.That(args, Does.Contain("palettegen"));
        Assert.That(args, Does.Not.Contain("-c:v libx264"));
    }
}
