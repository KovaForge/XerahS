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

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common.NetworkMonitor;

namespace XerahS.UI.ViewModels;

public sealed class NetworkMonitorTimeRangeOption
{
    public NetworkMonitorTimeRangeOption(NetworkMonitorTimeRange range)
    {
        Range = range;
        DisplayName = NetworkMonitorTimeRanges.GetDisplayName(range);
    }

    public NetworkMonitorTimeRange Range { get; }
    public string DisplayName { get; }

    public override string ToString() => DisplayName;
}

public sealed class NetworkEventFilterOption
{
    public NetworkEventFilterOption(NetworkEventFilter filter, string displayName)
    {
        Filter = filter;
        DisplayName = displayName;
    }

    public NetworkEventFilter Filter { get; }
    public string DisplayName { get; }

    public override string ToString() => DisplayName;
}

public sealed record NetworkMonitorTargetOption(string DisplayName, IReadOnlyList<string> Addresses)
{
    public override string ToString() => DisplayName;
}

public sealed record NetworkMonitorIntervalOption(string DisplayName, TimeSpan Interval)
{
    public override string ToString() => DisplayName;
}

public sealed class NetworkStatusEventItem
{
    public NetworkStatusEventItem(NetworkStatusEvent statusEvent, DateTime now)
    {
        Timestamp = statusEvent.Timestamp;
        TimestampText = statusEvent.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
        IsConnected = statusEvent.IsConnected;
        StatusText = statusEvent.IsConnected ? "Connected." : "Disconnected.";
        StatusBrush = statusEvent.IsConnected
            ? new SolidColorBrush(Color.FromRgb(34, 197, 94))
            : new SolidColorBrush(Color.FromRgb(239, 68, 68));
        DurationText = FormatDuration(statusEvent, now);
        LatencyText = statusEvent.RoundtripMs.HasValue ? $"{statusEvent.RoundtripMs.Value} ms" : "-";
    }

    public DateTime Timestamp { get; }
    public string TimestampText { get; }
    public bool IsConnected { get; }
    public string StatusText { get; }
    public IBrush StatusBrush { get; }
    public string DurationText { get; }
    public string LatencyText { get; }

    public string ToLogLine() => $"{TimestampText} - {StatusText} Duration: {DurationText} Latency: {LatencyText}";

    private static string FormatDuration(NetworkStatusEvent statusEvent, DateTime now)
    {
        TimeSpan? duration = statusEvent.Duration;
        if (duration == null && !statusEvent.IsConnected)
        {
            duration = now - statusEvent.Timestamp;
        }

        if (duration == null)
        {
            return "-";
        }

        if (duration.Value.TotalSeconds < 1)
        {
            return $"{Math.Max(0, duration.Value.TotalMilliseconds):0} ms";
        }

        if (duration.Value.TotalMinutes < 1)
        {
            return $"{duration.Value.Seconds}s";
        }

        if (duration.Value.TotalHours < 1)
        {
            return $"{duration.Value.Minutes}m {duration.Value.Seconds}s";
        }

        return $"{(int)duration.Value.TotalHours}h {duration.Value.Minutes}m";
    }
}

