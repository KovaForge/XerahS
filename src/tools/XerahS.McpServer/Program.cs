using XerahS.McpServer.Server;
using XerahS.McpServer.Transport;

namespace XerahS.McpServer;

/// <summary>
/// Entry point for XerahS MCP Server
/// 
/// Usage:
///   xerahs-mcp --mcp          Run in MCP server mode (stdio transport)
///   xerahs-mcp                Run normal XerahS application
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Check for MCP mode flag
        var mcpMode = args.Contains("--mcp") || args.Contains("--mcp-server");

        if (mcpMode)
        {
            return await RunMcpServerAsync();
        }
        else
        {
            return await RunNormalAppAsync(args);
        }
    }

    /// <summary>
    /// Run as MCP server with stdio transport
    /// </summary>
    private static async Task<int> RunMcpServerAsync()
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
  --help                 Show this help message

MCP Server Mode:
  When running with --mcp, the server communicates via JSON-RPC 2.0 over stdio.
  This allows AI agents to invoke XerahS tools directly.
");

        return 0;
    }
}
