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

using System.Reflection;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System.Diagnostics;
using NUnit.Framework;
using SkiaSharp;
using XerahS.UI.Services;

namespace XerahS.Tests.UI;

[TestFixture]
public class AvaloniaClipboardMemoryTests
{
    [AvaloniaTest]
    public void ContainsImage_OnlyChecksFormats()
    {
        var data = new DataTransfer();
        var item = new DataTransferItem();
        item.Set(DataFormat.Bitmap, () => throw new AssertionException("ContainsImage must not decode bitmap data."));
        data.Add(item);
        var service = new AvaloniaClipboardService(CreateClipboard(data));

        Assert.That(service.ContainsImage(), Is.True);
    }

    [AvaloniaTest]
    public void GetImage_ReleasesRetrievedBitmap_AndReturnsIndependentPixels()
    {
        using var bitmap = CreateBitmap();
        var service = CreateService(bitmap);

        using var result = service.GetImage();

        Assert.That(bitmap.IsDisposed, Is.True);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.GetPixel(0, 0), Is.EqualTo(SKColors.Red));
    }

    [AvaloniaTest]
    public async Task GetImageAsync_ReleasesRetrievedBitmap_AndReturnsIndependentPixels()
    {
        using var bitmap = CreateBitmap();
        var service = CreateService(bitmap);

        using var result = await service.GetImageAsync();

        Assert.That(bitmap.IsDisposed, Is.True);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.GetPixel(0, 0), Is.EqualTo(SKColors.Red));
    }

    /// <summary>
    /// Regression: SetText from a background thread (CaptureJobProcessor "copy file path") ran
    /// SetTextAsync(...).GetResult() on the UI thread; clipboard writes that need the UI loop to
    /// finish never did, freezing the UI and the tray menu.
    /// </summary>
    [AvaloniaTest]
    public void SetText_FromBackgroundThread_DoesNotDeadlockWhenClipboardNeedsTheUiLoop()
    {
        var clipboard = DispatchProxy.Create<IClipboard, UiLoopClipboardProxy>();
        var service = new AvaloniaClipboardService(clipboard);

        Task worker = Task.Run(() => service.SetText("/tmp/shot.png"));
        var stopwatch = Stopwatch.StartNew();
        while (!worker.IsCompleted && stopwatch.Elapsed < TimeSpan.FromSeconds(5))
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }

        Assert.That(worker.IsCompleted, Is.True, "SetText did not return; the UI thread is blocked on the clipboard.");
        worker.GetAwaiter().GetResult();
        Assert.That(((UiLoopClipboardProxy)(object)clipboard).Writes, Is.EqualTo(1));
    }

    [AvaloniaTest]
    public void SetText_OnUiThread_PumpsTheLoopInsteadOfBlocking()
    {
        var clipboard = DispatchProxy.Create<IClipboard, UiLoopClipboardProxy>();
        var service = new AvaloniaClipboardService(clipboard);

        service.SetText("hello");

        Assert.That(((UiLoopClipboardProxy)(object)clipboard).Writes, Is.EqualTo(1));
    }

    /// <summary>Completes clipboard writes only when the UI loop runs, like X11 selection ownership.</summary>
    public class UiLoopClipboardProxy : DispatchProxy
    {
        public int Writes;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IClipboard.SetDataAsync))
            {
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Dispatcher.UIThread.Post(() =>
                {
                    Interlocked.Increment(ref Writes);
                    completion.SetResult();
                });
                return completion.Task;
            }

            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    private static AvaloniaClipboardService CreateService(Bitmap bitmap)
    {
        var data = new DataTransfer();
        var item = new DataTransferItem();
        item.SetBitmap(bitmap);
        data.Add(item);
        return new AvaloniaClipboardService(CreateClipboard(data));
    }

    private static IClipboard CreateClipboard(IAsyncDataTransfer data)
    {
        var clipboard = DispatchProxy.Create<IClipboard, ClipboardProxy>();
        ((ClipboardProxy)(object)clipboard).Data = data;
        return clipboard;
    }

    private static TrackedBitmap CreateBitmap()
    {
        using var source = new SKBitmap(2, 2);
        source.Erase(SKColors.Red);
        using var encoded = source.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = encoded.AsStream();
        return new TrackedBitmap(stream);
    }

    public class ClipboardProxy : DispatchProxy
    {
        public IAsyncDataTransfer? Data { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IClipboard.TryGetDataAsync))
                return Task.FromResult(Data);

            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    private sealed class TrackedBitmap(Stream stream) : Bitmap(stream)
    {
        public bool IsDisposed { get; private set; }

        public override void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                base.Dispose();
            }
        }
    }
}