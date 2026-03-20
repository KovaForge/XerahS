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

using System.Drawing;
using NUnit.Framework;
using SkiaSharp;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture;

namespace XerahS.Tests.RegionCapture;

public class ScrollingCaptureManagerTests
{
    private const int FrameWidth = 60;
    private const int FrameHeight = 100;
    private const int FixedChromeHeight = 10;
    private const int ScrollStep = 15;
    private static readonly SKColor FixedHeaderColor = new(240, 32, 32);
    private static readonly SKColor FixedFooterColor = new(32, 32, 240);

    [Test]
    public async Task CaptureAsync_TrimsDuplicateRowsWhenFrameContainsFixedHeader()
    {
        using var firstFrame = CreateFrame(contentStartIndex: 0, fixedTopRows: FixedChromeHeight);
        using var secondFrame = CreateFrame(contentStartIndex: ScrollStep, fixedTopRows: FixedChromeHeight);

        ScrollingCaptureResult result = await RunCaptureAsync([firstFrame, secondFrame], autoIgnoreBottomEdge: false);

        try
        {
            Assert.That(result.Status, Is.EqualTo(ScrollingCaptureStatus.Successful));
            Assert.That(result.FramesCaptured, Is.EqualTo(2));
            Assert.That(result.Image, Is.Not.Null);
            Assert.That(result.Image!.Height, Is.EqualTo(FrameHeight + ScrollStep));

            for (int row = 0; row < FixedChromeHeight; row++)
            {
                AssertRowColor(result.Image, row, FixedHeaderColor);
            }

            for (int row = 0; row < FrameHeight - FixedChromeHeight + ScrollStep; row++)
            {
                AssertRowColor(result.Image, FixedChromeHeight + row, ContentColor(row));
            }
        }
        finally
        {
            result.Image?.Dispose();
        }
    }

    [Test]
    public async Task CaptureAsync_TrimsLargeOverlapWithoutDuplicatingContent()
    {
        using var firstFrame = CreateFrame(contentStartIndex: 0);
        using var secondFrame = CreateFrame(contentStartIndex: ScrollStep);

        ScrollingCaptureResult result = await RunCaptureAsync([firstFrame, secondFrame], autoIgnoreBottomEdge: false);

        try
        {
            Assert.That(result.Status, Is.EqualTo(ScrollingCaptureStatus.Successful));
            Assert.That(result.FramesCaptured, Is.EqualTo(2));
            Assert.That(result.Image, Is.Not.Null);
            Assert.That(result.Image!.Height, Is.EqualTo(FrameHeight + ScrollStep));

            for (int row = 0; row < FrameHeight + ScrollStep; row++)
            {
                AssertRowColor(result.Image, row, ContentColor(row));
            }
        }
        finally
        {
            result.Image?.Dispose();
        }
    }

    [Test]
    public async Task CaptureAsync_KeepsLatestBottomChromeOnceWhenIgnoringBottomEdge()
    {
        using var firstFrame = CreateFrame(contentStartIndex: 0, fixedBottomRows: FixedChromeHeight);
        using var secondFrame = CreateFrame(contentStartIndex: ScrollStep, fixedBottomRows: FixedChromeHeight);

        ScrollingCaptureResult result = await RunCaptureAsync([firstFrame, secondFrame], autoIgnoreBottomEdge: true);

        try
        {
            Assert.That(result.Status, Is.EqualTo(ScrollingCaptureStatus.Successful));
            Assert.That(result.FramesCaptured, Is.EqualTo(2));
            Assert.That(result.Image, Is.Not.Null);
            Assert.That(result.Image!.Height, Is.EqualTo(FrameHeight + ScrollStep));

            for (int row = 0; row < FrameHeight - FixedChromeHeight + ScrollStep; row++)
            {
                AssertRowColor(result.Image, row, ContentColor(row));
            }

            for (int row = 0; row < FixedChromeHeight; row++)
            {
                AssertRowColor(result.Image, result.Image.Height - FixedChromeHeight + row, FixedFooterColor);
            }
        }
        finally
        {
            result.Image?.Dispose();
        }
    }

    private static async Task<ScrollingCaptureResult> RunCaptureAsync(IReadOnlyList<SKBitmap> frames, bool autoIgnoreBottomEdge)
    {
        var manager = new ScrollingCaptureManager(
            new StubScrollingCaptureService(),
            new StubScreenCaptureService(frames),
            new StubWindowService());

        return await manager.CaptureAsync(
            windowHandle: IntPtr.Zero,
            captureRegion: new SKRect(0, 0, FrameWidth, FrameHeight),
            scrollMethod: ScrollMethod.MouseWheel,
            scrollAmount: 1,
            startDelayMs: 0,
            scrollDelayMs: 0,
            autoScrollTop: false,
            autoIgnoreBottomEdge: autoIgnoreBottomEdge);
    }

