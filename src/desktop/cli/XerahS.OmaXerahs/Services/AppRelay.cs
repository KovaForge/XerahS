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
using XerahS.Common;

namespace XerahS.OmaXerahs.Services;

/// <summary>How a relayed request reached XerahS.</summary>
internal enum AppRelayOutcome
{
    /// <summary>A running XerahS accepted the arguments over the single-instance channel.</summary>
    Delivered,

    /// <summary>XerahS was not running and was started with the arguments.</summary>
    Started,

    /// <summary>XerahS was not running and could not be started.</summary>
    Unavailable
}

/// <summary>
/// Hands a request to the XerahS desktop app: the running instance if there is one, otherwise a
/// new instance started with the same arguments. Never loads settings or waits for the work, so
/// a Hyprland key press returns at once while the capture runs in the app.
/// </summary>
internal static class AppRelay
{
    /// <summary>Overrides the XerahS executable started when no instance is running.</summary>
    internal const string AppPathEnvironmentVariable = "XERAHS_APP_PATH";

    internal static Func<string[], bool> SendToRunningInstance { get; set; } = static args =>
        SingleInstanceManager.TrySendToRunningInstance(AppContracts.SingleInstance.PipeName, args, timeoutMs: 500);

    internal static Func<string, string[], bool> StartApp { get; set; } = StartDetached;

    internal static AppRelayOutcome Relay(string[] args)
    {
        if (SendToRunningInstance(args))
        {
            return AppRelayOutcome.Delivered;
        }

        string? appPath = ResolveAppExecutable(AppContext.BaseDirectory, Environment.GetEnvironmentVariable(AppPathEnvironmentVariable));
        if (appPath != null && StartApp(appPath, args))
        {
            return AppRelayOutcome.Started;
        }

        return AppRelayOutcome.Unavailable;
    }

    /// <summary>XerahS ships next to omaxerahs; the override wins when it points at a file.</summary>
    internal static string? ResolveAppExecutable(string baseDirectory, string? overridePath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return File.Exists(overridePath) ? overridePath : null;
        }

        string candidate = Path.Combine(baseDirectory, OperatingSystem.IsWindows() ? "XerahS.exe" : "XerahS");
        return File.Exists(candidate) ? candidate : null;
    }

    private static bool StartDetached(string appPath, string[] args)
    {
        try
        {
            var startInfo = new ProcessStartInfo(appPath)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(appPath) ?? AppContext.BaseDirectory
            };
            foreach (string argument in args)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process? process = Process.Start(startInfo);
            return process != null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            DebugHelper.WriteLine($"omaxerahs: could not start XerahS at '{appPath}': {ex.Message}");
            return false;
        }
    }
}

internal sealed class AppRelayResponse
{
    public int SchemaVersion { get; init; } = 1;
    public bool Ok { get; init; } = true;

    /// <summary>"workflow.run" or "capture".</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>Workflow id or name, or the capture target, exactly as relayed.</summary>
    public string Target { get; init; } = string.Empty;

    /// <summary>"delivered" (running app) or "started" (app launched with the request).</summary>
    public string Delivery { get; init; } = string.Empty;
}
