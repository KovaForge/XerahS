namespace XerahS.McpServer.Server;

/// <summary>
/// MCP server capabilities
/// </summary>
public static class Capabilities
{
    public const string ProtocolVersion = "2024-11-05";
    public const string ServerName = "xerahs";
    public const string ServerVersion = "0.22.0";

    /// <summary>
    /// Server capability declaration
    /// </summary>
    public static Dictionary<string, object> GetCapabilities()
    {
        return new Dictionary<string, object>
        {
            ["tools"] = new { listChanged = true },
            ["resources"] = new { subscribe = true, listChanged = true },
            ["prompts"] = new { listChanged = true }
        };
    }

    /// <summary>
    /// Server info for initialize response
    /// </summary>
    public static Dictionary<string, string> GetServerInfo()
    {
        return new Dictionary<string, string>
        {
            ["name"] = ServerName,
            ["version"] = ServerVersion
        };
    }
}
