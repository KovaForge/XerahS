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

using System.Diagnostics;
using SkiaSharp;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.RegionCapture
{
    /// <summary>
    /// Platform-agnostic scrolling capture manager. Orchestrates the capture loop
    /// and image stitching using platform services for scroll simulation and screen capture.
    /// </summary>
    public class ScrollingCaptureManager
    {
        private readonly IScrollingCaptureService _scrollService;
        private readonly IScreenCaptureService _captureService;
        private readonly IWindowService _windowService;

        public ScrollingCaptureManager(
            IScrollingCaptureService scrollService,
            IScreenCaptureService captureService,
            IWindowService windowService)
        {
            _scrollService = scrollService ?? throw new ArgumentNullException(nameof(scrollService));
            _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
            _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
        }

        /// <summary>
        /// Performs a scrolling capture of the specified window region.
        /// Captures frames, detects scroll end, and stitches into a single image.
        /// </summary>
        /// <param name="windowHandle">Target window handle</param>
        /// <param name="captureRegion">Screen region to capture each frame</param>
        /// <param name="scrollMethod">Method to use for scrolling</param>
        /// <param name="scrollAmount">Number of scroll units per iteration</param>
        /// <param name="startDelayMs">Delay before first capture (ms)</param>
        /// <param name="scrollDelayMs">Delay between scroll operations (ms)</param>
        /// <param name="maxFrames">Maximum number of frames to capture before stopping</param>
        /// <param name="autoScrollTop">Whether to scroll to top before starting</param>
        /// <param name="autoIgnoreBottomEdge">Whether to detect non-scrolling bottom elements</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task<ScrollingCaptureResult> CaptureAsync(
            IntPtr windowHandle,
            SKRect captureRegion,
            ScrollMethod scrollMethod,
            int scrollAmount = 2,
            int startDelayMs = 300,
            int scrollDelayMs = 300,
            int maxFrames = 100,
            bool autoScrollTop = false,
            bool autoIgnoreBottomEdge = true,
            IProgress<ScrollingCaptureProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new ScrollingCaptureResult
            {
                Status = ScrollingCaptureStatus.Successful
            };
            SKBitmap? stitchedResult = null;
            SKBitmap? previousFrame = null;
            int bestMatchCount = 0;
            int bestMatchIndex = 0;
            int bestIgnoreBottomOffset = 0;

            try
            {
                System.Drawing.Point preferredScrollPoint = GetPreferredScrollPoint(captureRegion);

                // Focus target window
                _windowService.ActivateWindow(windowHandle);
                await Task.Delay(200, cancellationToken);

                // Optionally scroll to top
                if (autoScrollTop)
                {
                    await _scrollService.ScrollToTopAsync(windowHandle, preferredScrollPoint);
                    await Task.Delay(startDelayMs, cancellationToken);
                }

                // Wait start delay
                await Task.Delay(startDelayMs, cancellationToken);

                var stopwatch = new Stopwatch();
                int frameIndex = 0;
                maxFrames = Math.Clamp(maxFrames, 1, 1000);
                int lastResultHeight = 0;
                int noProgressCount = 0;
                const int NoProgressLimit = 3; // Stop if height unchanged for this many frames

                while (frameIndex < maxFrames && !cancellationToken.IsCancellationRequested)
                {
                    stopwatch.Restart();

                    // Capture current frame
                    var currentFrame = await _captureService.CaptureRectAsync(captureRegion);
                    if (currentFrame == null)
                    {
                        DebugHelper.WriteLine("ScrollingCapture: Failed to capture frame.");
                        break;
                    }

                    frameIndex++;
                    result.FramesCaptured = frameIndex;

                    // Report progress
                    progress?.Report(new ScrollingCaptureProgress
                    {
                        FramesCaptured = frameIndex,
                        LatestFrame = currentFrame
                    });

                    if (previousFrame == null)
                    {
                        // First frame - use as initial result
                        stitchedResult = currentFrame.Copy();
                        previousFrame = currentFrame;
                        lastResultHeight = stitchedResult.Height;
                    }
                    else
                    {
                        // Check if frames are identical (bottom reached)
                        if (AreFramesIdentical(previousFrame, currentFrame))
                        {
                            DebugHelper.WriteLine("ScrollingCapture: Identical frames detected - bottom reached.");
                            currentFrame.Dispose();
                            break;
                        }

                        // Check scroll bar for bottom detection
                        var scrollInfo = _scrollService.GetScrollBarInfo(windowHandle);
                        bool scrollAtBottom = scrollInfo?.IsAtBottom ?? false;

                        // Stitch current frame onto result
                        var stitchResult = StitchFrame(
                            stitchedResult!,
                            currentFrame,
                            autoIgnoreBottomEdge,
                            ref bestMatchCount,
                            ref bestMatchIndex,
                            ref bestIgnoreBottomOffset);

                        if (stitchResult.NewImage == null)
                        {
                            result.Status = stitchResult.Status;
                            currentFrame.Dispose();
                            break;
                        }

                        stitchedResult?.Dispose();
                        stitchedResult = stitchResult.NewImage;

                        if (stitchResult.Status == ScrollingCaptureStatus.Failed &&
                            result.Status != ScrollingCaptureStatus.PartiallySuccessful)
                        {
                            result.Status = ScrollingCaptureStatus.Failed;
                        }
                        else if (stitchResult.Status == ScrollingCaptureStatus.PartiallySuccessful)
                        {
                            result.Status = ScrollingCaptureStatus.PartiallySuccessful;
                        }

                        // No-progress detection: stop if stitched height hasn't increased for several frames
                        // (avoids infinite loop when scroll bar never reports bottom or content keeps changing)
                        int currentHeight = stitchedResult.Height;
                        if (currentHeight <= lastResultHeight + 2)
                        {
                            noProgressCount++;
                            if (noProgressCount >= NoProgressLimit)
                            {
                                DebugHelper.WriteLine("ScrollingCapture: No height progress - stopping to avoid infinite loop.");
                                break;
                            }
                        }
                        else
                        {
                            noProgressCount = 0;
                        }

                        lastResultHeight = currentHeight;

                        previousFrame.Dispose();
                        previousFrame = currentFrame;

                        if (scrollAtBottom)
                        {
                            DebugHelper.WriteLine("ScrollingCapture: Scroll bar at bottom - stopping.");
                            break;
                        }
                    }

                    // Scroll
                    await _scrollService.ScrollWindowAsync(windowHandle, scrollMethod, scrollAmount, preferredScrollPoint);

                    // Wait scroll delay, compensating for processing time
                    stopwatch.Stop();
                    int elapsed = (int)stopwatch.ElapsedMilliseconds;
                    int remainingDelay = Math.Max(50, scrollDelayMs - elapsed);
                    await Task.Delay(remainingDelay, cancellationToken);
                }

                if (result.Status != ScrollingCaptureStatus.Failed &&
                    result.Status != ScrollingCaptureStatus.PartiallySuccessful)
                {
                    result.Status = ScrollingCaptureStatus.Successful;
                }

                result.Image = stitchedResult;
            }
            catch (OperationCanceledException)
            {
                result.Status = ScrollingCaptureStatus.Failed;
                result.Image = stitchedResult;
            }
            finally
            {
                previousFrame?.Dispose();
            }

            return result;
        }

        private static System.Drawing.Point GetPreferredScrollPoint(SKRect captureRegion)
        {
            int x = (int)MathF.Round(captureRegion.Left + (captureRegion.Width * 0.3f));
            int y = (int)MathF.Round(captureRegion.Top + (captureRegion.Height * 0.5f));
            return new System.Drawing.Point(x, y);
        }

        /// <summary>
        /// Checks if two frames are pixel-identical using fast span comparison.
        /// </summary>
        private static bool AreFramesIdentical(SKBitmap a, SKBitmap b)
        {
            if (a.Width != b.Width || a.Height != b.Height)
            {
                return false;
            }

            var spanA = a.GetPixelSpan();
            var spanB = b.GetPixelSpan();

            return spanA.SequenceEqual(spanB);
        }

        /// <summary>
        /// Stitches a new frame onto the existing result image using overlap detection.
        /// </summary>
        private static StitchResult StitchFrame(
            SKBitmap result,
            SKBitmap currentFrame,
            bool autoIgnoreBottomEdge,
            ref int bestMatchCount,
            ref int bestMatchIndex,
            ref int bestIgnoreBottomOffset)
        {
            int ignoreSideOffset = Math.Max(50, currentFrame.Width / 20);
            ignoreSideOffset = Math.Min(ignoreSideOffset, currentFrame.Width / 3);

            int ignoreBottomOffset = CalculateIgnoreBottomOffset(
                result,
                currentFrame,
                ignoreSideOffset,
                autoIgnoreBottomEdge,
                bestIgnoreBottomOffset);

            int matchIndex = FindBestMatchIndex(
                result,
                currentFrame,
                ignoreSideOffset,
                ignoreBottomOffset,
                out int matchCount);

            bool bestGuess = false;

            if (matchCount == 0 && bestMatchCount > 0)
            {
                matchCount = bestMatchCount;
                matchIndex = bestMatchIndex;
                ignoreBottomOffset = bestIgnoreBottomOffset;
                bestGuess = true;
            }

            if (matchCount > 0)
            {
                int matchHeight = currentFrame.Height - matchIndex - 1;
                if (matchHeight > 0)
                {
                    if (matchCount > bestMatchCount)
                    {
                        bestMatchCount = matchCount;
                        bestMatchIndex = matchIndex;
                        bestIgnoreBottomOffset = ignoreBottomOffset;
                    }

                    return new StitchResult
                    {
                        NewImage = CreateStitchedImage(result, currentFrame, matchIndex, ignoreBottomOffset),
                        Status = bestGuess ? ScrollingCaptureStatus.PartiallySuccessful : ScrollingCaptureStatus.Successful
                    };
                }
            }

            return new StitchResult
            {
                Status = ScrollingCaptureStatus.Failed
            };
        }

        /// <summary>
        /// Calculates the bottom edge offset to ignore while matching.
        /// </summary>
        private static int CalculateIgnoreBottomOffset(
            SKBitmap result,
            SKBitmap current,
            int ignoreSideOffset,
            bool autoIgnoreBottomEdge,
            int bestIgnoreBottomOffset)
        {
            if (!autoIgnoreBottomEdge)
            {
                return 0;
            }

            int ignoreBottomOffsetMax = current.Height / 3;
            if (ignoreBottomOffsetMax <= 0)
            {
                return 0;
            }

            int ignoreBottomOffset = Math.Max(50, current.Height / 10);
            ignoreBottomOffset = Math.Min(ignoreBottomOffset, ignoreBottomOffsetMax);

            int bytesPerPixel = current.BytesPerPixel;
            int compareStart = ignoreSideOffset * bytesPerPixel;
            int compareLength = (current.Width - ignoreSideOffset * 2) * bytesPerPixel;
            if (compareLength <= 0)
            {
                return 0;
            }

            var resultSpan = result.GetPixelSpan();
            var currentSpan = current.GetPixelSpan();
            int resultStride = result.RowBytes;
            int currentStride = current.RowBytes;

            for (int offset = 0; offset <= ignoreBottomOffsetMax; offset++)
            {
                if (!RowsEqual(
                    resultSpan,
                    resultStride,
                    result.Height - 1 - offset,
                    currentSpan,
                    currentStride,
                    current.Height - 1 - offset,
                    compareStart,
                    compareLength))
                {
                    ignoreBottomOffset += offset;
                    break;
                }
            }

            ignoreBottomOffset = Math.Max(ignoreBottomOffset, bestIgnoreBottomOffset);
            return Math.Min(ignoreBottomOffset, ignoreBottomOffsetMax);
        }

        /// <summary>
        /// Finds the row in the current frame where the overlap with the bottom of the result ends.
        /// </summary>
        private static int FindBestMatchIndex(
            SKBitmap result,
            SKBitmap current,
            int ignoreSideOffset,
            int ignoreBottomOffset,
            out int matchCount)
        {
            matchCount = 0;

            int resultBottom = result.Height - ignoreBottomOffset - 1;
            if (resultBottom < 0)
            {
                return -1;
            }

            int matchLimit = Math.Max(1, current.Height / 2);
            int bytesPerPixel = current.BytesPerPixel;
            int compareStart = ignoreSideOffset * bytesPerPixel;
            int compareLength = (current.Width - ignoreSideOffset * 2) * bytesPerPixel;
            if (compareLength <= 0)
            {
                return -1;
            }

            var resultSpan = result.GetPixelSpan();
            var currentSpan = current.GetPixelSpan();
            int resultStride = result.RowBytes;
            int currentStride = current.RowBytes;

            int bestMatchIndex = -1;

            for (int currentY = current.Height - 1; currentY >= 0 && matchCount < matchLimit; currentY--)
            {
                int currentMatchCount = 0;

                for (int y = 0;
                     currentY - y >= 0 && resultBottom - y >= 0 && currentMatchCount < matchLimit;
                     y++)
                {
                    if (RowsEqual(
                        resultSpan,
                        resultStride,
                        resultBottom - y,
                        currentSpan,
                        currentStride,
                        currentY - y,
                        compareStart,
                        compareLength))
                    {
                        currentMatchCount++;
                    }
                    else
                    {
                        break;
                    }
                }

                if (currentMatchCount > matchCount)
                {
                    matchCount = currentMatchCount;
                    bestMatchIndex = currentY;
                }
            }

            return bestMatchIndex;
        }

        /// <summary>
        /// Creates a new stitched image combining the existing result with new content from the current frame.
        /// </summary>
        private static SKBitmap CreateStitchedImage(
            SKBitmap result,
            SKBitmap currentFrame,
            int matchIndex,
            int ignoreBottomOffset)
        {
            int width = result.Width;
            int resultUsableHeight = Math.Max(0, result.Height - ignoreBottomOffset);
            int matchHeight = currentFrame.Height - matchIndex - 1;
            int totalHeight = resultUsableHeight + matchHeight;

            // Safety: cap total height to prevent memory issues
            if (totalHeight > 32768)
            {
                DebugHelper.WriteLine($"ScrollingCapture: Result height {totalHeight} exceeds limit, capping at 32768.");
                totalHeight = 32768;
                matchHeight = totalHeight - resultUsableHeight;
                if (matchHeight <= 0)
                {
                    return result.Copy();
                }
            }

            var newResult = new SKBitmap(width, totalHeight);
            using (var canvas = new SKCanvas(newResult))
            {
                // Draw existing result (minus bottom edge offset)
                var srcResultRect = new SKRect(0, 0, width, resultUsableHeight);
                var dstResultRect = new SKRect(0, 0, width, resultUsableHeight);
                canvas.DrawBitmap(result, srcResultRect, dstResultRect);

                // Draw the non-overlapping bottom portion of the current frame.
                int currentFrameNewStart = Math.Max(0, matchIndex + 1);
                var srcCurrentRect = new SKRect(0, currentFrameNewStart, width, currentFrameNewStart + matchHeight);
                var dstCurrentRect = new SKRect(0, resultUsableHeight, width, totalHeight);
                canvas.DrawBitmap(currentFrame, srcCurrentRect, dstCurrentRect);
            }

            return newResult;
        }

        private static bool RowsEqual(
            ReadOnlySpan<byte> resultSpan,
            int resultStride,
            int resultRow,
            ReadOnlySpan<byte> currentSpan,
            int currentStride,
            int currentRow,
            int compareStart,
            int compareLength)
        {
            if (resultRow < 0 || currentRow < 0)
            {
                return false;
            }

            int resultRowStart = resultRow * resultStride + compareStart;
            int currentRowStart = currentRow * currentStride + compareStart;

            if (resultRowStart < 0 ||
                currentRowStart < 0 ||
                resultRowStart + compareLength > resultSpan.Length ||
                currentRowStart + compareLength > currentSpan.Length)
            {
                return false;
            }

            return resultSpan.Slice(resultRowStart, compareLength)
                .SequenceEqual(currentSpan.Slice(currentRowStart, compareLength));
        }

        private struct StitchResult
        {
            public SKBitmap? NewImage;
            public ScrollingCaptureStatus Status;
        }
    }
}
