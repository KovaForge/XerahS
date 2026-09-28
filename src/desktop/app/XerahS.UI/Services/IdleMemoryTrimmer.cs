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
using System.Runtime;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using XerahS.Bootstrap;
using XerahS.Common;

namespace XerahS.UI.Services;

/// <summary>
/// Returns memory to the OS once XerahS settles back into the tray.
/// A capture leaves full-screen pixel buffers on the Large Object Heap and in the native
/// allocator. The GC rarely compacts the LOH on its own while the app sits idle, so without
/// this the process keeps its peak footprint indefinitely.
/// </summary>
public static class IdleMemoryTrimmer
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(2);

    private static DispatcherTimer? _timer;
    private static IDesktopTaskManager? _taskManager;
    private static IClassicDesktopStyleApplicationLifetime? _desktop;
    private static DateTime _lastTrimUtc = DateTime.MinValue;
    private static int _trimRunning;

    public static void Initialize(IClassicDesktopStyleApplicationLifetime desktop, IDesktopTaskManager taskManager)
    {
        if (_timer != null)
        {
            return;
        }

        _desktop = desktop;
        _taskManager = taskManager;
        _timer = new DispatcherTimer { Interval = IdleDelay };
        _timer.Tick += OnTimerTick;

        taskManager.TaskCompleted += (_, _) => Dispatcher.UIThread.Post(RequestTrim);
        Window.WindowClosedEvent.AddClassHandler<Window>((_, _) => RequestTrim());
        Visual.IsVisibleProperty.Changed.AddClassHandler<Window>((_, e) =>
        {
            if (e.NewValue is false)
            {
                RequestTrim();
            }
        });

        // Startup allocations (settings, search index pre-warm, plugins) are the first idle peak.
        RequestTrim();
    }

    /// <summary>Schedules a trim once the app has been idle for <see cref="IdleDelay"/>. Must run on the UI thread.</summary>
    public static void RequestTrim()
    {
        if (_timer == null)
        {
            return;
        }

        // Restarting debounces bursts of closes and task completions into one trim.
        _timer.Stop();
        _timer.Interval = IdleDelay;
        _timer.Start();
    }

    private static void OnTimerTick(object? sender, EventArgs e)
    {
        _timer?.Stop();

        if (IsBusy())
        {
            RequestTrim();
            return;
        }

        TimeSpan sinceLast = DateTime.UtcNow - _lastTrimUtc;
        if (sinceLast < MinimumInterval)
        {
            _timer!.Interval = MinimumInterval - sinceLast;
            _timer.Start();
            return;
        }

        bool anyWindowVisible = _desktop?.Windows.Any(w => w.IsVisible) ?? false;
        _lastTrimUtc = DateTime.UtcNow;
        _ = Task.Run(() => Trim(trimWorkingSet: !anyWindowVisible));
    }

    private static bool IsBusy()
    {
        // A running capture, upload or recording still owns its buffers; an active window means
        // the user is interacting and a blocking compaction would be felt.
        if (_taskManager?.Tasks.Any(t => t.IsWorking) == true)
        {
            return true;
        }

        return _desktop?.Windows.Any(w => w.IsActive) == true;
    }

    private static void Trim(bool trimWorkingSet)
    {
        if (Interlocked.Exchange(ref _trimRunning, 1) == 1)
        {
            return;
        }

        try
        {
            long managedBefore = GC.GetTotalMemory(false);
            long workingSetBefore = Environment.WorkingSet;

            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            // Finalizers release SkiaSharp native pixel buffers; a second pass frees their wrappers.
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);

            TrimNativeHeap();
            if (trimWorkingSet)
            {
                TrimWorkingSet();
            }

            DebugHelper.WriteLine(
                $"Idle memory trim: managed {managedBefore / 1048576} MB -> {GC.GetTotalMemory(false) / 1048576} MB, " +
                $"working set {workingSetBefore / 1048576} MB -> {Environment.WorkingSet / 1048576} MB.");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Idle memory trim failed");
        }
        finally
        {
            Volatile.Write(ref _trimRunning, 0);
        }
    }

    private static void TrimNativeHeap()
    {
        try
        {
            // Skia and the Avalonia compositor free through malloc; glibc and macOS keep freed
            // pages in the process unless asked to release them.
            if (OperatingSystem.IsLinux())
            {
                malloc_trim(0);
            }
            else if (OperatingSystem.IsMacOS())
            {
                malloc_zone_pressure_relief(IntPtr.Zero, 0);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // musl and other non-glibc runtimes have no malloc_trim.
        }
    }

    private static void TrimWorkingSet()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            using var process = Process.GetCurrentProcess();
            SetProcessWorkingSetSize(process.Handle, -1, -1);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
        }
    }

    [DllImport("libc.so.6")]
    private static extern int malloc_trim(nuint pad);

    [DllImport("/usr/lib/libSystem.dylib")]
    private static extern nuint malloc_zone_pressure_relief(IntPtr zone, nuint goal);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, nint minimumWorkingSetSize, nint maximumWorkingSetSize);
}
