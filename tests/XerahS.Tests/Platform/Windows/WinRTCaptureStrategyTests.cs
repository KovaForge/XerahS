using NUnit.Framework;
using ShareX.Avalonia.Platform.Windows.Capture;

namespace XerahS.Tests.Platform.Windows;

public class WinRTCaptureStrategyTests
{
    [Test]
    public void GetCapabilities_WhenWinRtFallsBackToGdi_ReportsFallbackCapabilities()
    {
        var strategy = new WinRTCaptureStrategy();

        var capabilities = strategy.GetCapabilities();

        Assert.Multiple(() =>
        {
            Assert.That(capabilities.BackendName, Is.EqualTo("WinRT Graphics Capture (GDI fallback)"));
            Assert.That(capabilities.Version, Is.EqualTo("fallback"));
            Assert.That(capabilities.SupportsHardwareAcceleration, Is.False);
            Assert.That(capabilities.SupportsCursorCapture, Is.False);
            Assert.That(capabilities.SupportsHDR, Is.False);
            Assert.That(capabilities.SupportsPerMonitorDpi, Is.True);
            Assert.That(capabilities.SupportsMonitorHotplug, Is.True);
            Assert.That(capabilities.MaxCaptureResolution, Is.EqualTo(32767));
            Assert.That(capabilities.RequiresPermission, Is.False);
        });
    }
}
