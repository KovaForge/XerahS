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
using XerahS.CLI.Commands;
using XerahS.Core;
using XerahS.Platform.Abstractions;
using XerahS.Services.Abstractions;

namespace XerahS.Tests.Tools;

[TestFixture]
public class VerifyMacOSNativeCrosshairCommandTests
{
    [Test]
    public void CreateVerificationTaskSettings_ForcesNativeSelectorAndSaveOnlyOutput()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), "xerahs-native-verify.jpg");
        var source = new TaskSettings
        {
            Job = WorkflowType.PrintScreen,
            AfterCaptureJob = AfterCaptureTasks.CopyImageToClipboard | AfterCaptureTasks.UploadImageToHost,
            AfterUploadJob = AfterUploadTasks.CopyURLToClipboard
        };
        source.CaptureSettings.MacOSRegionSelectorPreference = MacOSInteractiveRegionSelectorPreference.XerahSOverlay;

        var settings = VerifyMacOSNativeCrosshairCommand.CreateVerificationTaskSettings(source, outputPath, upload: false);

        Assert.Multiple(() =>
        {
            Assert.That(settings.Job, Is.EqualTo(WorkflowType.RectangleRegion));
            Assert.That(settings.CaptureSettings.MacOSRegionSelectorPreference, Is.EqualTo(MacOSInteractiveRegionSelectorPreference.NativeCrosshair));
            Assert.That(settings.AfterCaptureJob, Is.EqualTo(AfterCaptureTasks.SaveImageToFile));
            Assert.That(settings.AfterUploadJob, Is.EqualTo(AfterUploadTasks.None));
            Assert.That(settings.OverrideScreenshotsFolder, Is.True);
            Assert.That(settings.ScreenshotsFolder, Is.EqualTo(Path.GetDirectoryName(outputPath)));
            Assert.That(settings.UploadSettings.NameFormatPattern, Is.EqualTo("xerahs-native-verify"));
            Assert.That(settings.ImageSettings.ImageFormat, Is.EqualTo(EImageFormat.JPEG));
            Assert.That(source.CaptureSettings.MacOSRegionSelectorPreference, Is.EqualTo(MacOSInteractiveRegionSelectorPreference.XerahSOverlay));
        });
    }

    [Test]
    public void CreateVerificationTaskSettings_WhenUploadRequested_PreservesUploadStep()
    {
        var settings = VerifyMacOSNativeCrosshairCommand.CreateVerificationTaskSettings(null, "capture.png", upload: true);

        Assert.Multiple(() =>
        {
            Assert.That(settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.SaveImageToFile), Is.True);
            Assert.That(settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.UploadImageToHost), Is.True);
            Assert.That(settings.AfterUploadJob, Is.EqualTo(AfterUploadTasks.CopyURLToClipboard));
        });
    }

    [TestCase(null, true, null)]
    [TestCase(1, true, null)]
    [TestCase(0, false, "Timeout must be greater than zero seconds.")]
    [TestCase(-1, false, "Timeout must be greater than zero seconds.")]
    public void TryNormalizeTimeout_ValidatesPositiveTimeouts(int? seconds, bool expectedResult, string? expectedError)
    {
        bool result = VerifyMacOSNativeCrosshairCommand.TryNormalizeTimeout(seconds, out var timeout, out var error);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(expectedResult));
            Assert.That(error, Is.EqualTo(expectedError));
            if (expectedResult && seconds.HasValue)
            {
                Assert.That(timeout, Is.EqualTo(TimeSpan.FromSeconds(seconds.Value)));
            }
        });
    }
}
