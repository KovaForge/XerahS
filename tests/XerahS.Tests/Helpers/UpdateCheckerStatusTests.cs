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
using XerahS.Common;

namespace XerahS.Tests.Helpers;

[TestFixture]
public class UpdateCheckerStatusTests
{
    [Test]
    public void RefreshStatus_PreservesUpdateCheckFailedState()
    {
        var checker = new TestUpdateChecker
        {
            Status = UpdateStatus.UpdateCheckFailed,
            CurrentVersion = new Version(1, 0, 0),
            LatestVersion = new Version(2, 0, 0),
            DownloadURL = "https://example.invalid/XerahS-2.0.0-win-x64.exe"
        };

        checker.RefreshStatus();

        Assert.That(checker.Status, Is.EqualTo(UpdateStatus.UpdateCheckFailed));
    }

    [Test]
    public void RefreshStatus_WithoutDownloadUrl_IsUpToDate()
    {
        var checker = new TestUpdateChecker
        {
            Status = UpdateStatus.None,
            CurrentVersion = new Version(1, 0, 0),
            LatestVersion = new Version(2, 0, 0)
        };

        checker.RefreshStatus();

        Assert.That(checker.Status, Is.EqualTo(UpdateStatus.UpToDate));
    }

    [Test]
    public async Task AppVeyorUpdateChecker_LeavesStatusUpToDate_WhenLatestVersionMatchesCurrentVersion()
    {
        var checker = new TestAppVeyorUpdateChecker
        {
            CurrentVersion = new Version(1, 2, 3),
            IsPortable = false
        };

        await checker.CheckUpdateAsync();

        Assert.That(checker.Status, Is.EqualTo(UpdateStatus.UpToDate));
        Assert.That(checker.DownloadURL, Is.EqualTo("https://ci.example.invalid/download/XerahS-setup.exe"));
    }

    private sealed class TestUpdateChecker : UpdateChecker
    {
        public override Task CheckUpdateAsync()
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestAppVeyorUpdateChecker : AppVeyorUpdateChecker
    {
        public override async Task CheckUpdateAsync()
        {
            await Task.Yield();

            FileName = "XerahS-setup.exe";
            DownloadURL = "https://ci.example.invalid/download/XerahS-setup.exe";
            LatestVersion = new Version(1, 2, 3);

            RefreshStatus();
        }
    }
}
