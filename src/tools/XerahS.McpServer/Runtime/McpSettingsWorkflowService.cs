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

using System.Text.Json.Nodes;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.History;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.McpServer.Runtime;

internal sealed class McpSettingsWorkflowService
{
    public JsonObject ListWorkflows()
    {
        var workflows = SettingsManager.WorkflowsConfig?.Hotkeys ?? [];
        return new JsonObject
        {
            ["workflows"] = new JsonArray(workflows.Select(CreateWorkflow).Cast<JsonNode>().ToArray()),
            ["count"] = workflows.Count
        };
    }

    public JsonObject GetWorkflow(string workflowId)
    {
        var workflow = SettingsManager.GetWorkflowById(workflowId)
            ?? throw new InvalidOperationException($"Workflow '{workflowId}' was not found.");

        return new JsonObject { ["workflow"] = CreateWorkflow(workflow) };
    }

    public JsonObject GetSettings(string? category)
    {
        var normalized = category?.Trim().ToLowerInvariant();
        JsonNode settings = normalized switch
        {
            "capture" => CreateCaptureSettings(),
            "upload" => CreateUploadSettings(),
            "history" => CreateHistorySettings(),
            "general" => CreateGeneralSettings(),
            "integration" => CreateIntegrationSettings(),
            null or "" => new JsonObject
            {
                ["capture"] = CreateCaptureSettings(),
                ["upload"] = CreateUploadSettings(),
                ["history"] = CreateHistorySettings(),
                ["general"] = CreateGeneralSettings(),
                ["integration"] = CreateIntegrationSettings()
            },
            _ => throw new ArgumentException($"Unknown settings category: {category}")
        };

        return new JsonObject
        {
            ["category"] = string.IsNullOrWhiteSpace(normalized) ? "all" : normalized,
            ["settings"] = settings
        };
    }

