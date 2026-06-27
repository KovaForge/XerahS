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
using System.Text.Json;
using XerahS.Indexer;

namespace XerahS.CLI.Commands;

public static class IndexCommand
{
    public static Command Create()
    {
        var command = new Command("index", "Generate a directory index from a folder");

        var folderArgument = new Argument<string>("folder")
        {
            Description = "Folder path to index"
        };
        var formatOption = new Option<string?>("--format")
        {
            Description = "Output format: html, txt, xml, json, or md. Defaults to html."
        };
        var outputOption = new Option<string?>("--output")
        {
            Description = "Output file path. Defaults to <folder-name>.<format> in the current directory."
        };
        var maxDepthOption = new Option<int>("--max-depth")
        {
            Description = "Maximum folder depth to index. 0 means unlimited."
        };
        var includeOption = new Option<string?>("--include")
        {
            Description = "Comma-separated file extensions to include, for example .cs,.txt."
        };
        var excludeOption = new Option<string?>("--exclude")
        {
            Description = "Comma-separated file extensions to exclude, for example .bin,.obj."
        };
        var includeHiddenOption = new Option<bool>("--include-hidden")
        {
            Description = "Include hidden files and folders."
        };
        var foldersOnlyOption = new Option<bool>("--folders-only")
        {
            Description = "Index folders only and skip files."
        };
        var noSizeOption = new Option<bool>("--no-size")
        {
            Description = "Do not include folder and file size information."
        };
        var noFooterOption = new Option<bool>("--no-footer")
        {
            Description = "Do not include the generated-by footer."
        };
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Write result metadata as JSON."
        };

        command.Add(folderArgument);
        command.Add(formatOption);
        command.Add(outputOption);
        command.Add(maxDepthOption);
        command.Add(includeOption);
        command.Add(excludeOption);
        command.Add(includeHiddenOption);
        command.Add(foldersOnlyOption);
        command.Add(noSizeOption);
        command.Add(noFooterOption);
        command.Add(jsonOption);

        command.SetAction(parseResult =>
        {
            Environment.ExitCode = ExecuteAsync(
                parseResult.GetValue(folderArgument),
                parseResult.GetValue(formatOption),
                parseResult.GetValue(outputOption),
                parseResult.GetValue(maxDepthOption),
                parseResult.GetValue(includeOption),
                parseResult.GetValue(excludeOption),
                parseResult.GetValue(includeHiddenOption),
                parseResult.GetValue(foldersOnlyOption),
                parseResult.GetValue(noSizeOption),
                parseResult.GetValue(noFooterOption),
                parseResult.GetValue(jsonOption),
                CancellationToken.None).GetAwaiter().GetResult();
        });

