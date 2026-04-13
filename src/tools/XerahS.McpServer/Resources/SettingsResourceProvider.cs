namespace XerahS.McpServer.Resources;

/// <summary>
/// Provides settings-related MCP resource descriptors.
/// </summary>
public sealed class SettingsResourceProvider
{
    public string[] GetResourceTemplatesJson() => [
        """
        {
          "uri": "xerahs://settings/capture",
          "name": "Capture Settings",
          "mimeType": "application/json",
          "description": "Non-secret capture defaults used by the MCP runtime."
        }
        """,
        """
        {
          "uri": "xerahs://settings/upload",
          "name": "Upload Settings",
          "mimeType": "application/json",
          "description": "Configured upload defaults and destination summaries."
        }
        """,
        """
        {
          "uri": "xerahs://settings/history",
          "name": "History Settings",
          "mimeType": "application/json",
          "description": "History persistence settings."
        }
        """,
        """
        {
          "uri": "xerahs://settings/general",
          "name": "General Settings",
          "mimeType": "application/json",
          "description": "General application settings exposed to MCP clients."
        }
        """,
        """
        {
          "uri": "xerahs://settings/integration",
          "name": "Integration Settings",
          "mimeType": "application/json",
          "description": "MCP integration metadata without revealing secrets."
        }
        """,
        """
        {
          "uri": "xerahs://destinations",
          "name": "Upload Destinations",
          "mimeType": "application/json",
          "description": "Configured upload destinations and defaults."
        }
        """,
        """
        {
          "uri": "xerahs://monitors",
          "name": "Monitor Inventory",
          "mimeType": "application/json",
          "description": "Available display monitors and their bounds."
        }
        """
    ];
}
