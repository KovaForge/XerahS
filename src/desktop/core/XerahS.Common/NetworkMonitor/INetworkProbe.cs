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
using System.Net.NetworkInformation;

namespace XerahS.Common.NetworkMonitor;

public interface INetworkProbe
{
    Task<NetworkProbeResult> ProbeAsync(string host, int timeoutMs, CancellationToken cancellationToken);
}

public sealed class IcmpNetworkProbe : INetworkProbe
{
    public async Task<NetworkProbeResult> ProbeAsync(string host, int timeoutMs, CancellationToken cancellationToken)
    {
        if (!NetworkInterface.GetIsNetworkAvailable())
        {
            return new NetworkProbeResult(false, null, "No network interface is available.");
        }

        int timeout = Math.Max(200, timeoutMs);
        try
        {
            using Ping ping = new();
            using CancellationTokenRegistration registration = cancellationToken.Register(ping.SendAsyncCancel);
            Stopwatch stopwatch = Stopwatch.StartNew();
            PingReply reply = await ping.SendPingAsync(host, timeout)
                .WaitAsync(TimeSpan.FromMilliseconds(timeout + 250), cancellationToken)
                .ConfigureAwait(false);
            stopwatch.Stop();

            if (reply.Status == IPStatus.Success)
            {
                long latency = reply.RoundtripTime > 0
                    ? reply.RoundtripTime
                    : (long)Math.Round(stopwatch.Elapsed.TotalMilliseconds);
                return new NetworkProbeResult(true, latency, string.Empty, "ICMP");
            }

            if (reply.Status == IPStatus.TimedOut)
            {
                return new NetworkProbeResult(false, null, $"{host} timed out.");
            }

            return new NetworkProbeResult(false, null, $"{host} failed ({reply.Status}).");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return new NetworkProbeResult(false, null, $"{host} timed out.");
        }
        catch (Exception ex)
        {
            return new NetworkProbeResult(false, null, $"{host} failed ({ex.Message}).");
        }
    }
}
