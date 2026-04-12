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
using System.Reflection;
using XerahS.Core;
using XerahS.Platform.Abstractions;

namespace XerahS.Tests.Helpers;

/// <summary>
/// Unit tests for AfterCapture task flags defined in TaskEnums.cs.
/// Verifies that DoOCR, BeautifyImage, ScanQRCode and other AfterCapture tasks
/// are correctly defined as powers of 2 for flag-based composition.
/// </summary>
[TestFixture]
public class AfterCaptureTaskFlagsTests
{
    [Test]
    public void DoOCR_IsDefinedAsBitFlag()
    {
        // DoOCR must be a single bit to allow composition with other AfterCaptureTasks
        Assert.Multiple(() =>
        {
            // 1 << 17 = 131072
            Assert.That((int)AfterCaptureTasks.DoOCR, Is.EqualTo(1 << 17));
            Assert.That((int)(AfterCaptureTasks.DoOCR & (AfterCaptureTasks.DoOCR - 1)), Is.EqualTo(0), "DoOCR must be a single bit");
        });
    }

    [Test]
    public void BeautifyImage_IsDefinedAsBitFlag()
    {
        Assert.Multiple(() =>
        {
            Assert.That((int)AfterCaptureTasks.BeautifyImage, Is.EqualTo(1 << 2));
        });
    }

    [Test]
    public void ScanQRCode_IsDefinedAsBitFlag()
    {
        Assert.Multiple(() =>
        {
            Assert.That((int)AfterCaptureTasks.ScanQRCode, Is.EqualTo(1 << 16));
        });
    }

    [Test]
    public void AllAfterCaptureTaskFlags_AreDistinctPowersOfTwo()
    {
        var values = typeof(AfterCaptureTasks)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.Name != nameof(AfterCaptureTasks.None))
            .Where(field => field.GetCustomAttribute<ObsoleteAttribute>() == null)
            .Select(field => (AfterCaptureTasks)field.GetValue(null)!)
            .ToList();

        var bits = values.Select(v => (int)v).ToList();

        // Verify each value is a power of 2
        foreach (var value in values)
        {
            int bit = (int)value;
            Assert.That(bit & (bit - 1), Is.EqualTo(0), $"{value} is not a power of 2");
        }

        // Verify no duplicate values
        Assert.That(bits, Is.Unique);
    }

    [Test]
    public void AfterCaptureTasks_CanBeComposed()
    {
        // Verify flags can be safely combined without collision
        var combined = AfterCaptureTasks.DoOCR
                             | AfterCaptureTasks.BeautifyImage
                             | AfterCaptureTasks.ScanQRCode
                             | AfterCaptureTasks.ShowAfterCaptureWindow;

        Assert.Multiple(() =>
        {
            Assert.That(combined.HasFlag(AfterCaptureTasks.DoOCR), Is.True);
            Assert.That(combined.HasFlag(AfterCaptureTasks.BeautifyImage), Is.True);
            Assert.That(combined.HasFlag(AfterCaptureTasks.ScanQRCode), Is.True);
            Assert.That(combined.HasFlag(AfterCaptureTasks.ShowAfterCaptureWindow), Is.True);
        });
    }
}
