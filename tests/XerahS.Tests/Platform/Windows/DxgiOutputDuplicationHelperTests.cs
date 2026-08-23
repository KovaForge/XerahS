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

public class DxgiOutputDuplicationHelperTests
{
    [Test]
    public void ShouldUseDuplicateOutput1_FalseOnArm64()
    {
        Assert.That(DxgiOutputDuplicationHelper.ShouldUseDuplicateOutput1(Architecture.Arm64), Is.False);
        Assert.That(DxgiOutputDuplicationHelper.ShouldUseDuplicateOutput1(Architecture.Arm), Is.False);
    }

    [Test]
    public void ShouldUseDuplicateOutput1_TrueOnIntel()
    {
        Assert.That(DxgiOutputDuplicationHelper.ShouldUseDuplicateOutput1(Architecture.X64), Is.True);
        Assert.That(DxgiOutputDuplicationHelper.ShouldUseDuplicateOutput1(Architecture.X86), Is.True);
    }
}
