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
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.Tests.RegionCapture;

[TestFixture]
public class RecordingCodecSupportPolicyTests
{
    [Test]
    public void RequiresFfmpegFallback_WindowsNonH264_ReturnsTrue()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RecordingCodecSupportPolicy.RequiresFfmpegFallback(VideoCodec.HEVC, isWindows: true, isMacOS: false, isLinux: false), Is.True);
            Assert.That(RecordingCodecSupportPolicy.RequiresFfmpegFallback(VideoCodec.VP9, isWindows: true, isMacOS: false, isLinux: false), Is.True);
            Assert.That(RecordingCodecSupportPolicy.RequiresFfmpegFallback(VideoCodec.AV1, isWindows: true, isMacOS: false, isLinux: false), Is.True);
            Assert.That(RecordingCodecSupportPolicy.RequiresFfmpegFallback(VideoCodec.H264, isWindows: true, isMacOS: false, isLinux: false), Is.False);
        });
    }

    [Test]
    public void GetSelectableCodecs_WindowsWithoutFfmpeg_ReturnsH264Only()
    {
        var codecs = RecordingCodecSupportPolicy.GetSelectableCodecs(
            ffmpegAvailable: false,
            isWindows: true,
            isMacOS: false,
            isLinux: false);

        Assert.That(codecs, Is.EqualTo(new[] { VideoCodec.H264 }));
    }

    [Test]
    public void GetSelectableCodecs_LinuxWithoutFfmpeg_KeepsAllCodecs()
    {
        var codecs = RecordingCodecSupportPolicy.GetSelectableCodecs(
            ffmpegAvailable: false,
            isWindows: false,
            isMacOS: false,
            isLinux: true);

        Assert.That(codecs, Is.EqualTo(new[]
        {
            VideoCodec.H264,
            VideoCodec.HEVC,
            VideoCodec.VP9,
            VideoCodec.AV1
        }));
    }
}
