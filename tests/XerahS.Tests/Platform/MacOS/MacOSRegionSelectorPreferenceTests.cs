using NUnit.Framework;
using XerahS.Platform.Abstractions;
using XerahS.Platform.MacOS;

namespace XerahS.Tests.Platform.MacOS;

[TestFixture]
public class MacOSRegionSelectorPreferenceTests
{
    [Test]
    public void NativeRegionCaptureArguments_ForceSelectionOnlyInteractiveCapture()
    {
        Assert.That(MacOSScreenshotService.NativeRegionCaptureArguments, Does.Contain("-i"));
        Assert.That(MacOSScreenshotService.NativeRegionCaptureArguments, Does.Contain("-s"));
        Assert.That(MacOSScreenshotService.NativeRegionCaptureArguments, Does.Contain("-x"));
        Assert.That(MacOSScreenshotService.NativeRegionCaptureArguments, Does.Contain("-t png"));
    }

    [TestCase(MacOSInteractiveRegionSelectorPreference.Automatic)]
    [TestCase(MacOSInteractiveRegionSelectorPreference.XerahSOverlay)]
    public async Task CaptureRegionAsync_SkipsNativeCrosshair_WhenNativeCrosshairIsNotRequested(
        MacOSInteractiveRegionSelectorPreference preference)
    {
        var service = new MacOSScreenshotService();

        var bitmap = await service.CaptureRegionAsync(new CaptureOptions
        {
            MacOSRegionSelectorPreference = preference
        });

        Assert.That(bitmap, Is.Null);
    }
}
