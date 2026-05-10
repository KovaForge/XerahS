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
public sealed class OpenClawPluginExporterTests
{
    private static readonly string[] ExpectedToolNames =
    [
        "xerahs_upload_file",
        "xerahs_upload_text",
        "xerahs_doctor_uploaders",
        "xerahs_bootstrap_uploaders"
    ];

    [Test]
    public async Task Export_CreatesUploadFocusedNativeOpenClawPlugin()
    {
        string outputDirectory = CreateTempDirectory();

        try
        {
            OpenClawPluginExportResult result = OpenClawPluginExporter.Export(outputDirectory, force: false);

            Assert.Multiple(() =>
            {
                Assert.That(result.OutputDirectory, Is.EqualTo(Path.GetFullPath(outputDirectory)));
                Assert.That(result.Files, Has.Count.EqualTo(8));
                Assert.That(File.Exists(Path.Combine(outputDirectory, "package.json")), Is.True);
                Assert.That(File.Exists(Path.Combine(outputDirectory, "openclaw.plugin.json")), Is.True);
                Assert.That(File.Exists(Path.Combine(outputDirectory, "cli-metadata.ts")), Is.True);
                Assert.That(File.Exists(Path.Combine(outputDirectory, "index.ts")), Is.True);
                Assert.That(File.Exists(Path.Combine(outputDirectory, "src", "tools.ts")), Is.True);
                Assert.That(File.Exists(Path.Combine(outputDirectory, "src", "runner.ts")), Is.True);
                Assert.That(File.Exists(Path.Combine(outputDirectory, "src", "config.ts")), Is.True);
                Assert.That(File.Exists(Path.Combine(outputDirectory, "src", "cli.ts")), Is.True);
            });

            using JsonDocument manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outputDirectory, "openclaw.plugin.json")));
            string[] manifestTools = manifest.RootElement
                .GetProperty("contracts")
                .GetProperty("tools")
                .EnumerateArray()
                .Select(element => element.GetString())
                .Where(value => value is not null)
                .Select(value => value!)
                .ToArray();
            string index = await File.ReadAllTextAsync(Path.Combine(outputDirectory, "index.ts"));
            string tools = await File.ReadAllTextAsync(Path.Combine(outputDirectory, "src", "tools.ts"));
            string runner = await File.ReadAllTextAsync(Path.Combine(outputDirectory, "src", "runner.ts"));
            string cli = await File.ReadAllTextAsync(Path.Combine(outputDirectory, "src", "cli.ts"));

            Assert.Multiple(() =>
            {
                Assert.That(manifestTools, Is.EqualTo(ExpectedToolNames));
                Assert.That(index, Does.Contain("definePluginEntry"));
                Assert.That(index, Does.Contain("api.registerTool(tool)"));
                Assert.That(index, Does.Contain("api.registerCli("));
                Assert.That(tools, Does.Contain("xerahs_upload_file"));
                Assert.That(tools, Does.Contain("xerahs_upload_text"));
                Assert.That(tools, Does.Contain("import { jsonResult } from \"openclaw/plugin-sdk/core\";"));
                Assert.That(tools, Does.Not.Contain("plugin-sdk/provider-web-search"));
                Assert.That(tools, Does.Contain("execute: async (_toolCallId: string, rawParams: Record<string, unknown>, signal?: AbortSignal)"));
                Assert.That(tools, Does.Contain("execute: async (_toolCallId: string, _rawParams: Record<string, unknown>, signal?: AbortSignal)"));
                Assert.That(tools, Does.Contain("runXerahS(config, args, { expectJson: true, signal })"));
                Assert.That(tools, Does.Contain("[\"upload\", \"--pipe\", \"--name\", name, \"--json\"]"));
                Assert.That(tools, Does.Contain("signal,"));
                Assert.That(tools, Does.Contain("runXerahS(config, [\"bootstrap\", \"uploaders\"], { signal })"));
                Assert.That(tools, Does.Contain("requireUploadUrl"));
                Assert.That(tools, Does.Contain("Array.isArray(value)"));
                Assert.That(tools, Does.Contain("parsedUrl = new URL(url);"));
                Assert.That(tools, Does.Contain("parsedUrl.protocol !== \"http:\" && parsedUrl.protocol !== \"https:\""));
                Assert.That(tools, Does.Contain("XerahS upload did not return an HTTP URL."));
                Assert.That(runner, Does.Contain("windowsHide: true"));
                Assert.That(runner, Does.Contain("signalCode: NodeJS.Signals | null;"));
                Assert.That(runner, Does.Contain("const rawStdout = Buffer.concat(stdout).toString(\"utf8\").trim();"));
                Assert.That(runner, Does.Contain("const rawStderr = Buffer.concat(stderr).toString(\"utf8\").trim();"));
                Assert.That(runner, Does.Contain("child.on(\"close\", (exitCode, signalCode) =>"));
                Assert.That(runner, Does.Contain("signalCode,"));
                Assert.That(runner, Does.Contain("stdout: redactDiagnostics(rawStdout)"));
                Assert.That(runner, Does.Contain("stderr: redactDiagnostics(rawStderr)"));
                Assert.That(runner, Does.Contain("result.exitCode === null && result.signalCode"));
                Assert.That(runner, Does.Contain("`signal ${result.signalCode}`"));
                Assert.That(runner, Does.Contain("result.json = JSON.parse(rawStdout);"));
                Assert.That(runner, Does.Contain("formatInvalidJsonFailure(error as Error, result)"));
                Assert.That(runner, Does.Contain("function formatInvalidJsonFailure(error: Error, result: XerahSRunResult): string"));
                Assert.That(runner, Does.Contain("XerahS did not return valid JSON: ${error.message}"));
                Assert.That(runner, Does.Contain("child.stdin.on(\"error\", (error: NodeJS.ErrnoException) =>"));
                Assert.That(runner, Does.Contain("if (error.code !== \"EPIPE\")"));
                Assert.That(runner, Does.Contain("child.on(\"error\", rejectOnce);"));
                Assert.That(runner, Does.Contain("child.stdout.on(\"error\", rejectOnce);"));
                Assert.That(runner, Does.Contain("child.stderr.on(\"error\", rejectOnce);"));
                Assert.That(runner, Does.Contain("let timedOut = false;"));
                Assert.That(runner, Does.Contain("signal?: AbortSignal;"));
                Assert.That(runner, Does.Contain("const abortSignal = options.signal;"));
                Assert.That(runner, Does.Contain("if (abortSignal?.aborted)"));
                Assert.That(runner, Does.Contain("abortSignal?.addEventListener(\"abort\", abortListener, { once: true });"));
                Assert.That(runner, Does.Contain("abortSignal?.removeEventListener(\"abort\", abortListener);"));
                Assert.That(runner, Does.Contain("XerahS command was cancelled."));
                Assert.That(runner, Does.Contain("const terminateChild = () =>"));
                Assert.That(runner, Does.Contain("forceKillTimer = setTimeout(() => child.kill(\"SIGKILL\"), forceKillDelayMs);"));
                Assert.That(runner, Does.Contain("terminateChild();"));
                Assert.That(runner, Does.Contain("if (timedOut)"));
                Assert.That(cli, Does.Contain("const abortController = new AbortController();"));
                Assert.That(cli, Does.Contain("let cancellationExitCode: number | undefined;"));
                Assert.That(cli, Does.Contain("cancellationExitCode = 130;"));
                Assert.That(cli, Does.Contain("cancellationExitCode = 143;"));
                Assert.That(cli, Does.Contain("process.once(\"SIGINT\", abortSigint);"));
                Assert.That(cli, Does.Contain("process.once(\"SIGTERM\", abortSigterm);"));
                Assert.That(cli, Does.Contain("runXerahS(config, args, { expectJson, signal: abortController.signal })"));
                Assert.That(cli, Does.Contain("process.exitCode = cancellationExitCode;"));
                Assert.That(cli, Does.Contain("process.off(\"SIGINT\", abortSigint);"));
                Assert.That(cli, Does.Contain("process.off(\"SIGTERM\", abortSigterm);"));
            });
        }
        finally
        {
            DeleteDirectory(outputDirectory);
        }
    }

    [Test]
    public void Export_WithExistingGeneratedFileWithoutForce_RefusesOverwrite()
    {
        string outputDirectory = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(outputDirectory, "package.json"), "{}");

            IOException exception = Assert.Throws<IOException>(() => OpenClawPluginExporter.Export(outputDirectory, force: false))!;

            Assert.That(exception.Message, Does.Contain("Refusing to overwrite existing file without --force"));
        }
        finally
        {
            DeleteDirectory(outputDirectory);
        }
    }

    [Test]
    public async Task Export_WithForce_OverwritesGeneratedFiles()
    {
        string outputDirectory = CreateTempDirectory();

        try
        {
            string packagePath = Path.Combine(outputDirectory, "package.json");
            File.WriteAllText(packagePath, "{}");

            OpenClawPluginExporter.Export(outputDirectory, force: true);

            string packageJson = await File.ReadAllTextAsync(packagePath);

            Assert.That(packageJson, Does.Contain("@xerahs/openclaw-plugin"));
        }
        finally
        {
            DeleteDirectory(outputDirectory);
        }
    }

    private static string CreateTempDirectory()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), $"xerahs-openclaw-plugin-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        return outputDirectory;
    }

    private static void DeleteDirectory(string outputDirectory)
    {
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }
}
