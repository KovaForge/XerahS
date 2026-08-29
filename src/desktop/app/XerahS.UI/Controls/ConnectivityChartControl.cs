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

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using XerahS.Common.NetworkMonitor;

namespace XerahS.UI.Controls;

public sealed class ConnectivityChartControl : Control
{
    public static readonly StyledProperty<IReadOnlyList<NetworkChartPoint>?> PointsProperty =
        AvaloniaProperty.Register<ConnectivityChartControl, IReadOnlyList<NetworkChartPoint>?>(nameof(Points));

    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<ConnectivityChartControl, string>(
            nameof(EmptyText),
            "No samples yet. Outages and latency appear here while monitoring.");

    private static readonly IBrush ConnectedFill = new SolidColorBrush(Color.FromArgb(48, 34, 197, 94));
    private static readonly IBrush DisconnectedFill = new SolidColorBrush(Color.FromArgb(72, 239, 68, 68));
    private static readonly IBrush AxisBrush = new SolidColorBrush(Color.FromRgb(120, 120, 128));
    private static readonly IBrush GridBrush = new SolidColorBrush(Color.FromArgb(55, 142, 148, 158));
    private static readonly IBrush LatencyBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94));
    private static readonly IBrush FailureBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.FromRgb(160, 160, 168));
    private static readonly Pen AxisPen = new(AxisBrush, 1);
    private static readonly Pen GridPen = new(GridBrush, 1);
    private static readonly Pen LatencyPen = new(LatencyBrush, 2);
    private static readonly Pen FailurePen = new(FailureBrush, 2);

    static ConnectivityChartControl()
    {
        AffectsRender<ConnectivityChartControl>(PointsProperty, EmptyTextProperty);
    }

    public IReadOnlyList<NetworkChartPoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public string EmptyText
    {
        get => GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        Rect bounds = new(0, 0, Bounds.Width, Bounds.Height);
        if (bounds.Width < 8 || bounds.Height < 8)
        {
            return;
        }

        context.FillRectangle(Brushes.Transparent, bounds);

        IReadOnlyList<NetworkChartPoint>? points = Points;
        if (points == null || points.Count < 2)
        {
            DrawCenteredText(context, EmptyText, bounds);
            return;
        }

        const double left = 52;
        const double right = 12;
        const double top = 10;
        const double bottom = 28;
        double plotWidth = Math.Max(1, bounds.Width - left - right);
        double plotHeight = Math.Max(1, bounds.Height - top - bottom);
        Rect plot = new(left, top, plotWidth, plotHeight);

        DateTime minTime = points.Min(point => point.Timestamp);
        DateTime maxTime = points.Max(point => point.Timestamp);
        double duration = Math.Max(1, (maxTime - minTime).TotalSeconds);

        List<NetworkChartPoint> connectivity = [.. points.Where(point => !point.IsSample)];
        if (connectivity.Count < 2)
        {
            connectivity = [.. points];
        }

        DrawConnectivityBands(context, plot, connectivity, minTime, duration);
        double maximumLatency = GetMaximumLatency(points);
        DrawGridLines(context, plot, maximumLatency);
        DrawLatencyLine(context, plot, points, minTime, duration, maximumLatency);
        context.DrawRectangle(null, AxisPen, plot);
        DrawAxisLabels(context, plot, minTime, maxTime);
    }

    private static void DrawConnectivityBands(
        DrawingContext context,
        Rect plot,
        IReadOnlyList<NetworkChartPoint> points,
        DateTime minTime,
        double durationSeconds)
    {
        for (int i = 0; i < points.Count - 1; i++)
        {
            NetworkChartPoint current = points[i];
            NetworkChartPoint next = points[i + 1];
            if (next.Timestamp <= current.Timestamp)
            {
                continue;
            }

            double x1 = MapX(current.Timestamp, minTime, durationSeconds, plot);
            double x2 = MapX(next.Timestamp, minTime, durationSeconds, plot);
            double width = Math.Max(1, x2 - x1);
            IBrush fill = current.IsConnected ? ConnectedFill : DisconnectedFill;
            context.FillRectangle(fill, new Rect(x1, plot.Y, width, plot.Height));
        }
    }

    private static void DrawLatencyLine(
        DrawingContext context,
        Rect plot,
        IReadOnlyList<NetworkChartPoint> points,
        DateTime minTime,
        double durationSeconds,
        double maximumLatency)
    {
        List<NetworkChartPoint> samples = [.. points.Where(point => point.IsSample).OrderBy(point => point.Timestamp)];
        if (samples.Count < 2)
        {
            return;
        }

        Point? previousPoint = null;
        bool previousSucceeded = false;
        foreach (NetworkChartPoint sample in samples)
        {
            bool succeeded = sample.IsConnected && sample.LatencyMs.HasValue;
            double x = MapX(sample.Timestamp, minTime, durationSeconds, plot);
            double y = succeeded
                ? plot.Bottom - (sample.LatencyMs!.Value / maximumLatency * plot.Height)
                : plot.Bottom;
            y = Math.Clamp(y, plot.Y, plot.Y + plot.Height);
            Point point = new(x, y);
            if (previousPoint.HasValue)
            {
                context.DrawLine(previousSucceeded && succeeded ? LatencyPen : FailurePen, previousPoint.Value, point);
            }

            previousPoint = point;
            previousSucceeded = succeeded;
        }
    }

    private static double GetMaximumLatency(IReadOnlyList<NetworkChartPoint> points)
    {
        double observedMaximum = points
            .Where(point => point.IsSample && point.LatencyMs.HasValue)
            .Select(point => point.LatencyMs!.Value)
            .DefaultIfEmpty(0)
            .Max();
        return Math.Max(50, Math.Ceiling(observedMaximum / 25) * 25);
    }

    private static void DrawGridLines(DrawingContext context, Rect plot, double maximumLatency)
    {
        const int horizontalGridLines = 4;
        for (int index = 0; index <= horizontalGridLines; index++)
        {
            double ratio = index / (double)horizontalGridLines;
            double y = plot.Bottom - (plot.Height * ratio);
            context.DrawLine(GridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            FormattedText label = CreateLabel($"{maximumLatency * ratio:0} ms");
            context.DrawText(label, new Point(Math.Max(2, plot.Left - label.Width - 6), y - (label.Height / 2)));
        }
    }

    private static void DrawAxisLabels(DrawingContext context, Rect plot, DateTime minTime, DateTime maxTime)
    {
        FormattedText start = CreateLabel(FormatTick(minTime, maxTime - minTime));
        FormattedText mid = CreateLabel(FormatTick(minTime + (maxTime - minTime) / 2, maxTime - minTime));
        FormattedText end = CreateLabel(FormatTick(maxTime, maxTime - minTime));
        context.DrawText(start, new Point(plot.X, plot.Bottom + 6));
        context.DrawText(mid, new Point(plot.X + (plot.Width / 2) - (mid.Width / 2), plot.Bottom + 6));
        context.DrawText(end, new Point(plot.Right - end.Width, plot.Bottom + 6));
    }

    private static double MapX(DateTime timestamp, DateTime minTime, double durationSeconds, Rect plot)
    {
        double t = Math.Clamp((timestamp - minTime).TotalSeconds / durationSeconds, 0, 1);
        return plot.X + (t * plot.Width);
    }

    private static string FormatTick(DateTime value, TimeSpan span)
    {
        if (span.TotalHours <= 6)
        {
            return value.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        }

        if (span.TotalDays <= 2)
        {
            return value.ToString("HH:mm", CultureInfo.CurrentCulture);
        }

        return value.ToString("MM-dd HH:mm", CultureInfo.CurrentCulture);
    }

    private static void DrawCenteredText(DrawingContext context, string text, Rect bounds)
    {
        FormattedText formatted = CreateLabel(text);
        double x = Math.Max(8, (bounds.Width - formatted.Width) / 2);
        double y = Math.Max(8, (bounds.Height - formatted.Height) / 2);
        context.DrawText(formatted, new Point(x, y));
    }

    private static FormattedText CreateLabel(string text)
    {
        return new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"),
            11,
            LabelBrush);
    }
}
