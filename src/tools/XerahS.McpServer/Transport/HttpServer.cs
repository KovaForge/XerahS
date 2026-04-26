using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using XerahS.McpServer.JsonRpc;
using XerahS.McpServer.Runtime;
using XerahS.McpServer.Server;

namespace XerahS.McpServer.Transport;

/// <summary>
/// HTTP + SSE transport for MCP server
/// POST /mcp/ - JSON-RPC requests
/// GET /mcp/events/ - SSE stream
/// </summary>
public class HttpServer : IDisposable
{
    private readonly XerahSMcpServer _mcpServer;
    private readonly IXerahSMcpRuntime _runtime;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly int _port;
    private WebApplication? _app;
    private CancellationTokenSource? _cts;
    private bool _isDisposed;

    private const string McpPath = "/mcp/";
    private const string EventsPath = "/mcp/events/";

    public HttpServer(XerahSMcpServer mcpServer, IXerahSMcpRuntime runtime, int port = 7890)
    {
        _mcpServer = mcpServer ?? throw new ArgumentNullException(nameof(mcpServer));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _port = port;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Start the HTTP server
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var builder = WebApplication.CreateSlimBuilder();

        builder.WebHost.UseKestrel(options =>
        {
            options.ListenAnyIP(_port);
        });

        _app = builder.Build();

        // CORS for SSE endpoint
        _app.Use(async (context, next) =>
        {
            // CORS headers for SSE endpoint
            if (context.Request.Path.StartsWithSegments(EventsPath) ||
                context.Request.Path.StartsWithSegments("/mcp"))
            {
                context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
                context.Response.Headers.Append("Access-Control-Allow-Headers", "Authorization, Content-Type");
                context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            }

            if (context.Request.Method == "OPTIONS")
            {
                context.Response.StatusCode = 204;
                return;
            }

            await next();
        });

        // POST /mcp/ - JSON-RPC requests
        _app.MapPost(McpPath, HandleJsonRpcAsync);

        // GET /mcp/events/ - SSE stream
        _app.MapGet(EventsPath, HandleSseAsync);

        // Health check
        _app.MapGet("/health", () => Results.Ok(new { status = "ok", server = "xerahs-mcp" }));

        await _app.RunAsync(_cts.Token);
    }

    /// <summary>
    /// Stop the HTTP server
    /// </summary>
    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_app != null)
        {
            await _app.DisposeAsync();
            _app = null;
        }
    }

    /// <summary>
    /// Handle JSON-RPC POST request
    /// </summary>
    private async Task HandleJsonRpcAsync(HttpContext context)
    {
        // Validate Authorization header
        if (!await ValidateAuthorizationAsync(context))
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            var error = JsonRpcResponse.FromError(null, JsonRpcErrorCodes.ServerError, "Invalid or missing API key");
            await context.Response.WriteAsync(JsonSerializer.Serialize(error, _jsonOptions));
            return;
        }

        try
        {
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(body))
            {
                context.Response.StatusCode = 400;
                return;
            }

            var request = JsonSerializer.Deserialize<JsonRpcRequest>(body, _jsonOptions);

            if (request == null || request.JsonRpc != "2.0")
            {
                context.Response.StatusCode = 400;
                context.Response.ContentType = "application/json";
                var error = JsonRpcResponse.FromError(null, JsonRpcErrorCodes.InvalidRequest, "Invalid JSON-RPC request");
                await context.Response.WriteAsync(JsonSerializer.Serialize(error, _jsonOptions));
                return;
            }

            var response = await _mcpServer.HandleRequestAsync(request, context.RequestAborted);

            if (request.IsNotification)
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(response, _jsonOptions));
        }
        catch (JsonException ex)
        {
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            var error = JsonRpcResponse.FromError(null, JsonRpcErrorCodes.ParseError, $"Parse error: {ex.Message}");
            await context.Response.WriteAsync(JsonSerializer.Serialize(error, _jsonOptions));
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            var error = JsonRpcResponse.FromError(null, JsonRpcErrorCodes.InternalError, $"Internal error: {ex.Message}");
            await context.Response.WriteAsync(JsonSerializer.Serialize(error, _jsonOptions));
        }
    }

    /// <summary>
    /// Handle SSE GET request
    /// </summary>
    private async Task HandleSseAsync(HttpContext context)
    {
        // Validate Authorization header
        if (!await ValidateAuthorizationAsync(context))
        {
            context.Response.StatusCode = 401;
            return;
        }

        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");
        context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        context.Response.Headers.Append("Access-Control-Allow-Headers", "Authorization, Content-Type");

        await using var stream = new SseStream(context.Response.Body, context.RequestAborted);

        // Send heartbeat every 30 seconds to keep connection alive
        using var heartbeatTimer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        // Send initial initialized notification
        await stream.SendInitializedAsync();

        try
        {
            // Keep connection alive and send heartbeats
            while (!context.RequestAborted.IsCancellationRequested)
            {
                await heartbeatTimer.WaitForNextTickAsync(context.RequestAborted);
                await stream.SendHeartbeatAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected
        }
    }

    /// <summary>
    /// Validate the Authorization Bearer token
    /// </summary>
    private async Task<bool> ValidateAuthorizationAsync(HttpContext context)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();

        if (string.IsNullOrEmpty(authHeader))
        {
            return false;
        }

        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        var configuredApiKey = await _runtime.GetApiKeyAsync(context.RequestAborted);
        return !string.IsNullOrWhiteSpace(configuredApiKey) &&
               string.Equals(token, configuredApiKey, StringComparison.Ordinal);
    }

    /// <summary>
    /// Generate a new API key (32 characters)
    /// </summary>
    public static string GenerateApiKey()
    {
        var bytes = new byte[24];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)[..32];
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _cts?.Cancel();
        _cts?.Dispose();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
