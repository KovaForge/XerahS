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
using XerahS.Indexer;

namespace XerahS.Tests.Tools;

[TestFixture]
public class IndexCommandTests
{
    [TestCase(null, IndexerOutput.Html)]
    [TestCase("", IndexerOutput.Html)]
    [TestCase("html", IndexerOutput.Html)]
    [TestCase("htm", IndexerOutput.Html)]
    [TestCase("txt", IndexerOutput.Txt)]
    [TestCase("text", IndexerOutput.Txt)]
    [TestCase("xml", IndexerOutput.Xml)]
    [TestCase("json", IndexerOutput.Json)]
    [TestCase("md", IndexerOutput.Markdown)]
    [TestCase("markdown", IndexerOutput.Markdown)]
    public void TryParseFormat_WithSupportedFormat_ReturnsIndexerOutput(string? format, IndexerOutput expectedOutput)
    {
        bool result = IndexCommand.TryParseFormat(format, out IndexerOutput output);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(output, Is.EqualTo(expectedOutput));
        });
    }

    [Test]
    public void ResolveOutputPath_WithMarkdownFormat_UsesMdExtension()
    {
        string folderPath = Path.Combine(Path.GetTempPath(), "xerahs-index-source");
        string originalCurrentDirectory = Environment.CurrentDirectory;
        string workDirectory = TestContext.CurrentContext.WorkDirectory;

        try
        {
            Environment.CurrentDirectory = workDirectory;

            string outputPath = IndexCommand.ResolveOutputPath(folderPath, null, IndexerOutput.Markdown);

            Assert.That(outputPath, Is.EqualTo(Path.Combine(workDirectory, "xerahs-index-source.md")));
        }
        finally
        {
            Environment.CurrentDirectory = originalCurrentDirectory;
        }
    }

    [Test]
    public void TryParseFormat_WithUnsupportedFormat_ReturnsFalse()
    {
        bool result = IndexCommand.TryParseFormat("pdf", out _);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IndexerSettings_ShouldRecurseIntoLevel_HandlesEdgeCases()
    {
        Assert.Multiple(() =>
        {
            // 0 = unlimited, always recurse
            Assert.That(new IndexerSettings { MaxDepthLevel = 0 }.ShouldRecurseIntoLevel(0), Is.True);
            Assert.That(new IndexerSettings { MaxDepthLevel = 0 }.ShouldRecurseIntoLevel(5), Is.True);
            Assert.That(new IndexerSettings { MaxDepthLevel = 0 }.ShouldRecurseIntoLevel(1000), Is.True);

            // Negative = unlimited (defensive — same as 0)
            Assert.That(new IndexerSettings { MaxDepthLevel = -1 }.ShouldRecurseIntoLevel(0), Is.True);
            Assert.That(new IndexerSettings { MaxDepthLevel = -5 }.ShouldRecurseIntoLevel(5), Is.True);

            // Positive = bounded
            Assert.That(new IndexerSettings { MaxDepthLevel = 1 }.ShouldRecurseIntoLevel(0), Is.True);
            Assert.That(new IndexerSettings { MaxDepthLevel = 1 }.ShouldRecurseIntoLevel(1), Is.False);
            Assert.That(new IndexerSettings { MaxDepthLevel = 3 }.ShouldRecurseIntoLevel(2), Is.True);
            Assert.That(new IndexerSettings { MaxDepthLevel = 3 }.ShouldRecurseIntoLevel(3), Is.False);
        });
    }

    [Test]
    public void IndexerSettings_ExtensionMatchesFilter_HandlesEdgeCases()
    {
        Assert.Multiple(() =>
        {
            // Null/empty filter returns false (no filter to match)
            Assert.That(IndexerSettings.ExtensionMatchesFilter(".cs", null), Is.False);
            Assert.That(IndexerSettings.ExtensionMatchesFilter(".cs", []), Is.False);

            // With-dot vs without-dot normalization
            Assert.That(IndexerSettings.ExtensionMatchesFilter(".cs", [".cs"]), Is.True);
            Assert.That(IndexerSettings.ExtensionMatchesFilter(".cs", ["cs"]), Is.True);
            Assert.That(IndexerSettings.ExtensionMatchesFilter("cs", [".cs"]), Is.True);

            // Case-insensitive matching
            Assert.That(IndexerSettings.ExtensionMatchesFilter(".CS", ["cs"]), Is.True);
            Assert.That(IndexerSettings.ExtensionMatchesFilter(".cs", ["CS"]), Is.True);

            // Whitespace in extension
            Assert.That(IndexerSettings.ExtensionMatchesFilter(" .cs ", ["cs"]), Is.True);

            // Whitespace/null filter entries are ignored
            Assert.That(IndexerSettings.ExtensionMatchesFilter(".cs", ["", " ", "cs"]), Is.True);
            Assert.That(IndexerSettings.ExtensionMatchesFilter(".cs", ["", " "]), Is.False);

            // Non-matching extensions
            Assert.That(IndexerSettings.ExtensionMatchesFilter(".txt", [".cs", ".md"]), Is.False);
        });
    }

    [Test]
    public async Task ExecuteAsync_WithMarkdownFormat_WritesExpectedIndexFile()
    {
        string rootDirectory = Path.Combine(Path.GetTempPath(), $"xerahs-index-cli-md-{Guid.NewGuid():N}");
        string outputPath = Path.Combine(Path.GetTempPath(), $"xerahs-index-cli-md-{Guid.NewGuid():N}.md");

        try
        {
            Directory.CreateDirectory(Path.Combine(rootDirectory, "captures"));
            await File.WriteAllTextAsync(Path.Combine(rootDirectory, "capture.txt"), "hello");
            await File.WriteAllTextAsync(Path.Combine(rootDirectory, "captures", "nested.txt"), "nested");

            int exitCode = await IndexCommand.ExecuteAsync(
                rootDirectory,
                "md",
                outputPath,
                maxDepth: 0,
                includeExtensions: null,
                excludeExtensions: null,
                includeHidden: false,
                foldersOnly: false,
                noSize: false,
                noFooter: true,
                jsonOutput: false,
                CancellationToken.None);

            string markdown = await File.ReadAllTextAsync(outputPath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(markdown, Does.StartWith("# Directory Index: "));
                Assert.That(markdown, Does.Contain("- **captures/**"));
                Assert.That(markdown, Does.Contain("- capture\\.txt"));
                Assert.That(markdown, Does.Contain("- nested\\.txt"));
            });
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
            if (Directory.Exists(rootDirectory)) Directory.Delete(rootDirectory, recursive: true);
        }
    }

    [Test]
    public void ResolveOutputPath_WithoutOutput_UsesCurrentDirectoryFolderNameAndFormatExtension()
    {
        string folderPath = Path.Combine(Path.GetTempPath(), "xerahs-index-source");
        string originalCurrentDirectory = Environment.CurrentDirectory;
        string workDirectory = TestContext.CurrentContext.WorkDirectory;

        try
        {
            Environment.CurrentDirectory = workDirectory;

            string outputPath = IndexCommand.ResolveOutputPath(folderPath, null, IndexerOutput.Html);

            Assert.That(outputPath, Is.EqualTo(Path.Combine(workDirectory, "xerahs-index-source.html")));
        }
        finally
        {
            Environment.CurrentDirectory = originalCurrentDirectory;
        }
    }

    [Test]
    public async Task ExecuteAsync_WithNegativeMaxDepth_ReturnsFailure()
    {
        string rootDirectory = Path.Combine(Path.GetTempPath(), $"xerahs-index-cli-depth-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(rootDirectory);

            int exitCode = await IndexCommand.ExecuteAsync(
                rootDirectory,
                "html",
                null,
                maxDepth: -1,
                includeExtensions: null,
                excludeExtensions: null,
                includeHidden: false,
                foldersOnly: false,
                noSize: false,
                noFooter: false,
                jsonOutput: false,
                CancellationToken.None);

            Assert.That(exitCode, Is.EqualTo(1));
        }
        finally
        {
            if (Directory.Exists(rootDirectory)) Directory.Delete(rootDirectory, recursive: true);
        }
    }

    [Test]
    public async Task ExecuteAsync_WithHtmlFormat_WritesExpectedIndexFile()
    {
        string rootDirectory = Path.Combine(Path.GetTempPath(), $"xerahs-index-cli-{Guid.NewGuid():N}");
        string outputPath = Path.Combine(Path.GetTempPath(), $"xerahs-index-cli-{Guid.NewGuid():N}.html");

        try
        {
            Directory.CreateDirectory(Path.Combine(rootDirectory, "captures"));
            await File.WriteAllTextAsync(Path.Combine(rootDirectory, "capture.txt"), "hello");
            await File.WriteAllTextAsync(Path.Combine(rootDirectory, "captures", "nested.txt"), "nested");

            int exitCode = await IndexCommand.ExecuteAsync(
                rootDirectory,
                "html",
                outputPath,
                maxDepth: 0,
                includeExtensions: null,
                excludeExtensions: null,
                includeHidden: false,
                foldersOnly: false,
                noSize: false,
                noFooter: false,
                jsonOutput: false,
                CancellationToken.None);

            string html = await File.ReadAllTextAsync(outputPath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(html, Does.Contain("<!DOCTYPE html>"));
                Assert.That(html, Does.Contain("capture.txt"));
                Assert.That(html, Does.Contain("nested.txt"));
            });
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
            if (Directory.Exists(rootDirectory)) Directory.Delete(rootDirectory, recursive: true);
        }
    }

    [Test]
    public async Task ExecuteAsync_WithJsonOutput_WritesMachineReadableMetadata()
    {
        string rootDirectory = Path.Combine(Path.GetTempPath(), $"xerahs-index-cli-json-{Guid.NewGuid():N}");
        string outputPath = Path.Combine(Path.GetTempPath(), $"xerahs-index-cli-json-{Guid.NewGuid():N}.html");
        TextWriter originalOut = Console.Out;
        using var output = new StringWriter();

        try
        {
            Directory.CreateDirectory(rootDirectory);
            await File.WriteAllTextAsync(Path.Combine(rootDirectory, "capture.txt"), "hello");
            Console.SetOut(output);

            int exitCode = await IndexCommand.ExecuteAsync(
                rootDirectory,
                "html",
                outputPath,
                maxDepth: 0,
                includeExtensions: null,
                excludeExtensions: null,
                includeHidden: false,
                foldersOnly: false,
                noSize: false,
                noFooter: false,
                jsonOutput: true,
                CancellationToken.None);

            using JsonDocument document = JsonDocument.Parse(output.ToString());

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(document.RootElement.GetProperty("outputFilePath").GetString(), Is.EqualTo(outputPath));
                Assert.That(document.RootElement.GetProperty("totalFiles").GetInt64(), Is.EqualTo(1));
                Assert.That(document.RootElement.GetProperty("format").GetString(), Is.EqualTo("html"));
            });
        }
        finally
        {
            Console.SetOut(originalOut);
            if (File.Exists(outputPath)) File.Delete(outputPath);
            if (Directory.Exists(rootDirectory)) Directory.Delete(rootDirectory, recursive: true);
        }
    }
}
