using System.Text.Json.Serialization;

namespace XerahS.McpServer.JsonRpc;

/// <summary>
/// JSON-RPC 2.0 error object
/// </summary>
public class JsonRpcError
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }
}

/// <summary>
/// Standard JSON-RPC error codes
/// </summary>
public static class JsonRpcErrorCodes
{
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
    public const int ServerError = -32000;
    
    // MCP-specific error codes
    public const int UserCancelled = -32500;
    public const int NotConfigured = -32400;
}
