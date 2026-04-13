using System.Text.Json.Nodes;
using XerahS.McpServer.Runtime;

namespace XerahS.McpServer.Tools;

/// <summary>
/// Upload tools for MCP server.
/// </summary>
public sealed class UploadTools(IXerahSMcpRuntime runtime)
{
    private readonly IXerahSMcpRuntime _runtime = runtime;

    public string[] GetToolDefinitionsJson() => [
        """
        {
          "name": "upload_file",
          "title": "Upload File",
          "description": "Uploads a file through XerahS using the default or specified configured destination.",
          "inputSchema": {
            "type": "object",
            "properties": {
              "file_path": {
                "type": "string",
                "description": "Absolute path to the file to upload."
              },
              "destination": {
                "type": "string",
                "description": "Optional destination instance ID, provider ID, or display name."
              }
            },
            "required": ["file_path"]
          }
        }
        """,
        """
        {
          "name": "upload_clipboard",
          "title": "Upload Clipboard",
          "description": "Uploads the current clipboard contents if they are image, text, or file data supported by XerahS.",
          "inputSchema": {
            "type": "object",
            "properties": {
              "destination": {
                "type": "string",
                "description": "Optional destination instance ID, provider ID, or display name."
              }
            }
          }
        }
        """
    ];

    public Task<JsonObject> UploadFileAsync(string? filePath, string? destination, CancellationToken cancellationToken = default) =>
        _runtime.UploadFileAsync(filePath, destination, cancellationToken);

    public Task<JsonObject> UploadClipboardAsync(string? destination, CancellationToken cancellationToken = default) =>
        _runtime.UploadClipboardAsync(destination, cancellationToken);
}
