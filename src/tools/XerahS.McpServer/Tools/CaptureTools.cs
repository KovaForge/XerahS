namespace XerahS.McpServer.Tools;

/// <summary>
/// Capture tools for MCP server
/// </summary>
public class CaptureTools
{
    public string[] GetToolDefinitionsJson() => [
        @"{
          ""name"": ""capture_region"",
          ""title"": ""Capture Screen Region"",
          ""description"": ""Opens the XerahS region selector overlay. User selects an area; returns the saved file path. Blocks until capture completes or is cancelled."",
          ""inputSchema"": {
            ""type"": ""object"",
            ""properties"": {
              ""workflow_id"": {
                ""type"": ""string"",
                ""description"": ""Optional workflow UUID to apply after capture (uses default if omitted)""
              },
              ""monitor"": {
                ""type"": ""integer"",
                ""description"": ""Monitor index to capture (0 = primary, 1 = secondary). If omitted, all monitors are shown in the selector.""
              }
            }
          }
        }",
        @"{
          ""name"": ""capture_window"",
          ""title"": ""Capture Single Window"",
          ""description"": ""Captures a specific window by title. Opens window picker if title is omitted."",
          ""inputSchema"": {
            ""type"": ""object"",
            ""properties"": {
              ""window_title"": {
                ""type"": ""string"",
                ""description"": ""Substring match on window title. If omitted, shows window picker.""
              },
              ""include_decoration"": {
                ""type"": ""boolean"",
                ""default"": true,
                ""description"": ""Include the window title bar and borders.""
              }
            }
          }
        }",
        @"{
          ""name"": ""capture_full_screen"",
          ""title"": ""Capture Full Screen"",
          ""description"": ""Captures all monitors or a specific monitor."",
          ""inputSchema"": {
            ""type"": ""object"",
            ""properties"": {
              ""monitor"": {
                ""type"": ""integer"",
                ""description"": ""Monitor index (0 = primary). If omitted, captures all monitors as a stitched image.""
              }
            }
          }
        }",
        @"{
          ""name"": ""capture_scrolling"",
          ""title"": ""Scrolling Capture"",
          ""description"": ""Activates XerahS scrolling capture mode. User selects a region then scrolls manually. Returns the stitched result."",
          ""inputSchema"": {
            ""type"": ""object"",
            ""properties"": {
              ""scroll_direction"": {
                ""type"": ""string"",
                ""enum"": [""down"", ""up"", ""left"", ""right""],
                ""default"": ""down"",
                ""description"": ""Expected scroll direction""
              },
              ""max_frames"": {
                ""type"": ""integer"",
                ""default"": 50,
                ""description"": ""Maximum frames before auto-stop""
              }
            }
          }
        }"
    ];

    public Task<string> CaptureRegionAsync(string? workflowId, int? monitor)
    {
        // STUB: needs integration with ScreenCaptureService
        return Task.FromResult(@"{
          ""file_path"": ""/home/user/Pictures/XerahS/capture_2026-04-08_001.png"",
          ""url"": null,
          ""note"": ""STUB: needs integration with ScreenCaptureService""
        }");
    }

    public Task<string> CaptureWindowAsync(string? windowTitle, bool includeDecoration)
    {
        // STUB: needs integration with ScreenCaptureService
        return Task.FromResult(@"{
          ""file_path"": ""/home/user/Pictures/XerahS/window_capture_2026-04-08_001.png"",
          ""url"": null,
          ""window_title"": ""selected_window"",
          ""note"": ""STUB: needs integration with ScreenCaptureService""
        }");
    }

    public Task<string> CaptureFullScreenAsync(int? monitor)
    {
        // STUB: needs integration with ScreenCaptureService
        return Task.FromResult(@"{
          ""file_path"": ""/home/user/Pictures/XerahS/fullscreen_2026-04-08_001.png"",
          ""url"": null,
          ""monitor"": null,
          ""note"": ""STUB: needs integration with ScreenCaptureService""
        }");
    }

    public Task<string> CaptureScrollingAsync(string scrollDirection, int maxFrames)
    {
        // STUB: needs integration with ScrollingCaptureManager
        return Task.FromResult(@"{
          ""file_path"": ""/home/user/Pictures/XerahS/scrolling_2026-04-08_001.png"",
          ""url"": null,
          ""frames_captured"": 12,
          ""scroll_direction"": ""down"",
          ""note"": ""STUB: needs integration with ScrollingCaptureManager""
        }");
    }
}
