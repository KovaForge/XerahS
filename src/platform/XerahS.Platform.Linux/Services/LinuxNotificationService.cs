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
using Avalonia.Threading;
using XerahS.Common;
using XerahS.Services.Abstractions;

namespace XerahS.Platform.Linux.Services;

/// <summary>
/// Notification service using notify-send when available (XIP0079 P2: async delivery + action buttons).
/// </summary>
public sealed class LinuxNotificationService : INotificationService
{
    internal const string DefaultActionKey = "xerahs_action";
    private static readonly Lazy<bool> SupportsActions = new(ProbeNotifySendActions);

    public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info)
    {
        _ = Task.Run(() => ShowSimpleAsync(title, message, type));
    }

    public void ShowNotification(string title, string message, string actionText, Action action, NotificationType type = NotificationType.Info)
    {
        _ = Task.Run(() => ShowWithActionAsync(title, message, actionText, action, type));
    }

    private static void ShowSimpleAsync(string title, string message, NotificationType type)
    {
        if (!TryNotifySend(title, message, type))
        {
            DebugHelper.WriteLine($"[Notification:{type}] {title}: {message}");
        }
    }

    private static void ShowWithActionAsync(string title, string message, string actionText, Action action, NotificationType type)
    {
        if (SupportsActions.Value && TryNotifySendWithAction(title, message, actionText, action, type))
        {
            return;
        }

        if (!TryNotifySend(title, $"{message} ({actionText})", type))
        {
            DebugHelper.WriteLine($"[Notification:{type}] {title}: {message} (Action: {actionText})");
        }
    }

    private static bool TryNotifySend(string title, string message, NotificationType type)
    {
        try
        {
            using var process = Process.Start(CreateStartInfo(title, message, type));
            if (process == null)
            {
                return false;
            }

            return WaitForSuccessfulExit(process, 2000);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryNotifySendWithAction(string title, string message, string actionText, Action action, NotificationType type)
    {
        Process? process = null;
        try
        {
            process = Process.Start(CreateActionStartInfo(title, message, actionText, type));
            if (process == null)
            {
                return false;
            }

            string? selectedKey = process.StandardOutput.ReadLine();
            if (!process.WaitForExit(60_000))
            {
                process.Kill(entireProcessTree: true);
                return false;
            }

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(selectedKey))
            {
                return false;
            }

            if (string.Equals(selectedKey.Trim(), DefaultActionKey, StringComparison.Ordinal))
            {
                Dispatcher.UIThread.Post(action);
            }

            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "LinuxNotificationService: notify-send action failed");
            return false;
        }
        finally
        {
            process?.Dispose();
        }
    }

    internal static bool WaitForSuccessfulExit(Process process, int timeoutMilliseconds)
    {
        if (process.WaitForExit(timeoutMilliseconds))
        {
            return process.ExitCode == 0;
        }

        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(500);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to kill timed-out notification process");
        }

        return false;
    }

    internal static ProcessStartInfo CreateStartInfo(string title, string message, NotificationType type = NotificationType.Info)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "notify-send",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = false,
            RedirectStandardOutput = false
        };

        startInfo.ArgumentList.Add("-u");
        startInfo.ArgumentList.Add(MapUrgency(type));
        startInfo.ArgumentList.Add(title);
        startInfo.ArgumentList.Add(message);
        return startInfo;
    }

    internal static ProcessStartInfo CreateActionStartInfo(string title, string message, string actionText, NotificationType type)
    {
        var startInfo = CreateStartInfo(title, message, type);
        startInfo.RedirectStandardOutput = true;
        startInfo.ArgumentList.Insert(0, "--wait");
        startInfo.ArgumentList.Insert(0, $"{DefaultActionKey}={actionText}");
        startInfo.ArgumentList.Insert(0, "--action");
        return startInfo;
    }

    internal static string MapUrgency(NotificationType type)
    {
        return type switch
        {
            NotificationType.Success => "low",
            NotificationType.Warning => "normal",
            NotificationType.Error => "critical",
            _ => "normal"
        };
    }

    internal static bool ProbeNotifySendActions()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "notify-send",
                Arguments = "--help",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process == null || !process.WaitForExit(2000))
            {
                return false;
            }

            string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            return output.Contains("--action", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
}
