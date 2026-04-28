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
using ShareX.ImageEditor.Hosting;
using SkiaSharp;
using XerahS.CLI.Services;
using XerahS.Platform.Abstractions;

namespace XerahS.Tests.Editor;

[TestFixture]
public class HeadlessEditorServiceTests
{
    [Test]
    public async Task CliHeadlessShowEditorAsync_ReturnsNull_WhenImageEditorUnavailable()
    {
        using var image = new SKBitmap(4, 3);
        var service = new HeadlessUIService();

        SKBitmap? edited = await service.ShowEditorAsync(image, taskMode: true);

        Assert.That(edited, Is.Null);
    }

    [Test]
    public async Task CliHeadlessShowEditorSessionAsync_ReturnsNull_WhenImageEditorUnavailable()
    {
        using var image = new SKBitmap(4, 3);
        IUIService service = new HeadlessUIService();

        ImageEditorSessionResult? result = await service.ShowEditorSessionAsync(image, taskMode: true);

        Assert.That(result, Is.Null);
    }
}
