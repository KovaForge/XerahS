using System.Text.Json.Nodes;
using XerahS.McpServer.Runtime;

namespace XerahS.McpServer.Tools;

/// <summary>
/// Annotation tools for MCP server.
/// </summary>
public sealed class AnnotationTools(IXerahSMcpRuntime runtime)
{
    private readonly IXerahSMcpRuntime _runtime = runtime;

    public string[] GetToolDefinitionsJson() => [
        """
        {
          "name": "annotate_image",
          "title": "Annotate Image",
          "description": "Applies XerahS-compatible annotations to an existing image and writes a new annotated file. This MCP path is headless and does not launch the interactive editor.",
          "inputSchema": {
            "type": "object",
            "properties": {
              "image_path": {
                "type": "string",
                "description": "Absolute path to the image file to annotate."
              },
              "annotations": {
                "type": "array",
                "description": "Ordered list of annotations to render.",
                "items": {
                  "type": "object",
                  "properties": {
                    "type": {
                      "type": "string",
                      "enum": ["arrow", "rectangle", "ellipse", "line", "text", "freehand", "blur", "pixelate", "step"]
                    },
                    "params": {
                      "type": "object",
                      "description": "Annotation-specific parameters such as coordinates, color, or text."
                    }
                  },
                  "required": ["type"]
                }
              },
              "auto_save": {
                "type": "boolean",
                "default": true,
                "description": "Accepted for compatibility; the MCP implementation always writes the annotated result to disk."
              }
            },
            "required": ["image_path"]
          }
        }
        """
    ];

    public Task<JsonObject> AnnotateImageAsync(string? imagePath, JsonArray? annotations, bool autoSave, CancellationToken cancellationToken = default) =>
        _runtime.AnnotateImageAsync(imagePath, annotations, autoSave, cancellationToken);
}
