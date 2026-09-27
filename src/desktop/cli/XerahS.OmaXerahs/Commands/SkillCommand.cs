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

using System.CommandLine;
using System.Reflection;
using XerahS.OmaXerahs.Services;

namespace XerahS.OmaXerahs.Commands;

/// <summary>
/// Installs the bundled "xerahs" agent skill so coding agents (including the one Omarchy launches
/// with "omarchy agent") know how to drive XerahS. Mirrors how Omarchy links its own skills:
/// one real copy, symlinked into each agent's skills directory.
/// </summary>
internal static class SkillCommand
{
    internal const string SkillName = "xerahs";
    private const string ResourcePrefix = "AgentSkill/xerahs/";

    internal static Command Create()
    {
        var command = new Command("skill", "Install the XerahS agent skill for AI coding agents (Claude, Codex, Hermes, OpenCode, ...).");
        command.Add(CreateInstall());
        command.Add(CreateUninstall());
        command.Add(CreatePath());
        return command;
    }

    private static Command CreateInstall()
    {
        var command = new Command("install", "Write the skill to the XerahS data folder and link it into agent skill directories.");
        var jsonOption = JsonStdout.CreateJsonOption();
        command.Add(jsonOption);
        command.SetAction(parseResult =>
        {
            JsonStdout.Enabled = parseResult.GetValue(jsonOption);
            try
            {
                var response = Install(GetHomeDirectory(), GetSkillDirectory());
                JsonStdout.Write(response);
                if (!JsonStdout.Enabled)
                {
                    foreach (var link in response.Links)
                    {
                        Console.Error.WriteLine($"{link.Status,-8} {link.Path}{(link.Reason != null ? $" ({link.Reason})" : string.Empty)}");
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                var (code, message) = ErrorMapper.FromException(ex);
                return JsonStdout.WriteFailureAndExit(code, message);
            }
        });
        return command;
    }

    private static Command CreateUninstall()
    {
        var command = new Command("uninstall", "Remove the skill links this command created and the installed skill files.");
        var jsonOption = JsonStdout.CreateJsonOption();
        command.Add(jsonOption);
        command.SetAction(parseResult =>
        {
            JsonStdout.Enabled = parseResult.GetValue(jsonOption);
            try
            {
                JsonStdout.Write(Uninstall(GetHomeDirectory(), GetSkillDirectory()));
                return 0;
            }
            catch (Exception ex)
            {
                var (code, message) = ErrorMapper.FromException(ex);
                return JsonStdout.WriteFailureAndExit(code, message);
            }
        });
        return command;
    }

    private static Command CreatePath()
    {
        var command = new Command("path", "Print where the skill is installed.");
        var jsonOption = JsonStdout.CreateJsonOption();
        command.Add(jsonOption);
        command.SetAction(parseResult =>
        {
            JsonStdout.Enabled = parseResult.GetValue(jsonOption);
            string skillDirectory = GetSkillDirectory();
            JsonStdout.Write(new SkillPathResponse
            {
                SkillPath = skillDirectory,
                Installed = File.Exists(Path.Combine(skillDirectory, "SKILL.md"))
            });
            return 0;
        });
        return command;
    }

    internal static SkillInstallResponse Install(string home, string skillDirectory)
    {
        WriteSkillFiles(skillDirectory);

        var links = new List<SkillLinkResult>();
        foreach (string skillsDirectory in GetAgentSkillDirectories(home))
        {
            links.Add(Link(skillsDirectory, skillDirectory));
        }

        return new SkillInstallResponse { SkillPath = skillDirectory, Links = links.ToArray() };
    }

    internal static SkillInstallResponse Uninstall(string home, string skillDirectory)
    {
        var links = new List<SkillLinkResult>();
        foreach (string skillsDirectory in GetAgentSkillDirectories(home))
        {
            string linkPath = Path.Combine(skillsDirectory, SkillName);
            if (IsLinkTo(linkPath, skillDirectory))
            {
                File.Delete(linkPath);
                links.Add(new SkillLinkResult { Path = linkPath, Status = "removed" });
            }
        }

        if (Directory.Exists(skillDirectory))
        {
            Directory.Delete(skillDirectory, recursive: true);
        }

        return new SkillInstallResponse { SkillPath = skillDirectory, Links = links.ToArray() };
    }

    /// <summary>
    /// Skill directories to link into. ~/.agents/skills is the shared location Omarchy also uses;
    /// the others are added only for agents that are set up on this machine.
    /// </summary>
    internal static IEnumerable<string> GetAgentSkillDirectories(string home)
    {
        yield return Path.Combine(home, ".agents", "skills");

        string[] agentHomes =
        [
            Path.Combine(home, ".claude"),
            Path.Combine(home, ".codex"),
            Path.Combine(home, ".hermes"),
            Path.Combine(home, ".config", "opencode")
        ];

        foreach (string agentHome in agentHomes)
        {
            if (Directory.Exists(agentHome))
            {
                yield return Path.Combine(agentHome, "skills");
            }
        }

        string hermesProfiles = Path.Combine(home, ".hermes", "profiles");
        if (Directory.Exists(hermesProfiles))
        {
            foreach (string profile in Directory.EnumerateDirectories(hermesProfiles))
            {
                yield return Path.Combine(profile, "skills");
            }
        }
    }

    private static SkillLinkResult Link(string skillsDirectory, string skillDirectory)
    {
        string linkPath = Path.Combine(skillsDirectory, SkillName);

        if (IsLinkTo(linkPath, skillDirectory))
        {
            return new SkillLinkResult { Path = linkPath, Status = "exists" };
        }

        if (File.Exists(linkPath) || Directory.Exists(linkPath))
        {
            return new SkillLinkResult
            {
                Path = linkPath,
                Status = "skipped",
                Reason = "a different 'xerahs' skill is already there; left untouched"
            };
        }

        Directory.CreateDirectory(skillsDirectory);
        Directory.CreateSymbolicLink(linkPath, skillDirectory);
        return new SkillLinkResult { Path = linkPath, Status = "linked" };
    }

    private static bool IsLinkTo(string linkPath, string target)
    {
        var info = new FileInfo(linkPath);
        if (info.LinkTarget == null)
        {
            return false;
        }

        string resolved = Path.GetFullPath(info.LinkTarget, Path.GetDirectoryName(linkPath)!);
        return string.Equals(
            Path.TrimEndingDirectorySeparator(resolved),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(target)),
            StringComparison.Ordinal);
    }

    private static void WriteSkillFiles(string skillDirectory)
    {
        Assembly assembly = typeof(SkillCommand).Assembly;
        string[] resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .ToArray();

        if (resources.Length == 0)
        {
            throw new InvalidOperationException("The xerahs agent skill is missing from this omaxerahs build.");
        }

        // Replace wholesale so files removed from newer skill versions do not linger.
        if (Directory.Exists(skillDirectory))
        {
            Directory.Delete(skillDirectory, recursive: true);
        }

        foreach (string resource in resources)
        {
            string relativePath = resource[ResourcePrefix.Length..];
            string destination = Path.Combine(skillDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            using Stream source = assembly.GetManifestResourceStream(resource)!;
            using FileStream target = File.Create(destination);
            source.CopyTo(target);
        }
    }

    internal static string GetHomeDirectory()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    internal static string GetSkillDirectory()
    {
        string? dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(dataHome) || !Path.IsPathRooted(dataHome))
        {
            dataHome = Path.Combine(GetHomeDirectory(), ".local", "share");
        }

        return Path.Combine(dataHome, "xerahs", "agents", "skills", SkillName);
    }
}

internal sealed class SkillLinkResult
{
    public string Path { get; init; } = string.Empty;

    /// <summary>linked, exists, skipped, or removed.</summary>
    public string Status { get; init; } = string.Empty;
    public string? Reason { get; init; }
}

internal sealed class SkillInstallResponse
{
    public int SchemaVersion { get; init; } = 1;
    public bool Ok { get; init; } = true;
    public string SkillPath { get; init; } = string.Empty;
    public SkillLinkResult[] Links { get; init; } = [];
}

internal sealed class SkillPathResponse
{
    public int SchemaVersion { get; init; } = 1;
    public bool Ok { get; init; } = true;
    public string SkillPath { get; init; } = string.Empty;
    public bool Installed { get; init; }
}
