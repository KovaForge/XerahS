using System.Drawing;
using NUnit.Framework;
using SkiaSharp;
using XerahS.Platform.Windows.Capture;

namespace XerahS.Tests.Platform.Windows;

public class WindowsModernCaptureServiceTests
{
    [Test]
    public void TryCreateDxgiCropRect_TranslatesAndClampsFractionalScreenCoordinates()
    {
        var virtualBounds = new Rectangle(-100, -50, 300, 200);
        var rect = new SKRect(-99.6f, -49.2f, 25.1f, 80.9f);

        bool created = DxgiCropRectHelper.TryCreateCropRect(rect, virtualBounds, 300, 200, out var cropRect);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.True);
            Assert.That(cropRect, Is.EqualTo(new SKRectI(0, 0, 126, 131)));
        });
    }

    [Test]
    public void TryCreateDxgiCropRect_ScalesVirtualDesktopCoordinatesToBitmapPixels()
    {
        var virtualBounds = new Rectangle(-100, -50, 150, 100);
        var rect = new SKRect(-99.6f, -49.2f, 25.1f, 20.4f);

        bool created = DxgiCropRectHelper.TryCreateCropRect(rect, virtualBounds, 300, 200, out var cropRect);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.True);
            Assert.That(cropRect, Is.EqualTo(new SKRectI(0, 1, 251, 141)));
        });
    }

    [Test]
    public void TryCreateDxgiCropRect_RejectsEmptyVirtualBounds()
    {
        var virtualBounds = new Rectangle(0, 0, 0, 100);
        var rect = new SKRect(0, 0, 10, 10);

        bool created = DxgiCropRectHelper.TryCreateCropRect(rect, virtualBounds, 100, 100, out var cropRect);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.False);
            Assert.That(cropRect, Is.EqualTo(default(SKRectI)));
        });
    }

    [Test]
    public void TryCreateDxgiCropRect_RejectsNonFiniteCoordinatesBeforeCasting()
    {
        var virtualBounds = new Rectangle(0, 0, 100, 100);
        var rect = new SKRect(0, 0, float.PositiveInfinity, 10);

        bool created = DxgiCropRectHelper.TryCreateCropRect(rect, virtualBounds, 100, 100, out var cropRect);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.False);
            Assert.That(cropRect, Is.EqualTo(default(SKRectI)));
        });
    }

    [Test]
    public void TryCreateDxgiCropRect_ClampsHugeFiniteCoordinatesWithoutOverflowing()
    {
        var virtualBounds = new Rectangle(int.MinValue, int.MinValue, 100, 100);
        var rect = new SKRect(int.MaxValue - 10f, int.MaxValue - 10f, float.MaxValue, float.MaxValue);

        bool created = DxgiCropRectHelper.TryCreateCropRect(rect, virtualBounds, 100, 100, out var cropRect);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.False);
            Assert.That(cropRect, Is.EqualTo(default(SKRectI)));
        });
    }

    [Test]
    public void TryCreateGdiCaptureRect_RoundsOutwardAndClampsToVirtualScreen()
    {
        var screenBounds = new Rectangle(-100, -50, 300, 200);
        var rect = new SKRect(-99.6f, -49.2f, 25.1f, 80.9f);

        bool created = GdiCaptureRectHelper.TryCreateCaptureRect(rect, screenBounds, out var captureRect);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.True);
            Assert.That(captureRect, Is.EqualTo(new Rectangle(-100, -50, 126, 131)));
        });
    }

    [Test]
    public void TryCreateGdiCaptureRect_RejectsNonFiniteCoordinatesBeforeCasting()
    {
        var screenBounds = new Rectangle(0, 0, 100, 100);
        var rect = new SKRect(0, 0, float.PositiveInfinity, 10);

        bool created = GdiCaptureRectHelper.TryCreateCaptureRect(rect, screenBounds, out var captureRect);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.False);
            Assert.That(captureRect, Is.EqualTo(default(Rectangle)));
        });
    }
}

