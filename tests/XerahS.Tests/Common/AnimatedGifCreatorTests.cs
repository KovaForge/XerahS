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

#nullable enable

using NUnit.Framework;
using XerahS.Common.GIF;

namespace XerahS.Tests.Common;

[TestFixture]
public sealed class AnimatedGifCreatorTests
{
    [TestCase(0, 0x00, 0x00)]
    [TestCase(1, 0x01, 0x00)]
    [TestCase(256, 0x00, 0x01)]
    [TestCase(65535, 0xFF, 0xFF)]
    [TestCase(65536, 0xFF, 0xFF)] // clamps to ushort.MaxValue
    [TestCase(-1, 0x00, 0x00)] // clamps to 0 (endless loop)
    [TestCase(int.MaxValue, 0xFF, 0xFF)]
    public void CreateApplicationExtensionBlock_ClampsRepeatTo16BitRange(int repeat, byte expectedLow, byte expectedHigh)
    {
        using var creator = new AnimatedGifCreator("ignored.gif", delay: 100, repeat: repeat);
        byte[] bytes = creator.CreateApplicationExtensionBlock(repeat);

        Assert.That(bytes.Length, Is.EqualTo(19));
        Assert.That(bytes[0], Is.EqualTo(0x21));
        Assert.That(bytes[1], Is.EqualTo(0xFF));
        Assert.That(bytes[15], Is.EqualTo(0x01)); // loop indicator
        Assert.That(bytes[16], Is.EqualTo(expectedLow), "loop count low byte");
        Assert.That(bytes[17], Is.EqualTo(expectedHigh), "loop count high byte");
        Assert.That(bytes[18], Is.EqualTo(0x00), "block terminator");
    }

    [Test]
    public void Dispose_WithNoFrames_DoesNotThrow()
    {
        Assert.DoesNotThrow(() =>
        {
            using var creator = new AnimatedGifCreator("ignored.gif", delay: 100, repeat: 0);
        });
    }
}
