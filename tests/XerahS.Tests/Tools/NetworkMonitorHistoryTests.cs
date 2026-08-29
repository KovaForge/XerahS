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
using XerahS.Common.NetworkMonitor;

namespace XerahS.Tests.Tools;

[TestFixture]
public class NetworkMonitorHistoryTests
{
    [Test]
    public void AddEvent_SetsDurationOnReconnect()
    {
        NetworkMonitorHistory history = new();
        DateTime start = new(2026, 8, 29, 9, 0, 0);
        history.AddEvent(new NetworkStatusEvent { Timestamp = start, IsConnected = false });
        history.AddEvent(new NetworkStatusEvent { Timestamp = start.AddMinutes(5), IsConnected = true, RoundtripMs = 20 });

        IReadOnlyList<NetworkStatusEvent> events = history.Events;
        Assert.That(events[0].Duration, Is.EqualTo(TimeSpan.FromMinutes(5)));
        Assert.That(events[1].Duration, Is.EqualTo(TimeSpan.FromMinutes(5)));
    }

    [Test]
    public void GetStats_ComputesUptimeAndDisconnectsForRange()
    {
        NetworkMonitorHistory history = new();
        DateTime start = new(2026, 8, 29, 12, 0, 0);
        history.AddEvent(new NetworkStatusEvent { Timestamp = start.AddMinutes(10), IsConnected = false });
        history.AddEvent(new NetworkStatusEvent { Timestamp = start.AddMinutes(20), IsConnected = true });

        NetworkMonitorStats stats = history.GetStats(start, start.AddHours(1), isCurrentlyConnected: true, now: start.AddHours(1));

        Assert.That(stats.DisconnectCount, Is.EqualTo(1));
        Assert.That(stats.UptimePercent, Is.EqualTo(50.0 / 60.0 * 100.0).Within(0.2));
        Assert.That(stats.LongestOutage, Is.EqualTo(TimeSpan.FromMinutes(10)));
    }

    [Test]
    public void GetEvents_FiltersByTimeRangeAndKind()
    {
        NetworkMonitorHistory history = new();
        DateTime now = new(2026, 8, 29, 18, 0, 0);
        history.AddEvent(new NetworkStatusEvent { Timestamp = now.AddHours(-3), IsConnected = false });
        history.AddEvent(new NetworkStatusEvent { Timestamp = now.AddHours(-2), IsConnected = true });
        history.AddEvent(new NetworkStatusEvent { Timestamp = now.AddMinutes(-10), IsConnected = false });

        DateTime from = NetworkMonitorTimeRanges.GetStart(NetworkMonitorTimeRange.LastHour, now);
        IReadOnlyList<NetworkStatusEvent> disconnects = history.GetEvents(from, now, NetworkEventFilter.Disconnects);

        Assert.That(disconnects, Has.Count.EqualTo(1));
        Assert.That(disconnects[0].Timestamp, Is.EqualTo(now.AddMinutes(-10)));
    }

    [Test]
    public void Store_RoundTripsEvents()
    {
        string path = Path.Combine(Path.GetTempPath(), $"xerahs-network-monitor-{Guid.NewGuid():N}.json");
        try
        {
            NetworkMonitorHistory history = new();
            DateTime timestamp = new(2026, 8, 29, 8, 30, 0);
            history.AddEvent(new NetworkStatusEvent { Timestamp = timestamp, IsConnected = false });
            history.AddEvent(new NetworkStatusEvent { Timestamp = timestamp.AddSeconds(12), IsConnected = true, RoundtripMs = 9 });
            NetworkMonitorStore.Save(history, path);

            NetworkMonitorHistory loaded = NetworkMonitorStore.Load(path);
            Assert.That(loaded.Events, Has.Count.EqualTo(2));
            Assert.That(loaded.Events[0].IsConnected, Is.False);
            Assert.That(loaded.Events[1].RoundtripMs, Is.EqualTo(9));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
