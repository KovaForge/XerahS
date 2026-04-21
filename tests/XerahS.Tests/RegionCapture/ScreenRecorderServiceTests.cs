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
using System.Drawing;
using System.IO;
using NUnit.Framework;
using XerahS.Common;
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.Tests.RegionCapture;

[TestFixture]
public sealed class ScreenRecorderServiceTests
{
    private string? _originalPersonalFolder;
    private FakeVideoEncoder? _lastEncoder;

    [SetUp]
    public void SetUp()
    {
        _originalPersonalFolder = PathsManager.PersonalFolder;
        PathsManager.PersonalFolder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "screen-recorder-tests", Guid.NewGuid().ToString("N"));
        ScreenRecorderService.CaptureSourceFactory = () => new FakeCaptureSource();
        ScreenRecorderService.EncoderFactory = () => _lastEncoder = new FakeVideoEncoder();
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

        Assert.That(_lastEncoder, Is.Not.Null);
        Assert.That(_lastEncoder!.OutputPath, Is.Not.Null.And.Not.Empty);

        Assert.DoesNotThrowAsync(async () => await service.StopRecordingAsync());
        Assert.That(File.Exists(_lastEncoder.OutputPath), Is.True);
    }

    [Test]
    public async Task StartRecordingAsync_ClampsOddSinglePixelRegionDimensions_ToMinimumEvenEncoderSize()
    {
        using var service = new ScreenRecorderService();

        await service.StartRecordingAsync(new RecordingOptions
        {
            Mode = CaptureMode.Region,
            Region = new Rectangle(10, 20, 1, 1)
        });

        Assert.That(_lastEncoder, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(_lastEncoder!.InitializedFormat, Is.Not.Null);
            Assert.That(_lastEncoder.InitializedFormat!.Width, Is.EqualTo(2));
            Assert.That(_lastEncoder.InitializedFormat.Height, Is.EqualTo(2));
        });
    }

    public sealed class FakeCaptureSource : ICaptureSource
    {
        public bool ShowCursor { get; set; }

        public event EventHandler<FrameArrivedEventArgs>? FrameArrived
        {
            add { }
            remove { }
        }

        public void InitializeForPrimaryMonitor()
        {
        }

        public Task StartCaptureAsync() => Task.CompletedTask;

        public Task StopCaptureAsync() => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    public sealed class FakeVideoEncoder : IVideoEncoder
    {
        public VideoFormat? InitializedFormat { get; private set; }
        public string? OutputPath { get; private set; }

        public void Initialize(VideoFormat format, string outputPath)
        {
            InitializedFormat = format;
            OutputPath = outputPath;
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
