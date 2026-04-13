using System.Text.Json.Nodes;
using XerahS.McpServer.Runtime;

namespace XerahS.McpServer.Tools;

/// <summary>
/// Capture tools for MCP server.
/// </summary>
public sealed class CaptureTools(IXerahSMcpRuntime runtime)
{
    private readonly IXerahSMcpRuntime _runtime = runtime;

    public string[] GetToolDefinitionsJson() => [
        """
        {
          "name": "capture_region",
          "title": "Capture Screen Region",
          "description": "Opens the XerahS region selector overlay, waits for the user to pick an area, saves the capture, and returns the file path.",
          "inputSchema": {
            "type": "object",
            "properties": {
              "workflow_id": {
                "type": "string",
                "description": "Optional workflow ID whose capture settings should be used."
              },
              "monitor": {
                "type": "integer",
                "description": "Optional monitor index to constrain the selected region to."
              }
            }
          }
        }
        """,
        """
        {
          "name": "capture_window",
          "title": "Capture Single Window",
          "description": "Captures the foreground window or the first window whose title contains the provided text.",
          "inputSchema": {
            "type": "object",
            "properties": {
              "window_title": {
                "type": "string",
                "description": "Substring match against a window title. If omitted, the foreground window is captured."
              },
              "include_decoration": {
                "type": "boolean",
                "default": true,
                "description": "When false, captures only the client area."
              }
            }
          }
        }
        """,
        """
        {
          "name": "capture_full_screen",
          "title": "Capture Full Screen",
          "description": "Captures all monitors as one image, or a single monitor if an index is provided.",
          "inputSchema": {
            "type": "object",
            "properties": {
              "monitor": {
                "type": "integer",
                "description": "Optional monitor index to capture."
              }
            }
          }
        }
        """,
        """
        {
          "name": "capture_scrolling",
          "title": "Scrolling Capture",
          "description": "Runs XerahS scrolling capture against the active window and returns the stitched image result.",
          "inputSchema": {
            "type": "object",
            "properties": {
              "scroll_direction": {
                "type": "string",
                "enum": ["down", "up", "left", "right"],
                "default": "down",
                "description": "Requested scroll direction metadata for the capture run."
              },
              "max_frames": {
                "type": "integer",
                "default": 50,
                "description": "Requested frame cap metadata for the capture run."
              }
            }
          }
        }
        """
    ];

    public Task<JsonObject> CaptureRegionAsync(string? workflowId, int? monitor, CancellationToken cancellationToken = default) =>
        _runtime.CaptureRegionAsync(workflowId, monitor, cancellationToken);

    public Task<JsonObject> CaptureWindowAsync(string? windowTitle, bool includeDecoration, CancellationToken cancellationToken = default) =>
        _runtime.CaptureWindowAsync(windowTitle, includeDecoration, cancellationToken);

    public Task<JsonObject> CaptureFullScreenAsync(int? monitor, CancellationToken cancellationToken = default) =>
        _runtime.CaptureFullScreenAsync(monitor, cancellationToken);

    public Task<JsonObject> CaptureScrollingAsync(string scrollDirection, int maxFrames, CancellationToken cancellationToken = default) =>
        _runtime.CaptureScrollingAsync(scrollDirection, maxFrames, cancellationToken);
}
