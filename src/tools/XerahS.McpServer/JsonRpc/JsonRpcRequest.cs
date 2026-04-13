namespace XerahS.McpServer.JsonRpc;

/// <summary>
/// JSON-RPC 2.0 request object
/// </summary>
public class JsonRpcRequest
{
    public string JsonRpc { get; set; } = "2.0";
    public string Method { get; set; } = string.Empty;
    public object? Params { get; set; }
    public object? Id { get; set; }
}
