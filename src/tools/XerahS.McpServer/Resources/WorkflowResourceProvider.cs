namespace XerahS.McpServer.Resources;

/// <summary>
/// Provides workflow-related MCP resources
/// </summary>
public class WorkflowResourceProvider
{
    public string[] GetResourceTemplatesJson() => [
        @"{
          ""uri"": ""xerahs://workflows"",
          ""name"": ""All Workflows"",
          ""mimeType"": ""application/json"",
          ""description"": ""List of all configured workflows""
        }",
        @"{
          ""uri"": ""xerahs://workflows/{id}"",
          ""name"": ""Single Workflow"",
          ""mimeType"": ""application/json"",
          ""description"": ""Single workflow configuration""
        }"
    ];

    public string ReadResourceJson(string uri)
    {
        // STUB: needs integration with SettingsManager.WorkflowsConfig
        if (uri == "xerahs://workflows")
        {
            return @"{
              ""uri"": ""xerahs://workflows"",
              ""mimeType"": ""application/json"",
              ""workflows"": [
                {
                  ""id"": ""default-region"",
                  ""name"": ""Region Capture"",
                  ""job"": ""RectangleRegion"",
                  ""capture_mode"": ""region"",
                  ""hotkey"": ""PrintScreen"",
                  ""after_capture"": [""save_to_file""],
                  ""after_upload"": [""copy_url_to_clipboard""]
                },
                {
                  ""id"": ""default-fullscreen"",
                  ""name"": ""Full Screen"",
                  ""job"": ""PrintScreen"",
                  ""capture_mode"": ""fullscreen"",
                  ""hotkey"": ""Ctrl+PrintScreen"",
                  ""after_capture"": [""save_to_file"", ""upload""],
                  ""after_upload"": [""copy_url_to_clipboard""]
                }
              ],
              ""note"": ""STUB: needs integration with SettingsManager.WorkflowsConfig""
            }";
        }

        if (uri.StartsWith("xerahs://workflows/"))
        {
            var id = uri.Substring("xerahs://workflows/".Length);
            return $@"{{
              ""uri"": ""{uri}"",
              ""mimeType"": ""application/json"",
              ""workflow"": {{
                ""id"": ""{id}"",
                ""name"": ""Custom Workflow"",
                ""job"": ""RectangleRegion"",
                ""capture_mode"": ""region"",
                ""after_capture"": [""save_to_file""],
                ""after_upload"": [""copy_url_to_clipboard""]
              }},
              ""note"": ""STUB: needs integration with SettingsManager.WorkflowsConfig""
            }}";
        }

        throw new ArgumentException($"Unknown workflow resource URI: {uri}");
    }
}
