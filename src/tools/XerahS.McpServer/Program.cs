using XerahS.McpServer.Server;
using XerahS.McpServer.Runtime;
using XerahS.McpServer.Transport;

namespace XerahS.McpServer;

/// <summary>
/// Entry point for the dedicated XerahS MCP server executable.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var mcpMode = args.Contains("--mcp") || args.Contains("--mcp-server");
        var showHelp = args.Contains("--help") || args.Contains("-h");

        if (showHelp || !mcpMode)
        {
            PrintHelp();
            return showHelp ? 0 : 1;
        }

        var transportIndex = Array.IndexOf(args, "--transport");
        var transport = transportIndex >= 0 && transportIndex < args.Length - 1
            ? args[transportIndex + 1].ToLowerInvariant()
            : "stdio";

        var portIndex = Array.IndexOf(args, "--port");
        var port = portIndex >= 0 && portIndex < args.Length - 1
            && int.TryParse(args[portIndex + 1], out var parsedPort)
            ? parsedPort
            : 7890;

        return transport switch
        {
            "stdio" => await RunMcpStdioServerAsync(),
            "http" => await RunMcpHttpServerAsync(port),
            _ => throw new ArgumentException($"Unsupported transport '{transport}'. Expected 'stdio' or 'http'.")
        };
    }

    private static async Task<int> RunMcpStdioServerAsync()
    {
        try
        {
            var runtime = new XerahSMcpRuntime();
            var mcpServer = new XerahSMcpServer(runtime);
            using var stdioServer = new StdioServer(mcpServer);
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
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"MCP Server error: {ex}");
            return 1;
        }
    }

    private static async Task<int> RunMcpHttpServerAsync(int port)
    {
        try
        {
            var runtime = new XerahSMcpRuntime();
            var mcpServer = new XerahSMcpServer(runtime);
            using var httpServer = new HttpServer(mcpServer, runtime, port);
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
            Console.WriteLine("Server stopped.");
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"MCP HTTP Server error: {ex}");
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
Usage: xerahs-mcp [options]

Options:
  --mcp, --mcp-server    Run in MCP server mode (stdio transport)
  --transport <type>     Transport type: stdio (default) or http
  --port <number>        HTTP server port (default: 7890, only with --transport http)
  --help                 Show this help message
""");
    }
}
