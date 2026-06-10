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

using NUnit.Framework;
using XerahS.UI.ViewModels;

namespace XerahS.Tests.UI;

/// <summary>
/// Regression tests for <see cref="HistoryViewModel.ShowHistoryBackupFailureToastIfPresent"/>.
/// The helper is null-safe and exception-safe so the history append failure path in
/// <c>HistoryViewModel.CombineSelectedImagesAsync</c> can call it without guarding for
/// headless / unit-test environments where <c>PlatformServices.Toast</c> is null.
/// </summary>
[TestFixture]
public class HistoryViewModelBackupToastTests
{
    [Test]
    public void ShowHistoryBackupFailureToastIfPresent_NullReason_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => HistoryViewModel.ShowHistoryBackupFailureToastIfPresent(null));
    }

    [Test]
    public void ShowHistoryBackupFailureToastIfPresent_EmptyReason_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => HistoryViewModel.ShowHistoryBackupFailureToastIfPresent(string.Empty));
    }

    [Test]
    public void ShowHistoryBackupFailureToastIfPresent_WhitespaceReason_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => HistoryViewModel.ShowHistoryBackupFailureToastIfPresent("   \t\n  "));
    }

    [Test]
    public void ShowHistoryBackupFailureToastIfPresent_NonEmptyReason_DoesNotThrowWhenPlatformToastNotInitialized()
    {
        // The helper uses `PlatformServices.Toast?.ShowToast(...)` — when the platform toast
        // service has not been initialized (typical in unit tests / headless runs), the
        // null-conditional short-circuits and the call returns silently. The user-friendly
        // reason must be passed through unchanged when the platform service is wired up,
        // so a regression that drops the reason would still be visible in production logs.
        const string reason = "Could not create zipped history backup in '/backups'. Check folder, write permission, and free space.";

        Assert.DoesNotThrow(() => HistoryViewModel.ShowHistoryBackupFailureToastIfPresent(reason));
    }
}
