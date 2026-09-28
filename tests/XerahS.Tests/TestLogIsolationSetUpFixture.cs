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

/// <summary>
/// Assembly-wide fixture (global namespace, so it wraps every test). Redirects XerahS logs to a
/// throwaway folder so failures exercised by tests never land in the developer's real
/// XerahS-errors log (XIP0088 Phase 0 item 2).
/// </summary>
[SetUpFixture]
public sealed class TestLogIsolationSetUpFixture
{
    private string? _logsFolder;
    private string? _previousOverride;

    [OneTimeSetUp]
    public void RedirectLogs()
    {
        _previousOverride = Environment.GetEnvironmentVariable(PathsManager.LogsFolderOverrideEnvironmentVariable);
        _logsFolder = Path.Combine(Path.GetTempPath(), $"xerahs-test-logs-{Environment.ProcessId}");
        Directory.CreateDirectory(_logsFolder);
        Environment.SetEnvironmentVariable(PathsManager.LogsFolderOverrideEnvironmentVariable, _logsFolder);
    }

    [OneTimeTearDown]
    public void RestoreLogs()
    {
        Environment.SetEnvironmentVariable(PathsManager.LogsFolderOverrideEnvironmentVariable, _previousOverride);
        if (_logsFolder != null)
        {
            try
            {
                Directory.Delete(_logsFolder, recursive: true);
            }
            catch (IOException)
            {
                // A log writer may still hold the file; the temp folder is harmless.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
