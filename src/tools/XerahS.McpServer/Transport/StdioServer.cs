using System.Text.Json;
using XerahS.McpServer.JsonRpc;
using XerahS.McpServer.Server;

namespace XerahS.McpServer.Transport;

/// <summary>
/// MCP stdio transport - reads JSON-RPC from stdin, writes to stdout
/// </summary>
public class StdioServer : IDisposable
{
    private readonly XerahSMcpServer _mcpServer;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _isRunning;
    private bool _isDisposed;

    public StdioServer(XerahSMcpServer mcpServer)
    {
        _mcpServer = mcpServer ?? throw new ArgumentNullException(nameof(mcpServer));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Run the stdio server loop
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
            throw new InvalidOperationException("Server is already running");

        _isRunning = true;

        try
        {
            await using var stdin = Console.OpenStandardInput();
            using var reader = new StreamReader(stdin);

            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                
                if (line == null)
                {
                    // EOF - client disconnected
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                await ProcessMessageAsync(line);
            }
        }
        finally
        {
            _isRunning = false;
        }
    }

    /// <summary>
    /// Process a single JSON-RPC message
    /// </summary>
    private async Task ProcessMessageAsync(string line)
    {
        JsonRpcRequest? request = null;
        object? requestId = null;

        try
        {
            request = JsonSerializer.Deserialize<JsonRpcRequest>(line, _jsonOptions);
            
            if (request == null)
            {
                await WriteResponseAsync(JsonRpcResponse.FromError(null, JsonRpcErrorCodes.InvalidRequest, "Invalid JSON-RPC request"));
                return;
            }

            requestId = request.Id;

            if (request.JsonRpc != "2.0")
            {
                await WriteResponseAsync(JsonRpcResponse.FromError(requestId, JsonRpcErrorCodes.InvalidRequest, "Invalid JSON-RPC version"));
                return;
            }

            var response = await _mcpServer.HandleRequestAsync(request);
            await WriteResponseAsync(response);

            // Exit on shutdown notification
            if (request.Method == "shutdown")
            {
                _isRunning = false;
            }
        }
        catch (JsonException ex)
        {
            await WriteResponseAsync(JsonRpcResponse.FromError(requestId, JsonRpcErrorCodes.ParseError, $"Parse error: {ex.Message}"));
        }
        catch (Exception ex)
        {
            await WriteResponseAsync(JsonRpcResponse.FromError(requestId, JsonRpcErrorCodes.InternalError, $"Internal error: {ex.Message}"));
        }
    }

    /// <summary>
    /// Write a JSON-RPC response to stdout
    /// </summary>
    private async Task WriteResponseAsync(JsonRpcResponse response)
    {
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        await Console.Out.WriteLineAsync(json);
    }

    public void Stop()
    {
        _isRunning = false;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        Stop();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
