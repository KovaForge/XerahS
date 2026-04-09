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

namespace XerahS.CLI.Commands;

public static class UploadCommand
{
    private static bool _jsonOutput;
    private static bool _quiet;

    public static Command Create(IDesktopTaskManager taskManager)
    {
        var uploadCommand = new Command("upload", "Upload a file, text content, or stdin to configured uploaders");

        var filePathArgument = new Argument<string>("file") { Description = "Path to the file to upload" };
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
                tempFilePath = Path.Combine(Path.GetTempPath(), name ?? "upload.txt");
                await File.WriteAllTextAsync(tempFilePath, text);
                filePath = tempFilePath;
            }
            // Handle --pipe input: read stdin to temp file
            else if (pipe)
            {
                tempFilePath = Path.Combine(Path.GetTempPath(), name ?? "upload.txt");
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

            if (!File.Exists(filePath))
            {
                PrintError($"File not found: {filePath}");
                return 1;
            }

            if (!_quiet) Console.WriteLine($"Uploading: {filePath}");

            // Configure task settings for file upload
            // Use the first FileUpload workflow's settings so user-configured options
            // like FileUploadUseNamePattern are respected. Fall back to default.
            var workflow = SettingsManager.GetFirstWorkflowOrDefault(WorkflowType.FileUpload);
            var taskSettings = CloneTaskSettings(workflow.TaskSettings);
            taskSettings.Job = WorkflowType.FileUpload;
            taskSettings.AfterCaptureJob = AfterCaptureTasks.UploadImageToHost;
            taskSettings.AfterUploadJob = AfterUploadTasks.CopyURLToClipboard;

            var tcs = new TaskCompletionSource<bool>();
            bool handlerFired = false;

            EventHandler<WorkerTask>? handler = null;
            handler = (sender, task) =>
            {
                if (handlerFired) return;
                handlerFired = true;
                taskManager.TaskCompleted -= handler;

                bool success = task.Status == Core.TaskStatus.Completed && !string.IsNullOrEmpty(task.Info.Metadata?.UploadURL);
                if (success)
                {
                    var url = task.Info.Metadata?.UploadURL ?? string.Empty;
                    if (_jsonOutput)
                    {
                        var result = new UploadResult(url, Path.GetFileName(filePath), new FileInfo(filePath).Length, GetContentType(filePath));
                        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result));
                    }
                    else
                    {
                        Console.WriteLine(url);
                    }
                }
                else
                {
                    var errorMsg = task.Error?.Message ?? "Upload failed";
                    PrintError(errorMsg);
                }
                tcs.SetResult(success);
            };

            taskManager.TaskCompleted += handler;
            await taskManager.StartFileTask(taskSettings, filePath);

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, cts.Token));

            if (completed != tcs.Task)
            {
                PrintError("Upload timed out after 5 minutes");
                taskManager.TaskCompleted -= handler;
                return 1;
            }

            return await tcs.Task ? 0 : 1;
        }
        catch (Exception ex)
        {
            PrintError($"Upload failed: {ex.Message}");
            DebugHelper.WriteException(ex);
            return 1;
        }
        finally
        {
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