public partial class NetworkMonitorViewModel : ViewModelBase, IDisposable
{
    private static readonly IBrush ConnectedBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94));
    private static readonly IBrush DisconnectedBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
    private static readonly IBrush CheckingBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
    private static readonly IBrush PausedBrush = new SolidColorBrush(Color.FromRgb(120, 120, 128));

    private readonly NetworkMonitorHost _host;
    private readonly DispatcherTimer _refreshTimer;
    private bool _disposed;

    public NetworkMonitorViewModel(NetworkMonitorHost? host = null)
    {
        _host = host ?? NetworkMonitorHost.Shared;
        TimeRangeOptions =
        [
            new NetworkMonitorTimeRangeOption(NetworkMonitorTimeRange.Last5Minutes),
            new NetworkMonitorTimeRangeOption(NetworkMonitorTimeRange.Last15Minutes),
            new NetworkMonitorTimeRangeOption(NetworkMonitorTimeRange.LastHour),
            new NetworkMonitorTimeRangeOption(NetworkMonitorTimeRange.Last6Hours),
            new NetworkMonitorTimeRangeOption(NetworkMonitorTimeRange.Last24Hours),
            new NetworkMonitorTimeRangeOption(NetworkMonitorTimeRange.Last7Days),
            new NetworkMonitorTimeRangeOption(NetworkMonitorTimeRange.Last30Days),
            new NetworkMonitorTimeRangeOption(NetworkMonitorTimeRange.All)
        ];
        FilterOptions =
        [
            new NetworkEventFilterOption(NetworkEventFilter.All, "All events"),
            new NetworkEventFilterOption(NetworkEventFilter.Disconnects, "Disconnects"),
            new NetworkEventFilterOption(NetworkEventFilter.Connects, "Connects")
        ];
        TargetOptions =
        [
            new NetworkMonitorTargetOption("Automatic (recommended)", ["1.1.1.1", "8.8.8.8", "9.9.9.9"]),
            new NetworkMonitorTargetOption("Cloudflare (1.1.1.1)", ["1.1.1.1"]),
            new NetworkMonitorTargetOption("Google DNS (8.8.8.8)", ["8.8.8.8"]),
            new NetworkMonitorTargetOption("Quad9 (9.9.9.9)", ["9.9.9.9"])
        ];
        IntervalOptions =
        [
            new NetworkMonitorIntervalOption("2 s", TimeSpan.FromSeconds(2)),
            new NetworkMonitorIntervalOption("5 s", TimeSpan.FromSeconds(5)),
            new NetworkMonitorIntervalOption("10 s", TimeSpan.FromSeconds(10)),
            new NetworkMonitorIntervalOption("30 s", TimeSpan.FromSeconds(30))
        ];
        _selectedTimeRange = TimeRangeOptions[0];
        _selectedFilter = FilterOptions[0];
        _selectedTarget = TargetOptions[0];
        _selectedInterval = IntervalOptions[1];
        ApplyMonitorOptions();
        _host.Monitor.StatusChanged += OnMonitorEvent;
        _host.Monitor.SampleReceived += OnMonitorSample;
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _refreshTimer.Tick += OnRefreshTick;
        _host.EnsureStarted();
        Refresh();
        _refreshTimer.Start();
        RefreshCommands();
    }

    public ObservableCollection<NetworkStatusEventItem> Events { get; } = [];
    public IReadOnlyList<NetworkMonitorTimeRangeOption> TimeRangeOptions { get; }
    public IReadOnlyList<NetworkEventFilterOption> FilterOptions { get; }
    public IReadOnlyList<NetworkMonitorTargetOption> TargetOptions { get; }
    public IReadOnlyList<NetworkMonitorIntervalOption> IntervalOptions { get; }

    public Func<string, Task>? CopyToClipboardRequested { get; set; }
    public Func<string, string, Task<string?>>? SaveFileRequested { get; set; }

    [ObservableProperty]
    private NetworkMonitorTimeRangeOption _selectedTimeRange = null!;

    [ObservableProperty]
    private NetworkEventFilterOption _selectedFilter = null!;

    [ObservableProperty]
    private NetworkMonitorTargetOption _selectedTarget = null!;

    [ObservableProperty]
    private NetworkMonitorIntervalOption _selectedInterval = null!;

    [ObservableProperty]
    private IReadOnlyList<NetworkChartPoint> _chartPoints = [];

    [ObservableProperty]
    private string _statusText = "Starting...";

    [ObservableProperty]
    private string _statusDetails = "Waiting for the first connection check.";

    [ObservableProperty]
    private IBrush _statusBrush = Brushes.Gray;

    [ObservableProperty]
    private string _disconnectCountText = "0";

    [ObservableProperty]
    private string _availabilityText = "-";

    [ObservableProperty]
    private string _currentLatencyText = "-";

    [ObservableProperty]
    private string _averageLatencyText = "-";

    [ObservableProperty]
    private string _longestOutageText = "No outages in this range";

    [ObservableProperty]
    private bool _isMonitoring;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEvents))]
    private bool _canCopy;

    [ObservableProperty]
    private bool _canClear;

    public bool HasEvents => CanCopy;

    partial void OnSelectedTimeRangeChanged(NetworkMonitorTimeRangeOption value) => Refresh();

    partial void OnSelectedFilterChanged(NetworkEventFilterOption value) => Refresh();

    partial void OnSelectedTargetChanged(NetworkMonitorTargetOption value)
    {
        ApplyMonitorOptions();
        RefreshStatusOnly();
    }

    partial void OnSelectedIntervalChanged(NetworkMonitorIntervalOption value)
    {
        ApplyMonitorOptions();
    }

    [RelayCommand]
    private void Start()
    {
        _host.EnsureStarted();
        RefreshCommands();
        Refresh();
    }

    [RelayCommand]
    private void Stop()
    {
        _host.Stop();
        RefreshCommands();
        Refresh();
    }

    [RelayCommand]
    private async Task CopyAllAsync()
    {
        if (CopyToClipboardRequested == null || Events.Count == 0)
        {
            return;
        }

        await CopyToClipboardRequested(BuildLogText());
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (SaveFileRequested == null)
        {
            return;
        }

        string fileName = $"xerahs-network-monitor-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
        string? path = await SaveFileRequested(fileName, "Text files");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        await File.WriteAllTextAsync(path, BuildLogText());
    }

    [RelayCommand]
    private void Clear()
    {
        _host.ClearHistory();
        Refresh();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _refreshTimer.Stop();
        _refreshTimer.Tick -= OnRefreshTick;
        _host.Monitor.StatusChanged -= OnMonitorEvent;
        _host.Monitor.SampleReceived -= OnMonitorSample;
    }

    private void OnMonitorEvent(NetworkStatusEvent _)
    {
        Dispatcher.UIThread.Post(Refresh);
    }

    private void OnMonitorSample(NetworkLatencySample _)
    {
        Dispatcher.UIThread.Post(RefreshStatusOnly);
    }

    private void OnRefreshTick(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void RefreshCommands()
    {
        IsMonitoring = _host.Monitor.IsMonitoring;
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }

    private void RefreshStatusOnly()
    {
        InternetConnectionMonitor monitor = _host.Monitor;
        NetworkLatencySample? sample = monitor.LastSample;
        IsMonitoring = monitor.IsMonitoring;

        if (!monitor.IsMonitoring)
        {
            StatusText = "Paused";
            StatusDetails = "Monitoring is paused. Connection history is preserved.";
            StatusBrush = PausedBrush;
        }
        else if (!monitor.HasConnectionState || sample == null)
        {
            StatusText = "Checking";
            StatusDetails = $"Waiting for a reply from {SelectedTarget.DisplayName}.";
            StatusBrush = CheckingBrush;
        }
        else if (monitor.IsConnected)
        {
            StatusText = "Connected";
            StatusDetails = sample.Success
                ? $"Reply from {sample.Address}."
                : $"No reply from {sample.Address}; confirming the connection.";
            StatusBrush = sample.Success ? ConnectedBrush : CheckingBrush;
        }
        else
        {
            StatusText = "Disconnected";
            StatusDetails = $"No reply from {sample.Address}. XerahS will keep checking.";
            StatusBrush = DisconnectedBrush;
        }

        CurrentLatencyText = sample?.RoundtripMs != null
            ? $"{sample.RoundtripMs.Value} ms"
            : "-";
    }

    private void Refresh()
    {
        DateTime now = DateTime.Now;
        DateTime from = NetworkMonitorTimeRanges.GetStart(SelectedTimeRange.Range, now);
        IReadOnlyList<NetworkStatusEvent> events = _host.History.GetEvents(from, now, SelectedFilter.Filter);
        IReadOnlyList<NetworkStatusEvent> allEvents = SelectedFilter.Filter == NetworkEventFilter.All
            ? events
            : _host.History.GetEvents(from, now);
        List<NetworkStatusEventItem> refreshedEvents = [.. events.Select(statusEvent => new NetworkStatusEventItem(statusEvent, now))];
        bool eventsChanged = Events.Count != refreshedEvents.Count;
        if (!eventsChanged)
        {
            for (int index = 0; index < Events.Count; index++)
            {
                NetworkStatusEventItem current = Events[index];
                NetworkStatusEventItem refreshed = refreshedEvents[index];
                if (current.Timestamp != refreshed.Timestamp ||
                    current.IsConnected != refreshed.IsConnected ||
                    current.DurationText != refreshed.DurationText ||
                    current.LatencyText != refreshed.LatencyText)
                {
                    eventsChanged = true;
                    break;
                }
            }
        }

        if (eventsChanged)
        {
            Events.Clear();
            foreach (NetworkStatusEventItem statusEvent in refreshedEvents)
            {
                Events.Add(statusEvent);
            }
        }

        CanCopy = Events.Count > 0;
        bool hasSamples = _host.History.Samples.Any(sample => sample.Timestamp >= from && sample.Timestamp <= now);
        CanClear = hasSamples || allEvents.Count > 0;
        ChartPoints = hasSamples || allEvents.Count > 0
            ? _host.History.BuildChartPoints(from, now, _host.Monitor.IsConnected, now)
            : [];
        NetworkMonitorStats stats = _host.History.GetStats(from, now, _host.Monitor.IsConnected, now);
        DisconnectCountText = stats.DisconnectCount.ToString(CultureInfo.CurrentCulture);
        AvailabilityText = hasSamples || allEvents.Count > 0
            ? $"{stats.UptimePercent:0.0}%"
            : "-";
        AverageLatencyText = stats.AverageLatencyMs.HasValue
            ? $"{stats.AverageLatencyMs.Value:0} ms"
            : "-";
        LongestOutageText = stats.LongestOutage > TimeSpan.Zero
            ? $"Longest outage: {FormatTimeSpan(stats.LongestOutage)}"
            : "No outages in this range";

        RefreshStatusOnly();
        CopyAllCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
    }

    private void ApplyMonitorOptions()
    {
        NetworkMonitorOptions options = _host.Monitor.Options;
        options.PingAddresses = [.. SelectedTarget.Addresses];
        options.PingIntervalMs = (int)SelectedInterval.Interval.TotalMilliseconds;
        _host.Monitor.Options = options;
    }

    private string BuildLogText()
    {
        StringBuilder builder = new();
        foreach (NetworkStatusEventItem item in Events.Reverse())
        {
            builder.AppendLine(item.ToLogLine());
        }

        return builder.ToString().Trim();
    }

    private static string FormatTimeSpan(TimeSpan value)
    {
        if (value.TotalSeconds < 1)
        {
            return $"{value.TotalMilliseconds:0} ms";
        }

        if (value.TotalMinutes < 1)
        {
            return $"{value.Seconds}s";
        }

        if (value.TotalHours < 1)
        {
            return $"{value.Minutes}m {value.Seconds}s";
        }

        return $"{(int)value.TotalHours}h {value.Minutes}m";
    }
}
