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
using Newtonsoft.Json;
using XerahS.Bootstrap;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Tasks;
using XerahS.Core.Tasks.Processors;
using XerahS.CLI.Services;
using System.Collections.Generic;

namespace XerahS.CLI.Commands;

public static class UploadCommand
{
    private static bool _jsonOutput;
    private static bool _quiet;
    private static Func<string, bool, UploadReadiness> _checkUploadReadiness = CliUploaderBootstrapper.CheckUploadReadiness;
    private static Func<TaskInfo, CancellationToken, Task> _processUploadAsync = static (taskInfo, cancellationToken) =>
    {
        var uploadProcessor = new UploadJobProcessor();
        return uploadProcessor.ProcessAsync(taskInfo, cancellationToken);
    };

    internal static string SanitizeUploadFileName(string? name, string fallbackFileName)
    {
        var leafName = Path.GetFileName(name);
        var sanitizedName = string.IsNullOrWhiteSpace(leafName) ? string.Empty : FileHelpers.SanitizeFileName(leafName);

        return string.IsNullOrWhiteSpace(sanitizedName) || sanitizedName is "." or ".."
            ? fallbackFileName
            : sanitizedName;
    }

    internal static string CreateTemporaryUploadFilePath(string? requestedName, string fallbackFileName)
    {
        var sanitizedName = SanitizeUploadFileName(requestedName, fallbackFileName);
        var uploadDirectory = CreateTemporaryUploadDirectory();
        return Path.Combine(uploadDirectory, sanitizedName);
    }

    internal static string CreateTemporaryUploadDirectory()
    {
        var uploadDirectory = Path.Combine(Path.GetTempPath(), "xerahs-upload", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(uploadDirectory);
        return uploadDirectory;
    }

    internal static void CleanupTemporaryUploadDirectories(IEnumerable<string?> directories)
    {
        foreach (string directory in directories
                     .Where(static path => !string.IsNullOrWhiteSpace(path))
                     .Select(static path => path!)
                     .Distinct(StringComparer.Ordinal))
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch
            {
                // ignore cleanup errors
            }
        }
    }

    public static Command Create(IDesktopTaskManager taskManager)
    {
        var uploadCommand = new Command("upload", "Upload a file, text content, or stdin to configured uploaders");

        var filePathArgument = new Argument<string?>("file")
        {
            Description = "Path to the file to upload",
            Arity = ArgumentArity.ZeroOrOne
        };
        var textOption = new Option<string?>("--text") { Description = "Text content to upload" };
        var pipeOption = new Option<bool>("--pipe") { Description = "Read content from stdin" };
        var nameOption = new Option<string?>("--name") { Description = "Name of the file (inferred from path if not specified)" };
        var jsonOption = new Option<bool>("--json") { Description = "Output JSON result to stdout" };
        var quietOption = new Option<bool>("--quiet") { Description = "Suppress console output except for the URL or errors" };

        uploadCommand.Add(filePathArgument);
        uploadCommand.Add(textOption);
        uploadCommand.Add(pipeOption);
        uploadCommand.Add(nameOption);
        uploadCommand.Add(jsonOption);
        uploadCommand.Add(quietOption);

        uploadCommand.SetAction(parseResult =>
        {
            _jsonOutput = parseResult.GetValue(jsonOption);
            _quiet = parseResult.GetValue(quietOption);

            var filePath = parseResult.GetValue(filePathArgument);
            var text = parseResult.GetValue(textOption);
            var pipe = parseResult.GetValue(pipeOption);
            var name = parseResult.GetValue(nameOption);

            Environment.ExitCode = UploadAsync(taskManager, filePath, text, pipe, name).GetAwaiter().GetResult();
        });

        return uploadCommand;
    }

