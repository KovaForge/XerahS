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

namespace XerahS.Uploaders.Multipart;

public class RetryPolicy
{
    public int MaxRetries { get; set; } = 3;

    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    public bool JitterEnabled { get; set; } = true;

    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(MaxRetries);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(BaseDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(MaxDelay, TimeSpan.Zero);

        if (BaseDelay > MaxDelay)
        {
            throw new ArgumentOutOfRangeException(nameof(BaseDelay), "Base delay must be less than or equal to max delay.");
        }
    }

    public TimeSpan GetDelay(int retryAttempt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(retryAttempt, 0);
        Validate();

        double baseDelayMs = BaseDelay.TotalMilliseconds * Math.Pow(2, retryAttempt - 1);
        double cappedDelayMs = Math.Min(baseDelayMs, MaxDelay.TotalMilliseconds);

        if (JitterEnabled)
        {
            cappedDelayMs = Math.Min(cappedDelayMs * (0.5d + Random.Shared.NextDouble()), MaxDelay.TotalMilliseconds);
        }

        return TimeSpan.FromMilliseconds(Math.Max(1d, cappedDelayMs));
    }
}
