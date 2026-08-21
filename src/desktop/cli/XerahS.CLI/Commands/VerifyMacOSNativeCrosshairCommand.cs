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
using XerahS.Bootstrap;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Helpers;
using XerahS.Core.Tasks;
using XerahS.Platform.Abstractions;

namespace XerahS.CLI.Commands;

public static class VerifyMacOSNativeCrosshairCommand
{
    private const string VerificationWorkflowId = "cli-verify-macos-native-crosshair";
    private const int DefaultTimeoutSeconds = 120;

    public static Command Create(IDesktopTaskManager taskManager)
    {
        var command = new Command(
            "verify-macos-native-crosshair",
            "Verify the native macOS interactive region selector through the workflow capture pipeline");

        var workflowOption = new Option<string?>("--workflow")
        {
            Description = "Optional workflow ID whose destination/upload settings should be reused"
        };
        var outputOption = new Option<string?>("--output")
        {
            Description = "Output file path (default: a PNG under the system temp directory)"
        };
        var uploadOption = new Option<bool>("--upload")
        {
            Description = "Run upload after a successful capture, using the selected workflow/default destination"
        };
        var timeoutOption = new Option<int?>("--timeout-seconds")
        {
            Description = $"Timeout while waiting for the interactive selector and task pipeline (default: {DefaultTimeoutSeconds})"
        };

        command.Add(workflowOption);
        command.Add(outputOption);
        command.Add(uploadOption);
        command.Add(timeoutOption);

        command.SetAction((parseResult) =>
        {
            var workflowId = parseResult.GetValue(workflowOption);
            var output = parseResult.GetValue(outputOption);
            var upload = parseResult.GetValue(uploadOption);
            var timeoutSeconds = parseResult.GetValue(timeoutOption);

            Environment.ExitCode = RunAsync(taskManager, workflowId, output, upload, timeoutSeconds).GetAwaiter().GetResult();
        });

        return command;
    }

    internal static TaskSettings CreateVerificationTaskSettings(TaskSettings? sourceSettings, string outputPath, bool upload)
    {
        var settings = sourceSettings?.Copy() as TaskSettings ?? new TaskSettings();

        settings.Job = WorkflowType.RectangleRegion;
        settings.CaptureSettings ??= new TaskSettingsCapture();
        settings.GeneralSettings ??= new TaskSettingsGeneral();
        settings.UploadSettings ??= new TaskSettingsUpload();

        // This command exists to catch regressions where the native screencapture path returns
        // null inside RectangleRegion. Keep it on the workflow pipeline, but force the native
        // macOS selector so a successful manual selection must produce a completed task.
        settings.CaptureSettings.MacOSRegionSelectorPreference = MacOSInteractiveRegionSelectorPreference.NativeCrosshair;
        settings.GeneralSettings.ShowToastNotificationAfterTaskCompleted = false;
        settings.AfterCaptureJob = AfterCaptureTasks.SaveImageToFile;
        settings.AfterUploadJob = AfterUploadTasks.None;

        if (upload)
        {
            settings.AfterCaptureJob |= AfterCaptureTasks.UploadImageToHost;
            settings.AfterUploadJob = AfterUploadTasks.CopyURLToClipboard;
        }

        CaptureCommand.ApplyOutputOverride(settings, outputPath);

        return settings;
    }

    internal static string CreateDefaultOutputPath()
    {
        string fileName = $"xerahs-macos-native-crosshair-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        return Path.Combine(Path.GetTempPath(), fileName);
    }

    internal static bool TryNormalizeTimeout(int? timeoutSeconds, out TimeSpan timeout, out string? error)
    {
        error = null;

        int seconds = timeoutSeconds ?? DefaultTimeoutSeconds;
        if (seconds <= 0)
        {
            timeout = default;
            error = "Timeout must be greater than zero seconds.";
            return false;
        }

        timeout = TimeSpan.FromSeconds(seconds);
        return true;
    }