    private static async Task<int> UploadAsync(IDesktopTaskManager taskManager, string? filePath, string? text, bool pipe, string? name)
    {
        string? tempFilePath = null;
        var tempDirectories = new List<string>();

        try
        {
            if (string.IsNullOrEmpty(filePath) && string.IsNullOrEmpty(text) && !pipe)
            {
                PrintError("Specify a file path, --text content, or --pipe to upload from stdin.");
                return 1;
            }

            if (!string.IsNullOrEmpty(filePath) && (!string.IsNullOrEmpty(text) || pipe))
            {
                PrintError("Cannot use both file path and --text/--pipe at the same time.");
                return 1;
            }

            // Handle --text input: write to temp file
            if (!string.IsNullOrEmpty(text))
            {
                tempFilePath = CreateTemporaryUploadFilePath(name, "upload.txt");
                tempDirectories.Add(Path.GetDirectoryName(tempFilePath)!);
                await File.WriteAllTextAsync(tempFilePath, text);
                filePath = tempFilePath;
            }
            // Handle --pipe input: read stdin to temp file
            else if (pipe)
            {
                tempFilePath = CreateTemporaryUploadFilePath(name, "upload.txt");
                tempDirectories.Add(Path.GetDirectoryName(tempFilePath)!);
                using var stdin = Console.OpenStandardInput();
                using var fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write);
                await stdin.CopyToAsync(fs);
                filePath = tempFilePath;
            }

            if (string.IsNullOrEmpty(filePath))
            {
                PrintError("No file path available for upload.");
                return 1;
            }

            string displayName = Path.GetFileName(filePath);

            if (!string.IsNullOrEmpty(filePath))
            {
                string? namedFilePath = null;
                if (!string.IsNullOrEmpty(name) && File.Exists(filePath))
                {
                    // Copy to a unique temp file with the requested name so uploaders see the right filename.
                    // Avoid reusing shared temp paths, which can clobber concurrent uploads and leave stale files behind.
                    namedFilePath = CreateTemporaryUploadFilePath(name, Path.GetFileName(filePath));
                    tempDirectories.Add(Path.GetDirectoryName(namedFilePath)!);
                    File.Copy(filePath, namedFilePath, overwrite: true);
                    filePath = namedFilePath;
                }

                displayName = !string.IsNullOrEmpty(name)
                    ? SanitizeUploadFileName(name, Path.GetFileName(filePath))
                    : Path.GetFileName(filePath);

                if (!File.Exists(filePath))
                {
                    PrintError($"File not found: {filePath}");
                    return 1;
                }

                if (!_quiet && !_jsonOutput) Console.WriteLine($"Uploading: {displayName}");
            }

            bool uploadAsText = !string.IsNullOrEmpty(text) || pipe || FileHelpers.IsTextFile(filePath);
            var readiness = _checkUploadReadiness(filePath, uploadAsText);
            if (!readiness.IsReady)
            {
                PrintError(readiness.ErrorMessage ?? "No usable uploader is configured.");
                return 1;
            }

            // Use the first FileUpload workflow's settings so user-configured options
            // like FileUploadUseNamePattern are respected. Fall back to default.
            var workflow = SettingsManager.GetFirstWorkflowOrDefault(WorkflowType.FileUpload);
            var taskSettings = CloneTaskSettings(workflow.TaskSettings);
            taskSettings.Job = WorkflowType.FileUpload;
            taskSettings.AfterCaptureJob = AfterCaptureTasks.UploadImageToHost;
            taskSettings.AfterUploadJob = AfterUploadTasks.CopyURLToClipboard;

            var taskInfo = new TaskInfo(taskSettings)
            {
                DataType = uploadAsText ? EDataType.Text : EDataType.File,
                Job = uploadAsText ? TaskJob.TextUpload : TaskJob.FileUpload
            };

            if (uploadAsText)
            {
                taskSettings.DestinationInstanceId = null;
                taskInfo.TextContent = await File.ReadAllTextAsync(filePath);
                taskInfo.SetFileName(displayName);
            }
            else
            {
                taskInfo.FilePath = filePath;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            await _processUploadAsync(taskInfo, cts.Token);

            bool success = taskInfo.Result?.IsSuccess == true || !string.IsNullOrEmpty(taskInfo.Result?.URL);
            if (!success)
            {
                PrintError(taskInfo.Result?.Response ?? "Upload failed");
                return 1;
            }

            var url = taskInfo.Result!.URL ?? string.Empty;
            if (_jsonOutput)
            {
                var result = new UploadResult(url, displayName, new FileInfo(filePath).Length, GetContentType(filePath));
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result));
            }
            else
            {
                Console.WriteLine(url);
            }

            return 0;
        }
        catch (Exception ex)
        {
            PrintError($"Upload failed: {ex.Message}");
            DebugHelper.WriteException(ex);
            return 1;
        }
        finally
        {
            CleanupTemporaryUploadDirectories(tempDirectories);

            if (tempFilePath != null && File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); }
                catch { /* ignore cleanup errors */ }
            }
        }
    }

    private static void PrintError(string message)
    {
        if (_jsonOutput)
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { error = message }));
        }
        Console.Error.WriteLine(message);
    }

    private static string GetContentType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            ".md" or ".markdown" => "text/markdown",
            ".txt" => "text/plain",
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".pdf" => "application/pdf",
            ".zip" => "application/zip",
            ".tar" => "application/x-tar",
            ".gz" => "application/gzip",
            _ => "application/octet-stream"
        };
    }

    private sealed class UploadResult
    {
        public string url { get; }
        public string filename { get; }
        public long size { get; }
        public string type { get; }

        public UploadResult(string url, string filename, long size, string type)
        {
            this.url = url;
            this.filename = filename;
            this.size = size;
            this.type = type;
        }
    }

    private static TaskSettings CloneTaskSettings(TaskSettings source)
    {
        var jsonSettings = new Newtonsoft.Json.JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };

        string json = JsonConvert.SerializeObject(source, jsonSettings);
        return JsonConvert.DeserializeObject<TaskSettings>(json, jsonSettings) ?? new TaskSettings();
    }
}
