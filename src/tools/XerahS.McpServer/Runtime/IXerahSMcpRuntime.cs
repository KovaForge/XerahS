using System.Text.Json.Nodes;

namespace XerahS.McpServer.Runtime;

public interface IXerahSMcpRuntime
{
    string ServerVersion { get; }
    Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default);

    Task<JsonObject> CaptureRegionAsync(string? workflowId, int? monitor, CancellationToken cancellationToken = default);
    Task<JsonObject> CaptureWindowAsync(string? windowTitle, bool includeDecoration, CancellationToken cancellationToken = default);
    Task<JsonObject> CaptureFullScreenAsync(int? monitor, CancellationToken cancellationToken = default);
    Task<JsonObject> CaptureScrollingAsync(string scrollDirection, int maxFrames, CancellationToken cancellationToken = default);

    Task<JsonObject> AnnotateImageAsync(string? imagePath, JsonArray? annotations, bool autoSave, CancellationToken cancellationToken = default);

    Task<JsonObject> UploadFileAsync(string? filePath, string? destination, CancellationToken cancellationToken = default);
    Task<JsonObject> UploadClipboardAsync(string? destination, CancellationToken cancellationToken = default);

    Task<JsonObject> QueryHistoryAsync(string? query, string? fromDate, string? toDate, string fileType, int limit, CancellationToken cancellationToken = default);
    Task<JsonObject> GetHistoryItemAsync(string? id, CancellationToken cancellationToken = default);

    Task<JsonObject> ListWorkflowsAsync(CancellationToken cancellationToken = default);
    Task<JsonObject> GetSettingsAsync(string? category, CancellationToken cancellationToken = default);

    Task<JsonObject> ReadResourceAsync(string uri, CancellationToken cancellationToken = default);
}
