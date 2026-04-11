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
using ShareX.ImageEditor.Core.Annotations;
using ShareX.ImageEditor.Core.Persistence;
using SkiaSharp;
using XerahS.History;

namespace XerahS.Tests.Editor;

[TestFixture]
public class XannProjectFileServiceTests
{
    [Test]
    public async Task SaveLoad_RoundTripsPolymorphicAnnotationsAndEmbeddedImages()
    {
        string directory = CreateTempDirectory();
        string imagePath = Path.Combine(directory, "capture.png");

        using var source = new SKBitmap(24, 16);
        source.Erase(SKColors.White);
        SaveBitmap(imagePath, source);

        using var embedded = new SKBitmap(4, 4);
        embedded.Erase(SKColors.Blue);

        var imageAnnotation = new ImageAnnotation
        {
            StartPoint = new SKPoint(1, 2),
            EndPoint = new SKPoint(5, 6)
        };
        imageAnnotation.SetImage(embedded.Copy()!);

        var annotations = new Annotation[]
        {
            new RectangleAnnotation
            {
                StartPoint = new SKPoint(2, 3),
                EndPoint = new SKPoint(10, 12),
                StrokeColor = "#ff0000",
                StrokeWidth = 3,
                RotationAngle = 15
            },
            new FreehandAnnotation
            {
                Points = new List<SKPoint>
                {
                    new(1, 1),
                    new(2, 3),
                    new(4, 8)
                }
            },
            imageAnnotation
        };

        try
        {
            string? sidecarPath = await XannProjectFileService.SaveAsync(imagePath, source, annotations);

            Assert.That(sidecarPath, Is.Not.Null);
            Assert.That(File.Exists(sidecarPath!), Is.True);

            var loaded = await XannProjectFileService.LoadAsync(sidecarPath!, imagePath);

            Assert.That(loaded.ImageHashMatches, Is.True);
            Assert.That(loaded.SourceImage.Width, Is.EqualTo(source.Width));
            Assert.That(loaded.SourceImage.Height, Is.EqualTo(source.Height));
            Assert.That(loaded.Project.Annotations, Has.Count.EqualTo(3));
            Assert.That(loaded.Project.Annotations[0], Is.TypeOf<RectangleAnnotation>());
            Assert.That(((RectangleAnnotation)loaded.Project.Annotations[0]).RotationAngle, Is.EqualTo(15).Within(0.001));
            Assert.That(((FreehandAnnotation)loaded.Project.Annotations[1]).Points, Has.Count.EqualTo(3));

            var loadedImageAnnotation = (ImageAnnotation)loaded.Project.Annotations[2];
            Assert.That(loadedImageAnnotation.ImageBitmap, Is.Not.Null);
            Assert.That(loadedImageAnnotation.ImageBitmap!.GetPixel(0, 0), Is.EqualTo(SKColors.Blue));

            loaded.SourceImage.Dispose();
        }
        finally
        {
            imageAnnotation.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task SaveAsync_DeletesExistingSidecar_WhenAnnotationsAreEmpty()
    {
        string directory = CreateTempDirectory();
        string imagePath = Path.Combine(directory, "capture.png");

        using var source = new SKBitmap(8, 8);
        source.Erase(SKColors.White);
        SaveBitmap(imagePath, source);

        try
        {
            string? sidecarPath = await XannProjectFileService.SaveAsync(
                imagePath,
                source,
                new Annotation[] { new RectangleAnnotation { EndPoint = new SKPoint(1, 1) } });

            Assert.That(File.Exists(sidecarPath!), Is.True);

            string? emptySavePath = await XannProjectFileService.SaveAsync(imagePath, source, Array.Empty<Annotation>());

            Assert.That(emptySavePath, Is.Null);
            Assert.That(File.Exists(sidecarPath!), Is.False);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void HistoryItem_StoresAnnotationSidecarPath_InTags()
    {
        string sidecarPath = Path.Combine(Path.GetTempPath(), $"xerahs-{Guid.NewGuid():N}.xann");
        var item = new HistoryItem
        {
            AnnotationSidecarPath = sidecarPath
        };

        Assert.That(item.Tags[nameof(HistoryItem.AnnotationSidecarPath)], Is.EqualTo(sidecarPath));

        item.AnnotationSidecarPath = null;

        Assert.That(item.Tags.ContainsKey(nameof(HistoryItem.AnnotationSidecarPath)), Is.False);
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-xann-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void SaveBitmap(string path, SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
    }
}
