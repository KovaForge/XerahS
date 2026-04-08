using System.Text.Json;
using System.Text.Json.Nodes;
using XerahS.McpServer.JsonRpc;
using XerahS.McpServer.Tools;
using XerahS.McpServer.Resources;
using XerahS.McpServer.Prompts;

namespace XerahS.McpServer.Server;

/// <summary>
/// Main MCP server class handling JSON-RPC requests
/// </summary>
public class XerahSMcpServer
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

    public XerahSMcpServer()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        _captureTools = new CaptureTools();
        _annotationTools = new AnnotationTools();
        _uploadTools = new UploadTools();
        _historyTools = new HistoryTools();
        _settingsTools = new SettingsTools();
        _historyResourceProvider = new HistoryResourceProvider();
        _settingsResourceProvider = new SettingsResourceProvider();
        _workflowResourceProvider = new WorkflowResourceProvider();
    }

    /// <summary>
    /// Handle a JSON-RPC request
    /// </summary>
    public Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request)
    {
        return request.Method switch
        {
            "initialize" => HandleInitializeAsync(request),
            "initialized" => HandleInitializedAsync(request),
            "tools/list" => HandleToolsListAsync(request),
            "tools/call" => HandleToolsCallAsync(request),
            "resources/list" => HandleResourcesListAsync(request),
            "resources/read" => HandleResourcesReadAsync(request),
            "prompts/list" => HandlePromptsListAsync(request),
            "shutdown" => HandleShutdownAsync(request),
            _ => Task.FromResult(JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.MethodNotFound, $"Method not found: {request.Method}"))
        };
    }

    private Task<JsonRpcResponse> HandleInitializeAsync(JsonRpcRequest request)
    {
        var result = new
        {
            protocolVersion = Capabilities.ProtocolVersion,
            serverInfo = Capabilities.GetServerInfo(),
            capabilities = Capabilities.GetCapabilities()
        };

        return Task.FromResult(JsonRpcResponse.Success(request.Id, result));
    }

    private Task<JsonRpcResponse> HandleInitializedAsync(JsonRpcRequest request)
    {
        // Notification - no response needed
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

        var result = new { tools = allTools };
        return Task.FromResult(JsonRpcResponse.Success(request.Id, result));
    }

    private async Task<JsonRpcResponse> HandleToolsCallAsync(JsonRpcRequest request)
    {
        if (request.Params == null)
        {
            return JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InvalidParams, "Missing params");
        }

        JsonNode? paramsNode;
        if (request.Params is JsonNode node)
        {
            paramsNode = node;
        }
        else
        {
            var paramsJson = JsonSerializer.Serialize(request.Params, _jsonOptions);
            paramsNode = JsonNode.Parse(paramsJson);
        }

        var name = paramsNode?["name"]?.GetValue<string>();
        if (string.IsNullOrEmpty(name))
        {
            return JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InvalidParams, "Missing tool name");
        }

        var arguments = paramsNode?["arguments"];
        try
        {
            var result = await ExecuteToolAsync(name, arguments);
            return JsonRpcResponse.Success(request.Id, JsonNode.Parse(result));
        }
        catch (Exception ex)
        {
            return JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InternalError, ex.Message);
        }
    }

    private Task<string> ExecuteToolAsync(string name, JsonNode? arguments)
    {
        string? query = null, fromDate = null, toDate = null, fileType = null;
        int limit = 20;
        if (arguments != null)
        {
            query = arguments["query"]?.GetValue<string>();
            fromDate = arguments["from_date"]?.GetValue<string>();
            toDate = arguments["to_date"]?.GetValue<string>();
            fileType = arguments["file_type"]?.GetValue<string>() ?? "all";
            limit = arguments["limit"]?.GetValue<int>() ?? 20;
        }

        return name switch
        {
            "capture_region" => _captureTools.CaptureRegionAsync(
                arguments?["workflow_id"]?.GetValue<string>(),
                arguments?["monitor"]?.GetValue<int>()),
            "capture_window" => _captureTools.CaptureWindowAsync(
                arguments?["window_title"]?.GetValue<string>(),
                arguments?["include_decoration"]?.GetValue<bool>() ?? true),
            "capture_full_screen" => _captureTools.CaptureFullScreenAsync(
                arguments?["monitor"]?.GetValue<int>()),
            "capture_scrolling" => _captureTools.CaptureScrollingAsync(
                arguments?["scroll_direction"]?.GetValue<string>() ?? "down",
                arguments?["max_frames"]?.GetValue<int>() ?? 50),
            "annotate_image" => _annotationTools.AnnotateImageAsync(
                arguments?["image_path"]?.GetValue<string>(),
                arguments?["auto_save"]?.GetValue<bool>() ?? false,
                arguments?["annotations"]?.AsArray().Count ?? 0),
            "upload_file" => _uploadTools.UploadFileAsync(
                arguments?["file_path"]?.GetValue<string>(),
                arguments?["destination"]?.GetValue<string>()),
            "upload_clipboard" => _uploadTools.UploadClipboardAsync(
                arguments?["destination"]?.GetValue<string>()),
            "query_history" => _historyTools.QueryHistoryAsync(query, fromDate, toDate, fileType ?? "all", limit),
            "get_history_item" => _historyTools.GetHistoryItemAsync(
                arguments?["id"]?.GetValue<string>()),
            "list_workflows" => _settingsTools.ListWorkflowsAsync(),
            "get_settings" => _settingsTools.GetSettingsAsync(
                arguments?["category"]?.GetValue<string>()),
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

        var result = new { resources = allResources };
        return Task.FromResult(JsonRpcResponse.Success(request.Id, result));
    }

    private Task<JsonRpcResponse> HandleResourcesReadAsync(JsonRpcRequest request)
    {
        if (request.Params == null)
        {
            return Task.FromResult(JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InvalidParams, "Missing params"));
        }

        JsonNode? paramsNode;
        if (request.Params is JsonNode node)
        {
            paramsNode = node;
        }
        else
        {
            var paramsJson = JsonSerializer.Serialize(request.Params, _jsonOptions);
            paramsNode = JsonNode.Parse(paramsJson);
        }

        var uri = paramsNode?["uri"]?.GetValue<string>();
        if (string.IsNullOrEmpty(uri))
        {
            return Task.FromResult(JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InvalidParams, "Missing resource URI"));
        }

        try
        {
            var result = ReadResource(uri);
            return Task.FromResult(JsonRpcResponse.Success(request.Id, JsonNode.Parse(result)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(JsonRpcResponse.FromError(request.Id, JsonRpcErrorCodes.InternalError, ex.Message));
        }
    }

    private string ReadResource(string uri)
    {
        if (uri.StartsWith("xerahs://history/"))
            return _historyResourceProvider.ReadResourceJson(uri);

        if (uri.StartsWith("xerahs://settings/"))
            return _settingsResourceProvider.ReadResourceJson(uri);

        if (uri.StartsWith("xerahs://workflows"))
            return _workflowResourceProvider.ReadResourceJson(uri);

        throw new ArgumentException($"Unknown resource URI: {uri}");
    }

    private Task<JsonRpcResponse> HandlePromptsListAsync(JsonRpcRequest request)
    {
        var prompts = PromptTemplates.GetPrompts();
        var result = new { prompts };
        return Task.FromResult(JsonRpcResponse.Success(request.Id, result));
    }

    private Task<JsonRpcResponse> HandleShutdownAsync(JsonRpcRequest request)
    {
        var result = new { };
        return Task.FromResult(JsonRpcResponse.Success(request.Id, result));
    }
}
