using System.CommandLine;
using System.Text.Json;

namespace XerahS.CLI.Commands;

public static class OpenClawCommand
{
    public static Command Create()
    {
        var command = new Command("openclaw", "OpenClaw/Hermes agent integration helpers");
        var manifestCommand = new Command("manifest", "Print a machine-readable OpenClaw/Hermes capability manifest as JSON");

        manifestCommand.SetAction(_ =>
        {
            var manifest = new OpenClawManifest(
                Schema: "https://openclaw.ai/schemas/tool-manifest/v1",
                Name: "xerahs",
                DisplayName: "XerahS CLI",
                Description: "First-party XerahS automation CLI for OpenClaw and Hermes agents.",
                Invocation: "xerahs",
                VersionCommand: "xerahs --version",
                HealthCommand: "xerahs doctor uploaders --json",
                BootstrapCommand: "xerahs bootstrap uploaders",
                Principles:
                [
                    "Non-interactive by default",
                    "Stable exit codes",
                    "Machine-readable JSON for automation paths",
                    "No bundled uploader credentials or API keys",
                    "Capture/recording services are only initialized for recording commands"
                ],
                Commands:
                [
                    new OpenClawManifestCommand("openclaw manifest", "Describe CLI capabilities for agents", true, false),
                    new OpenClawManifestCommand("doctor uploaders --json", "Inspect uploader readiness", true, false),
                    new OpenClawManifestCommand("doctor uploaders --fix", "Repair safe local uploader defaults", false, true),
                    new OpenClawManifestCommand("bootstrap uploaders", "Idempotently initialize first-use uploader defaults", false, true),
                    new OpenClawManifestCommand("upload <file> --json", "Upload a file and return JSON containing url, filename, size, and type", true, true),
                    new OpenClawManifestCommand("upload <file> --as-file --json", "Force text-like artifacts such as HTML through the file uploader category", true, true),
                    new OpenClawManifestCommand("upload --text <text> --name <name> --json", "Upload generated text content", true, true),
                    new OpenClawManifestCommand("upload --pipe --name <name> --json", "Upload stdin content", true, true),
                    new OpenClawManifestCommand("index <folder> --format html --output <file> --json", "Generate a directory index file in html, txt, xml, or json format", true, false),
                    new OpenClawManifestCommand("reclip status --json", "Inspect ReClip integration settings", true, false),
                    new OpenClawManifestCommand("reclip use-default-watch-folder --json", "Configure ReClip to use the local OneDrive ReClip watch folder", true, false),
                    new OpenClawManifestCommand("config path", "Show XerahS settings paths", false, false),
                    new OpenClawManifestCommand("list workflows", "List configured workflows", false, false),
                    new OpenClawManifestCommand("capture", "Run capture workflows", false, true),
                    new OpenClawManifestCommand("record", "Run recording workflows", false, true)
                ]);

            Console.WriteLine(JsonSerializer.Serialize(manifest, OpenClawJsonOptions.Default));
            Environment.ExitCode = 0;
        });

        command.Add(manifestCommand);
        return command;
    }
}

internal static class OpenClawJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

internal sealed record OpenClawManifest(
    string Schema,
    string Name,
    string DisplayName,
    string Description,
    string Invocation,
    string VersionCommand,
    string HealthCommand,
    string BootstrapCommand,
    string[] Principles,
    OpenClawManifestCommand[] Commands);

internal sealed record OpenClawManifestCommand(
    string Command,
    string Description,
    bool JsonOutput,
    bool MayUseNetworkOrCapture);
