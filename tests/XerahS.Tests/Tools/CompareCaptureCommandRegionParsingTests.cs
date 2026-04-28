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
using XerahS.CLI.Commands;

namespace XerahS.Tests.Tools;

[TestFixture]
public class CompareCaptureCommandRegionParsingTests
{
    [TestCase(null, "Region must be specified as 'x,y,width,height'.")]
    [TestCase("", "Region must be specified as 'x,y,width,height'.")]
    [TestCase("0,0,100", "Region must be in format 'x,y,width,height'.")]
    [TestCase("0,0,abc,100", "Region values must be integers in format 'x,y,width,height'.")]
    [TestCase("0,0,0,100", "Region width and height must be greater than zero.")]
    [TestCase("0,0,100,-1", "Region width and height must be greater than zero.")]
    [TestCase("2147483647,0,1,100", "Region bounds are outside supported coordinate range.")]
    [TestCase("0,2147483647,100,1", "Region bounds are outside supported coordinate range.")]
    public void TryParseRegion_WhenInputInvalid_ReturnsExpectedError(string? input, string expectedError)
    {
        bool result = CompareCaptureCommand.TryParseRegion(input, out var rect, out var error);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(error, Is.EqualTo(expectedError));
            Assert.That(rect, Is.EqualTo(default(SkiaSharp.SKRect)));
        });
    }

    [Test]
    public void TryParseRegion_WhenInputValid_ParsesRectangle()
    {
        bool result = CompareCaptureCommand.TryParseRegion("10, 20, 300, 400", out var rect, out var error);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(rect.Left, Is.EqualTo(10));
            Assert.That(rect.Top, Is.EqualTo(20));
            Assert.That(rect.Right, Is.EqualTo(310));
            Assert.That(rect.Bottom, Is.EqualTo(420));
        });
    }
}