    public JsonObject CreateDestinationsResource()
    {
        var manager = InstanceManager.Instance;
        JsonNode[] destinations = manager.GetInstances()
            .OrderBy(instance => instance.Category)
            .ThenBy(instance => instance.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(instance => new JsonObject
            {
                ["id"] = instance.InstanceId,
                ["provider_id"] = instance.ProviderId,
                ["name"] = instance.DisplayName,
                ["category"] = instance.Category.ToString().ToLowerInvariant(),
                ["is_available"] = instance.IsAvailable,
                ["is_default"] = manager.IsDefaultInstance(instance.Category, instance.InstanceId)
            })
            .Cast<JsonNode>()
            .ToArray();

        return new JsonObject { ["destinations"] = new JsonArray(destinations) };
    }

    public static string? ResolveDestinationInstanceId(string? destination, UploaderCategory category)
    {
        var instanceManager = InstanceManager.Instance;
        if (string.IsNullOrWhiteSpace(destination))
        {
            return instanceManager.GetDefaultInstance(category)?.InstanceId;
        }

        var normalized = destination.Trim();
        var allInstances = instanceManager.GetInstances();
        var matched = allInstances.FirstOrDefault(instance =>
                string.Equals(instance.InstanceId, normalized, StringComparison.OrdinalIgnoreCase))
            ?? allInstances.FirstOrDefault(instance =>
                instance.Category == category &&
                (string.Equals(instance.ProviderId, normalized, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(instance.DisplayName, normalized, StringComparison.OrdinalIgnoreCase)));

        if (matched == null)
        {
            throw new InvalidOperationException($"Upload destination '{destination}' was not found.");
        }

        return matched.InstanceId;
    }

    public static string? ResolveDestinationSummary(string? destinationInstanceId)
    {
        if (string.IsNullOrWhiteSpace(destinationInstanceId))
        {
            return null;
        }

        var instance = InstanceManager.Instance.GetInstance(destinationInstanceId);
        return instance == null ? destinationInstanceId : $"{instance.DisplayName} ({instance.ProviderId})";
    }

    internal static string InferCaptureMode(WorkflowType workflowType)
    {
        return workflowType switch
        {
            WorkflowType.RectangleRegion or WorkflowType.RectangleTransparent => "region",
            WorkflowType.PrintScreen => "fullscreen",
            WorkflowType.ActiveWindow => "window",
            WorkflowType.ActiveMonitor => "monitor",
            WorkflowType.ScrollingCapture => "scrolling",
            _ => workflowType.ToString()
        };
    }

    private static JsonObject CreateWorkflow(WorkflowSettings workflow)
    {
        return new JsonObject
        {
            ["id"] = workflow.Id,
            ["name"] = string.IsNullOrWhiteSpace(workflow.Name) ? workflow.ToString() : workflow.Name,
            ["job"] = workflow.Job.ToString(),
            ["capture_mode"] = InferCaptureMode(workflow.Job),
            ["after_capture"] = McpJsonSerialization.FlagsToNames(workflow.TaskSettings.AfterCaptureJob),
            ["after_upload"] = McpJsonSerialization.FlagsToNames(workflow.TaskSettings.AfterUploadJob),
            ["enabled"] = workflow.Enabled,
            ["pinned_to_tray"] = workflow.PinnedToTray
        };
    }

    private static JsonObject CreateCaptureSettings()
    {
        var captureSettings = SettingsManager.GetFirstWorkflowOrDefault(WorkflowType.RectangleRegion).TaskSettings;

        return new JsonObject
        {
            ["default_capture_mode"] = InferCaptureMode(captureSettings.Job),
            ["use_modern_capture"] = captureSettings.CaptureSettings.UseModernCapture,
            ["show_cursor"] = captureSettings.CaptureSettings.ShowCursor,
            ["macos_play_capture_sound"] = captureSettings.CaptureSettings.MacOSPlayCaptureSound,
            ["capture_delay_seconds"] = captureSettings.CaptureSettings.ScreenshotDelay,
            ["capture_transparent"] = captureSettings.CaptureSettings.CaptureTransparent,
            ["capture_shadow"] = captureSettings.CaptureSettings.CaptureShadow,
            ["capture_client_area"] = captureSettings.CaptureSettings.CaptureClientArea,
            ["image_format"] = captureSettings.ImageSettings.ImageFormat.ToString().ToLowerInvariant(),
            ["jpeg_quality"] = captureSettings.ImageSettings.ImageJPEGQuality,
            ["screenshot_folder"] = SettingsManager.ScreenshotsFolder
        };
    }

    private JsonObject CreateUploadSettings()
    {
        var manager = InstanceManager.Instance;

        return new JsonObject
        {
            ["default_image_destination"] = ResolveDestinationSummary(manager.GetDefaultInstance(UploaderCategory.Image)?.InstanceId),
            ["default_text_destination"] = ResolveDestinationSummary(manager.GetDefaultInstance(UploaderCategory.Text)?.InstanceId),
            ["default_file_destination"] = ResolveDestinationSummary(manager.GetDefaultInstance(UploaderCategory.File)?.InstanceId),
            ["default_url_shortener"] = ResolveDestinationSummary(manager.GetDefaultInstance(UploaderCategory.UrlShortener)?.InstanceId),
            ["copy_url_after_upload"] = SettingsManager.GetFirstWorkflowOrDefault(WorkflowType.FileUpload).TaskSettings.AfterUploadJob.HasFlag(AfterUploadTasks.CopyURLToClipboard),
            ["destinations"] = CreateDestinationsResource()["destinations"]?.DeepClone()
        };
    }

    private static JsonObject CreateHistorySettings()
    {
        return new JsonObject
        {
            ["save_history"] = SettingsManager.Settings.HistorySaveTasks,
            ["verify_urls"] = SettingsManager.Settings.HistoryCheckURL,
            ["save_recent_tasks"] = SettingsManager.Settings.RecentTasksSave,
            ["recent_tasks_limit"] = SettingsManager.Settings.RecentTasksMaxCount,
            ["screenshot_content_search_enabled"] = SettingsManager.Settings.ScreenshotContentSearchEnabled,
            ["ocr_indexed_count"] = new HistoryOcrIndexStore(SettingsManager.GetHistoryFilePath()).CountIndexed(),
            ["history_folder"] = SettingsManager.HistoryFolder,
            ["history_file"] = SettingsManager.GetHistoryFilePath()
        };
    }

    private static JsonObject CreateGeneralSettings()
    {
        return new JsonObject
        {
            ["language"] = SettingsManager.Settings.Language.ToString(),
            ["theme_mode"] = SettingsManager.Settings.ThemeMode.ToString(),
            ["show_tray"] = SettingsManager.Settings.ShowTray,
            ["run_at_startup"] = SettingsManager.Settings.RunAtStartup,
            ["disable_hotkeys"] = SettingsManager.Settings.DisableHotkeys,
            ["settings_folder"] = SettingsManager.SettingsFolder,
            ["screenshots_folder"] = SettingsManager.ScreenshotsFolder
        };
    }

    private static JsonObject CreateIntegrationSettings()
    {
        var apiKey = SettingsManager.Settings.McpApiKey;
        var preview = string.IsNullOrWhiteSpace(apiKey)
            ? null
            : $"{new string('*', Math.Max(apiKey.Length - 4, 0))}{apiKey[^Math.Min(4, apiKey.Length)..]}";

        return new JsonObject
        {
            ["mcp_api_key_configured"] = !string.IsNullOrWhiteSpace(apiKey),
            ["mcp_api_key_preview"] = preview,
            ["mcp_manifest_url"] = "https://xerahs.com/.well-known/mcp/manifest.json"
        };
    }
}
