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

using System;
using System.IO;
using NUnit.Framework;
using XerahS.Common;
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.Tests.RegionCapture;

[TestFixture]
public sealed class ScreenRecorderServiceTests
{
    private string? _originalPersonalFolder;

    [SetUp]
    public void SetUp()
    {
        _originalPersonalFolder = PathsManager.PersonalFolder;
        PathsManager.PersonalFolder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "screen-recorder-tests", Guid.NewGuid().ToString("N"));
        ScreenRecorderService.CaptureSourceFactory = () => new FakeCaptureSource();
        ScreenRecorderService.EncoderFactory = () => new FakeVideoEncoder();
    }

    [TearDown]
    public void TearDown()
    {
        ScreenRecorderService.CaptureSourceFactory = null;
        ScreenRecorderService.EncoderFactory = null;

        if (!string.IsNullOrEmpty(_originalPersonalFolder))
        {
            PathsManager.PersonalFolder = _originalPersonalFolder;
        }
    }

    [Test]
    public async Task StopRecordingAsync_UsesResolvedDefaultOutputPath_WhenOptionsOutputPathIsNull()
    {
        using var service = new ScreenRecorderService();

        await service.StartRecordingAsync(new RecordingOptions());

        Assert.That(
            async () => await service.StopRecordingAsync(),
            Throws.InvalidOperationException.With.Message.Contains("Recording output invalid"));
    }

    private sealed class FakeCaptureSource : ICaptureSource
    {
        public event EventHandler<FrameArrivedEventArgs>? FrameArrived
        {
            add { }
            remove { }
        }

        public Task StartCaptureAsync() => Task.CompletedTask;

        public Task StopCaptureAsync() => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    private sealed class FakeVideoEncoder : IVideoEncoder
    {
        public void Initialize(VideoFormat format, string outputPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using var _ = File.Create(outputPath);
        }

        public void WriteFrame(FrameData frame)
        {
        }

        public void FinalizeEncoding()
        {
        }

        public void Dispose()
        {
        }
    }
}
