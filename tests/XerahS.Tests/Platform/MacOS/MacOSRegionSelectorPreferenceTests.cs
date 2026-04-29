using NUnit.Framework;
using XerahS.Platform.Abstractions;
using XerahS.Platform.MacOS;

namespace XerahS.Tests.Platform.MacOS;

[TestFixture]
public class MacOSRegionSelectorPreferenceTests
{
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
