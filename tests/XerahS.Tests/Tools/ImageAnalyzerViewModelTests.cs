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
using XerahS.UI.ViewModels;

namespace XerahS.Tests.Tools;

[TestFixture]
public class ImageAnalyzerViewModelTests
{
    [Test]
    public void SetInputImage_AnalyzesInMemoryCapture()
    {
        using var bitmap = new SKBitmap(12, 8);
        bitmap.Erase(SKColors.CornflowerBlue);

        var viewModel = new ImageAnalyzerViewModel();

        viewModel.SetInputImage(bitmap);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasInput, Is.True);
            Assert.That(viewModel.InputFilePath, Is.Null);
            Assert.That(viewModel.InputDisplayText, Is.EqualTo("Captured image"));
            Assert.That(viewModel.StatusText, Is.EqualTo("Captured image (12 x 8)"));
            Assert.That(viewModel.Properties.Any(x => x.Category == "Image" && x.Name == "Width" && x.Value == "12 px"), Is.True);
            Assert.That(viewModel.Properties.Any(x => x.Category == "Image" && x.Name == "Height" && x.Value == "8 px"), Is.True);
            Assert.That(viewModel.Properties.Any(x => x.Category == "Source" && x.Name == "Type" && x.Value == "In-memory image"), Is.True);
        });
    }
}
