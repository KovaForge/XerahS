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
using Newtonsoft.Json;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Tasks;
using XerahS.Core.Tasks.Processors;
using XerahS.OmaXerahs.Models;
using XerahS.OmaXerahs.Services;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.OmaXerahs.Commands;

internal static class UploadCommand
{
    internal static Func<TaskInfo, CancellationToken, Task> ProcessUploadAsync { get; set; } =
        static (taskInfo, cancellationToken) => new UploadJobProcessor().ProcessAsync(taskInfo, cancellationToken);

    internal static Func<ImageDestinationInspection> InspectImage { get; set; } =
        UploadHost.InspectImageDestination;

    internal static Command Create()
    {
        var command = new Command("upload", "Upload an image file to the configured XerahS Image destination.");
        var pathArgument = new Argument<string?>("path")
        {
            Description = "Path to the image file. Pass after -- so names starting with - are safe.",
            Arity = ArgumentArity.ZeroOrOne
        };
        var jsonOption = JsonStdout.CreateJsonOption();
        command.Add(pathArgument);
        command.Add(jsonOption);
        command.SetAction(parseResult =>
        {
            JsonStdout.Enabled = parseResult.GetValue(jsonOption);
            return UploadAsync(parseResult.GetValue(pathArgument)).GetAwaiter().GetResult();
        });
        return command;
    }

    internal static bool TryValidateImagePath(string? path, out string canonicalPath, out string errorCode, out string errorMessage)
    {
        canonicalPath = string.Empty;
        errorCode = CliErrorCodes.InvalidPath;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            errorCode = CliErrorCodes.Usage;
            errorMessage = "Specify an image path after --.";
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            errorMessage = $"Invalid path: {ex.Message}";
            return false;
        }

        string resolved = fullPath;
        try
        {
            var linkTarget = File.ResolveLinkTarget(fullPath, returnFinalTarget: true);
            if (linkTarget != null)
            {
                resolved = linkTarget.FullName;
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Unable to resolve path: {ex.Message}";
            return false;
        }

        if (!File.Exists(resolved))
        {
            errorMessage = $"File not found: {resolved}";
            return false;
        }

        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(resolved);
        }
        catch (Exception ex)
        {
            errorMessage = $"Unable to inspect path: {ex.Message}";
            return false;
        }

        if ((attributes & FileAttributes.Directory) != 0 ||
            (attributes & FileAttributes.Device) != 0)
        {
            errorMessage = "Path is not a regular file.";
            return false;
        }

        if (!FileHelpers.IsImageFile(resolved))
        {
            errorCode = CliErrorCodes.UnsupportedType;
            errorMessage = "File is not a supported image type.";
            return false;
        }

        canonicalPath = resolved;
        errorCode = string.Empty;
        return true;
    }

    internal static async Task<int> UploadAsync(string? path)
    {
        if (!TryValidateImagePath(path, out string canonicalPath, out string errorCode, out string errorMessage))
        {
            return JsonStdout.WriteFailureAndExit(errorCode, errorMessage);
        }

        try
        {
            await UploadHost.EnsureBootstrappedAsync();
            var inspection = InspectImage();
            if (!inspection.Ready || inspection.Instance == null)
            {
                return JsonStdout.WriteFailureAndExit(
                    CliErrorCodes.NotReady,
                    "No usable image uploader is configured in XerahS.");
            }

            var workflow = SettingsManager.GetFirstWorkflowOrDefault(WorkflowType.FileUpload);
            var taskSettings = CloneTaskSettings(workflow.TaskSettings);
            taskSettings.Job = WorkflowType.FileUpload;
            taskSettings.AfterCaptureJob = AfterCaptureTasks.None;
            taskSettings.AfterUploadJob = AfterUploadTasks.None;
            taskSettings.DestinationInstanceId = null;
            // Fail closed: no Image→File. Property lives on TaskSettings (PR-CFail).
            taskSettings.AllowCrossCategoryFallback = false;
            taskSettings.GeneralSettings.ShowToastNotificationAfterTaskCompleted = false;

            var taskInfo = new TaskInfo(taskSettings)
            {
                DataType = EDataType.Image,
                Job = TaskJob.FileUpload,
                FilePath = canonicalPath
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            try
            {
                await ProcessUploadAsync(taskInfo, cts.Token);
            }
            catch (Exception ex)
            {
                var mapped = ErrorMapper.FromException(ex, timeoutRequested: cts.IsCancellationRequested);
                return JsonStdout.WriteFailureAndExit(mapped.Code, mapped.Message);
            }

            var url = taskInfo.Result?.URL;
            bool success = taskInfo.Result?.IsSuccess == true || !string.IsNullOrEmpty(url);
            if (!success || !ErrorMapper.IsHttpUrl(url))
            {
                var mapped = ErrorMapper.FromUploadResult(taskInfo.Result);
                if (!success && mapped.Code == CliErrorCodes.Provider && !inspection.Ready)
                {
                    mapped = (CliErrorCodes.NotReady, mapped.Message);
                }

                if (success && !ErrorMapper.IsHttpUrl(url))
                {
                    mapped = (CliErrorCodes.Provider, "Upload did not return an http:// or https:// URL.");
                }

                return JsonStdout.WriteFailureAndExit(mapped.Code, mapped.Message);
            }

            var instance = ResolveUploadedInstance(taskInfo, inspection.Instance);
            var fileInfo = new FileInfo(canonicalPath);
            JsonStdout.Write(new UploadSuccessResponse
            {
                SchemaVersion = 1,
                Ok = true,
                Url = url!,
                Filename = Path.GetFileName(canonicalPath),
                Size = fileInfo.Length,
                Type = GetContentType(canonicalPath),
                DataType = "image",
                ProviderId = instance?.ProviderId,
                InstanceId = instance?.InstanceId,
                DisplayName = instance?.DisplayName ?? taskInfo.UploaderHost
            });
            return 0;
        }
        catch (Exception ex)
        {
            var mapped = ErrorMapper.FromException(ex);
            return JsonStdout.WriteFailureAndExit(mapped.Code, mapped.Message);
        }
    }

    private static UploaderInstance? ResolveUploadedInstance(TaskInfo taskInfo, UploaderInstance fallback)
    {
        string? host = taskInfo.UploaderHost;
        if (!string.IsNullOrWhiteSpace(host))
        {
            var match = UploadHost.GetUsableImageInstances()
                .FirstOrDefault(i => string.Equals(i.DisplayName, host, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return match;
            }
        }

        return fallback;
    }

    internal static string GetContentType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".tif" or ".tiff" => "image/tiff",
            ".ico" => "image/x-icon",
            _ => "application/octet-stream"
        };
    }

    private static TaskSettings CloneTaskSettings(TaskSettings source)
    {
        var jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };

        string json = JsonConvert.SerializeObject(source, jsonSettings);
        return JsonConvert.DeserializeObject<TaskSettings>(json, jsonSettings) ?? new TaskSettings();
    }
}
