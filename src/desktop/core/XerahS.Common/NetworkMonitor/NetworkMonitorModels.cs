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

namespace XerahS.Common.NetworkMonitor;

public readonly record struct NetworkProbeResult(bool Success, long? RoundtripMs);

public enum NetworkMonitorTimeRange
{
    Last5Minutes,
    Last15Minutes,
    LastHour,
    Last6Hours,
    Last24Hours,
    Last7Days,
    Last30Days,
    All
}

public enum NetworkEventFilter
{
    All,
    Disconnects,
    Connects
}

public sealed class NetworkMonitorOptions
{
    public int FailThreshold { get; set; } = 4;
    public int PingIntervalMs { get; set; } = 1000;
    public int PingTimeoutMs { get; set; } = 4000;
    public string[] PingAddresses { get; set; } = ["8.8.8.8", "8.8.4.4", "1.1.1.1", "1.0.0.1"];
}

public sealed class NetworkStatusEvent
{
    public DateTime Timestamp { get; set; }
    public bool IsConnected { get; set; }
    public long? RoundtripMs { get; set; }
    public TimeSpan? Duration { get; set; }
}

public sealed class NetworkLatencySample
{
    public DateTime Timestamp { get; set; }
    public bool Success { get; set; }
    public long? RoundtripMs { get; set; }
    public string Address { get; set; } = string.Empty;
}

public sealed class NetworkChartPoint
{
    public DateTime Timestamp { get; init; }
    public bool IsConnected { get; init; }
    public double? LatencyMs { get; init; }
    public bool IsSample { get; init; }
}

public sealed class NetworkMonitorStats
{
    public int DisconnectCount { get; init; }
    public int EventCount { get; init; }
    public double UptimePercent { get; init; }
    public TimeSpan LongestOutage { get; init; }
    public double? AverageLatencyMs { get; init; }
    public long? LastLatencyMs { get; init; }
}

public static class NetworkMonitorTimeRanges
{
    public static DateTime GetStart(NetworkMonitorTimeRange range, DateTime now)
    {
        return range switch
        {
            NetworkMonitorTimeRange.Last5Minutes => now.AddMinutes(-5),
            NetworkMonitorTimeRange.Last15Minutes => now.AddMinutes(-15),
            NetworkMonitorTimeRange.LastHour => now.AddHours(-1),
            NetworkMonitorTimeRange.Last6Hours => now.AddHours(-6),
            NetworkMonitorTimeRange.Last24Hours => now.AddHours(-24),
            NetworkMonitorTimeRange.Last7Days => now.AddDays(-7),
            NetworkMonitorTimeRange.Last30Days => now.AddDays(-30),
            NetworkMonitorTimeRange.All => DateTime.MinValue,
            _ => now.AddHours(-24)
        };
    }

    public static string GetDisplayName(NetworkMonitorTimeRange range)
    {
        return range switch
        {
            NetworkMonitorTimeRange.Last5Minutes => "Last 5 minutes",
            NetworkMonitorTimeRange.Last15Minutes => "Last 15 minutes",
            NetworkMonitorTimeRange.LastHour => "Last 1 hour",
            NetworkMonitorTimeRange.Last6Hours => "Last 6 hours",
            NetworkMonitorTimeRange.Last24Hours => "Last 24 hours",
            NetworkMonitorTimeRange.Last7Days => "Last 7 days",
            NetworkMonitorTimeRange.Last30Days => "Last 30 days",
            NetworkMonitorTimeRange.All => "All time",
            _ => range.ToString()
        };
    }
}
