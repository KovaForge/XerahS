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
using Tmds.DBus;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Capture;

namespace XerahS.Platform.Linux.Services;

public sealed class FlatpakPortalStartupService : IStartupService, IDisposable
{
    private const string PortalBusName = "org.freedesktop.portal.Desktop";
    private const string EnabledMarkerFileName = "flatpak-autostart.enabled";
    private static readonly ObjectPath PortalObjectPath = new("/org/freedesktop/portal/desktop");

    private readonly string _appId;
    private readonly string _legacyConfigStateFilePath;
    private readonly string _stateFilePath;
    private Connection? _connection;
    private IBackgroundPortal? _portal;
    private bool _disposed;

    public FlatpakPortalStartupService(string appId)
    {
        _appId = LinuxRuntimeEnvironment.NormalizeAppId(appId, "io.github.ShareX.XerahS");
        var xdgDirectories = LinuxXdgDirectories.Detect();
        _stateFilePath = GetStateFilePath(xdgDirectories);
        _legacyConfigStateFilePath = GetLegacyConfigStateFilePath(xdgDirectories);

        try
        {
            _connection = new Connection(Address.Session);
            var connectionInfo = _connection.ConnectAsync().GetAwaiter().GetResult();
            PortalRequestExtensions.CacheLocalConnectionName(_connection, connectionInfo);
            _portal = _connection.CreateProxy<IBackgroundPortal>(PortalBusName, PortalObjectPath);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "FlatpakPortalStartupService: Unable to initialize Background portal");
            Dispose();
        }
    }

    public bool IsRunAtStartupEnabled()
    {
        return File.Exists(_stateFilePath) || File.Exists(_legacyConfigStateFilePath);
    }

    public bool SetRunAtStartup(bool enable)
    {
        if (_portal == null || _connection == null)
        {
            return false;
        }

        try
        {
            var options = new Dictionary<string, object>
            {
                ["handle_token"] = $"xerahs_background_{Guid.NewGuid():N}",
                ["reason"] = "Keep XerahS capture hotkeys and watch folders available after login.",
                ["autostart"] = enable,
                ["commandline"] = BuildAutostartCommandLine(_appId)
            };

            var parentWindow = PlatformServices.NativeWindowHandleProvider?.Invoke() ?? string.Empty;
            var (response, results) = _connection
                .SendPortalRequestAsync(
                    PortalBusName,
                    options,
                    () => _portal.RequestBackgroundAsync(parentWindow, options))
                .GetAwaiter()
                .GetResult();

            if (response != 0)
            {
                DebugHelper.WriteLine($"FlatpakPortalStartupService: RequestBackground failed ({response}).");
                return false;
            }

            bool autostartGranted = TryReadBool(results, "autostart");
            if (enable && autostartGranted)
            {
                WriteEnabledMarker();
                return true;
            }

            if (!enable)
            {
                DeleteEnabledMarker();
                return true;
            }

            DeleteEnabledMarker();
            return false;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "FlatpakPortalStartupService: Failed to update autostart through Background portal");
            return false;
        }
    }

    private static bool TryReadBool(IDictionary<string, object> results, string key)
    {
        if (!results.TryGetValue(key, out var value) || value == null)
        {
            return false;
        }

        if (value is bool boolValue)
        {
            return boolValue;
        }

        var valueProperty = value.GetType().GetProperty("Value");
        if (valueProperty?.GetValue(value) is bool variantBool)
        {
            return variantBool;
        }

        return false;
    }

    private void WriteEnabledMarker()
    {
        string? directory = Path.GetDirectoryName(_stateFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_stateFilePath, _appId);
        DeleteEnabledMarkerFile(_legacyConfigStateFilePath);
    }

    private void DeleteEnabledMarker()
    {
        DeleteEnabledMarkerFile(_stateFilePath);
        DeleteEnabledMarkerFile(_legacyConfigStateFilePath);
    }

    private static void DeleteEnabledMarkerFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    internal static string GetStateFilePath(LinuxXdgDirectories xdgDirectories)
    {
        return Path.Combine(xdgDirectories.StateDirectory, EnabledMarkerFileName);
    }

    internal static string GetLegacyConfigStateFilePath(LinuxXdgDirectories xdgDirectories)
    {
        return Path.Combine(xdgDirectories.ConfigDirectory, EnabledMarkerFileName);
    }

    internal static string[] BuildAutostartCommandLine(string appId)
    {
        return ["flatpak", "run", LinuxRuntimeEnvironment.NormalizeAppId(appId, "io.github.ShareX.XerahS")];
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

[DBusInterface("org.freedesktop.portal.Background")]
public interface IBackgroundPortal : IDBusObject
{
    Task<ObjectPath> RequestBackgroundAsync(string parentWindow, IDictionary<string, object> options);
}