        return command;
    }

    internal static async Task<int> ExecuteAsync(
        string? folderPath,
        string? format,
        string? outputFilePath,
        int maxDepth,
        string? includeExtensions,
        string? excludeExtensions,
        bool includeHidden,
        bool foldersOnly,
        bool noSize,
        bool noFooter,
        bool jsonOutput,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            Console.Error.WriteLine("Specify a folder path to index.");
            return 1;
        }

        string fullFolderPath = Path.GetFullPath(folderPath);
        if (!Directory.Exists(fullFolderPath))
        {
            Console.Error.WriteLine($"Folder not found: {fullFolderPath}");
            return 1;
        }

        if (!TryParseFormat(format, out IndexerOutput indexerOutput))
        {
            Console.Error.WriteLine("Unsupported format. Use html, txt, xml, json, or md.");
            return 1;
        }

        if (maxDepth < 0)
        {
            Console.Error.WriteLine("--max-depth must be zero or greater.");
            return 1;
        }

        string resolvedOutputPath = ResolveOutputPath(fullFolderPath, outputFilePath, indexerOutput);
        string? outputDirectory = Path.GetDirectoryName(resolvedOutputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var settings = new IndexerSettings
        {
            Output = indexerOutput,
            SkipHiddenFolders = !includeHidden,
            SkipHiddenFiles = !includeHidden,
            SkipFiles = foldersOnly,
            MaxDepthLevel = maxDepth,
            ShowSizeInfo = !noSize,
            AddFooter = !noFooter,
            IncludedFileExtensions = ParseExtensionList(includeExtensions),
            ExcludedFileExtensions = ParseExtensionList(excludeExtensions)
        };

        IndexResult result = await IndexerAsync.IndexToFileAsync(
            fullFolderPath,
            resolvedOutputPath,
            settings,
            cancellationToken: cancellationToken);

        if (!result.Success)
        {
            Console.Error.WriteLine($"Failed to create directory index: {result.ErrorMessage}");
            return 1;
        }

        if (result.TotalFolders == 0 && result.TotalFiles == 0 && Directory.Exists(fullFolderPath))
        {
            var totals = CountIndexedContents(fullFolderPath, settings);
            result.TotalFiles = totals.TotalFiles;
            result.TotalFolders = totals.TotalFolders;
            result.TotalBytes = totals.TotalBytes;
        }

        if (jsonOutput)
        {
            Console.WriteLine(JsonSerializer.Serialize(new IndexCommandResult(
                result.OutputFilePath,
                result.TotalFiles,
                result.TotalFolders,
                result.TotalBytes,
                result.Duration.TotalMilliseconds,
                indexerOutput.ToString().ToLowerInvariant()), OpenClawJsonOptions.Default));
        }
        else
        {
            Console.WriteLine($"Index written to: {result.OutputFilePath}");
        }

        return 0;
    }

    internal static string ResolveOutputPath(string folderPath, string? outputFilePath, IndexerOutput output)
    {
        string extension = GetExtension(output);
        string folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath)));
        if (string.IsNullOrWhiteSpace(folderName))
        {
            folderName = "index";
        }

        string defaultFileName = folderName + extension;
        if (string.IsNullOrWhiteSpace(outputFilePath))
        {
            return Path.Combine(Environment.CurrentDirectory, defaultFileName);
        }

        string resolvedOutputPath = Path.GetFullPath(outputFilePath);
        if (Directory.Exists(resolvedOutputPath) || outputFilePath.EndsWith(Path.DirectorySeparatorChar) || outputFilePath.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return Path.Combine(resolvedOutputPath, defaultFileName);
        }

        return resolvedOutputPath;
    }

    internal static (long TotalFiles, long TotalFolders, long TotalBytes) CountIndexedContents(string folderPath, IndexerSettings settings)
    {
        return CountIndexedContents(folderPath, settings, 0);
    }

    internal static (long TotalFiles, long TotalFolders, long TotalBytes) CountIndexedContents(string folderPath, IndexerSettings settings, int level)
    {
        long totalFiles = 0;
        long totalFolders = 1;
        long totalBytes = 0;

        if (settings.MaxDepthLevel > 0 && level >= settings.MaxDepthLevel)
        {
            return (totalFiles, totalFolders, totalBytes);
        }

        try
        {
            var directoryInfo = new DirectoryInfo(folderPath);
            foreach (DirectoryInfo subdirectory in directoryInfo.EnumerateDirectories())
            {
                if (settings.SkipHiddenFolders && subdirectory.Attributes.HasFlag(FileAttributes.Hidden))
                {
                    continue;
                }

                var childTotals = CountIndexedContents(subdirectory.FullName, settings, level + 1);
                if (settings.IgnoreEmptyFolders && childTotals.TotalFiles == 0 && childTotals.TotalFolders <= 1)
                {
                    continue;
                }

                totalFiles += childTotals.TotalFiles;
                totalFolders += childTotals.TotalFolders;
                totalBytes += childTotals.TotalBytes;
            }

            if (!settings.SkipFiles)
            {
                foreach (FileInfo file in directoryInfo.EnumerateFiles())
                {
                    if (settings.SkipHiddenFiles && file.Attributes.HasFlag(FileAttributes.Hidden))
                    {
                        continue;
                    }

                    if (ShouldSkipByExtension(file.Extension, settings.IncludedFileExtensions, settings.ExcludedFileExtensions))
                    {
                        continue;
                    }

                    totalFiles++;
                    totalBytes += file.Length;
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we cannot access — best-effort count
        }
        catch (DirectoryNotFoundException)
        {
            // Directory removed between initial check and enumeration
        }
        catch (PathTooLongException)
        {
            // Skip paths exceeding system limits
        }
        catch (ArgumentException)
        {
            // Invalid path characters — best-effort count
        }
        catch (NotSupportedException)
        {
            // Path format not supported (e.g. colon outside volume identifier)
        }
        catch (IOException)
        {
            // I/O error (disk error, network share unavailable, etc.) — best-effort count
        }

        return (totalFiles, totalFolders, totalBytes);
    }

    private static bool ShouldSkipByExtension(string extension, List<string>? includeExtensions, List<string>? excludeExtensions)
    {
        string normalized = NormalizeExtension(extension);

        if (includeExtensions is { Count: > 0 } && !includeExtensions.Any(value => NormalizeExtension(value).Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return excludeExtensions is { Count: > 0 } && excludeExtensions.Any(value => NormalizeExtension(value).Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeExtension(string extension)
    {
        return extension.Trim().TrimStart('.');
    }

    internal static bool TryParseFormat(string? format, out IndexerOutput output)
    {
        output = IndexerOutput.Html;

        if (string.IsNullOrWhiteSpace(format))
        {
            return true;
        }

        switch (format.Trim().ToLowerInvariant())
        {
            case "html":
            case "htm":
                output = IndexerOutput.Html;
                return true;
            case "txt":
            case "text":
                output = IndexerOutput.Txt;
                return true;
            case "xml":
                output = IndexerOutput.Xml;
                return true;
            case "json":
                output = IndexerOutput.Json;
                return true;
            case "md":
            case "markdown":
                output = IndexerOutput.Markdown;
                return true;
            default:
                return false;
        }
    }

    private static List<string>? ParseExtensionList(string? extensions)
    {
        if (string.IsNullOrWhiteSpace(extensions))
        {
            return null;
        }

        List<string> values = extensions
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static extension => !string.IsNullOrWhiteSpace(extension))
            .ToList();

        return values.Count == 0 ? null : values;
    }

    private static string GetExtension(IndexerOutput output)
    {
        return output switch
        {
            IndexerOutput.Html => ".html",
            IndexerOutput.Txt => ".txt",
            IndexerOutput.Xml => ".xml",
            IndexerOutput.Json => ".json",
            IndexerOutput.Markdown => ".md",
            _ => ".html"
        };
    }
}

internal sealed record IndexCommandResult(
    string OutputFilePath,
    long TotalFiles,
    long TotalFolders,
    long TotalBytes,
    double DurationMilliseconds,
    string Format);
