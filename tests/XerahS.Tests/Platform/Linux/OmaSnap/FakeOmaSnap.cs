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

namespace XerahS.Tests.Platform.Linux.OmaSnap;

/// <summary>Locates tests/fixtures/fake-omasnap/omasnap and sets its behaviour through environment variables.</summary>
internal static class FakeOmaSnap
{
    public static string BinaryPath { get; } = Locate();

    private static string Locate()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "tests", "fixtures", "fake-omasnap", "omasnap");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("tests/fixtures/fake-omasnap/omasnap was not found above the test directory.");
    }

    public static IDisposable Use(string mode = "ok", string caps = "ok", string? argvFile = null, string? envFile = null)
    {
        var previous = new Dictionary<string, string?>
        {
            ["FAKE_OMASNAP_MODE"] = Environment.GetEnvironmentVariable("FAKE_OMASNAP_MODE"),
            ["FAKE_OMASNAP_CAPS"] = Environment.GetEnvironmentVariable("FAKE_OMASNAP_CAPS"),
            ["FAKE_OMASNAP_ARGV"] = Environment.GetEnvironmentVariable("FAKE_OMASNAP_ARGV"),
            ["FAKE_OMASNAP_ENV"] = Environment.GetEnvironmentVariable("FAKE_OMASNAP_ENV")
        };

        Environment.SetEnvironmentVariable("FAKE_OMASNAP_MODE", mode);
        Environment.SetEnvironmentVariable("FAKE_OMASNAP_CAPS", caps);
        Environment.SetEnvironmentVariable("FAKE_OMASNAP_ARGV", argvFile);
        Environment.SetEnvironmentVariable("FAKE_OMASNAP_ENV", envFile);
        return new Restore(previous);
    }

    private sealed class Restore(Dictionary<string, string?> previous) : IDisposable
    {
        public void Dispose()
        {
            foreach (var (key, value) in previous)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
