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
/// Finds the OmaSnap binary. Order: settings override, <c>XERAHS_OMASNAP_PATH</c>, the copy bundled
/// next to XerahS (<c>omasnap/omasnap</c>), the AUR location (<c>/usr/lib/xerahs/omasnap/omasnap</c>),
/// then <c>omasnap</c> on PATH. Any candidate is used only after the host-mode probe accepts it.
/// </summary>
internal static class OmaSnapLocator
{
    public const string PathOverrideVariable = "XERAHS_OMASNAP_PATH";
    public const string PackagedPath = "/usr/lib/xerahs/omasnap/omasnap";

    /// <summary>Development override from settings; empty means none.</summary>
    public static string? SettingsOverridePath { get; set; }

    public static IReadOnlyList<string> GetCandidates(
        Func<string, string?> getEnvironment,
        string baseDirectory,
        string? settingsOverride)
    {
        var candidates = new List<string>();

        void Add(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !candidates.Contains(path, StringComparer.Ordinal))
            {
                candidates.Add(path);
            }
        }

        Add(settingsOverride);
        Add(getEnvironment(PathOverrideVariable));
        Add(Path.Combine(baseDirectory, "omasnap", "omasnap"));
        Add(PackagedPath);

        string? pathVariable = getEnvironment("PATH");
        if (!string.IsNullOrEmpty(pathVariable))
        {
            foreach (string directory in pathVariable.Split(':', StringSplitOptions.RemoveEmptyEntries))
            {
                Add(Path.Combine(directory, "omasnap"));
            }
        }

        return candidates;
    }

    public static string? Resolve(Func<string, string?> getEnvironment, string baseDirectory, string? settingsOverride, Func<string, bool> isExecutable)
    {
        return GetCandidates(getEnvironment, baseDirectory, settingsOverride).FirstOrDefault(isExecutable);
    }

    public static string? Resolve()
    {
        return Resolve(Environment.GetEnvironmentVariable, AppContext.BaseDirectory, SettingsOverridePath, IsExecutableFile);
    }

    internal static bool IsExecutableFile(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            UnixFileMode mode = File.GetUnixFileMode(path);
            return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Private per-user folder for OmaSnap output: <c>$XDG_RUNTIME_DIR/xerahs/omasnap/</c>, falling back
/// to <c>/tmp/xerahs-&lt;uid&gt;/omasnap/</c>. Created with mode 0700.
/// </summary>
internal static class OmaSnapRuntimeFolder
{
    public static string Resolve(Func<string, string?> getEnvironment, Func<string> getUserId)
    {
        string? runtimeDir = getEnvironment("XDG_RUNTIME_DIR");
        if (!string.IsNullOrWhiteSpace(runtimeDir) && Path.IsPathRooted(runtimeDir))
        {
            return Path.Combine(runtimeDir, "xerahs", "omasnap");
        }

        return Path.Combine(Path.GetTempPath(), $"xerahs-{getUserId()}", "omasnap");
    }

    public static string Ensure()
    {
        string folder = Resolve(Environment.GetEnvironmentVariable, GetUserId);
        Directory.CreateDirectory(folder);
        TrySetPrivate(folder);
        TrySetPrivate(Path.GetDirectoryName(folder)!);
        return folder;
    }

    private static void TrySetPrivate(string folder)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        catch
        {
            // Best effort; the parent of $XDG_RUNTIME_DIR is already private.
        }
    }

    private static string GetUserId()
    {
        try
        {
            return NativeUserId.Get().ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return Environment.UserName;
        }
    }

    private static class NativeUserId
    {
        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "getuid")]
        private static extern uint getuid();

        public static uint Get() => getuid();
    }
}
