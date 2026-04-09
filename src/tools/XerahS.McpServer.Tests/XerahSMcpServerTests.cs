using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace XerahS.McpServer.Tests;

public class XerahSMcpServerTests
{
    private static readonly string ServerPath = "/home/majk/Documents/GitHub/XerahS/src/tools/XerahS.McpServer/bin/Release/net10.0/linux-x64/xerahs-mcp";

    [Fact]
    public async Task Initialize_ReturnsProtocolVersion()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "/home/majk/.dotnet/dotnet",
            Arguments = $"exec --roll-forward LatestMajor {ServerPath}",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi);
        if (process == null) throw new Exception("Failed to start process");

        try
        {
            var initializeRequest = JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new { } });
            await process.StandardInput.WriteLineAsync(initializeRequest);
            await process.StandardInput.FlushAsync();

            var response = await process.StandardOutput.ReadLineAsync();
            Assert.NotNull(response);

            var json = JsonSerializer.Deserialize<JsonElement>(response);
            Assert.Equal("2.0", json.GetProperty("jsonrpc").GetString());
            Assert.True(json.TryGetProperty("result", out var result));
            Assert.Equal("2024-11-05", result.GetProperty("protocolVersion").GetString());
        }
        finally
        {
            process.Kill();
            await process.WaitForExitAsync();
        }
    }

    [Fact]
    public async Task ToolsList_ReturnsExpectedTools()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "/home/majk/.dotnet/dotnet",
            Arguments = $"exec --roll-forward LatestMajor {ServerPath}",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi);
        if (process == null) throw new Exception("Failed to start process");

        try
        {
            // Initialize first
            var initRequest = JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new { } });
            await process.StandardInput.WriteLineAsync(initRequest);
            await process.StandardInput.FlushAsync();
            await process.StandardOutput.ReadLineAsync(); // consume initialize response

            // Send tools/list request
            var toolsRequest = JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 2, method = "tools/list", @params = new { } });
            await process.StandardInput.WriteLineAsync(toolsRequest);
            await process.StandardInput.FlushAsync();

            var response = await process.StandardOutput.ReadLineAsync();
            Assert.NotNull(response);

            var json = JsonSerializer.Deserialize<JsonElement>(response);
            Assert.True(json.TryGetProperty("result", out var result));
            Assert.True(result.GetProperty("tools").GetArrayLength() > 0);
        }
        finally
        {
            process.Kill();
            await process.WaitForExitAsync();
        }
    }
}
