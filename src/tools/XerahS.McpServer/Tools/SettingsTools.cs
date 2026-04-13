using System.Text.Json.Nodes;
using XerahS.McpServer.Runtime;

namespace XerahS.McpServer.Tools;

/// <summary>
/// Settings and workflow tools for MCP server.
/// </summary>
public sealed class SettingsTools(IXerahSMcpRuntime runtime)
{
    private readonly IXerahSMcpRuntime _runtime = runtime;

    public string[] GetToolDefinitionsJson() => [
        """
        {
          "name": "list_workflows",
          "title": "List Workflows",
          "description": "Lists configured XerahS workflows and their after-capture and after-upload actions.",
          "inputSchema": {
            "type": "object",
            "properties": {}
          }
        }
        """,
        """
        {
          "name": "get_settings",
          "title": "Get Settings",
          "description": "Reads non-secret XerahS settings. If no category is supplied, returns the full MCP-safe settings view.",
          "inputSchema": {
            "type": "object",
            "properties": {
              "category": {
                "type": "string",
                "enum": ["capture", "upload", "history", "general", "integration"],
                "description": "Optional settings category."
              }
            }
          }
        }
        """
    ];

    public Task<JsonObject> ListWorkflowsAsync(CancellationToken cancellationToken = default) =>
        _runtime.ListWorkflowsAsync(cancellationToken);

    public Task<JsonObject> GetSettingsAsync(string? category, CancellationToken cancellationToken = default) =>
        _runtime.GetSettingsAsync(category, cancellationToken);
}
