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
using ShareX.ImageEditor.Core.Editor;
using SkiaSharp;

namespace XerahS.Tests.Editor;

[TestFixture]
public class EditorHistoryEffectsTests
{
    private EditorCore _core = null!;

    [SetUp]
    public void SetUp()
    {
        _core = new EditorCore();
        _core.LoadImage(CreateBitmap(SKColors.CornflowerBlue));
    }

    [TearDown]
    public void TearDown()
    {
        _core.Dispose();
    }

    [Test]
    public void ApplyImageOperation_UndoRedo_PreservesAnnotationsAcrossHistory()
    {
        _core.AddAnnotation(CreateRectangle(10, 10, 40, 40));

        bool applied = _core.ApplyImageOperation(_ => CreateBitmap(SKColors.Orange), clearAnnotations: false);

        Assert.That(applied, Is.True);
        Assert.That(_core.Annotations, Has.Count.EqualTo(1));
        Assert.That(GetPixel(_core.SourceImage!), Is.EqualTo(SKColors.Orange));

        _core.Undo();

        Assert.Multiple(() =>
        {
            Assert.That(_core.Annotations, Has.Count.EqualTo(1), "Undoing the canvas step should restore the annotation layer.");
            Assert.That(GetPixel(_core.SourceImage!), Is.EqualTo(SKColors.CornflowerBlue));
            Assert.That(_core.CanRedo, Is.True);
        });

        _core.Redo();

        Assert.Multiple(() =>
        {
            Assert.That(_core.Annotations, Has.Count.EqualTo(1));
            Assert.That(GetPixel(_core.SourceImage!), Is.EqualTo(SKColors.Orange));
            Assert.That(_core.CanRedo, Is.False);
        });
    }

    [Test]
    public void ApplyImageOperation_ClearAnnotations_UndoRestoresAnnotationsAndPixels()
    {
        _core.AddAnnotation(CreateRectangle(5, 5, 20, 20));

        bool applied = _core.ApplyImageOperation(_ => CreateBitmap(SKColors.ForestGreen), clearAnnotations: true);

        Assert.Multiple(() =>
        {
            Assert.That(applied, Is.True);
            Assert.That(_core.Annotations, Is.Empty, "ClearAnnotations operations should drop live annotations immediately.");
            Assert.That(GetPixel(_core.SourceImage!), Is.EqualTo(SKColors.ForestGreen));
        });

        _core.Undo();

        Assert.Multiple(() =>
        {
            Assert.That(_core.Annotations, Has.Count.EqualTo(1), "Undo should restore annotations cleared by the canvas mutation.");
            Assert.That(GetPixel(_core.SourceImage!), Is.EqualTo(SKColors.CornflowerBlue));
        });
    }

    [Test]
    public void NewAnnotationAfterUndo_ClearsRedoStack_ForCanvasHistory()
    {
        _core.ApplyImageOperation(_ => CreateBitmap(SKColors.Orange), clearAnnotations: false);
        _core.Undo();

        Assert.That(_core.CanRedo, Is.True);

        _core.AddAnnotation(CreateRectangle(1, 1, 12, 12));

        Assert.Multiple(() =>
        {
            Assert.That(_core.CanRedo, Is.False, "New annotation work should invalidate stale canvas redo history.");
            Assert.That(_core.Annotations, Has.Count.EqualTo(1));
            Assert.That(GetPixel(_core.SourceImage!), Is.EqualTo(SKColors.CornflowerBlue));
        });
    }

    [Test]
    public void StepAnnotation_AfterUndo_UsesNextVisibleNumber()
    {
        for (int i = 0; i < 5; i++)
        {
            _core.AddAnnotation(CreateStep(_core.NumberCounter++, i * 10, i * 10));
        }

        _core.Undo();
        _core.Undo();

        Assert.That(_core.NumberCounter, Is.EqualTo(4));

        _core.AddAnnotation(CreateStep(_core.NumberCounter++, 60, 60));

        Assert.Multiple(() =>
        {
            Assert.That(_core.Annotations.OfType<NumberAnnotation>().Select(annotation => annotation.Number), Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(_core.NumberCounter, Is.EqualTo(5));
            Assert.That(_core.CanRedo, Is.False);
        });
    }

    [Test]
    public void HistoryChanged_Fires_ForCanvasApplyUndoAndRedo()
    {
        int historyChangedCount = 0;
        _core.HistoryChanged += () => historyChangedCount++;

        _core.ApplyImageOperation(_ => CreateBitmap(SKColors.Orange), clearAnnotations: false);
        _core.Undo();
        _core.Redo();

        Assert.That(historyChangedCount, Is.EqualTo(3));
    }

    private static RectangleAnnotation CreateRectangle(float x1, float y1, float x2, float y2) =>
        new()
        {
            StartPoint = new SKPoint(x1, y1),
            EndPoint = new SKPoint(x2, y2)
        };

    private static NumberAnnotation CreateStep(int number, float x, float y) =>
        new()
        {
            Number = number,
            StartPoint = new SKPoint(x, y)
        };

    private static SKBitmap CreateBitmap(SKColor color)
    {
        SKBitmap bitmap = new(64, 64);
        bitmap.Erase(color);
        return bitmap;
    }

    private static SKColor GetPixel(SKBitmap bitmap) => bitmap.GetPixel(0, 0);
}
