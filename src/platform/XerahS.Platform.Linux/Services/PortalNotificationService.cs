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

using System.Collections.Concurrent;
using System.Threading;
using Avalonia.Threading;
using Tmds.DBus;
using XerahS.Common;
using XerahS.Services.Abstractions;

namespace XerahS.Platform.Linux.Services;

/// <summary>
/// XDG Desktop Portal notification service with actionable buttons (XIP0079 P2).
/// Falls back to <see cref="LinuxNotificationService"/> when the portal is unavailable.
/// </summary>
public sealed class PortalNotificationService : INotificationService, IDisposable
{
    private const string PortalBusName = "org.freedesktop.portal.Desktop";
    private static readonly ObjectPath PortalObjectPath = new("/org/freedesktop/portal/desktop");
    private static readonly TimeSpan ActionTtl = TimeSpan.FromMinutes(10);

    private readonly LinuxNotificationService _fallback;
    private readonly bool _allowNativeFallback;
    private readonly ConcurrentDictionary<string, ActionEntry> _actionCallbacks = new(StringComparer.Ordinal);
    private Connection? _connection;
    private INotificationPortal? _portal;
    private IDisposable? _actionInvokedSubscription;
    private int _notificationId;
    private bool _disposed;

    public PortalNotificationService(bool allowNativeFallback)
    {
        _allowNativeFallback = allowNativeFallback;
        _fallback = new LinuxNotificationService();

        try
        {
            _connection = new Connection(Address.Session);
            _connection.ConnectAsync().GetAwaiter().GetResult();
            _portal = _connection.CreateProxy<INotificationPortal>(PortalBusName, PortalObjectPath);
            _actionInvokedSubscription = _portal.WatchActionInvokedAsync(OnActionInvoked, OnPortalWatchError)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "PortalNotificationService: Unable to initialize Notification portal");
            Dispose();
        }
    }

    public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info)
    {
        _ = Task.Run(() => ShowSimpleAsync(title, message, type));
    }

    public void ShowNotification(string title, string message, string actionText, Action action, NotificationType type = NotificationType.Info)
    {
        _ = Task.Run(() => ShowWithActionAsync(title, message, actionText, action, type));
    }

    private async Task ShowSimpleAsync(string title, string message, NotificationType type)
    {
        if (await TryShowPortalNotificationAsync(title, message, type, actionId: null, actionText: null).ConfigureAwait(false))
        {
            return;
        }

        if (_allowNativeFallback)
        {
            _fallback.ShowNotification(title, message, type);
        }
        else
        {
            DebugHelper.WriteLine($"[Notification:{type}] {title}: {message}");
        }
    }

    private async Task ShowWithActionAsync(string title, string message, string actionText, Action action, NotificationType type)
    {
        string notificationId = $"xerahs-{Interlocked.Increment(ref _notificationId)}";
        string actionId = $"xerahs.act.{notificationId}";

        if (await TryShowPortalNotificationAsync(title, message, type, actionId, actionText).ConfigureAwait(false))
        {
            RegisterActionCallback(actionId, action);
            return;
        }

        if (_allowNativeFallback)
        {
            _fallback.ShowNotification(title, message, actionText, action, type);
        }
        else
        {
            DebugHelper.WriteLine($"[Notification:{type}] {title}: {message} (Action: {actionText})");
        }
    }

    private async Task<bool> TryShowPortalNotificationAsync(
        string title,
        string message,
        NotificationType type,
        string? actionId,
        string? actionText)
    {
        if (_portal == null)
        {
            return false;
        }

        try
        {
            string id = actionId ?? $"xerahs-{Interlocked.Increment(ref _notificationId)}";
            var notification = new Dictionary<string, object>
            {
                ["title"] = title,
                ["body"] = message,
                ["priority"] = MapPriority(type),
            };

            if (!string.IsNullOrWhiteSpace(actionId) && !string.IsNullOrWhiteSpace(actionText))
            {
                notification["buttons"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["label"] = actionText,
                        ["action"] = actionId
                    }
                };
            }
            else
            {
                notification["display-hint"] = new[] { "transient" };
            }

            await _portal.AddNotificationAsync(id, notification).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "PortalNotificationService: AddNotification failed");
            return false;
        }
    }

    private void RegisterActionCallback(string actionId, Action action)
    {
        EvictExpiredActions();
        _actionCallbacks[actionId] = new ActionEntry(action, DateTime.UtcNow);
    }

    private void EvictExpiredActions()
    {
        DateTime cutoff = DateTime.UtcNow - ActionTtl;
        foreach (var pair in _actionCallbacks)
        {
            if (pair.Value.RegisteredUtc < cutoff)
            {
                _actionCallbacks.TryRemove(pair.Key, out _);
            }
        }
    }

    private void OnActionInvoked((string id, string action, object[] parameters) signal)
    {
        if (!_actionCallbacks.TryRemove(signal.action, out ActionEntry entry))
        {
            return;
        }

        try
        {
            Dispatcher.UIThread.Post(entry.Callback);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "PortalNotificationService: ActionInvoked callback failed");
        }
    }

    private static void OnPortalWatchError(Exception ex)
    {
        DebugHelper.WriteException(ex, "PortalNotificationService: portal signal watch failed");
    }

    internal static string MapPriority(NotificationType type)
    {
        return type switch
        {
            NotificationType.Success => "low",
            NotificationType.Warning => "normal",
            NotificationType.Error => "urgent",
            _ => "normal"
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _actionInvokedSubscription?.Dispose();
        _portal = null;
        _connection?.Dispose();
        _connection = null;
        _actionCallbacks.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private readonly record struct ActionEntry(Action Callback, DateTime RegisteredUtc);
}

[DBusInterface("org.freedesktop.portal.Notification")]
public interface INotificationPortal : IDBusObject
{
    Task AddNotificationAsync(string id, IDictionary<string, object> notification);

    Task RemoveNotificationAsync(string id);

    Task<IDisposable> WatchActionInvokedAsync(
        Action<(string id, string action, object[] parameters)> handler,
        Action<Exception>? error = null);
}
