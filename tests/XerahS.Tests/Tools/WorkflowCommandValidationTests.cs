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
public class WorkflowCommandValidationTests
{
    [TestCase(null, "Region must be specified as x,y,width,height.")]
    [TestCase("", "Region must be specified as x,y,width,height.")]
    [TestCase("0,0,100", "Region must be in format x,y,width,height.")]
    [TestCase("0,0,abc,100", "Region values must be integers in format x,y,width,height.")]
    [TestCase("0,0,0,100", "Region width and height must be greater than zero.")]
    [TestCase("0,0,100,-1", "Region width and height must be greater than zero.")]
    public void TryParseRegion_WhenInputInvalid_ReturnsExpectedError(string? input, string expectedError)
    {
        bool result = WorkflowCommand.TryParseRegion(input, out var rect, out var error);

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
        bool result = WorkflowCommand.TryParseRegion("10, 20, 300, 400", out var rect, out var error);

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

    [TestCase(-1, false, "Duration must be zero or greater.")]
    [TestCase(0, true, null)]
    [TestCase(30, true, null)]
    public void TryValidateDuration_RejectsNegativeValues(int duration, bool expectedResult, string? expectedError)
    {
        bool result = WorkflowCommand.TryValidateDuration(duration, out var error);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(expectedResult));
            Assert.That(error, Is.EqualTo(expectedError));
        });
    }
}
