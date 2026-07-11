using System.CommandLine;
using System.Text.Json;

namespace XerahS.CLI.Commands;

public static class OpenClawCommand
{
    public static Command Create()
    {
        var command = new Command("openclaw", "OpenClaw/Hermes agent integration helpers");
        var manifestCommand = new Command("manifest", "Print a machine-readable OpenClaw/Hermes capability manifest as JSON");
        var pluginCommand = new Command("plugin", "OpenClaw native plugin helpers");
        var pluginExportCommand = new Command("export", "Export the XerahS upload-to-URL native OpenClaw plugin source");
        var outputOption = new Option<string?>("--output")
        {
            Description = "Directory that will receive the OpenClaw plugin files."
        };
        var forceOption = new Option<bool>("--force")
        {
            Description = "Overwrite generated files if they already exist."
        };

        manifestCommand.SetAction(_ =>
        {
            Console.WriteLine(JsonSerializer.Serialize(BuildManifest(), OpenClawJsonOptions.Default));
            Environment.ExitCode = 0;
        });

        command.Add(manifestCommand);
        pluginExportCommand.Add(outputOption);
        pluginExportCommand.Add(forceOption);
        pluginExportCommand.SetAction(parseResult =>
        {
            string? outputDirectory = parseResult.GetValue(outputOption);
            bool force = parseResult.GetValue(forceOption);

            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                Console.Error.WriteLine("Missing required option: --output <directory>");
                Environment.ExitCode = 1;
                return;
            }

            try
            {
                OpenClawPluginExportResult result = OpenClawPluginExporter.Export(outputDirectory, force);
                Console.WriteLine(JsonSerializer.Serialize(result, OpenClawJsonOptions.Default));
                Environment.ExitCode = 0;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Console.Error.WriteLine(ex.Message);
                Environment.ExitCode = 1;
            }
        });

        pluginCommand.Add(pluginExportCommand);
        command.Add(pluginCommand);
        return command;
    }

    /// <summary>
    /// Builds the OpenClaw/Hermes capability manifest that the <c>openclaw manifest</c> subcommand prints.
    /// Extracted as a public static so tests can validate the command/flag parity without invoking
    /// System.CommandLine and capturing <see cref="Console.Out"/>. The JSON output the subcommand
    /// prints is exactly <c>JsonSerializer.Serialize(BuildManifest(), OpenClawJsonOptions.Default)</c>.
    /// </summary>
    public static OpenClawManifest BuildManifest()
    {
        return new OpenClawManifest(
            Schema: "https://openclaw.ai/schemas/tool-manifest/v1",
            Name: "xerahs",
            DisplayName: "XerahS CLI",
            Description: "First-party XerahS automation CLI for OpenClaw and Hermes agents.",
            Invocation: "xerahscli",
            VersionCommand: "xerahscli --version",
            HealthCommand: "xerahscli doctor uploaders --json",
            BootstrapCommand: "xerahscli bootstrap uploaders --json",
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
                new OpenClawManifestCommand("openclaw manifest", "Describe CLI capabilities for agents", false, false),
                new OpenClawManifestCommand("doctor uploaders --json", "Inspect uploader readiness", true, false),
                new OpenClawManifestCommand("doctor uploaders --fix", "Repair safe local uploader defaults", false, true),
                new OpenClawManifestCommand("bootstrap uploaders --json", "Idempotently initialize first-use uploader defaults and report Created/Repaired/Skipped/Diagnostics as JSON", true, true),
                new OpenClawManifestCommand("upload <file> --json", "Upload a file and return JSON containing url, filename, size, and type", true, true),
                new OpenClawManifestCommand("upload <file> --as-file --json", "Force text-like artifacts such as HTML through the file uploader category", true, true),
                new OpenClawManifestCommand("upload --text <text> --name <name> --json", "Upload generated text content", true, true),
                new OpenClawManifestCommand("upload --pipe --name <name> --json", "Upload stdin content", true, true),
                new OpenClawManifestCommand("index <folder> --format html --output <file> --json", "Generate a directory index file in html, txt, xml, json, or md format", true, false),
                new OpenClawManifestCommand("reclip status --json", "Inspect ReClip integration settings", true, false),
                new OpenClawManifestCommand("reclip use-default-watch-folder --json", "Configure ReClip to use the local OneDrive ReClip watch folder", true, false),
                new OpenClawManifestCommand("config path", "Show XerahS settings paths", false, false),
                new OpenClawManifestCommand("list workflows", "List configured workflows", false, false),
                new OpenClawManifestCommand("capture", "Run capture workflows", false, true),
                new OpenClawManifestCommand("record", "Run recording workflows", false, true)
            ]);
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

public sealed record OpenClawManifest(
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

public sealed record OpenClawManifestCommand(
    string Command,
    string Description,
    bool JsonOutput,
    bool MayUseNetworkOrCapture);
