using System.Text.Json;
using System.Text.Json.Nodes;
using XerahS.McpServer.JsonRpc;
using XerahS.McpServer.Prompts;
using XerahS.McpServer.Resources;
using XerahS.McpServer.Runtime;
using XerahS.McpServer.Tools;

namespace XerahS.McpServer.Server;

/// <summary>
/// Main MCP server class handling JSON-RPC requests.
/// </summary>
public sealed class XerahSMcpServer
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly CaptureTools _captureTools;
    private readonly AnnotationTools _annotationTools;
    private readonly UploadTools _uploadTools;
    private readonly HistoryTools _historyTools;
    private readonly SettingsTools _settingsTools;
    private readonly HistoryResourceProvider _historyResourceProvider;
    private readonly SettingsResourceProvider _settingsResourceProvider;
    private readonly WorkflowResourceProvider _workflowResourceProvider;

    public XerahSMcpServer(IXerahSMcpRuntime runtime)
    {
        Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        _captureTools = new CaptureTools(Runtime);
        _annotationTools = new AnnotationTools(Runtime);
        _uploadTools = new UploadTools(Runtime);
        _historyTools = new HistoryTools(Runtime);
        _settingsTools = new SettingsTools(Runtime);
        _historyResourceProvider = new HistoryResourceProvider();
        _settingsResourceProvider = new SettingsResourceProvider();
        _workflowResourceProvider = new WorkflowResourceProvider();
    }

    public IXerahSMcpRuntime Runtime { get; }

    /// <summary>
    /// Handle a JSON-RPC request.
    /// </summary>
    public Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
    {
        return request.Method switch
        {
            "initialize" => HandleInitializeAsync(request),
            "initialized" => HandleInitializedAsync(request),
            "tools/list" => HandleToolsListAsync(request),
            "tools/call" => HandleToolsCallAsync(request, cancellationToken),
            "resources/list" => HandleResourcesListAsync(request),
            "resources/read" => HandleResourcesReadAsync(request, cancellationToken),
            "prompts/list" => HandlePromptsListAsync(request),
            "prompts/get" => HandlePromptsGetAsync(request),
            "shutdown" => HandleShutdownAsync(request),
            _ => Task.FromResult(JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.MethodNotFound, $"Method not found: {request.Method}"))
        };
    }

    private Task<JsonRpcResponse> HandleInitializeAsync(JsonRpcRequest request)
    {
        var result = new JsonObject
        {
            ["protocolVersion"] = Capabilities.ProtocolVersion,
            ["serverInfo"] = new JsonObject
            {
                ["name"] = Capabilities.ServerName,
                ["version"] = Runtime.ServerVersion
            },
            ["capabilities"] = JsonSerializer.SerializeToNode(Capabilities.GetCapabilities(), _jsonOptions)
        };

        return Task.FromResult(JsonRpcResponse.Success(request.Id, result));
    }

    private static Task<JsonRpcResponse> HandleInitializedAsync(JsonRpcRequest request)
    {
        return Task.FromResult(JsonRpcResponse.Success(request.Id, null));
    }

    private Task<JsonRpcResponse> HandleToolsListAsync(JsonRpcRequest request)
    {
        var allTools = new List<JsonNode>();

        foreach (var json in _captureTools.GetToolDefinitionsJson())
            allTools.Add(JsonNode.Parse(json)!);
        foreach (var json in _annotationTools.GetToolDefinitionsJson())
            allTools.Add(JsonNode.Parse(json)!);
        foreach (var json in _uploadTools.GetToolDefinitionsJson())
            allTools.Add(JsonNode.Parse(json)!);
        foreach (var json in _historyTools.GetToolDefinitionsJson())
            allTools.Add(JsonNode.Parse(json)!);
        foreach (var json in _settingsTools.GetToolDefinitionsJson())
            allTools.Add(JsonNode.Parse(json)!);

        return Task.FromResult(JsonRpcResponse.Success(request.Id, new JsonObject
        {
            ["tools"] = new JsonArray(allTools.ToArray())
        }));
    }

    private async Task<JsonRpcResponse> HandleToolsCallAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (ToParamsNode(request.Params) is not JsonObject paramsNode)
        {
            return JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InvalidParams, "Missing params");
        }

        if (!TryGetStringProperty(paramsNode, "name", out var name, out var nameError))
        {
            return JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InvalidParams, nameError);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InvalidParams, "Missing tool name");
        }

        if (!TryGetArgumentsObject(paramsNode, out var arguments, out var argumentsError))
        {
            return JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InvalidParams, argumentsError);
        }

        try
        {
            var result = await ExecuteToolAsync(name, arguments, cancellationToken);
            return JsonRpcResponse.Success(request.Id, result);
        }
        catch (ArgumentException ex)
        {
            return JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InvalidParams, ex.Message);
        }
        catch (McpUserCancelledException ex)
        {
            return JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.UserCancelled, ex.Message);
        }
        catch (FileNotFoundException ex)
        {
            return JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InvalidParams, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.ServerError, ex.Message);
        }
        catch (Exception ex)
        {
            return JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InternalError, ex.Message);
        }
    }

    private async Task<JsonObject> ExecuteToolAsync(string name, JsonObject? arguments, CancellationToken cancellationToken)
    {
        string? query = arguments?["query"]?.GetValue<string>();
        string? fromDate = arguments?["from_date"]?.GetValue<string>();
        string? toDate = arguments?["to_date"]?.GetValue<string>();
        string fileType = arguments?["file_type"]?.GetValue<string>() ?? "all";
        int limit = arguments?["limit"]?.GetValue<int>() ?? 20;

        return name switch
        {
            "capture_region" => await _captureTools.CaptureRegionAsync(
                arguments?["workflow_id"]?.GetValue<string>(),
                arguments?["monitor"]?.GetValue<int>(),
                cancellationToken),
            "capture_window" => await _captureTools.CaptureWindowAsync(
                arguments?["window_title"]?.GetValue<string>(),
                arguments?["include_decoration"]?.GetValue<bool>() ?? true,
                cancellationToken),
            "capture_full_screen" => await _captureTools.CaptureFullScreenAsync(
                arguments?["monitor"]?.GetValue<int>(),
                cancellationToken),
            "capture_scrolling" => await _captureTools.CaptureScrollingAsync(
                arguments?["scroll_direction"]?.GetValue<string>() ?? "down",
                arguments?["max_frames"]?.GetValue<int>() ?? 50,
                cancellationToken),
            "annotate_image" => await _annotationTools.AnnotateImageAsync(
                arguments?["image_path"]?.GetValue<string>(),
                arguments?["annotations"] as JsonArray,
                arguments?["auto_save"]?.GetValue<bool>() ?? true,
                cancellationToken),
            "upload_file" => await _uploadTools.UploadFileAsync(
                arguments?["file_path"]?.GetValue<string>(),
                arguments?["destination"]?.GetValue<string>(),
                cancellationToken),
            "upload_clipboard" => await _uploadTools.UploadClipboardAsync(
                arguments?["destination"]?.GetValue<string>(),
                cancellationToken),
            "query_history" => await _historyTools.QueryHistoryAsync(query, fromDate, toDate, fileType, limit, cancellationToken),
            "get_history_item" => await _historyTools.GetHistoryItemAsync(
                arguments?["id"]?.GetValue<string>(),
                cancellationToken),
            "list_workflows" => await _settingsTools.ListWorkflowsAsync(cancellationToken),
            "get_settings" => await _settingsTools.GetSettingsAsync(
                arguments?["category"]?.GetValue<string>(),
                cancellationToken),
            _ => throw new ArgumentException($"Unknown tool: {name}")
        };
    }

    private Task<JsonRpcResponse> HandleResourcesListAsync(JsonRpcRequest request)
    {
        var allResources = new List<JsonNode>();

        foreach (var json in _historyResourceProvider.GetResourceTemplatesJson())
            allResources.Add(JsonNode.Parse(json)!);
        foreach (var json in _settingsResourceProvider.GetResourceTemplatesJson())
            allResources.Add(JsonNode.Parse(json)!);
        foreach (var json in _workflowResourceProvider.GetResourceTemplatesJson())
            allResources.Add(JsonNode.Parse(json)!);

        return Task.FromResult(JsonRpcResponse.Success(request.Id, new JsonObject
        {
            ["resources"] = new JsonArray(allResources.ToArray())
        }));
    }

    private async Task<JsonRpcResponse> HandleResourcesReadAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var paramsNode = ToParamsObject(request.Params);
        if (paramsNode == null)
        {
            return JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InvalidParams, "Params must be a JSON object");
        }

        if (!TryGetStringProperty(paramsNode, "uri", out var uri, out var uriError))
        {
            return JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InvalidParams, uriError);
        }

        if (string.IsNullOrWhiteSpace(uri))
        {
            return JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InvalidParams, "Missing resource URI");
        }

        try
        {
            return JsonRpcResponse.Success(request.Id, await Runtime.ReadResourceAsync(uri, cancellationToken));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InvalidParams, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InvalidParams, ex.Message);
        }
        catch (FileNotFoundException ex)
        {
            return JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InvalidParams, ex.Message);
        }
        catch (McpUserCancelledException ex)
        {
            return JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.UserCancelled, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.ServerError, ex.Message);
        }
        catch (Exception ex)
        {
            return JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InternalError, ex.Message);
        }
    }

    private static Task<JsonRpcResponse> HandlePromptsListAsync(JsonRpcRequest request)
    {
        return Task.FromResult(JsonRpcResponse.Success(request.Id, new JsonObject
        {
            ["prompts"] = JsonSerializer.SerializeToNode(PromptTemplates.GetPrompts())!
        }));
    }

    private Task<JsonRpcResponse> HandlePromptsGetAsync(JsonRpcRequest request)
    {
        var paramsNode = ToParamsObject(request.Params);
        if (paramsNode == null)
        {
            return Task.FromResult(JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InvalidParams, "Params must be a JSON object"));
        }

        if (!TryGetStringProperty(paramsNode, "name", out var name, out var nameError))
        {
            return Task.FromResult(JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InvalidParams, nameError));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult(JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InvalidParams, "Missing prompt name"));
        }

        try
        {
            var text = PromptTemplates.GetPromptTemplate(name);
            if (paramsNode["arguments"] is JsonObject arguments)
            {
                foreach (var pair in arguments)
                {
                    var replacement = pair.Value is JsonValue value && value.TryGetValue<string>(out var stringValue)
                        ? stringValue
                        : pair.Value?.ToJsonString() ?? string.Empty;
                    text = text.Replace($"{{{{{pair.Key}}}}}", replacement, StringComparison.Ordinal);
                }
            }

            return Task.FromResult(JsonRpcResponse.Success(request.Id, new JsonObject
            {
                ["description"] = PromptTemplates.GetPrompts().FirstOrDefault(prompt => prompt.Name == name)?.Description,
                ["messages"] = new JsonArray(
                    new JsonObject
                    {
                        ["role"] = "user",
                        ["content"] = new JsonObject
                        {
                            ["type"] = "text",
                            ["text"] = text
                        }
                    })
            }));
        }
        catch (ArgumentException ex)
        {
            return Task.FromResult(JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InvalidParams, ex.Message));
        }
    }

    private static Task<JsonRpcResponse> HandleShutdownAsync(JsonRpcRequest request)
    {
        return Task.FromResult(JsonRpcResponse.Success(request.Id, new JsonObject()));
    }

    private JsonNode? ToParamsNode(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonNode node)
        {
            return node;
        }

        return JsonSerializer.SerializeToNode(value, _jsonOptions);
    }

    private JsonObject? ToParamsObject(object? value)
    {
        return ToParamsNode(value) as JsonObject;
    }

    private static bool TryGetArgumentsObject(JsonObject paramsNode, out JsonObject? arguments, out string error)
    {
        arguments = null;
        error = string.Empty;

        if (!paramsNode.TryGetPropertyValue("arguments", out var argumentsNode) || argumentsNode == null)
        {
            return true;
        }

        if (argumentsNode is JsonObject argumentsObject)
        {
            arguments = argumentsObject;
            return true;
        }

        error = "Tool arguments must be a JSON object when provided.";
        return false;
    }

    private static bool TryGetStringProperty(JsonObject paramsNode, string propertyName, out string? value, out string error)
    {
        value = null;
        error = string.Empty;

        if (!paramsNode.TryGetPropertyValue(propertyName, out var node) || node == null)
        {
            return true;
        }

        if (node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var stringValue))
        {
            value = stringValue;
            return true;
        }

        error = $"Param '{propertyName}' must be a string when provided.";
        return false;
    }
}
