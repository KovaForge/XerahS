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

namespace XerahS.Platform.Linux.Capture.OmaSnap;

/// <summary>
/// Candidate omasnap binaries, best first: a development override, the copy bundled with
/// XerahS, the AUR package location, then a standalone install on PATH. The service probes them
/// in order and uses the first that passes with host mode, so a standalone omasnap without host
/// mode (for example v1.21.0 in ~/.local/bin) is never used.
/// </summary>
internal static class OmaSnapLocator
{
    public const string OverrideEnvironmentVariable = "XERAHS_OMASNAP_PATH";
    public const string PackagedPath = "/usr/lib/xerahs/omasnap/omasnap";

    public static IReadOnlyList<string> GetCandidates(string? overridePath = null)
    {
        return GetCandidates(
            overridePath ?? Environment.GetEnvironmentVariable(OverrideEnvironmentVariable),
            AppContext.BaseDirectory,
            Environment.GetEnvironmentVariable("PATH"),
            File.Exists);
    }

    internal static IReadOnlyList<string> GetCandidates(string? overridePath, string baseDirectory, string? pathVariable, Func<string, bool> fileExists)
    {
        var candidates = new List<string>();

        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                return;
            }

            if (!candidates.Contains(full, StringComparer.Ordinal) && SafeExists(fileExists, full))
            {
                candidates.Add(full);
            }
        }

        Add(overridePath);
        Add(Path.Combine(baseDirectory, "omasnap", "omasnap"));
        Add(PackagedPath);
        if (!string.IsNullOrWhiteSpace(pathVariable))
        {
            foreach (string directory in pathVariable.Split(':', StringSplitOptions.RemoveEmptyEntries))
            {
                Add(Path.Combine(directory, "omasnap"));
            }
        }

        return candidates;
    }

    private static bool SafeExists(Func<string, bool> fileExists, string path)
    {
        try
        {
            return fileExists(path);
        }
        catch
        {
            return false;
        }
    }
}
