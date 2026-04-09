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

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus;
using XerahS.Common;

namespace XerahS.Platform.Linux.Capture;



internal static class PortalRequestExtensions
{
    private static readonly ConditionalWeakTable<Connection, CachedConnectionInfo> LocalConnectionNames = new();

    public static async Task<(uint response, IDictionary<string, object> results)> WaitForResponseAsync(
        this IPortalRequest request,
        CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<(uint, IDictionary<string, object>)>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() =>
            tcs.TrySetCanceled(cancellationToken));
        using var watch = await request.WatchResponseAsync(
            data =>
            {
                DebugHelper.WriteLine($"[XDG Portal] SIGNAL RECEIVED: Response={data.response}, Count={data.results?.Count ?? 0}");
                tcs.TrySetResult((data.response, data.results ?? new Dictionary<string, object>()));
            },
            ex =>
            {
                if (ex is DBusException dbusEx)
                {
                    DebugHelper.WriteLine($"[XDG Portal] RESPONSE WATCH ERROR: {dbusEx.ErrorName} ({dbusEx.ErrorMessage})");
                }
                else
                {
                    DebugHelper.WriteLine($"[XDG Portal] RESPONSE WATCH ERROR: {ex.GetType().Name}: {ex.Message}");
                }

                tcs.TrySetException(ex);
            }).ConfigureAwait(false);
        return await tcs.Task.ConfigureAwait(false);
    }

    public static async Task<(uint response, IDictionary<string, object> results)> SendPortalRequestAsync(
        this Connection connection,
        string portalBusName,
        IDictionary<string, object> options,
        Func<Task<ObjectPath>> sendRequest,
        CancellationToken cancellationToken = default)
    {
        var requestPath = await PrepareRequestPathAsync(connection, options).ConfigureAwait(false);
        var request = connection.CreateProxy<IPortalRequest>(portalBusName, requestPath);
        using var expectedRequestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var responseTask = request.WaitForResponseAsync(expectedRequestCts.Token);
        var actualRequestPath = await sendRequest().ConfigureAwait(false);

        if (!actualRequestPath.Equals(requestPath))
        {
            expectedRequestCts.Cancel();
            try
            {
                await responseTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            DebugHelper.WriteLine($"[XDG Portal] Request handle mismatch. Expected {requestPath}, portal returned {actualRequestPath}. Falling back to returned path watcher.");
            var actualRequest = connection.CreateProxy<IPortalRequest>(portalBusName, actualRequestPath);
            return await actualRequest.WaitForResponseAsync(cancellationToken).ConfigureAwait(false);
        }

        return await responseTask.ConfigureAwait(false);
    }

    public static void CacheLocalConnectionName(Connection connection, ConnectionInfo connectionInfo)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(connectionInfo);
        CacheLocalConnectionName(connection, connectionInfo.LocalName);
    }

    public static void CacheLocalConnectionName(Connection connection, string localName)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (string.IsNullOrWhiteSpace(localName))
        {
            throw new ArgumentException("A connected D-Bus local name is required.", nameof(localName));
        }

        LocalConnectionNames.Remove(connection);
        LocalConnectionNames.Add(connection, new CachedConnectionInfo(localName));
    }

    internal static string CreateExpectedRequestPath(string localName, string handleToken)
    {
        if (string.IsNullOrWhiteSpace(localName))
        {
            throw new ArgumentException("A connected D-Bus local name is required.", nameof(localName));
        }

        if (string.IsNullOrWhiteSpace(handleToken))
        {
            throw new ArgumentException("A non-empty portal handle token is required.", nameof(handleToken));
        }

        string sender = localName.TrimStart(':').Replace('.', '_');
        return $"/org/freedesktop/portal/desktop/request/{sender}/{handleToken}";
    }

    private static async Task<ObjectPath> PrepareRequestPathAsync(Connection connection, IDictionary<string, object> options)
    {
        if (!options.TryGetValue("handle_token", out var tokenObj) ||
            tokenObj is not string handleToken ||
            string.IsNullOrWhiteSpace(handleToken))
        {
            handleToken = $"xerahs_{Guid.NewGuid():N}";
            options["handle_token"] = handleToken;
        }

        string localName = await GetLocalConnectionNameAsync(connection).ConfigureAwait(false);
        return new ObjectPath(CreateExpectedRequestPath(localName, handleToken));
    }

    private static async Task<string> GetLocalConnectionNameAsync(Connection connection)
    {
        if (LocalConnectionNames.TryGetValue(connection, out CachedConnectionInfo? cached))
        {
            return cached.LocalName;
        }

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<ConnectionStateChangedEventArgs>? handler = null;
        handler = (_, args) =>
        {
            if (args.State == ConnectionState.Connected &&
                args.ConnectionInfo != null &&
                !string.IsNullOrWhiteSpace(args.ConnectionInfo.LocalName))
            {
                tcs.TrySetResult(args.ConnectionInfo.LocalName);
                return;
            }

            if (args.State == ConnectionState.Disconnected)
            {
                tcs.TrySetException(args.DisconnectReason ?? new InvalidOperationException("D-Bus connection disconnected before a local bus name was assigned."));
            }
        };

        connection.StateChanged += handler;
        using var registration = new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token.Register(() =>
            tcs.TrySetException(new InvalidOperationException("Unable to resolve the D-Bus local name. Cache it from ConnectAsync() before sending portal requests.")));
        try
        {
            string localName = await tcs.Task.ConfigureAwait(false);
            CacheLocalConnectionName(connection, localName);
            return localName;
        }
        finally
        {
            connection.StateChanged -= handler;
        }
    }

    private sealed class CachedConnectionInfo(string localName)
    {
        public string LocalName { get; } = localName;
    }

    public static bool TryGetResult<T>(this IDictionary<string, object> results, string key, out T? value)
    {
        value = default;
        if (!results.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        raw = UnwrapVariant(raw);

        if (raw is T typed)
        {
            value = typed;
            return true;
        }

        if (typeof(T) == typeof(string) && raw is ObjectPath path)
        {
            value = (T)(object)path.ToString();
            return true;
        }

        return false;
    }

    private static object UnwrapVariant(object value)
    {
        var current = value;
        while (current != null)
        {
            var type = current.GetType();
            var typeName = type.FullName;
            if (typeName != "Tmds.DBus.Protocol.Variant" &&
                typeName != "Tmds.DBus.Protocol.VariantValue" &&
                typeName != "Tmds.DBus.Variant")
            {
                break;
            }

            var valueProp = type.GetProperty("Value");
            var unwrapped = valueProp?.GetValue(current);
            if (unwrapped == null)
            {
                break;
            }

            current = unwrapped;
        }

        return current ?? value;
    }
}
