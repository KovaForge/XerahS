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
using System.Text.Json;
using XerahS.CLI.Commands;

namespace XerahS.Tests.Tools;

[TestFixture]
public sealed class OpenClawCommandTests
{
    [Test]
    public void BuildManifest_TopLevelBootstrapCommandIncludesJsonFlag()
    {
        OpenClawManifest manifest = OpenClawCommand.BuildManifest();

        Assert.That(manifest.BootstrapCommand, Is.EqualTo("xerahscli bootstrap uploaders --json"),
            "Top-level bootstrapCommand must include the --json flag so the OpenClaw/Hermes "
            + "wrapper tool can parse Created/Repaired/Skipped/Diagnostics output. Without --json, "
            + "the bootstrap call returns human-readable text and the wrapper's requireUploaderReport "
            + "check throws before any routing is recorded.");
    }

    [Test]
    public void BuildManifest_CommandsArrayContainsBootstrapEntryWithJsonFlag()
    {
        OpenClawManifest manifest = OpenClawCommand.BuildManifest();

        OpenClawManifestCommand? bootstrap = Array.Find(
            manifest.Commands,
            c => c.Command.StartsWith("bootstrap uploaders", StringComparison.Ordinal));

        Assert.That(bootstrap, Is.Not.Null,
            "Manifest must list a 'bootstrap uploaders ...' command entry for the OpenClaw plugin's "
            + "xerahs_bootstrap_uploaders tool description.");
        Assert.That(bootstrap!.Command, Is.EqualTo("bootstrap uploaders --json"),
            "The bootstrap command entry's literal string is what the OpenClaw plugin's runner.ts "
            + "spawns. It must include --json so the spawned process emits the JSON shape the wrapper "
            + "expects.");
        Assert.That(bootstrap.JsonOutput, Is.True,
            "Bootstrap entry's JsonOutput flag must be true because it does produce structured JSON.");
    }

    [Test]
    public void BuildManifest_TopLevelBootstrapCommandMatchesCommandsArrayEntry()
    {
        OpenClawManifest manifest = OpenClawCommand.BuildManifest();

        // Defensive: the top-level field and the Commands[] entry must stay in sync.
        // The wrapper tool consults both, and a future refactor that updates only one
        // site would otherwise re-introduce the parity drift.
        string topLevelInvocation = manifest.BootstrapCommand;
        string? entry = Array.Find(manifest.Commands, c => c.Command.StartsWith("bootstrap uploaders", StringComparison.Ordinal))
            ?.Command;

        Assert.That(entry, Is.Not.Null);
        Assert.That(topLevelInvocation, Does.EndWith(" " + entry),
            "The top-level bootstrapCommand is the invocation prefix ('xerahscli ...'); the Commands[] "
            + "entry is the suffix without the invocation. They must align so a user following the "
            + "manifest can copy the suffix and prefix it with 'xerahscli' to get the top-level command.");
    }

    [Test]
    public void BuildManifest_AllJsonOutputCommandsIncludeJsonFlag()
    {
        // Defensive regression: if anyone adds a new command with JsonOutput=true but forgets the
        // --json flag, the OpenClaw wrapper's requireUploaderReport-style checks will fail at runtime.
        // The wrapper has multiple commands that consume JSON; this guards the entire manifest.
        OpenClawManifest manifest = OpenClawCommand.BuildManifest();

        List<string> offenders = new();
        foreach (OpenClawManifestCommand command in manifest.Commands)
        {
            if (command.JsonOutput && !command.Command.Contains("--json", StringComparison.Ordinal))
            {
                offenders.Add(command.Command);
            }
        }

        Assert.That(offenders, Is.Empty,
            "Every command with JsonOutput=true must include '--json' in its literal command string. "
            + "Offending commands: " + string.Join(", ", offenders));
    }

    [Test]
    public void BuildManifest_HealthAndBootstrapCommandsAreBothJsonFlagged()
    {
        // Both the health and bootstrap invocations are consumed by the OpenClaw plugin's doctor and
        // bootstrap tools. Both must include --json or the wrapper's parsing fails.
        OpenClawManifest manifest = OpenClawCommand.BuildManifest();

        Assert.Multiple(() =>
        {
            Assert.That(manifest.HealthCommand, Does.Contain("--json"),
                "healthCommand must include --json so the OpenClaw xerahs_doctor_uploaders tool parses the report.");
            Assert.That(manifest.BootstrapCommand, Does.Contain("--json"),
                "bootstrapCommand must include --json so the OpenClaw xerahs_bootstrap_uploaders tool parses the report.");
        });
    }

    [Test]
    public void BuildManifest_SerializedJsonIncludesJsonFlagOnBootstrapCommands()
    {
        // The manifest subcommand's actual on-the-wire output is JsonSerializer.Serialize(BuildManifest(),
        // OpenClawJsonOptions.Default). This test pins the serialized form so a future serializer-options
        // change (e.g. dropping CamelCase) cannot silently re-introduce a key mismatch with the wrapper.
        OpenClawManifest manifest = OpenClawCommand.BuildManifest();
        string json = JsonSerializer.Serialize(manifest, OpenClawJsonOptions.Default);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("bootstrapCommand").GetString(), Is.EqualTo("xerahscli bootstrap uploaders --json"));
            Assert.That(root.GetProperty("healthCommand").GetString(), Is.EqualTo("xerahscli doctor uploaders --json"));

            JsonElement commands = root.GetProperty("commands");
            JsonElement bootstrapEntry = default;
            bool foundBootstrap = false;
            foreach (JsonElement element in commands.EnumerateArray())
            {
                string? command = element.GetProperty("command").GetString();
                if (command is not null && command.StartsWith("bootstrap uploaders", StringComparison.Ordinal))
                {
                    bootstrapEntry = element.Clone();
                    foundBootstrap = true;
                    break;
                }
            }

            Assert.That(foundBootstrap, Is.True, "Serialized manifest must include a 'bootstrap uploaders ...' command entry.");
            Assert.That(bootstrapEntry.GetProperty("command").GetString(), Is.EqualTo("bootstrap uploaders --json"));
            Assert.That(bootstrapEntry.GetProperty("jsonOutput").GetBoolean(), Is.True);
        });
    }
}
