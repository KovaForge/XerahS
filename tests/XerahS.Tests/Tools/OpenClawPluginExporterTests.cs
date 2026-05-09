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

            Assert.Multiple(() =>
            {
                Assert.That(manifestTools, Is.EqualTo(ExpectedToolNames));
                Assert.That(index, Does.Contain("definePluginEntry"));
                Assert.That(index, Does.Contain("api.registerTool(tool)"));
                Assert.That(index, Does.Contain("api.registerCli("));
                Assert.That(tools, Does.Contain("xerahs_upload_file"));
                Assert.That(tools, Does.Contain("xerahs_upload_text"));
                Assert.That(tools, Does.Contain("[\"upload\", \"--pipe\", \"--name\", name, \"--json\"]"));
                Assert.That(tools, Does.Contain("requireUploadUrl"));
                Assert.That(runner, Does.Contain("windowsHide: true"));
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
