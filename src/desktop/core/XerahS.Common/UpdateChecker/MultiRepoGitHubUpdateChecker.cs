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
    /// Checks multiple GitHub repositories and keeps the newest usable release.
    /// </summary>
    public class MultiRepoGitHubUpdateChecker : GitHubUpdateChecker
    {
        private readonly IReadOnlyList<(string Owner, string Repo)> _repositories;

        public MultiRepoGitHubUpdateChecker(IReadOnlyList<(string Owner, string Repo)> repositories)
            : base(GetPrimaryRepository(repositories).Owner, GetPrimaryRepository(repositories).Repo)
        {
            _repositories = NormalizeRepositories(repositories);
        }

        public IReadOnlyList<(string Owner, string Repo)> Repositories => _repositories;

        internal Func<string, string, GitHubUpdateChecker>? CheckerFactory { get; set; }

        public override async Task CheckUpdateAsync()
        {
            try
            {
                GitHubUpdateChecker[] results = await Task.WhenAll(_repositories.Select(CheckRepositoryAsync));
                GitHubUpdateChecker? best = SelectBestRelease(results);
                if (best == null)
                {
                    Status = UpdateStatus.UpdateCheckFailed;
                    return;
                }

                ApplyFrom(best);
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e, "Multi-repo GitHub update check failed.");
                Status = UpdateStatus.UpdateCheckFailed;
            }
        }

        public override async Task<string?> GetLatestDownloadURL(bool isBrowserDownloadURL, CancellationToken cancellationToken)
        {
            try
            {
                GitHubUpdateChecker[] results = await Task.WhenAll(
                    _repositories.Select(repository => CheckRepositoryDownloadAsync(repository, isBrowserDownloadURL, cancellationToken)));
                GitHubUpdateChecker? best = SelectBestRelease(results);
                if (best == null || string.IsNullOrEmpty(best.DownloadURL))
                {
                    return null;
                }

                ApplyFrom(best);
                return DownloadURL;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e, "Multi-repo GitHub download URL lookup failed.");
                return null;
            }
        }

        public static GitHubUpdateChecker? SelectBestRelease(IEnumerable<GitHubUpdateChecker> checkers)
        {
            GitHubUpdateChecker? best = null;

            foreach (GitHubUpdateChecker checker in checkers)
            {
                if (!IsUsableRelease(checker))
                {
                    continue;
                }

                if (best == null || IsNewerThan(checker, best))
                {
                    best = checker;
                }
            }

            return best;
        }

        internal void ApplyFrom(GitHubUpdateChecker source)
        {
            Owner = source.Owner;
            Repo = source.Repo;
            Status = source.Status;
            LatestVersion = source.LatestVersion;
            CurrentVersion = source.CurrentVersion;
            DownloadURL = source.DownloadURL;
            FileName = source.FileName;
            IsPreRelease = source.IsPreRelease;
            IsPortable = source.IsPortable;
        }

        private async Task<GitHubUpdateChecker> CheckRepositoryAsync((string Owner, string Repo) repository)
        {
            GitHubUpdateChecker checker = CreateChecker(repository.Owner, repository.Repo);
            await checker.CheckUpdateAsync();
            return checker;
        }

        private async Task<GitHubUpdateChecker> CheckRepositoryDownloadAsync(
            (string Owner, string Repo) repository,
            bool isBrowserDownloadURL,
            CancellationToken cancellationToken)
        {
            GitHubUpdateChecker checker = CreateChecker(repository.Owner, repository.Repo);
            string? downloadUrl = await checker.GetLatestDownloadURL(isBrowserDownloadURL, cancellationToken);
            if (string.IsNullOrEmpty(downloadUrl))
            {
                checker.Status = UpdateStatus.UpdateCheckFailed;
            }
            else if (checker.Status != UpdateStatus.UpdateAvailable && checker.Status != UpdateStatus.UpToDate)
            {
                checker.Status = checker.LatestVersion == null
                    ? UpdateStatus.UpdateCheckFailed
                    : UpdateStatus.UpToDate;
            }

            return checker;
        }

        private GitHubUpdateChecker CreateChecker(string owner, string repo)
        {
            if (CheckerFactory != null)
            {
                return CheckerFactory(owner, repo);
            }

            return new GitHubUpdateChecker(owner, repo)
            {
                IsPortable = IsPortable,
                IncludePreRelease = IncludePreRelease,
                CurrentVersion = CurrentVersion
            };
        }

        private static bool IsUsableRelease(GitHubUpdateChecker checker)
        {
            return checker.LatestVersion != null &&
                   (checker.Status == UpdateStatus.UpdateAvailable || checker.Status == UpdateStatus.UpToDate);
        }

        private static bool IsNewerThan(GitHubUpdateChecker candidate, GitHubUpdateChecker current)
        {
            return candidate.LatestVersion > current.LatestVersion;
        }

        private static (string Owner, string Repo) GetPrimaryRepository(IReadOnlyList<(string Owner, string Repo)> repositories)
        {
            IReadOnlyList<(string Owner, string Repo)> normalized = NormalizeRepositories(repositories);
            return normalized[0];
        }

        private static IReadOnlyList<(string Owner, string Repo)> NormalizeRepositories(
            IReadOnlyList<(string Owner, string Repo)> repositories)
        {
            if (repositories == null || repositories.Count == 0)
            {
                throw new ArgumentException("At least one repository is required.", nameof(repositories));
            }

            List<(string Owner, string Repo)> normalized = new(repositories.Count);
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

            foreach ((string owner, string repo) in repositories)
            {
                if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
                {
                    continue;
                }

                string key = owner + "/" + repo;
                if (seen.Add(key))
                {
                    normalized.Add((owner, repo));
                }
            }

            if (normalized.Count == 0)
            {
                throw new ArgumentException("At least one repository with an owner and name is required.", nameof(repositories));
            }

            return normalized;
        }
    }
}
