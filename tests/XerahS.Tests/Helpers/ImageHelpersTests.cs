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
using SkiaSharp;
using XerahS.Common;

namespace XerahS.Tests.Helpers;

public class ImageHelpersTests
{
    [Test]
    public void SaveBitmap_WithLeafFileName_SavesIntoCurrentDirectoryWithoutThrowing()
    {
        using var bitmap = new SKBitmap(2, 2);
        string fileName = $"xerahs-imagehelpers-{Guid.NewGuid():N}.png";
        string filePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, fileName);

        try
        {
            Assert.That(() => ImageHelpers.SaveBitmap(bitmap, fileName), Throws.Nothing);
            Assert.That(File.Exists(filePath), Is.True);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
