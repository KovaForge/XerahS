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
}
