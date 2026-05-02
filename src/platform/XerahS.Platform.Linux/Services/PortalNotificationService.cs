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

using System.Collections.Generic;
using System.Threading;
using Tmds.DBus;
using XerahS.Common;
using XerahS.Services.Abstractions;

namespace XerahS.Platform.Linux.Services;

public sealed class PortalNotificationService : INotificationService, IDisposable
{
    private const string PortalBusName = "org.freedesktop.portal.Desktop";
    private static readonly ObjectPath PortalObjectPath = new("/org/freedesktop/portal/desktop");
    private readonly LinuxNotificationService _fallback;
    private readonly bool _allowNativeFallback;
    private Connection? _connection;
    private INotificationPortal? _portal;
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
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "PortalNotificationService: Unable to initialize Notification portal");
            Dispose();
        }
    }

    public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info)
    {
        if (TryShowPortalNotification(title, message, type))
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

    public void ShowNotification(string title, string message, string actionText, Action action, NotificationType type = NotificationType.Info)
    {
        string body = string.IsNullOrWhiteSpace(actionText)
            ? message
            : $"{message} ({actionText})";

        ShowNotification(title, body, type);
    }

    private bool TryShowPortalNotification(string title, string message, NotificationType type)
    {
        if (_portal == null)
        {
            return false;
        }

        try
        {
            string id = $"xerahs-{Interlocked.Increment(ref _notificationId)}";
            var notification = new Dictionary<string, object>
            {
                ["title"] = title,
                ["body"] = message,
                ["priority"] = MapPriority(type),
                ["display-hint"] = new[] { "transient" }
            };

            _portal.AddNotificationAsync(id, notification).GetAwaiter().GetResult();
            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "PortalNotificationService: AddNotification failed");
            return false;
        }
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

        _portal = null;
        _connection?.Dispose();
        _connection = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

[DBusInterface("org.freedesktop.portal.Notification")]
public interface INotificationPortal : IDBusObject
{
    Task AddNotificationAsync(string id, IDictionary<string, object> notification);

    Task RemoveNotificationAsync(string id);
}
