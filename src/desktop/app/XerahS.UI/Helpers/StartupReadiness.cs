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

namespace XerahS.UI.Helpers;

/// <summary>
/// Waits for a bootstrap dependency without blocking the UI thread. Work that the main window
/// schedules on open (hotkey window-ready notification, settings index pre-warm) can run before
/// bootstrap finishes; it waits for readiness instead of throwing into the error log.
/// </summary>
public static class StartupReadiness
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Completes with true as soon as <paramref name="isReady"/> returns true, or false when
    /// <paramref name="timeout"/> elapses first. Each check runs on the caller's context.
    /// </summary>
    public static async Task<bool> WaitUntilAsync(Func<bool> isReady, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(isReady);
        DateTime deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);
        while (!isReady())
        {
            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            try
            {
                await Task.Delay(PollInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        return true;
    }
}
