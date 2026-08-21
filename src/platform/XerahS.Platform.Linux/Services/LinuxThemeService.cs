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
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Services
{
    /// <summary>
    /// Linux implementation of IThemeService using XDG Settings Portal for dark mode detection.
    /// Falls back to GTK settings if the portal is not available.
    /// </summary>
    public class LinuxThemeService : IThemeService, IDisposable
    {
        private const string PortalBusName = "org.freedesktop.portal.Desktop";
        private static readonly ObjectPath PortalObjectPath = new("/org/freedesktop/portal/desktop");
        private const string AppearanceNamespace = "org.freedesktop.appearance";
        private const string ColorSchemeKey = "color-scheme";

        // Color scheme values from XDG portal spec
        private const int ColorSchemeNoPreference = 0;
        private const int ColorSchemeDark = 1;
        private const int ColorSchemeLight = 2;

        private int _colorScheme = ColorSchemeNoPreference;
        private Connection? _connection;
        private IDisposable? _signalSubscription;
        private bool _isMonitoring;
        private bool _disposed;

        public bool IsDarkModePreferred => _colorScheme == ColorSchemeDark;
        public bool IsLightModePreferred => _colorScheme == ColorSchemeLight;
        public int ColorScheme => _colorScheme;

        public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

        public LinuxThemeService()
        {
            // Try to read initial theme preference
            _colorScheme = ReadColorScheme();
        }

        /// <summary>
        /// Reads the current color scheme preference from the system
        /// </summary>
        private int ReadColorScheme()
        {
            // First try XDG Settings Portal (preferred method for modern Linux desktops)
            int? portalResult = TryReadFromSettingsPortal();
            if (portalResult.HasValue)
            {
                Debug.WriteLine($"LinuxThemeService: Got color-scheme from XDG portal: {portalResult.Value}");
                return portalResult.Value;
            }

            // Fall back to gsettings (GNOME/GTK)
            int? gsettingsResult = TryReadFromGSettings();
            if (gsettingsResult.HasValue)
            {
                Debug.WriteLine($"LinuxThemeService: Got color-scheme from gsettings: {gsettingsResult.Value}");
                return gsettingsResult.Value;
            }

            // Fall back to checking GTK theme name (legacy)
            bool? gtkDark = TryReadGtkThemeDark();
            if (gtkDark.HasValue)
            {
                Debug.WriteLine($"LinuxThemeService: Got dark mode from GTK theme name: {gtkDark.Value}");
                return gtkDark.Value ? ColorSchemeDark : ColorSchemeLight;
            }

            Debug.WriteLine("LinuxThemeService: No theme preference detected, defaulting to dark");
            // Default to dark mode for no preference (common choice for developer tools)
            return ColorSchemeDark;
        }

        /// <summary>
        /// Reads color scheme from XDG Settings Portal via D-Bus
        /// </summary>
        private int? TryReadFromSettingsPortal()
        {
            try
            {
                using var connection = new Connection(Address.Session);
                connection.ConnectAsync().GetAwaiter().GetResult();

                var proxy = connection.CreateProxy<ISettingsPortal>(PortalBusName, PortalObjectPath);
                var result = proxy.ReadAsync(AppearanceNamespace, ColorSchemeKey).GetAwaiter().GetResult();

                // The result is a variant containing the actual value
                if (result is object[] arr && arr.Length > 0)
                {
                    // Unwrap nested variants
                    object value = arr[0];
                    while (value is object[] nested && nested.Length > 0)
                    {
                        value = nested[0];
                    }

                    if (value is uint uintVal)
                    {
                        return (int)uintVal;
                    }
                    if (value is int intVal)
                    {
                        return intVal;
                    }
                    if (int.TryParse(value?.ToString(), out int parsed))
                    {
                        return parsed;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LinuxThemeService: Failed to read from Settings Portal: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads color scheme from gsettings (GNOME desktop)
        /// </summary>
        private int? TryReadFromGSettings()
        {
            try
            {
                // Check GNOME color-scheme setting
                var (output, exitCode) = RunGsettingsCapture(
                    "gsettings",
                    "get org.gnome.desktop.interface color-scheme",
                    1000);

                if (exitCode != 0 || string.IsNullOrEmpty(output)) return null;

                string trimmed = output.Trim().Trim('\'');

                return trimmed switch
                {
                    "prefer-dark" => ColorSchemeDark,
                    "prefer-light" => ColorSchemeLight,
                    "default" => ColorSchemeNoPreference,
                    _ => null
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LinuxThemeService: Failed to read from gsettings: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Checks if the GTK theme name contains "dark"
        /// </summary>
        private bool? TryReadGtkThemeDark()
        {
            try
            {
                // Try gsettings for GTK theme
                var (output, exitCode) = RunGsettingsCapture(
                    "gsettings",
                    "get org.gnome.desktop.interface gtk-theme",
                    1000);

                if (exitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    string trimmed = output.Trim().Trim('\'');
                    return trimmed.Contains("dark", StringComparison.OrdinalIgnoreCase) ||
                           trimmed.Contains("Dark", StringComparison.Ordinal);
                }
            }
            catch
            {
                // Ignore
            }

            // Try reading GTK settings file
            try
            {
                string settingsPath = Path.Combine(
                    LinuxXdgDirectories.Detect().ConfigHome,
                    "gtk-3.0",
                    "settings.ini");

                if (File.Exists(settingsPath))
                {
                    string content = File.ReadAllText(settingsPath);
                    // Look for gtk-theme-name=...
                    foreach (string line in content.Split('\n'))
                    {
                        if (line.StartsWith("gtk-theme-name=", StringComparison.OrdinalIgnoreCase))
                        {
                            string themeName = line.Substring("gtk-theme-name=".Length).Trim();
                            return themeName.Contains("dark", StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }
            }
            catch
            {
                // Ignore
            }

            return null;
        }

        /// <summary>
        /// Runs a synchronous CLI invocation and returns its captured stdout
        /// plus exit code. Drains stderr asynchronously so a noisy child
        /// process cannot deadlock on a full OS pipe buffer (the same
        /// anti-pattern previously fixed in
        /// <see cref="XerahS.Platform.Linux.Capture.Helpers.LinuxCliToolRunner"/>
        /// for capture helpers and in
        /// <see cref="LinuxScreenService"/> for screen enumeration). Reads
        /// stdout asynchronously too, so a child that sleeps without
        /// producing output (e.g. <c>sleep 5</c>) cannot stretch a 1-second
        /// timeout into 5 seconds waiting for the child to close its stdout
        /// pipe.
        /// </summary>
        /// <param name="fileName">Executable to run (e.g. "gsettings").</param>
        /// <param name="arguments">Command-line arguments to pass.</param>
        /// <param name="timeoutMs">Maximum time to wait for the process to exit.</param>
        /// <returns>Tuple of (stdout, exitCode). stdout may be empty; exitCode is null on timeout.</returns>
        internal static (string output, int? exitCode) RunGsettingsCapture(string fileName, string arguments, int timeoutMs)
        {
            Process? process = null;
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                process = Process.Start(startInfo);
                if (process == null)
                    return (string.Empty, null);

                // Drain stderr asynchronously so a chatty child process
                // cannot block writing to a full 64KB OS pipe buffer. We
                // deliberately discard stderr text — gsettings only writes
                // to stdout for get-queries; any stderr line is a
                // warning/error that we do not need to surface at the
                // theme-parse layer.
                var stderrDrain = process.StandardError.ReadToEndAsync().ContinueWith(
                    _ => { },
                    TaskScheduler.Default);

                // Read stdout asynchronously too so a child that does not
                // close its stdout pipe within the timeout (e.g. `sleep 5`
                // with a 1s timeout) does not stretch the call to 5
                // seconds. The ReadToEndAsync + Task.WhenAny(timeout)
                // pattern bounds the wait.
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var timeoutTask = Task.Delay(timeoutMs);

                var completed = Task.WaitAny(stdoutTask, timeoutTask);
                if (completed != 0)
                {
                    // Timeout: kill the child. After Kill, the child's
                    // stdout and stderr handles are closed, so the async
                    // drainers will unblock and complete on their own. We
                    // use a bounded wait so we do not block forever if the
                    // kernel delays the kill delivery.
                    try { process.Kill(); } catch { /* best effort */ }
                    try
                    {
                        Task.WaitAll(new Task[] { stdoutTask, stderrDrain }, 1000);
                    }
                    catch
                    {
                        // Drainer may have faulted on a closed stream; ignore.
                    }
                    return (string.Empty, null);
                }

                // stdout closed within the timeout — the child has exited
                // (or is about to). Wait for exit to capture the exit
                // code, with a bounded follow-up to handle the case where
                // stdout is closed but the process has not yet fully
                // released.
                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(); } catch { /* best effort */ }
                    process.WaitForExit(1000);
                    return (stdoutTask.Result, null);
                }

                // Best-effort: make sure the stderr drainer is done so it
                // does not leak a task across process disposal. Bounded so
                // a stuck drainer cannot hang us.
                try
                {
                    Task.WaitAll(new Task[] { stderrDrain }, 1000);
                }
                catch
                {
                    // Drainer may have faulted on a closed stream; ignore.
                }

                return (stdoutTask.Result, process.ExitCode);
            }
            catch
            {
                return (string.Empty, null);
            }
            finally
            {
                // Manually dispose the process so we do not depend on
                // `using` syntax to clean up after a return from a
                // non-using block.
                process?.Dispose();
            }
        }

        /// <summary>
        /// Exposes <see cref="RunGsettingsCapture"/> for regression tests so
        /// the test assembly can drive the run helper with synthetic
        /// commands (e.g. <c>/bin/sh -c "..."</c>) without needing a real
        /// gsettings binary on the test machine.
        /// </summary>
        internal static class TestAccessor
        {
            public static (string output, int? exitCode) RunGsettingsCapture(string fileName, string arguments, int timeoutMs)
            {
                return LinuxThemeService.RunGsettingsCapture(fileName, arguments, timeoutMs);
            }
        }

        public void StartMonitoring()
        {
            if (_isMonitoring || _disposed) return;

            try
            {
                _connection = new Connection(Address.Session);
                _connection.ConnectAsync().GetAwaiter().GetResult();

                var proxy = _connection.CreateProxy<ISettingsPortal>(PortalBusName, PortalObjectPath);

                // Subscribe to SettingChanged signal
                _signalSubscription = proxy.WatchSettingChangedAsync(
                    OnSettingChanged,
                    ex => Debug.WriteLine($"LinuxThemeService: Signal error: {ex.Message}")
                ).GetAwaiter().GetResult();

                _isMonitoring = true;
                Debug.WriteLine("LinuxThemeService: Started monitoring for theme changes");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LinuxThemeService: Failed to start monitoring: {ex.Message}");
            }
        }

        public void StopMonitoring()
        {
            if (!_isMonitoring) return;

            _signalSubscription?.Dispose();
            _signalSubscription = null;

            _connection?.Dispose();
            _connection = null;

            _isMonitoring = false;
            Debug.WriteLine("LinuxThemeService: Stopped monitoring for theme changes");
        }

        private void OnSettingChanged((string Namespace, string Key, object Value) change)
        {
            if (change.Namespace != AppearanceNamespace || change.Key != ColorSchemeKey)
            {
                return;
            }

            int newColorScheme = ColorSchemeNoPreference;

            // Unwrap the value
            object value = change.Value;
            while (value is object[] nested && nested.Length > 0)
            {
                value = nested[0];
            }

            if (value is uint uintVal)
            {
                newColorScheme = (int)uintVal;
            }
            else if (value is int intVal)
            {
                newColorScheme = intVal;
            }
            else if (int.TryParse(value?.ToString(), out int parsed))
            {
                newColorScheme = parsed;
            }

            if (newColorScheme != _colorScheme)
            {
                _colorScheme = newColorScheme;
                Debug.WriteLine($"LinuxThemeService: Theme changed to color-scheme={newColorScheme} (dark={IsDarkModePreferred})");
                ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(IsDarkModePreferred, newColorScheme));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            StopMonitoring();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// D-Bus interface for the XDG Settings Portal
        /// </summary>
        [DBusInterface("org.freedesktop.portal.Settings")]
        private interface ISettingsPortal : IDBusObject
        {
            Task<object> ReadAsync(string Namespace, string Key);
            Task<IDisposable> WatchSettingChangedAsync(Action<(string Namespace, string Key, object Value)> handler, Action<Exception>? onError = null);
        }
    }
}
