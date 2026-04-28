using System.Text.Json.Nodes;
using XerahS.McpServer.JsonRpc;
using XerahS.McpServer.Runtime;
using XerahS.McpServer.Server;
using Xunit;

namespace XerahS.McpServer.Tests;

public class XerahSMcpServerTests
{
    [Fact]
    public async Task Initialize_ReturnsProtocolVersionAndRuntimeVersion()
    {
        var runtime = new FakeRuntime();
        var server = new XerahSMcpServer(runtime);

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 1,
            Method = "initialize",
            Params = new { }
        });

        var result = Assert.IsType<JsonObject>(response.Result);
        Assert.Equal("2024-11-05", result["protocolVersion"]?.GetValue<string>());
        Assert.Equal(runtime.ServerVersion, result["serverInfo"]?["version"]?.GetValue<string>());
    }

    [Fact]
    public async Task ToolsList_ContainsImplementedTools()
    {
        var server = new XerahSMcpServer(new FakeRuntime());

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 2,
            Method = "tools/list",
            Params = new { }
        });

        var result = Assert.IsType<JsonObject>(response.Result);
        var tools = Assert.IsType<JsonArray>(result["tools"]);
        Assert.Contains(tools, node => node?["name"]?.GetValue<string>() == "capture_region");
        Assert.Contains(tools, node => node?["name"]?.GetValue<string>() == "annotate_image");
        Assert.Contains(tools, node => node?["name"]?.GetValue<string>() == "upload_file");
        Assert.Contains(tools, node => node?["name"]?.GetValue<string>() == "query_history");
        Assert.Contains(tools, node => node?["name"]?.GetValue<string>() == "get_settings");
    }

    [Fact]
    public async Task ToolsCall_DelegatesToRuntime()
    {
        var runtime = new FakeRuntime();
        var server = new XerahSMcpServer(runtime);

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 3,
            Method = "tools/call",
            Params = JsonNode.Parse("""
            {
              "name": "capture_full_screen",
              "arguments": {
                "monitor": 1
              }
            }
            """)
        });

        Assert.Null(response.Error);
        var result = Assert.IsType<JsonObject>(response.Result);
        Assert.Equal("capture_full_screen", result["tool"]?.GetValue<string>());
        Assert.Equal(1, result["monitor"]?.GetValue<int>());
        Assert.Equal("capture_full_screen", runtime.LastCall);
    }

    [Fact]
    public async Task ResourcesRead_DelegatesToRuntime()
    {
        var runtime = new FakeRuntime();
        var server = new XerahSMcpServer(runtime);

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 4,
            Method = "resources/read",
            Params = JsonNode.Parse("""
            {
              "uri": "xerahs://settings/general"
            }
            """)
        });

        Assert.Null(response.Error);
        var result = Assert.IsType<JsonObject>(response.Result);
        Assert.Equal("xerahs://settings/general", result["contents"]?[0]?["uri"]?.GetValue<string>());
        Assert.Equal("xerahs://settings/general", runtime.LastResourceUri);
    }

    [Fact]
    public async Task ToolsCall_RejectsNonObjectArguments()
    {
        var server = new XerahSMcpServer(new FakeRuntime());

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 5,
            Method = "tools/call",
            Params = JsonNode.Parse("""
            {
              "name": "capture_full_screen",
              "arguments": [1, 2, 3]
            }
            """)
        });

        Assert.NotNull(response.Error);
        Assert.Equal(JsonRpcErrorCodes.InvalidParams, response.Error!.Code);
        Assert.Contains("JSON object", response.Error.Message);
    }

    [Theory]
    [InlineData("tools/call", "name")]
    [InlineData("resources/read", "uri")]
    [InlineData("prompts/get", "name")]
    public async Task RequiredStringParamMethods_RejectNonStringValuesAsInvalidParams(string method, string propertyName)
    {
        var server = new XerahSMcpServer(new FakeRuntime());

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 8,
            Method = method,
            Params = new JsonObject
            {
                [propertyName] = 123
            }
        });

        Assert.NotNull(response.Error);
        Assert.Equal(JsonRpcErrorCodes.InvalidParams, response.Error!.Code);
        Assert.Contains("must be a string", response.Error.Message);
    }


    [Theory]
    [InlineData("resources/read")]
    [InlineData("prompts/get")]
    public async Task ObjectParamMethods_RejectArrayParamsAsInvalidParams(string method)
    {
        var server = new XerahSMcpServer(new FakeRuntime());

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 7,
            Method = method,
            Params = JsonNode.Parse("[]")
        });

        Assert.NotNull(response.Error);
        Assert.Equal(JsonRpcErrorCodes.InvalidParams, response.Error!.Code);
        Assert.Contains("JSON object", response.Error.Message);
    }

    [Fact]
    public async Task PromptsGet_RendersTemplateArguments()
    {
        var server = new XerahSMcpServer(new FakeRuntime());

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 6,
            Method = "prompts/get",
            Params = JsonNode.Parse("""
            {
              "name": "upload_workflow",
              "arguments": {
                "user_request_describing_what_to_capture_and_annotate": "Capture the browser window",
                "destination_id_or_default": "imgur"
              }
            }
            """)
        });

        Assert.Null(response.Error);
        var result = Assert.IsType<JsonObject>(response.Result);
        var text = result["messages"]?[0]?["content"]?["text"]?.GetValue<string>();
        Assert.Contains("Capture the browser window", text);
        Assert.Contains("imgur", text);
    }

    [Fact]
    public async Task HeadlessMcpUIService_ShowEditorAsync_ReturnsNullWhenEditorUnavailable()
    {
        var service = new HeadlessMcpUIService();
        using var image = new SkiaSharp.SKBitmap(4, 4);

        var result = await service.ShowEditorAsync(image, taskMode: true);

        Assert.Null(result);
    }

    private sealed class FakeRuntime : IXerahSMcpRuntime
    {
        public string ServerVersion => "9.9.9-test";
        public string? LastCall { get; private set; }
        public string? LastResourceUri { get; private set; }

        public Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult("test-api-key");

        public Task<JsonObject> CaptureRegionAsync(string? workflowId, int? monitor, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result("capture_region", workflowId, monitor));

        public Task<JsonObject> CaptureWindowAsync(string? windowTitle, bool includeDecoration, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result("capture_window", windowTitle, includeDecoration));

        public Task<JsonObject> CaptureFullScreenAsync(int? monitor, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result("capture_full_screen", monitor));

        public Task<JsonObject> CaptureScrollingAsync(string scrollDirection, int maxFrames, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result("capture_scrolling", scrollDirection, maxFrames));

        public Task<JsonObject> AnnotateImageAsync(string? imagePath, JsonArray? annotations, bool autoSave, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result("annotate_image", imagePath, autoSave, annotations?.Count));

        public Task<JsonObject> UploadFileAsync(string? filePath, string? destination, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result("upload_file", filePath, destination));

        public Task<JsonObject> UploadClipboardAsync(string? destination, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result("upload_clipboard", destination));

        public Task<JsonObject> QueryHistoryAsync(string? query, string? fromDate, string? toDate, string fileType, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result("query_history", query, fromDate, toDate, fileType, limit));

        public Task<JsonObject> GetHistoryItemAsync(string? id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result("get_history_item", id));

        public Task<JsonObject> ListWorkflowsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result("list_workflows"));

        public Task<JsonObject> GetSettingsAsync(string? category, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result("get_settings", category));

        public Task<JsonObject> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
        {
            LastResourceUri = uri;
            return Task.FromResult(new JsonObject
            {
                ["contents"] = new JsonArray(
                    new JsonObject
                    {
                        ["uri"] = uri,
                        ["mimeType"] = "application/json",
                        ["text"] = "{}"
                    })
            });
        }

        private JsonObject Result(string call, object? arg1 = null, object? arg2 = null, object? arg3 = null, object? arg4 = null, object? arg5 = null)
        {
            LastCall = call;
            var result = new JsonObject
            {
                ["tool"] = call
            };

            if (arg1 is not null)
            {
                result["arg1"] = JsonValue.Create(arg1.ToString());
            }

            if (arg2 is not null)
            {
                result["arg2"] = JsonValue.Create(arg2.ToString());
            }

            if (arg3 is not null)
            {
                result["arg3"] = JsonValue.Create(arg3.ToString());
            }

            if (arg4 is not null)
            {
                result["arg4"] = JsonValue.Create(arg4.ToString());
            }

            if (arg5 is not null)
            {
                result["arg5"] = JsonValue.Create(arg5.ToString());
            }

            if (call == "capture_full_screen" && arg1 is int monitor)
            {
                result["monitor"] = monitor;
            }

            return result;
        }
    }
}
