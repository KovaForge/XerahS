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

using System.Runtime.InteropServices;
using NUnit.Framework;
using XerahS.Platform.Windows.Capture;

namespace XerahS.Tests.Platform.Windows;

[TestFixture]
public class BgraRowCopyHelperTests
{
    [Test]
    public void CopyRows_PaddedSourceStride_CopiesOnlyPixelBytes()
    {
        const int bytesPerRow = 8;
        const int sourceStride = 16;
        const int height = 2;

        byte[] source =
        [
            1, 2, 3, 4, 5, 6, 7, 8, 200, 201, 202, 203, 204, 205, 206, 207,
            9, 10, 11, 12, 13, 14, 15, 16, 210, 211, 212, 213, 214, 215, 216, 217
        ];
        byte[] destination = new byte[bytesPerRow * height];

        IntPtr sourcePtr = Marshal.AllocHGlobal(source.Length);
        IntPtr destinationPtr = Marshal.AllocHGlobal(destination.Length);

        try
        {
            Marshal.Copy(source, 0, sourcePtr, source.Length);
            BgraRowCopyHelper.CopyRows(sourcePtr, sourceStride, destinationPtr, bytesPerRow, bytesPerRow, height);
            Marshal.Copy(destinationPtr, destination, 0, destination.Length);
        }
        finally
        {
            Marshal.FreeHGlobal(destinationPtr);
            Marshal.FreeHGlobal(sourcePtr);
        }

        Assert.That(destination, Is.EqualTo(new byte[]
        {
            1, 2, 3, 4, 5, 6, 7, 8,
            9, 10, 11, 12, 13, 14, 15, 16
        }));
    }

    [Test]
    public void CopyRows_FlipVertically_ReversesRowOrderWithoutUsingPadding()
    {
        const int bytesPerRow = 4;
        const int sourceStride = 8;
        const int height = 3;

        byte[] source =
        [
            1, 2, 3, 4, 50, 51, 52, 53,
            5, 6, 7, 8, 60, 61, 62, 63,
            9, 10, 11, 12, 70, 71, 72, 73
        ];
        byte[] destination = new byte[bytesPerRow * height];

        IntPtr sourcePtr = Marshal.AllocHGlobal(source.Length);
        IntPtr destinationPtr = Marshal.AllocHGlobal(destination.Length);

        try
        {
            Marshal.Copy(source, 0, sourcePtr, source.Length);
            BgraRowCopyHelper.CopyRows(sourcePtr, sourceStride, destinationPtr, bytesPerRow, bytesPerRow, height, flipVertically: true);
            Marshal.Copy(destinationPtr, destination, 0, destination.Length);
        }
        finally
        {
            Marshal.FreeHGlobal(destinationPtr);
            Marshal.FreeHGlobal(sourcePtr);
        }

        Assert.That(destination, Is.EqualTo(new byte[]
        {
            9, 10, 11, 12,
            5, 6, 7, 8,
            1, 2, 3, 4
        }));
    }

    [Test]
    public void CopyRows_NegativeSourceStride_NormalizesToTopRow()
    {
        const int bytesPerRow = 4;
        const int sourceStride = -8;
        const int height = 2;

        byte[] source =
        [
            1, 2, 3, 4, 101, 102, 103, 104,
            5, 6, 7, 8, 105, 106, 107, 108
        ];
        byte[] destination = new byte[bytesPerRow * height];

        IntPtr sourceBase = Marshal.AllocHGlobal(source.Length);
        IntPtr destinationPtr = Marshal.AllocHGlobal(destination.Length);

        try
        {
            Marshal.Copy(source, 0, sourceBase, source.Length);
            IntPtr bottomRowPointer = IntPtr.Add(sourceBase, 8);
            BgraRowCopyHelper.CopyRows(bottomRowPointer, sourceStride, destinationPtr, bytesPerRow, bytesPerRow, height);
            Marshal.Copy(destinationPtr, destination, 0, destination.Length);
        }
        finally
        {
            Marshal.FreeHGlobal(destinationPtr);
            Marshal.FreeHGlobal(sourceBase);
        }

        Assert.That(destination, Is.EqualTo(new byte[]
        {
            1, 2, 3, 4,
            5, 6, 7, 8
        }));
    }
}
