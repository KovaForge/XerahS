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
using SkiaSharp;
using Vortice.DXGI;
using XerahS.Platform.Windows.Capture;

namespace XerahS.Tests.Platform.Windows;

[TestFixture]
public class DxgiHdrToneMapperTests
{
    [Test]
    public void IsHdrFormat_DetectsFloatAndHdr10()
    {
        Assert.That(DxgiHdrToneMapper.IsHdrFormat(Format.R16G16B16A16_Float), Is.True);
        Assert.That(DxgiHdrToneMapper.IsHdrFormat(Format.R10G10B10A2_UNorm), Is.True);
        Assert.That(DxgiHdrToneMapper.IsHdrFormat(Format.B8G8R8A8_UNorm), Is.False);
    }

    [Test]
    public void LinearToSrgbByte_MapsZeroAndOne()
    {
        Assert.That(DxgiHdrToneMapper.LinearToSrgbByte(0f), Is.EqualTo(0));
        Assert.That(DxgiHdrToneMapper.LinearToSrgbByte(1f), Is.EqualTo(255));
    }

    [Test]
    public void PqToLinearSrgb_MapsZeroToZero()
    {
        Assert.That(DxgiHdrToneMapper.PqToLinearSrgb(0f, 1f), Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void PqToLinearSrgb_ScalesBySdrWhiteLevel()
    {
        float unscaled = DxgiHdrToneMapper.PqToLinearSrgb(0.5f, 1f);
        float scaled = DxgiHdrToneMapper.PqToLinearSrgb(0.5f, 2f);
        Assert.That(scaled, Is.EqualTo(unscaled / 2f).Within(0.0001f));
        Assert.That(unscaled, Is.GreaterThan(0f));
    }

    [Test]
    public void ToneMapAndCompressGamut_LeavesSdrValuesUnchanged()
    {
        float red = 0.4f;
        float green = 0.5f;
        float blue = 0.6f;
        DxgiHdrToneMapper.ToneMapAndCompressGamut(ref red, ref green, ref blue, relativePeak: 12.5f);

        Assert.That(red, Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(green, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(blue, Is.EqualTo(0.6f).Within(0.0001f));
    }

    [Test]
    public void ToneMapAndCompressGamut_CompressesHighlightsIntoSdr()
    {
        float red = 4f;
        float green = 4f;
        float blue = 4f;
        DxgiHdrToneMapper.ToneMapAndCompressGamut(ref red, ref green, ref blue, relativePeak: 12.5f);

        Assert.That(red, Is.GreaterThan(0.75f).And.LessThanOrEqualTo(1f));
        Assert.That(green, Is.EqualTo(red).Within(0.0001f));
        Assert.That(blue, Is.EqualTo(red).Within(0.0001f));
    }

    [Test]
    public void ConvertRec2020ToSrgb_PreservesNeutralGray()
    {
        float red = 0.5f;
        float green = 0.5f;
        float blue = 0.5f;
        DxgiHdrToneMapper.ConvertRec2020ToSrgb(ref red, ref green, ref blue);

        Assert.That(red, Is.EqualTo(0.5f).Within(0.002f));
        Assert.That(green, Is.EqualTo(0.5f).Within(0.002f));
        Assert.That(blue, Is.EqualTo(0.5f).Within(0.002f));
    }

    [Test]
    public void HdrToneMapContext_Normalize_ClampsInvalidValues()
    {
        var normalized = new HdrToneMapContext(0f, 10f).Normalize();
        Assert.That(normalized.SdrWhiteScale, Is.EqualTo(1f));
        Assert.That(normalized.PeakNits, Is.EqualTo(HdrToneMapContext.DefaultPeakNits));
    }

    [Test]
    public void CopyToneMappedPixels_ReplacesExtendedRangeAndMismatchedSdr()
    {
        using var destination = new SKBitmap(2, 1, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var toneMapped = new SKBitmap(2, 1, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var reference = new SKBitmap(2, 1, SKColorType.Bgra8888, SKAlphaType.Unpremul);

        SetBgra(destination, 0, 0, 10, 20, 30, 255);
        SetBgra(destination, 1, 0, 40, 50, 60, 255);
        SetBgra(toneMapped, 0, 0, 1, 2, 3, 255);
        SetBgra(toneMapped, 1, 0, 7, 8, 9, 255);
        SetBgra(reference, 0, 0, 10, 20, 30, 0);
        SetBgra(reference, 1, 0, 0, 0, 0, 255);

        HdrScreenshotColorCorrector.CopyToneMappedPixels(
            destination,
            new SKRectI(0, 0, 2, 1),
            toneMapped,
            reference,
            new SKRectI(0, 0, 2, 1));

        Assert.That(GetBgra(destination, 0, 0), Is.EqualTo((10, 20, 30, 255)));
        Assert.That(GetBgra(destination, 1, 0), Is.EqualTo((7, 8, 9, 255)));
    }

    private static void SetBgra(SKBitmap bitmap, int x, int y, byte b, byte g, byte r, byte a)
    {
        unsafe
        {
            byte* pixel = (byte*)bitmap.GetPixels() + (y * bitmap.RowBytes) + (x * 4);
            pixel[0] = b;
            pixel[1] = g;
            pixel[2] = r;
            pixel[3] = a;
        }
    }

    private static (byte B, byte G, byte R, byte A) GetBgra(SKBitmap bitmap, int x, int y)
    {
        unsafe
        {
            byte* pixel = (byte*)bitmap.GetPixels() + (y * bitmap.RowBytes) + (x * 4);
            return (pixel[0], pixel[1], pixel[2], pixel[3]);
        }
    }
}
