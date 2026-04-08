namespace XerahS.McpServer.Resources;

/// <summary>
/// Provides history-related MCP resources
/// </summary>
public class HistoryResourceProvider
{
    public string[] GetResourceTemplatesJson() => [
        @"{
          ""uri"": ""xerahs://history/{uuid}"",
          ""name"": ""History Item"",
          ""mimeType"": ""application/json"",
          ""description"": ""Single history item metadata""
        }",
        @"{
          ""uri"": ""xerahs://history/thumb/{uuid}"",
          ""name"": ""History Item Thumbnail"",
          ""mimeType"": ""image/png"",
          ""description"": ""Thumbnail image for a history item""
        }",
        @"{
          ""uri"": ""xerahs://history/search?q={query}"",
          ""name"": ""History Search"",
          ""mimeType"": ""application/json"",
          ""description"": ""Search history by query""
        }",
        @"{
          ""uri"": ""xerahs://capture/latest"",
          ""name"": ""Latest Capture"",
          ""mimeType"": ""application/json"",
          ""description"": ""Most recent capture metadata""
        }"
    ];

    public string ReadResourceJson(string uri)
    {
        // STUB: needs integration with HistoryManager

        if (uri.StartsWith("xerahs://history/"))
        {
            var path = uri.Substring("xerahs://history/".Length);

            if (path.StartsWith("thumb/"))
            {
                var uuid = path.Substring("thumb/".Length);
                return $@"{{
                  ""uri"": ""{uri}"",
                  ""mimeType"": ""image/png"",
                  ""blob"": ""<binary thumbnail data for {uuid}>"",
                  ""note"": ""STUB: needs integration with HistoryManager""
                }}";
            }

            if (path == "search")
            {
                return @"{
                  ""uri"": ""xerahs://history/search"",
                  ""mimeType"": ""application/json"",
                  ""text"": ""{ \""results\"": [] }"",
                  ""note"": ""STUB: needs integration with HistoryManager""
                }";
            }

            var id = path.Split('/').FirstOrDefault() ?? path;
            return $@"{{
              ""uri"": ""{uri}"",
              ""mimeType"": ""application/json"",
              ""text"": ""{{ \""id\"": \""{id}\"", \""file_path\"": \""/home/user/Pictures/XerahS/capture_2026-04-08_001.png\"", \""file_url\"": \""file:///home/user/Pictures/XerahS/capture_2026-04-08_001.png\"", \""thumbnail_path\"": \""/home/user/Pictures/XerahS/thumb_capture_2026-04-08_001.png\"", \""capture_type\"": \""region\"", \""capture_width\"": 1920, \""capture_height\"": 1080, \""created_at\"": \""{DateTime.UtcNow:O}\"", \""file_size_bytes\"": 204800 }} "",
              ""note"": ""STUB: needs integration with HistoryManager""
            }}";
        }

        if (uri == "xerahs://capture/latest")
        {
            return $@"{{
              ""uri"": ""{uri}"",
              ""mimeType"": ""application/json"",
              ""text"": ""{{ \""id\"": \""{Guid.NewGuid()}\"", \""file_path\"": \""/home/user/Pictures/XerahS/capture_2026-04-08_latest.png\"", \""created_at\"": \""{DateTime.UtcNow:O}\"", \""capture_type\"": \""region\"" }}"",
              ""note"": ""STUB: needs integration with HistoryManager""
            }}";
        }

        throw new ArgumentException($"Unknown history resource URI: {uri}");
    }
}
