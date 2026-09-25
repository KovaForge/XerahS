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

public readonly record struct NetworkProbeResult(bool Success, long? RoundtripMs, string Error = "", string Method = "");

public enum NetworkMonitorTimeRange
{
    Last5Minutes,
    Last15Minutes,
    LastHour,
    Last6Hours,
    Session,
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
    /// <summary>
    /// Consecutive failed rounds required before the link is treated as down.
    /// Each round already requires every selected host to fail, so two rounds
    /// match a probe plus a confirmation probe.
    /// </summary>
    public int FailThreshold { get; set; } = 2;
    public int PingIntervalMs { get; set; } = 2000;
    public int PingTimeoutMs { get; set; } = 2000;
    public string[] PingAddresses { get; set; } = ["1.1.1.1", "8.8.8.8", "9.9.9.9"];
}

public sealed class NetworkStatusEvent
{
    public DateTime Timestamp { get; set; }
    public bool IsConnected { get; set; }
    public long? RoundtripMs { get; set; }
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// True when this is the first confirmed reading after monitoring started,
    /// rather than a later up or down transition.
    /// </summary>
    public bool IsBaseline { get; set; }
}

public sealed class NetworkLatencySample
{
    public DateTime Timestamp { get; set; }
    public bool Success { get; set; }
    public long? RoundtripMs { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
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
    public static DateTime GetStart(NetworkMonitorTimeRange range, DateTime now, DateTime? sessionStart = null)
    {
        return range switch
        {
            NetworkMonitorTimeRange.Last5Minutes => now.AddMinutes(-5),
            NetworkMonitorTimeRange.Last15Minutes => now.AddMinutes(-15),
            NetworkMonitorTimeRange.LastHour => now.AddHours(-1),
            NetworkMonitorTimeRange.Last6Hours => now.AddHours(-6),
            NetworkMonitorTimeRange.Session => sessionStart ?? now,
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
            NetworkMonitorTimeRange.Session => "Session",
            NetworkMonitorTimeRange.Last24Hours => "Last 24 hours",
            NetworkMonitorTimeRange.Last7Days => "Last 7 days",
            NetworkMonitorTimeRange.Last30Days => "Last 30 days",
            NetworkMonitorTimeRange.All => "All time",
            _ => range.ToString()
        };
    }
}
