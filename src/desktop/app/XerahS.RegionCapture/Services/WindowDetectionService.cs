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
using System.Runtime.CompilerServices;
using XerahS.RegionCapture.Models;
using PlatformWindowInfo = XerahS.Platform.Abstractions.WindowInfo;

namespace XerahS.RegionCapture.Services;

internal enum WindowPreselectionSupportLevel
{
    Full,
    Partial,
    Unsupported
}

internal readonly record struct WindowPreselectionCapability(
    WindowPreselectionSupportLevel Level,
    string? UserMessage)
{
    public bool IsEnabled => Level != WindowPreselectionSupportLevel.Unsupported;
}

/// <summary>
/// High-performance window detection service using spatial indexing.
/// Provides instant window detection under the cursor for smart snapping.
/// </summary>
public sealed class WindowDetectionService
{
    private static readonly object ExcludedHandlesLock = new();
    private static readonly HashSet<nint> ExcludedHandles = [];
    private IReadOnlyList<WindowInfo> _windows = [];
    private readonly object _lock = new();
    private DateTime _lastRefresh = DateTime.MinValue;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(100);
    private readonly Func<IReadOnlyList<WindowInfo>> _enumerateVisibleWindows;

    /// <summary>
    /// Gets the list of visible windows, refreshing if stale.
    /// </summary>
    private volatile bool _isRefreshing;
    private readonly object _refreshLock = new();

    public WindowDetectionService()
        : this(EnumerateVisibleWindows)
    {
    }

    internal WindowDetectionService(Func<IReadOnlyList<WindowInfo>> enumerateVisibleWindows)
    {
        _enumerateVisibleWindows = enumerateVisibleWindows ?? throw new ArgumentNullException(nameof(enumerateVisibleWindows));
    }

    /// <summary>
    /// Registers a native overlay handle so hover detection ignores the overlay itself.
    /// </summary>
    public static void ExcludeHandle(nint handle)
    {
        if (handle == 0)
            return;

        lock (ExcludedHandlesLock)
        {
            ExcludedHandles.Add(handle);
        }
    }

    /// <summary>
    /// Removes a native overlay handle from the exclusion list.
    /// </summary>
    public static void RemoveExcludedHandle(nint handle)
    {
        if (handle == 0)
            return;

        lock (ExcludedHandlesLock)
        {
            ExcludedHandles.Remove(handle);
        }
    }

    /// <summary>
    /// Gets the list of visible windows, refreshing if stale.
    /// </summary>
    public IReadOnlyList<WindowInfo> Windows
    {
        get
        {
            var timeSinceLastRefresh = DateTime.UtcNow - _lastRefresh;
            if (timeSinceLastRefresh > RefreshInterval && !_isRefreshing)
            {
                // Refresh asynchronously to not block the UI thread
                RefreshWindowsAsync();
            }
            return _windows;
        }
    }

    private void RefreshWindowsAsync()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;

