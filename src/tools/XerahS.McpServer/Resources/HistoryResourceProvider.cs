namespace XerahS.McpServer.Resources;

/// <summary>
/// Provides history-related MCP resource descriptors.
/// </summary>
public sealed class HistoryResourceProvider
{
    public string[] GetResourceTemplatesJson() => [
        """
        {
          "uri": "xerahs://history/{id}",
          "name": "History Item",
          "mimeType": "application/json",
          "description": "Detailed metadata for a single history row ID."
        }
        """,
        """
        {
          "uri": "xerahs://history/thumb/{id}",
          "name": "History Item Blob",
          "mimeType": "application/octet-stream",
          "description": "Base64-encoded file contents for the stored history item file."
        }
        """,
        """
        {
          "uri": "xerahs://history/search?q={query}",
          "name": "History Search",
          "mimeType": "application/json",
          "description": "History search results for the provided query string."
        }
        """,
        """
        {
          "uri": "xerahs://capture/latest",
          "name": "Latest Capture",
          "mimeType": "application/json",
          "description": "Detailed metadata for the most recent capture history item."
        }
        """
    ];
}
