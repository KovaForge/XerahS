using System.Text.Json.Serialization;

namespace XerahS.McpServer.JsonRpc;

/// <summary>
/// JSON-RPC 2.0 response object
/// </summary>
public class JsonRpcResponse
{
    public string JsonRpc { get; set; } = "2.0";
    public object? Id { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcError? Error { get; set; }

    public static JsonRpcResponse Success(object? id, object? result)
    {
        return new JsonRpcResponse { Id = id, Result = result };
    }

    public static JsonRpcResponse FromError(object? id, int code, string message, object? data = null)
    {
        return new JsonRpcResponse 
        { 
            Id = id, 
            Error = new JsonRpcError { Code = code, Message = message, Data = data } 
        };
    }
}
