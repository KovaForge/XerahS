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
public class MultiRepoGitHubUpdateCheckerTests
{
    [Test]
    public void SelectBestRelease_PicksHighestVersion()
    {
        var shareX = CreateChecker("ShareX", new Version(0, 24, 0), UpdateStatus.UpToDate);
        var kovaForge = CreateChecker("KovaForge", new Version(0, 24, 18), UpdateStatus.UpdateAvailable);

        GitHubUpdateChecker? best = MultiRepoGitHubUpdateChecker.SelectBestRelease([shareX, kovaForge]);

        Assert.That(best, Is.SameAs(kovaForge));
    }

    [Test]
    public void SelectBestRelease_IgnoresFailedChecks()
    {
        var failed = CreateChecker("ShareX", new Version(0, 25, 0), UpdateStatus.UpdateCheckFailed);
        var available = CreateChecker("KovaForge", new Version(0, 24, 1), UpdateStatus.UpdateAvailable);

        GitHubUpdateChecker? best = MultiRepoGitHubUpdateChecker.SelectBestRelease([failed, available]);

        Assert.That(best, Is.SameAs(available));
    }

    [Test]
    public void SelectBestRelease_ReturnsNullWhenAllChecksFail()
    {
        var failed = CreateChecker("ShareX", new Version(0, 25, 0), UpdateStatus.UpdateCheckFailed);

        GitHubUpdateChecker? best = MultiRepoGitHubUpdateChecker.SelectBestRelease([failed]);

        Assert.That(best, Is.Null);
    }

    [Test]
    public async Task CheckUpdateAsync_AppliesNewestSuccessfulRelease()
    {
        var checker = new MultiRepoGitHubUpdateChecker(
        [
            ("ShareX", "XerahS"),
            ("KovaForge", "XerahS")
        ])
        {
            CheckerFactory = (owner, _) => owner == "KovaForge"
                ? CreateChecker(owner, new Version(0, 24, 18), UpdateStatus.UpdateAvailable)
                : CreateChecker(owner, new Version(0, 23, 0), UpdateStatus.UpToDate)
        };

        await checker.CheckUpdateAsync();

        Assert.That(checker.Owner, Is.EqualTo("KovaForge"));
        Assert.That(checker.LatestVersion, Is.EqualTo(new Version(0, 24, 18)));
        Assert.That(checker.Status, Is.EqualTo(UpdateStatus.UpdateAvailable));
        Assert.That(checker.DownloadURL, Is.EqualTo("https://example.invalid/KovaForge/XerahS.exe"));
    }

    [Test]
    public void GitHubUpdateManager_CreateUpdateChecker_UsesMultiRepoWhenMultipleSourcesConfigured()
    {
        var manager = new GitHubUpdateManager("ShareX", "XerahS")
        {
            GitHubRepositories =
            [
                ("ShareX", "XerahS"),
                ("KovaForge", "XerahS")
            ],
            IncludePreRelease = true
        };

        GitHubUpdateChecker checker = manager.CreateUpdateChecker();

        Assert.That(checker, Is.TypeOf<MultiRepoGitHubUpdateChecker>());
        Assert.That(((MultiRepoGitHubUpdateChecker)checker).Repositories, Is.EqualTo(new[]
        {
            ("ShareX", "XerahS"),
            ("KovaForge", "XerahS")
        }));
        Assert.That(checker.IncludePreRelease, Is.True);
    }

    private static GitHubUpdateChecker CreateChecker(string owner, Version version, UpdateStatus status)
    {
        return new StubGitHubUpdateChecker(owner, "XerahS")
        {
            LatestVersion = version,
            Status = status,
            DownloadURL = $"https://example.invalid/{owner}/XerahS.exe",
            FileName = $"{owner}-XerahS.exe"
        };
    }

    private sealed class StubGitHubUpdateChecker(string owner, string repo) : GitHubUpdateChecker(owner, repo)
    {
        public override Task CheckUpdateAsync()
        {
            return Task.CompletedTask;
        }

        public override Task<string?> GetLatestDownloadURL(bool isBrowserDownloadURL, CancellationToken cancellationToken)
        {
            return Task.FromResult(DownloadURL);
        }
    }
}
