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
using System.Reflection;
using XerahS.UI.Services;

namespace XerahS.Tests.Editor;

[TestFixture]
public class AvaloniaUIServiceSendToTests
{
    [Test]
    public async Task OpenImageFileInEditorAsync_DisposesLoadedAndRenderedBitmaps()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        SaveBitmap(path);

        SKBitmap? loadedBitmap = null;
        SKBitmap? renderedBitmap = null;

        try
        {
            await InvokeOpenImageFileInEditorAsync(path, (bitmap, sourcePath) =>
            {
                loadedBitmap = bitmap;
                Assert.That(sourcePath, Is.EqualTo(path));

                renderedBitmap = new SKBitmap(2, 2);
                renderedBitmap.Erase(SKColors.CadetBlue);
                return Task.FromResult<SKBitmap?>(renderedBitmap);
            });

            Assert.That(loadedBitmap, Is.Not.Null);
            Assert.That(renderedBitmap, Is.Not.Null);
            Assert.That(loadedBitmap!.Handle, Is.EqualTo(IntPtr.Zero));
            Assert.That(renderedBitmap!.Handle, Is.EqualTo(IntPtr.Zero));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static Task InvokeOpenImageFileInEditorAsync(
        string path,
        Func<SKBitmap, string?, Task<SKBitmap?>> showEditorAsync)
    {
        var method = typeof(AvaloniaUIService).GetMethod("OpenImageFileInEditorAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(AvaloniaUIService), "OpenImageFileInEditorAsync");

        return (Task)(method.Invoke(null, new object?[] { path, showEditorAsync })
            ?? throw new InvalidOperationException("OpenImageFileInEditorAsync returned null."));
    }

    private static void SaveBitmap(string path)
    {
        using var bitmap = new SKBitmap(1, 1);
        bitmap.Erase(SKColors.White);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
    }
}
