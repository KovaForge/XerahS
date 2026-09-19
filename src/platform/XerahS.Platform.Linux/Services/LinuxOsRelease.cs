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

    public static void Refresh() => _cached = Load();

    private static LinuxOsReleaseInfo Load()
    {
        return Load(File.ReadAllLines);
    }

    internal static LinuxOsReleaseInfo Load(Func<string, string[]> readAllLines)
    {
        string? id = null;
        string? idLike = null;

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

                if (id != null && idLike != null)
                {
                    break;
                }
            }
        }
        catch
        {
            // /etc/os-release is best-effort: missing/unreadable means
            // IsOmarchy returns false, which is the safe fallback.
        }

        return new LinuxOsReleaseInfo(id, idLike);
    }
}

internal sealed record LinuxOsReleaseInfo(string? DistroId, string? DistroIdLike);
