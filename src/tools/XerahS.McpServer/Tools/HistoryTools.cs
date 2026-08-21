using System.Text.Json.Nodes;
using XerahS.McpServer.Runtime;

namespace XerahS.McpServer.Tools;

/// <summary>
/// History tools for MCP server.
/// </summary>
public sealed class HistoryTools(IXerahSMcpRuntime runtime)
{
    private readonly IXerahSMcpRuntime _runtime = runtime;

    public string[] GetToolDefinitionsJson() => [
        """
        {
          "name": "query_history",
          "title": "Query Capture History",
          "description": "Searches XerahS history by free text, OCR-indexed screenshot content, date range, and file type.",
          "inputSchema": {
            "type": "object",
            "properties": {
              "query": {
                "type": "string",
                "description": "Free-text match against file names, paths, URLs, window titles, process names, history tags, and OCR-indexed screenshot text."
              },
              "from_date": {
                "type": "string",
                "format": "date",
                "description": "Inclusive start date in ISO 8601 date format."
              },
              "to_date": {
                "type": "string",
                "format": "date",
                "description": "Inclusive end date in ISO 8601 date format."
              },
              "file_type": {
                "type": "string",
                "enum": ["image", "video", "text", "file", "all"],
                "default": "all",
                "description": "Filter by stored history item type."
              },
              "limit": {
                "type": "integer",
                "default": 20,
                "maximum": 100,
                "description": "Maximum number of results to return."
              }
            }
          }
        }
        """,
        """
        {
          "name": "get_history_item",
          "title": "Get History Item",
          "description": "Retrieves full details for a specific XerahS history item by row ID.",
          "inputSchema": {
            "type": "object",
            "properties": {
              "id": {
                "type": "string",
                "description": "History item row ID as a string."
              }
            },
            "required": ["id"]
          }
        }
        """
    ];

    public Task<JsonObject> QueryHistoryAsync(string? query, string? fromDate, string? toDate, string fileType, int limit, CancellationToken cancellationToken = default) =>
        _runtime.QueryHistoryAsync(query, fromDate, toDate, fileType, limit, cancellationToken);

    public Task<JsonObject> GetHistoryItemAsync(string? id, CancellationToken cancellationToken = default) =>
        _runtime.GetHistoryItemAsync(id, cancellationToken);
}
