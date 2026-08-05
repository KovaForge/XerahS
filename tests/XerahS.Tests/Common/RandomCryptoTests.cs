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

using System;
using NUnit.Framework;
using XerahS.Common;

namespace XerahS.Tests.Common;

[TestFixture]
public sealed class RandomCryptoTests
{
    [Test]
    public void Next_InclusiveRange_ReturnsValuesWithinBounds()
    {
        for (int i = 0; i < 64; i++)
        {
            int value = RandomCrypto.Next(3, 7);
            Assert.That(value, Is.InRange(3, 7));
        }
    }

    [Test]
    public void Next_EqualMinAndMax_ReturnsThatValue()
    {
        Assert.That(RandomCrypto.Next(42, 42), Is.EqualTo(42));
        Assert.That(RandomCrypto.Next(int.MaxValue, int.MaxValue), Is.EqualTo(int.MaxValue));
        Assert.That(RandomCrypto.Next(int.MinValue, int.MinValue), Is.EqualTo(int.MinValue));
    }

    [Test]
    public void Next_IntMaxValueUpperBound_DoesNotOverflowAndStaysInRange()
    {
        for (int i = 0; i < 32; i++)
        {
            int value = RandomCrypto.Next(int.MaxValue - 3, int.MaxValue);
            Assert.That(value, Is.InRange(int.MaxValue - 3, int.MaxValue));
        }
    }

    [Test]
    public void Next_MinGreaterThanMax_Throws()
    {
        Assert.That(() => RandomCrypto.Next(5, 4), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void Next_SingleArgNegative_Throws()
    {
        Assert.That(() => RandomCrypto.Next(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
    }
}
