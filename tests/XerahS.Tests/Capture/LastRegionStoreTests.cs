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
using NUnit.Framework;
using XerahS.Core.Capture;

namespace XerahS.Tests.Capture;

[TestFixture]
public class LastRegionStoreTests
{
    [SetUp]
    public void SetUp() => LastRegionStore.Clear();

    [TearDown]
    public void TearDown() => LastRegionStore.Clear();

    [Test]
    public void TryGet_WhenEmpty_ReturnsFalse()
    {
        Assert.That(LastRegionStore.TryGet(out var region), Is.False);
        Assert.That(region, Is.EqualTo(Rectangle.Empty));
    }

    [Test]
    public void Set_ThenTryGet_ReturnsStoredRectangle()
    {
        LastRegionStore.Set(12, 34, 56, 78);

        Assert.That(LastRegionStore.TryGet(out var region), Is.True);
        Assert.That(region, Is.EqualTo(new Rectangle(12, 34, 56, 78)));
    }

    [Test]
    public void Set_DoesNotUseCustomRegionSemantics()
    {
        LastRegionStore.Set(new Rectangle(1, 2, 3, 4));
        LastRegionStore.Set(new Rectangle(0, 0, 0, 0));

        Assert.That(LastRegionStore.TryGet(out _), Is.False);
    }
}
