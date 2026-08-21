namespace XerahS.McpServer.Resources;

/// <summary>
/// Provides workflow-related MCP resource descriptors.
/// </summary>
public sealed class WorkflowResourceProvider
{
    public string[] GetResourceTemplatesJson() => [
        """
        {
          "uri": "xerahs://workflows",
          "name": "All Workflows",
          "mimeType": "application/json",
          "description": "List of configured workflows."
        }
        """,
        """
        {
          "uri": "xerahs://workflows/{id}",
          "name": "Single Workflow",
          "mimeType": "application/json",
          "description": "Single workflow configuration by workflow ID."
        }
        """
    ];
}
