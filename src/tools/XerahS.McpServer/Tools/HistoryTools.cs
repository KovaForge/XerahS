namespace XerahS.McpServer.Tools;

/// <summary>
/// History tools for MCP server
/// </summary>
public class HistoryTools
{
    public string[] GetToolDefinitionsJson() => [
        @"{
          ""name"": ""query_history"",
          ""title"": ""Query Capture History"",
          ""description"": ""Searches XerahS capture history with optional filters."",
          ""inputSchema"": {
            ""type"": ""object"",
            ""properties"": {
              ""query"": {
                ""type"": ""string"",
                ""description"": ""Free-text search (matches filename, OCR text if indexed)""
              },
              ""from_date"": {
                ""type"": ""string"",
                ""format"": ""date"",
                ""description"": ""Start date (ISO 8601)""
              },
              ""to_date"": {
                ""type"": ""string"",
                ""format"": ""date"",
                ""description"": ""End date (ISO 8601)""
              },
              ""file_type"": {
                ""type"": ""string"",
                ""enum"": [""image"", ""video"", ""text"", ""all""],
                ""default"": ""all"",
                ""description"": ""Filter by file type""
              },
              ""limit"": {
                ""type"": ""integer"",
                ""default"": 20,
                ""maximum"": 100,
                ""description"": ""Maximum number of results""
              }
            }
          }
        }",
        @"{
          ""name"": ""get_history_item"",
          ""title"": ""Get History Item"",
          ""description"": ""Retrieves full details for a specific history item."",
          ""inputSchema"": {
            ""type"": ""object"",
            ""properties"": {
              ""id"": {
                ""type"": ""string"",
                ""description"": ""History item UUID"",
                ""required"": true
              }
            },
            ""required"": [""id""]
          }
        }"
    ];

    public Task<string> QueryHistoryAsync(string? query, string? fromDate, string? toDate, string fileType, int limit)
    {
        // STUB: needs integration with HistoryManager
        var items = new List<string>();
        for (int i = 0; i < Math.Min(limit, 5); i++)
        {
            var item = string.Format(
                @"{{""id"": ""{0}"", ""file_path"": ""/home/user/Pictures/XerahS/capture_2026-04-08_{1:D3}.png"", ""thumbnail_url"": ""file:///home/user/Pictures/XerahS/thumb_capture_2026-04-08_{1:D3}.png"", ""created_at"": ""{2:O}"", ""file_size_bytes"": {3}, ""ocr_text"": ""{4}"", ""tags"": []}}",
                Guid.NewGuid(), i, DateTime.UtcNow.AddHours(-i), 102400 + (i * 1024),
                i == 0 ? "Sample extracted text" : "null");
            items.Add(item);
        }

        var result = @"{
          ""items"": [" + string.Join(", ", items) + @"],
          ""total_count"": 47,
          ""has_more"": true,
          ""note"": ""STUB: needs integration with HistoryManager""
        }";
        return Task.FromResult(result);
    }

    public Task<string> GetHistoryItemAsync(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return Task.FromResult(@"{ ""error"": ""id is required"" }");
        }

        // STUB: needs integration with HistoryManager
        var result = string.Format(@"{{
          ""id"": ""{0}"",
          ""file_path"": ""/home/user/Pictures/XerahS/capture_2026-04-08_001.png"",
          ""file_url"": ""file:///home/user/Pictures/XerahS/capture_2026-04-08_001.png"",
          ""thumbnail_path"": ""/home/user/Pictures/XerahS/thumb_capture_2026-04-08_001.png"",
          ""capture_type"": ""region"",
          ""capture_width"": 1920,
          ""capture_height"": 1080,
          ""created_at"": ""{1:O}"",
          ""file_size_bytes"": 204800,
          ""file_hash_md5"": ""a1b2c3d4e5f6..."",
          ""upload_url"": ""https://imgur.com/abc123"",
          ""ocr_text"": ""Sample extracted text"",
          ""window_title"": ""Mozilla Firefox"",
          ""application_name"": ""firefox"",
          ""tags"": [],
          ""annotations_applied"": [""arrow"", ""text""],
          ""workflow_id"": ""default-region"",
          ""note"": ""STUB: needs integration with HistoryManager""
        }}", id, DateTime.UtcNow);

        return Task.FromResult(result);
    }
}