        Task.Run(() =>
        {
            try
            {
                var windows = _enumerateVisibleWindows();
                lock (_lock)
                {
                    _windows = windows;
                    _lastRefresh = DateTime.UtcNow;
                }
            }
            catch
            {
                // Ignore errors during enumeration
            }
            finally
            {
                _isRefreshing = false;
            }
        });
    }

    /// <summary>
    /// Forces a refresh of the window list (synchronous).
    /// </summary>
    public void RefreshWindows()
    {
        lock (_lock)
        {
            _windows = _enumerateVisibleWindows();
            _lastRefresh = DateTime.UtcNow;
        }
    }

    internal static WindowPreselectionCapability GetWindowPreselectionCapability()
    {
        if (!OperatingSystem.IsLinux())
        {
            return new(WindowPreselectionSupportLevel.Full, null);
        }

        bool isWaylandSession = MonitorEnumerationService.IsWaylandSession();
        bool hasX11Display = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"));
        return GetLinuxWindowPreselectionCapability(isWaylandSession, hasX11Display);
    }

    internal static WindowPreselectionCapability GetLinuxWindowPreselectionCapability(
        bool isWaylandSession,
        bool hasX11Display)
    {
        if (!isWaylandSession)
        {
            return new(WindowPreselectionSupportLevel.Full, null);
        }

        if (hasX11Display)
        {
            return new(
                WindowPreselectionSupportLevel.Partial,
                "Wayland session: only X11/XWayland windows can be snapped.");
        }

        return new(
            WindowPreselectionSupportLevel.Unsupported,
            "Wayland session: window snapping is unavailable.");
    }

    internal static IReadOnlyList<WindowInfo> EnumerateVisibleWindows()
    {
#if WINDOWS
        return FilterExcludedWindows(Platform.Windows.NativeWindowService.EnumerateVisibleWindows());
#else
        try
        {
            return ConvertPlatformWindows(XerahS.Platform.Abstractions.PlatformServices.Window.GetAllWindows());
        }
        catch (InvalidOperationException)
        {
            return [];
        }
#endif
    }

    internal static IReadOnlyList<WindowInfo> ConvertPlatformWindows(IEnumerable<PlatformWindowInfo> windows)
    {
        ArgumentNullException.ThrowIfNull(windows);

        var visibleWindows = new List<WindowInfo>();
        int zOrder = 0;

        foreach (var window in windows)
        {
            if (!ShouldIncludePlatformWindow(window))
                continue;

            var bounds = ToPixelRect(window.Bounds);
            visibleWindows.Add(new WindowInfo(
                Handle: window.Handle,
                Title: window.Title,
                ClassName: window.ClassName,
                Bounds: bounds,
                VisualBounds: bounds,
                IsMinimized: window.IsMinimized,
                ZOrder: zOrder++));
        }

        return visibleWindows;
    }

    internal static bool ShouldIncludePlatformWindow(PlatformWindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (window.Handle == IntPtr.Zero || IsExcludedHandle(window.Handle))
            return false;

        if (!window.IsVisible || window.IsMinimized)
            return false;

        if (string.IsNullOrWhiteSpace(window.Title))
            return false;

        return window.Bounds.Width > 1 && window.Bounds.Height > 1;
    }

    private static IReadOnlyList<WindowInfo> FilterExcludedWindows(IEnumerable<WindowInfo> windows)
    {
        return windows.Where(window => !IsExcludedHandle(window.Handle)).ToArray();
    }

    private static bool IsExcludedHandle(nint handle)
    {
        if (handle == 0)
            return false;

        lock (ExcludedHandlesLock)
        {
            return ExcludedHandles.Contains(handle);
        }
    }

    private static PixelRect ToPixelRect(System.Drawing.Rectangle bounds)
    {
        return new PixelRect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    /// <summary>
    /// Gets the topmost window at the specified physical point.
    /// Uses Z-order to determine which window is visually on top.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WindowInfo? GetWindowAtPoint(PixelPoint physicalPoint)
    {
        // Direct lookup - sorted by Z-order (topmost first)
        foreach (var window in Windows)
        {
            if (window.SnapBounds.Contains(physicalPoint))
            {
                return window;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all windows that intersect with the specified region.
    /// Useful for determining which windows a selection covers.
    /// </summary>
    public IEnumerable<WindowInfo> GetWindowsInRegion(PixelRect region)
    {
        foreach (var window in Windows)
        {
            if (window.SnapBounds.IntersectsWith(region))
            {
                yield return window;
            }
        }
    }

    /// <summary>
    /// Finds windows near a point within a specified radius.
    /// Useful for snap-to-edge functionality.
    /// </summary>
    public IEnumerable<(WindowInfo Window, double Distance)> GetWindowsNearPoint(PixelPoint point, double radius)
    {
        var radiusSquared = radius * radius;

        foreach (var window in Windows)
        {
            var bounds = window.SnapBounds;

            // Calculate distance to nearest edge
            var nearestX = Math.Clamp(point.X, bounds.Left, bounds.Right);
            var nearestY = Math.Clamp(point.Y, bounds.Top, bounds.Bottom);

            var dx = point.X - nearestX;
            var dy = point.Y - nearestY;
            var distanceSquared = dx * dx + dy * dy;

            if (distanceSquared <= radiusSquared)
            {
                yield return (window, Math.Sqrt(distanceSquared));
            }
        }
    }

    /// <summary>
    /// Gets snap edges from nearby windows for edge snapping behavior.
    /// </summary>
    public SnapEdges GetSnapEdges(PixelPoint cursorPosition, double snapDistance)
    {
        var edges = new SnapEdges();

        foreach (var window in Windows)
        {
            var bounds = window.SnapBounds;

            // Check left edge
            if (Math.Abs(cursorPosition.X - bounds.Left) <= snapDistance)
                edges.VerticalEdges.Add(bounds.Left);

            // Check right edge
            if (Math.Abs(cursorPosition.X - bounds.Right) <= snapDistance)
                edges.VerticalEdges.Add(bounds.Right);

            // Check top edge
            if (Math.Abs(cursorPosition.Y - bounds.Top) <= snapDistance)
                edges.HorizontalEdges.Add(bounds.Top);

            // Check bottom edge
            if (Math.Abs(cursorPosition.Y - bounds.Bottom) <= snapDistance)
                edges.HorizontalEdges.Add(bounds.Bottom);
        }

        return edges;
    }
}

/// <summary>
/// Contains horizontal and vertical snap edges from nearby windows.
/// </summary>
public sealed class SnapEdges
{
    public List<double> HorizontalEdges { get; } = [];
    public List<double> VerticalEdges { get; } = [];

    /// <summary>
    /// Gets the nearest horizontal edge to the specified Y coordinate.
    /// </summary>
    public double? GetNearestHorizontal(double y, double maxDistance)
    {
        double? nearest = null;
        double minDist = maxDistance;

        foreach (var edge in HorizontalEdges)
        {
            var dist = Math.Abs(edge - y);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = edge;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Gets the nearest vertical edge to the specified X coordinate.
    /// </summary>
    public double? GetNearestVertical(double x, double maxDistance)
    {
        double? nearest = null;
        double minDist = maxDistance;

        foreach (var edge in VerticalEdges)
        {
            var dist = Math.Abs(edge - x);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = edge;
            }
        }

        return nearest;
    }
}
