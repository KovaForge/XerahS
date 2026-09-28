// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
// Lightweight reader for /etc/os-release. Exposes the few fields XerahS needs
// to make platform-default decisions (Omarchy/Arch/Hyprland detection) without
// taking a hard dependency on a distro-info library.
namespace XerahS.Platform.Linux.Services;

internal static class LinuxOsRelease
{
    private static LinuxOsReleaseInfo _cached = Load();

    public static string? DistroId => _cached.DistroId;

    public static string? DistroIdLike => _cached.DistroIdLike;

    /// <summary>Human-readable name (PRETTY_NAME), e.g. "Arch Linux".</summary>
    public static string? PrettyName => _cached.PrettyName;

    public static void Refresh() => _cached = Load();

    private static LinuxOsReleaseInfo Load()
    {
        return Load(File.ReadAllLines);
    }

    internal static LinuxOsReleaseInfo Load(Func<string, string[]> readAllLines)
    {
        string? id = null;
        string? idLike = null;
        string? prettyName = null;

        try
        {
            string[] lines = readAllLines("/etc/os-release");
            foreach (string raw in lines)
            {
                int eq = raw.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                string key = raw[..eq].Trim();
                string value = raw[(eq + 1)..].Trim().Trim('"', '\'');

                if (string.Equals(key, "ID", StringComparison.Ordinal))
                {
                    id = value;
                }
                else if (string.Equals(key, "ID_LIKE", StringComparison.Ordinal))
                {
                    idLike = value;
                }
                else if (string.Equals(key, "PRETTY_NAME", StringComparison.Ordinal))
                {
                    prettyName = value;
                }
            }
        }
        catch
        {
            // /etc/os-release is best-effort: missing/unreadable means
            // IsOmarchy returns false, which is the safe fallback.
        }

        return new LinuxOsReleaseInfo(id, idLike, prettyName);
    }
}

internal sealed record LinuxOsReleaseInfo(string? DistroId, string? DistroIdLike, string? PrettyName = null);
