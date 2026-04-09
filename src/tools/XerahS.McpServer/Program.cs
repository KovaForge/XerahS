using XerahS.McpServer.Server;
using XerahS.McpServer.Transport;

namespace XerahS.McpServer;

/// <summary>
/// Entry point for XerahS MCP Server
/// 
/// Usage:
///   xerahs-mcp --mcp                    Run in MCP server mode (stdio transport)
///   xerahs-mcp --mcp-server --transport http --port 7890   Run in MCP server mode (HTTP transport)
///   xerahs-mcp                          Run normal XerahS application
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Check for MCP mode flag
        var mcpMode = args.Contains("--mcp") || args.Contains("--mcp-server");

        if (mcpMode)
        {
            // Check for transport type
            var transportIndex = Array.IndexOf(args, "--transport");
            var transport = transportIndex >= 0 && transportIndex < args.Length - 1
                ? args[transportIndex + 1].ToLowerInvariant()
                : "stdio";

            // Parse port if specified
            var portIndex = Array.IndexOf(args, "--port");
            var port = portIndex >= 0 && portIndex < args.Length - 1
                && int.TryParse(args[portIndex + 1], out var parsedPort)
                ? parsedPort
                : 7890;

            if (transport == "http")
            {
                return await RunMcpHttpServerAsync(port);
            }
            else
            {
                return await RunMcpStdioServerAsync();
            }
        }
        else
        {
            return await RunNormalAppAsync(args);
        }
    }

    /// <summary>
    /// Run as MCP server with stdio transport
    /// </summary>
    private static async Task<int> RunMcpStdioServerAsync()
    {
        try
        {
            var mcpServer = new XerahSMcpServer();
            
            using var stdioServer = new StdioServer(mcpServer);
            
            // Handle Ctrl+C gracefully
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            await stdioServer.RunAsync(cts.Token);
            
            return 0;
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"MCP Server error: {ex}");
            return 1;
        }
    }

    /// <summary>
    /// Run as MCP server with HTTP transport
    /// </summary>
    private static async Task<int> RunMcpHttpServerAsync(int port)
    {
        try
        {
            var mcpServer = new XerahSMcpServer();
            
            using var httpServer = new HttpServer(mcpServer, port);
            
            // Handle Ctrl+C gracefully
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            Console.WriteLine($"XerahS MCP HTTP Server starting on port {port}...");
            Console.WriteLine($"  POST /mcp/       - JSON-RPC requests");
            Console.WriteLine($"  GET  /mcp/events/ - SSE stream");
            Console.WriteLine($"  GET  /health     - Health check");
            Console.WriteLine("Press Ctrl+C to stop.");

            await httpServer.StartAsync(cts.Token);
            
            return 0;
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
            Console.WriteLine("\nServer stopped.");
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"MCP HTTP Server error: {ex}");
            return 1;
        }
    }

    /// <summary>
    /// Run normal XerahS application
    /// </summary>
    private static async Task<int> RunNormalAppAsync(string[] args)
    {
        // STUB: Delegate to normal XerahS application entry point
        // In real implementation, this would call the existing XerahS.Program.Main
        
        Console.WriteLine("XerahS MCP Server - Normal mode not implemented in this stub");
        Console.WriteLine("Use --mcp flag to run in MCP server mode");
        
        // For now, just show help
        Console.WriteLine(@"
Usage: xerahs-mcp [options]

Options:
  --mcp, --mcp-server    Run in MCP server mode (stdio transport)
  --transport <type>     Transport type: stdio (default) or http
  --port <number>        HTTP server port (default: 7890, only with --transport http)
  --help                 Show this help message

MCP Server Mode:
  When running with --mcp, the server communicates via JSON-RPC 2.0 over stdio.
  This allows AI agents to invoke XerahS tools directly.

HTTP Transport Mode (Phase 2):
  xerahs-mcp --mcp-server --transport http --port 7890
  
  Endpoints:
    POST /mcp/       - JSON-RPC requests
    GET  /mcp/events/ - SSE stream for notifications
    GET  /health     - Health check
");

        return 0;
    }
}
