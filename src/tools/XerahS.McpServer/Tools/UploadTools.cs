namespace XerahS.McpServer.Tools;

/// <summary>
/// Upload tools for MCP server
/// </summary>
public class UploadTools
{
    public string[] GetToolDefinitionsJson() => [
        @"{
          ""name"": ""upload_file"",
          ""title"": ""Upload File"",
          ""description"": ""Uploads a file to the configured default (or specified) upload destination."",
          ""inputSchema"": {
            ""type"": ""object"",
            ""properties"": {
              ""file_path"": {
                ""type"": ""string"",
                ""description"": ""Absolute path to the file to upload"",
                ""required"": true
              },
              ""destination"": {
                ""type"": ""string"",
                ""description"": ""Destination ID (e.g. 'imgur', 'imgur_anon', 'dropbox'). Uses default if omitted.""
              }
            },
            ""required"": [""file_path""]
          }
        }",
        @"{
          ""name"": ""upload_clipboard"",
          ""title"": ""Upload Clipboard"",
          ""description"": ""Reads the current clipboard contents (image or text) and uploads to the configured destination."",
          ""inputSchema"": {
            ""type"": ""object"",
            ""properties"": {
              ""destination"": {
                ""type"": ""string"",
                ""description"": ""Destination ID. Uses default if omitted.""
              }
            }
          }
        }"
    ];

    public Task<string> UploadFileAsync(string? filePath, string? destination)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return Task.FromResult(@"{ ""error"": ""file_path is required"" }");
        }

        // STUB: needs integration with uploaders
        var fileName = Path.GetFileName(filePath);
        long fileSize = 0;
        try { if (File.Exists(filePath)) fileSize = new FileInfo(filePath).Length; } catch { }

        return Task.FromResult($@"{{
          ""url"": ""https://example.com/upload/{Guid.NewGuid()}"",
          ""filename"": ""{fileName}"",
          ""size_bytes"": {fileSize},
          ""destination"": ""{destination ?? "default"}"",
          ""note"": ""STUB: needs integration with uploaders""
        }}");
    }

    public Task<string> UploadClipboardAsync(string? destination)
    {
        // STUB: needs integration with clipboard and uploaders
        return Task.FromResult($@"{{
          ""url"": ""https://example.com/upload/{Guid.NewGuid()}"",
          ""filename"": ""clipboard_upload.png"",
          ""size_bytes"": 10240,
          ""content_type"": ""image/png"",
          ""destination"": ""{destination ?? "default"}"",
          ""note"": ""STUB: needs integration with clipboard and uploaders""
        }}");
    }
}
