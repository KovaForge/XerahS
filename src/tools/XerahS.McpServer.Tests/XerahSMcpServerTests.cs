using System.Text.Json.Nodes;
using XerahS.History;
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
    public async Task PromptsGet_UnknownTemplate_ReturnsInvalidParamsError()
    {
        var server = new XerahSMcpServer(new FakeRuntime());

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 99,
            Method = "prompts/get",
            Params = JsonNode.Parse(/* lang=json */ """{ "name": "nonexistent_prompt" }""")
        });

        Assert.NotNull(response.Error);
        Assert.Equal(JsonRpcErrorCodes.InvalidParams, response.Error!.Code);
        Assert.Contains("Unknown prompt template", response.Error.Message);
    }

    [Fact]
    public async Task HeadlessMcpUIService_ShowEditorAsync_ReturnsNullWhenEditorUnavailable()
    {
        var service = new HeadlessMcpUIService();
        using var image = new SkiaSharp.SKBitmap(4, 4);

        var result = await service.ShowEditorAsync(image, taskMode: true);

        Assert.Null(result);
    }

    [Fact]
    public void RuntimeHistoryBlobPath_PrefersLocalThumbnailFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-mcp-history-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string sourcePath = Path.Combine(directory, "capture.png");
            string thumbnailPath = Path.Combine(directory, "thumb.png");
            File.WriteAllText(sourcePath, "source");
            File.WriteAllText(thumbnailPath, "thumb");

            var item = new HistoryItem
            {
                FilePath = sourcePath,
                ThumbnailURL = thumbnailPath
            };

            Assert.Equal(thumbnailPath, XerahSMcpRuntime.ResolveHistoryBlobPath(item));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RuntimeHistoryBlobPath_IgnoresRemoteThumbnailUrl()
    {
        string sourcePath = Path.Combine(Path.GetTempPath(), $"xerahs-mcp-history-{Guid.NewGuid():N}.txt");
        File.WriteAllText(sourcePath, "source");
        try
        {
            var item = new HistoryItem
            {
                FilePath = sourcePath,
                ThumbnailURL = "https://example.test/thumb.png"
            };

            Assert.Equal(sourcePath, XerahSMcpRuntime.ResolveHistoryBlobPath(item));
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Fact]
    public void RuntimeFileUrl_UsesAbsoluteFileUriForRelativePaths()
    {
        string relativePath = Path.Combine(".", "capture with spaces.png");
        string expected = new Uri(Path.GetFullPath(relativePath)).AbsoluteUri;

        Assert.Equal(expected, XerahSMcpRuntime.CreateFileUrl(relativePath));
    }

    [Fact]
    public void RuntimeFileUrl_PreservesLeadingAndTrailingFilePathWhitespace()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-mcp-uri-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, " capture trailing space .png ");
            File.WriteAllText(path, "source");
            string expected = new Uri(Path.GetFullPath(path)).AbsoluteUri;

            Assert.Equal(expected, XerahSMcpRuntime.CreateFileUrl(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RuntimeHistoryBlobPath_PreservesThumbnailPathWhitespace()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-mcp-history-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string sourcePath = Path.Combine(directory, "source.png");
            string thumbnailPath = Path.Combine(directory, " thumb with space .png ");
            File.WriteAllText(sourcePath, "source");
            File.WriteAllText(thumbnailPath, "thumb");

            var item = new HistoryItem
            {
                FilePath = sourcePath,
                ThumbnailURL = thumbnailPath
            };

            Assert.Equal(thumbnailPath, XerahSMcpRuntime.ResolveHistoryBlobPath(item));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RuntimeHistoryBlobResourceUri_UsesInvariantHistoryId()
    {
        var item = new HistoryItem
        {
            Id = 12345
        };

        Assert.Equal("xerahs://history/thumb/12345", XerahSMcpRuntime.CreateHistoryBlobResourceUri(item));
    }

    [Fact]
    public void RuntimeHistoryBlobTooLargeResponse_ReturnsActionableJsonTextContent()
    {
        const string uri = "xerahs://history/thumb/12345";
        const string blobPath = "/tmp/oversized-capture.png";
        const long blobSize = XerahSMcpRuntime.MaxInlineHistoryBlobBytes + 1;

        var response = XerahSMcpRuntime.CreateHistoryBlobTooLargeResponse(uri, blobPath, blobSize);

        var contents = Assert.IsType<JsonArray>(response["contents"]);
        var content = Assert.IsType<JsonObject>(contents[0]);
        Assert.Equal(uri, content["uri"]?.GetValue<string>());
        Assert.Equal("application/json", content["mimeType"]?.GetValue<string>());
        Assert.Null(content["blob"]);

        var details = JsonNode.Parse(content["text"]!.GetValue<string>()) as JsonObject;
        Assert.NotNull(details);
        Assert.Equal("history_blob_too_large", details["error"]?.GetValue<string>());
        Assert.Equal(blobPath, details["file_path"]?.GetValue<string>());
        Assert.Equal(blobSize, details["file_size_bytes"]?.GetValue<long>());
        Assert.Equal(XerahSMcpRuntime.MaxInlineHistoryBlobBytes, details["max_inline_bytes"]?.GetValue<long>());
        Assert.Contains("Open the local file path", details["message"]?.GetValue<string>());
    }

    [Fact]
    public void RuntimeHistoryBlobMissingResponse_ReturnsActionableJsonTextContent()
    {
        const string uri = "xerahs://history/thumb/12345";
        var item = new HistoryItem
        {
            Id = 12345,
            FilePath = "/tmp/moved-capture.png",
            ThumbnailURL = "/tmp/moved-thumbnail.png"
        };

        var response = XerahSMcpRuntime.CreateHistoryBlobMissingResponse(uri, item);

        var contents = Assert.IsType<JsonArray>(response["contents"]);
        var content = Assert.IsType<JsonObject>(contents[0]);
        Assert.Equal(uri, content["uri"]?.GetValue<string>());
        Assert.Equal("application/json", content["mimeType"]?.GetValue<string>());
        Assert.Null(content["blob"]);

        var details = JsonNode.Parse(content["text"]!.GetValue<string>()) as JsonObject;
        Assert.NotNull(details);
        Assert.Equal("history_blob_missing", details["error"]?.GetValue<string>());
        Assert.Equal("12345", details["history_id"]?.GetValue<string>());
        Assert.Equal(item.FilePath, details["file_path"]?.GetValue<string>());
        Assert.Equal(item.ThumbnailURL, details["thumbnail_path"]?.GetValue<string>());
        Assert.Contains("moved", details["message"]?.GetValue<string>());
    }

    [Fact]
    public async Task ResourcesRead_HistorySearch_ExtractsQueryFromQParameter()
    {
        var runtime = new TestHistoryRuntime();
        var server = new XerahSMcpServer(runtime);

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 10,
            Method = "resources/read",
            Params = new JsonObject
            {
                ["uri"] = "xerahs://history/search?q=test%20query"
            }
        });

        Assert.Null(response.Error);
        var result = Assert.IsType<JsonObject>(response.Result);
        var contents = Assert.IsType<JsonArray>(result["contents"]);
        var textContent = contents[0]?["text"]?.GetValue<string>();
        Assert.NotNull(textContent);
        var inner = JsonNode.Parse(textContent!) as JsonObject;
        Assert.NotNull(inner);
        Assert.Equal("test query", inner["lastQuery"]?.GetValue<string>());
    }

    [Fact]
    public async Task ResourcesRead_HistorySearch_HandlesAmpersandDelimiter()
    {
        var runtime = new TestHistoryRuntime();
        var server = new XerahSMcpServer(runtime);

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 11,
            Method = "resources/read",
            Params = new JsonObject
            {
                ["uri"] = "xerahs://history/search?q=hello&limit=5&from=2026-01-01"
            }
        });

        Assert.Null(response.Error);
        var result = Assert.IsType<JsonObject>(response.Result);
        var contents = Assert.IsType<JsonArray>(result["contents"]);
        var textContent = contents[0]?["text"]?.GetValue<string>();
        Assert.NotNull(textContent);
        var inner = JsonNode.Parse(textContent!) as JsonObject;
        Assert.NotNull(inner);
        Assert.Equal("hello", inner["lastQuery"]?.GetValue<string>());
        Assert.Equal(5, inner["lastLimit"]?.GetValue<int>());
        Assert.Equal("2026-01-01", inner["lastFromDate"]?.GetValue<string>());
    }

    [Fact]
    public async Task ResourcesRead_HistorySearch_DecodesPlusAsSpace()
    {
        var runtime = new TestHistoryRuntime();
        var server = new XerahSMcpServer(runtime);

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 16,
            Method = "resources/read",
            Params = new JsonObject
            {
                ["uri"] = "xerahs://history/search?q=window+capture&limit=5"
            }
        });

        Assert.Null(response.Error);
        var result = Assert.IsType<JsonObject>(response.Result);
        var contents = Assert.IsType<JsonArray>(result["contents"]);
        var textContent = contents[0]?["text"]?.GetValue<string>();
        Assert.NotNull(textContent);
        var inner = JsonNode.Parse(textContent!) as JsonObject;
        Assert.NotNull(inner);
        Assert.Equal("window capture", inner["lastQuery"]?.GetValue<string>());
        Assert.Equal(5, inner["lastLimit"]?.GetValue<int>());
    }

    [Fact]
    public async Task ResourcesRead_HistorySearch_HandlesQAfterOtherParams()
    {
        var runtime = new TestHistoryRuntime();
        var server = new XerahSMcpServer(runtime);

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 12,
            Method = "resources/read",
            Params = new JsonObject
            {
                ["uri"] = "xerahs://history/search?from=2026-01-01&q=searchterm&limit=10"
            }
        });

        Assert.Null(response.Error);
        var result = Assert.IsType<JsonObject>(response.Result);
        var contents = Assert.IsType<JsonArray>(result["contents"]);
        var textContent = contents[0]?["text"]?.GetValue<string>();
        Assert.NotNull(textContent);
        var inner = JsonNode.Parse(textContent!) as JsonObject;
        Assert.NotNull(inner);
        Assert.Equal("searchterm", inner["lastQuery"]?.GetValue<string>());
        Assert.Equal("2026-01-01", inner["lastFromDate"]?.GetValue<string>());
        Assert.Equal(10, inner["lastLimit"]?.GetValue<int>());
    }

    [Fact]
    public async Task ResourcesRead_HistorySearch_IgnoresMalformedPercentEncodedPairs()
    {
        var runtime = new TestHistoryRuntime();
        var server = new XerahSMcpServer(runtime);

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 15,
            Method = "resources/read",
            Params = new JsonObject
            {
                ["uri"] = "xerahs://history/search?q=%E0%A4%A&limit=7&from=2026-02-03"
            }
        });

        Assert.Null(response.Error);
        var result = Assert.IsType<JsonObject>(response.Result);
        var contents = Assert.IsType<JsonArray>(result["contents"]);
        var textContent = contents[0]?["text"]?.GetValue<string>();
        Assert.NotNull(textContent);
        var inner = JsonNode.Parse(textContent!) as JsonObject;
        Assert.NotNull(inner);
        Assert.Null(inner["lastQuery"]);
        Assert.Equal(7, inner["lastLimit"]?.GetValue<int>());
        Assert.Equal("2026-02-03", inner["lastFromDate"]?.GetValue<string>());
    }

    [Fact]
    public async Task ResourcesRead_HistorySearch_DoesNotMatchPrefixOnlyPaths()
    {
        var runtime = new TestHistoryRuntime();
        var server = new XerahSMcpServer(runtime);

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 17,
            Method = "resources/read",
            Params = new JsonObject
            {
                ["uri"] = "xerahs://history/searchfoo?limit=5"
            }
        });

        Assert.Null(response.Error);
        var result = Assert.IsType<JsonObject>(response.Result);
        var contents = Assert.IsType<JsonArray>(result["contents"]);
        Assert.Equal("{}", contents[0]?["text"]?.GetValue<string>());
        Assert.False(XerahSMcpRuntime.IsHistorySearchResourceUri("xerahs://history/searchfoo?limit=5"));
        Assert.True(XerahSMcpRuntime.IsHistorySearchResourceUri("xerahs://history/search?limit=5"));
    }

    [Fact]
    public async Task ResourcesRead_MapsUserCancelledToUserCancelledCode()
    {
        var runtime = new UserCancelledRuntime();
        var server = new XerahSMcpServer(runtime);

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 13,
            Method = "resources/read",
            Params = new JsonObject
            {
                ["uri"] = "xerahs://settings/general"
            }
        });

        Assert.NotNull(response.Error);
        Assert.Equal(JsonRpcErrorCodes.UserCancelled, response.Error!.Code);
    }

    [Fact]
    public async Task ResourcesRead_MapsArgumentOutOfRangeToInvalidParams()
    {
        var runtime = new ArgOutOfRangeRuntime();
        var server = new XerahSMcpServer(runtime);

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 14,
            Method = "resources/read",
            Params = new JsonObject
            {
                ["uri"] = "xerahs://settings/general"
            }
        });

        Assert.NotNull(response.Error);
        Assert.Equal(JsonRpcErrorCodes.InvalidParams, response.Error!.Code);
    }

    private sealed class TestHistoryRuntime : IXerahSMcpRuntime
    {
        public string ServerVersion => "9.9.9-test";

        public Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult("test-api-key");

        public Task<JsonObject> CaptureRegionAsync(string? workflowId, int? monitor, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<JsonObject> CaptureWindowAsync(string? windowTitle, bool includeDecoration, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<JsonObject> CaptureFullScreenAsync(int? monitor, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<JsonObject> CaptureScrollingAsync(string scrollDirection, int maxFrames, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<JsonObject> AnnotateImageAsync(string? imagePath, JsonArray? annotations, bool autoSave, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<JsonObject> UploadFileAsync(string? filePath, string? destination, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<JsonObject> UploadClipboardAsync(string? destination, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<JsonObject> QueryHistoryAsync(string? query, string? fromDate, string? toDate, string fileType, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult(new JsonObject
            {
                ["items"] = new JsonArray(),
                ["total_count"] = 0,
                ["has_more"] = false,
                ["lastQuery"] = query,
                ["lastFromDate"] = fromDate,
                ["lastToDate"] = toDate,
                ["lastLimit"] = limit
            });

        public Task<JsonObject> GetHistoryItemAsync(string? id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<JsonObject> ListWorkflowsAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<JsonObject> GetSettingsAsync(string? category, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<JsonObject> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
        {
            // Delegate to the real runtime's query parsing for history/search
            if (XerahSMcpRuntime.IsHistorySearchResourceUri(uri))
            {
                var queryStart = uri.IndexOf('?');
                string? query = null;
                string? fromDate = null;
                string? toDate = null;
                var limit = 20;

                if (queryStart >= 0)
                {
                    var queryString = uri[(queryStart + 1)..];
                    var pairs = queryString.Split('&', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var pair in pairs)
                    {
                        var eqIndex = pair.IndexOf('=');
                        if (eqIndex < 0) continue;
                        var key = XerahSMcpRuntime.DecodeResourceQueryComponent(pair[..eqIndex]);
                        var value = XerahSMcpRuntime.DecodeResourceQueryComponent(pair[(eqIndex + 1)..]);
                        if (key == null || value == null) continue;
                        if (string.Equals(key, "q", StringComparison.OrdinalIgnoreCase))
                            query = string.IsNullOrWhiteSpace(value) ? null : value;
                        else if (string.Equals(key, "from", StringComparison.OrdinalIgnoreCase))
                            fromDate = value;
                        else if (string.Equals(key, "to", StringComparison.OrdinalIgnoreCase))
                            toDate = value;
                        else if (string.Equals(key, "limit", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var p))
                            limit = p;
                    }
                }

                return Task.FromResult(new JsonObject
                {
                    ["contents"] = new JsonArray(
                        new JsonObject
                        {
                            ["uri"] = uri,
                            ["mimeType"] = "application/json",
                            ["text"] = new JsonObject
                            {
                                ["items"] = new JsonArray(),
                                ["total_count"] = 0,
                                ["has_more"] = false,
                                ["lastQuery"] = query,
                                ["lastFromDate"] = fromDate,
                                ["lastLimit"] = limit
                            }.ToJsonString()
                        })
                });
            }

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
    }

    private sealed class UserCancelledRuntime : IXerahSMcpRuntime
    {
        public string ServerVersion => "9.9.9-test";
        public Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult("test-api-key");
        public Task<JsonObject> CaptureRegionAsync(string? workflowId, int? monitor, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> CaptureWindowAsync(string? windowTitle, bool includeDecoration, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> CaptureFullScreenAsync(int? monitor, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> CaptureScrollingAsync(string scrollDirection, int maxFrames, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> AnnotateImageAsync(string? imagePath, JsonArray? annotations, bool autoSave, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> UploadFileAsync(string? filePath, string? destination, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> UploadClipboardAsync(string? destination, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> QueryHistoryAsync(string? query, string? fromDate, string? toDate, string fileType, int limit, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> GetHistoryItemAsync(string? id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> ListWorkflowsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> GetSettingsAsync(string? category, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> ReadResourceAsync(string uri, CancellationToken cancellationToken = default) =>
            throw new McpUserCancelledException("User cancelled the operation.");
    }

    private sealed class ArgOutOfRangeRuntime : IXerahSMcpRuntime
    {
        public string ServerVersion => "9.9.9-test";
        public Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult("test-api-key");
        public Task<JsonObject> CaptureRegionAsync(string? workflowId, int? monitor, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> CaptureWindowAsync(string? windowTitle, bool includeDecoration, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> CaptureFullScreenAsync(int? monitor, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> CaptureScrollingAsync(string scrollDirection, int maxFrames, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> AnnotateImageAsync(string? imagePath, JsonArray? annotations, bool autoSave, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> UploadFileAsync(string? filePath, string? destination, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> UploadClipboardAsync(string? destination, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> QueryHistoryAsync(string? query, string? fromDate, string? toDate, string fileType, int limit, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> GetHistoryItemAsync(string? id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> ListWorkflowsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> GetSettingsAsync(string? category, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> ReadResourceAsync(string uri, CancellationToken cancellationToken = default) =>
            throw new ArgumentOutOfRangeException("uri", "The specified value is out of range.");
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
