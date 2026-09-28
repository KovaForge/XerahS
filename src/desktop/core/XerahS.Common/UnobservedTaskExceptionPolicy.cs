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

namespace XerahS.Common
{
    /// <summary>
    /// Classifies exceptions that reach <see cref="System.Threading.Tasks.TaskScheduler.UnobservedTaskException"/>.
    /// D-Bus session teardown (the bus or a portal service going away) faults background tasks
    /// owned by Avalonia's tray/menu D-Bus stack; those are expected and are logged as one
    /// informational line instead of an error-log entry.
    /// </summary>
    public static class UnobservedTaskExceptionPolicy
    {
        public enum Disposition
        {
            /// <summary>Unexpected: write to the error log.</summary>
            Error,
            /// <summary>Expected D-Bus teardown: write one informational line.</summary>
            Informational,
            /// <summary>Known noise with no diagnostic value: ignore.</summary>
            Ignore
        }

        public static Disposition Classify(Exception? exception)
        {
            if (exception == null)
            {
                return Disposition.Ignore;
            }

            foreach (Exception inner in Flatten(exception))
            {
                string? typeName = inner.GetType().FullName;
                if (typeName == "Tmds.DBus.Protocol.DBusException" &&
                    inner.Message.Contains("ServiceUnknown", StringComparison.Ordinal))
                {
                    return Disposition.Ignore;
                }

                if (typeName is "Tmds.DBus.Protocol.DBusConnectionClosedException" or "Tmds.DBus.DisconnectedException")
                {
                    return Disposition.Informational;
                }
            }

            return Disposition.Error;
        }

        private static IEnumerable<Exception> Flatten(Exception exception)
        {
            if (exception is AggregateException aggregate)
            {
                foreach (Exception inner in aggregate.Flatten().InnerExceptions)
                {
                    for (Exception? current = inner; current != null; current = current.InnerException)
                    {
                        yield return current;
                    }
                }

                yield break;
            }

            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                yield return current;
            }
        }
    }
}