    private static async Task<int> RunAsync(
        IDesktopTaskManager taskManager,
        string? workflowId,
        string? output,
        bool upload,
        int? timeoutSeconds)
    {
        if (!OperatingSystem.IsMacOS())
        {
            Console.Error.WriteLine("verify-macos-native-crosshair is only available on macOS.");
            return 2;
        }

        if (!TryNormalizeTimeout(timeoutSeconds, out var timeout, out var timeoutError))
        {
            Console.Error.WriteLine(timeoutError);
            return 2;
        }

        if (!TryGetWorkflowTaskSettings(workflowId, out var sourceSettings, out var workflowError))
        {
            Console.Error.WriteLine(workflowError);
            return 2;
        }

        string outputPath = Path.GetFullPath(output ?? CreateDefaultOutputPath());
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory);

        var taskSettings = CreateVerificationTaskSettings(sourceSettings, outputPath, upload);
        taskSettings.WorkflowId = VerificationWorkflowId;

        Console.WriteLine("Verifying native macOS crosshair through RectangleRegion workflow pipeline.");
        Console.WriteLine($"Selector: {taskSettings.CaptureSettings.MacOSRegionSelectorPreference}");
        Console.WriteLine($"Workflow source: {workflowId ?? "default verification settings"}");
        Console.WriteLine($"Output: {outputPath}");
        Console.WriteLine($"Timeout: {(int)timeout.TotalSeconds}s");

        var completion = new TaskCompletionSource<WorkerTask>(TaskCreationOptions.RunContinuationsAsynchronously);

        EventHandler<WorkerTask>? handler = null;
        handler = (_, task) =>
        {
            if (!string.Equals(task.Info.TaskSettings.WorkflowId, VerificationWorkflowId, StringComparison.Ordinal))
            {
                return;
            }

            taskManager.TaskCompleted -= handler;
            completion.TrySetResult(task);
        };

        taskManager.TaskCompleted += handler;

        try
        {
            await XerahS.Core.Helpers.TaskHelpers.ExecuteJob(WorkflowType.RectangleRegion, taskSettings, VerificationWorkflowId);

            var completedTask = await Task.WhenAny(completion.Task, Task.Delay(timeout));
            if (completedTask != completion.Task)
            {
                taskManager.TaskCompleted -= handler;
                Console.Error.WriteLine("Timed out waiting for native macOS crosshair workflow completion.");
                return 1;
            }

            var task = await completion.Task;
            bool outputExists = !string.IsNullOrWhiteSpace(task.Info.FilePath) && File.Exists(task.Info.FilePath);

            if (task.Status != XerahS.Core.TaskStatus.Completed || !outputExists)
            {
                string error = task.Error?.Message ?? task.Status.ToString();
                Console.Error.WriteLine($"Native macOS crosshair verification failed: {error}");
                Console.Error.WriteLine($"Saved file present: {outputExists}");
                return 1;
            }

            Console.WriteLine($"Native macOS crosshair verification passed: {task.Info.FilePath}");
            if (task.Info.Metadata != null && !string.IsNullOrEmpty(task.Info.Metadata.UploadURL))
            {
                Console.WriteLine($"URL: {task.Info.Metadata.UploadURL}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            taskManager.TaskCompleted -= handler;
            Console.Error.WriteLine($"Native macOS crosshair verification failed: {ex.Message}");
            DebugHelper.WriteException(ex);
            return 1;
        }
    }

    private static bool TryGetWorkflowTaskSettings(string? workflowId, out TaskSettings? taskSettings, out string? error)
    {
        taskSettings = null;
        error = null;

        if (string.IsNullOrWhiteSpace(workflowId))
        {
            return true;
        }

        var workflow = SettingsManager.WorkflowsConfig?.Hotkeys?.FirstOrDefault(w => w.Id == workflowId);
        if (workflow == null)
        {
            error = $"Workflow not found: {workflowId}";
            return false;
        }

        if (!workflow.Enabled)
        {
            error = $"Workflow is disabled: {workflowId}";
            return false;
        }

        taskSettings = workflow.TaskSettings;
        return true;
    }
}