    private static SKBitmap CreateFrame(int contentStartIndex, int fixedTopRows = 0, int fixedBottomRows = 0)
    {
        int contentRows = FrameHeight - fixedTopRows - fixedBottomRows;
        var bitmap = new SKBitmap(FrameWidth, FrameHeight);

        for (int y = 0; y < FrameHeight; y++)
        {
            SKColor color =
                y < fixedTopRows ? FixedHeaderColor :
                y >= FrameHeight - fixedBottomRows ? FixedFooterColor :
                ContentColor(contentStartIndex + y - fixedTopRows);

            for (int x = 0; x < FrameWidth; x++)
            {
                bitmap.SetPixel(x, y, color);
            }
        }

        Assert.That(contentRows, Is.GreaterThan(0));
        return bitmap;
    }

    private static SKColor ContentColor(int index)
    {
        return new SKColor(
            (byte)((index * 17 + 11) % 251),
            (byte)((index * 29 + 37) % 251),
            (byte)((index * 43 + 71) % 251));
    }

    private static void AssertRowColor(SKBitmap bitmap, int row, SKColor expectedColor)
    {
        int sampleX = bitmap.Width / 2;
        Assert.That(bitmap.GetPixel(sampleX, row), Is.EqualTo(expectedColor), $"Unexpected pixel at row {row}.");
    }

    private sealed class StubScrollingCaptureService : IScrollingCaptureService
    {
        public bool IsSupported => true;

        public ScrollBarInfo? GetScrollBarInfo(IntPtr windowHandle)
        {
            return new ScrollBarInfo(91, 0, 100, 10);
        }

        public Task ScrollWindowAsync(IntPtr windowHandle, ScrollMethod method, int amount)
        {
            return Task.CompletedTask;
        }

        public Task ScrollToTopAsync(IntPtr windowHandle)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StubScreenCaptureService(IReadOnlyList<SKBitmap> frames) : IScreenCaptureService
    {
        private readonly IReadOnlyList<SKBitmap> _frames = frames;
        private int _captureIndex;

        public Task<SKRectI> SelectRegionAsync(CaptureOptions? options = null)
        {
            throw new NotSupportedException();
        }

        public Task<SKBitmap?> CaptureRegionAsync(CaptureOptions? options = null)
        {
            throw new NotSupportedException();
        }

        public Task<SKBitmap?> CaptureRectAsync(SKRect rect, CaptureOptions? options = null)
        {
            int index = Math.Min(_captureIndex, _frames.Count - 1);
            _captureIndex++;
            return Task.FromResult<SKBitmap?>(_frames[index].Copy());
        }

        public Task<SKBitmap?> CaptureFullScreenAsync(CaptureOptions? options = null)
        {
            throw new NotSupportedException();
        }

        public Task<SKBitmap?> CaptureActiveWindowAsync(IWindowService windowService, CaptureOptions? options = null)
        {
            throw new NotSupportedException();
        }

        public Task<SKBitmap?> CaptureWindowAsync(IntPtr windowHandle, IWindowService windowService, CaptureOptions? options = null)
        {
            throw new NotSupportedException();
        }

        public Task<CursorInfo?> CaptureCursorAsync()
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubWindowService : IWindowService
    {
        public IntPtr GetForegroundWindow() => IntPtr.Zero;

        public bool SetForegroundWindow(IntPtr handle) => true;

        public string GetWindowText(IntPtr handle) => string.Empty;

        public string GetWindowClassName(IntPtr handle) => string.Empty;

        public Rectangle GetWindowBounds(IntPtr handle) => Rectangle.Empty;

        public Rectangle GetWindowClientBounds(IntPtr handle) => Rectangle.Empty;

        public bool IsWindowVisible(IntPtr handle) => true;

        public bool IsWindowMaximized(IntPtr handle) => false;

        public bool IsWindowMinimized(IntPtr handle) => false;

        public bool ShowWindow(IntPtr handle, int cmdShow) => true;

        public bool SetWindowPos(IntPtr handle, IntPtr handleInsertAfter, int x, int y, int width, int height, uint flags) => true;

        public WindowInfo[] GetAllWindows() => [];

        public uint GetWindowProcessId(IntPtr handle) => 0;

        public IntPtr SearchWindow(string windowTitle) => IntPtr.Zero;

        public bool ActivateWindow(IntPtr handle) => true;

        public bool SetWindowClickThrough(IntPtr handle) => true;
    }
}
