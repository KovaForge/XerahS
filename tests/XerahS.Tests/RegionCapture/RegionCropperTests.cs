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

using System.Drawing;
using System.Runtime.InteropServices;
using NUnit.Framework;
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.Tests.RegionCapture;

[TestFixture]
public class RegionCropperTests
{
    [Test]
    public void CropFrame_PaddedStride_CopiesRequestedPixelsOnly()
    {
        const int width = 4;
        const int height = 3;
        const int stride = 20;

        byte[] source =
        [
            1, 2, 3, 4,   5, 6, 7, 8,   9, 10, 11, 12,  13, 14, 15, 16,  90, 91, 92, 93,
            17, 18, 19, 20,  21, 22, 23, 24,  25, 26, 27, 28,  29, 30, 31, 32,  94, 95, 96, 97,
            33, 34, 35, 36,  37, 38, 39, 40,  41, 42, 43, 44,  45, 46, 47, 48,  98, 99, 100, 101
        ];

        IntPtr sourcePtr = Marshal.AllocHGlobal(source.Length);
        Marshal.Copy(source, 0, sourcePtr, source.Length);

        FrameData cropped = default;

        try
        {
            var frame = new FrameData
            {
                DataPtr = sourcePtr,
                Stride = stride,
                Width = width,
                Height = height,
                Format = PixelFormat.Bgra32
            };

            cropped = RegionCropper.CropFrame(frame, new Rectangle(1, 1, 2, 2));

            byte[] actual = new byte[cropped.Stride * cropped.Height];
            Marshal.Copy(cropped.DataPtr, actual, 0, actual.Length);

            Assert.Multiple(() =>
            {
                Assert.That(cropped.Stride, Is.EqualTo(8));
                Assert.That(actual, Is.EqualTo(new byte[]
                {
                    21, 22, 23, 24, 25, 26, 27, 28,
                    37, 38, 39, 40, 41, 42, 43, 44
                }));
            });
        }
        finally
        {
            if (cropped.DataPtr != IntPtr.Zero)
            {
                RegionCropper.FreeCroppedFrame(cropped);
            }

            Marshal.FreeHGlobal(sourcePtr);
        }
    }

    [Test]
    public void CropFrame_InvalidStride_Throws()
    {
        byte[] source = new byte[16];
        IntPtr sourcePtr = Marshal.AllocHGlobal(source.Length);

        try
        {
            Marshal.Copy(source, 0, sourcePtr, source.Length);
            var frame = new FrameData
            {
                DataPtr = sourcePtr,
                Stride = 12,
                Width = 4,
                Height = 1,
                Format = PixelFormat.Bgra32
            };

            Assert.That(
                () => RegionCropper.CropFrame(frame, new Rectangle(0, 0, 1, 1)),
                Throws.ArgumentException.With.Message.Contains("stride"));
        }
        finally
        {
            Marshal.FreeHGlobal(sourcePtr);
        }
    }
}
