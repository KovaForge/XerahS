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

namespace XerahS.Common;

/// <summary>
/// Classifies D-Bus failures that are expected on some desktops (no EIS daemon, portal service
/// restarting, session bus closing at logout) so callers log one informational line instead of
/// writing a stack trace to the error log (XIP0088 Phase 0).
/// </summary>
public static class DBusExceptionClassifier
{
    /// <summary>True when the InputCapture portal cannot reach an EIS server (e.g. Hyprland).</summary>
    public static bool IsEisUnavailable(Exception? exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current.Message.Contains("Not connected to EIS", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("No EIS", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when the exception only says the D-Bus peer went away: the connection was closed,
    /// the service is unknown or disconnected. These happen at logout or portal restarts.
    /// </summary>
    public static bool IsConnectionGone(Exception? exception)
    {
        if (exception is AggregateException aggregate)
        {
            var inner = aggregate.Flatten().InnerExceptions;
            return inner.Count > 0 && inner.All(IsConnectionGone);
        }

        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            string typeName = current.GetType().FullName ?? string.Empty;
            if (typeName.EndsWith("DBusConnectionClosedException", StringComparison.Ordinal) ||
                typeName.EndsWith("DisconnectedException", StringComparison.Ordinal))
            {
                return true;
            }

            if (typeName.StartsWith("Tmds.DBus", StringComparison.Ordinal) &&
                (current.Message.Contains("ServiceUnknown", StringComparison.Ordinal) ||
                 current.Message.Contains("Connection closed", StringComparison.OrdinalIgnoreCase) ||
                 current.Message.Contains("NoReply", StringComparison.Ordinal) ||
                 current.Message.Contains("Disconnected", StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>One-line description used when an expected failure is logged informationally.</summary>
    public static string Describe(Exception exception)
    {
        Exception root = exception;
        while (root.InnerException != null)
        {
            root = root.InnerException;
        }

        return $"{root.GetType().Name}: {root.Message}";
    }
}
