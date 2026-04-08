namespace XerahS.McpServer.Tools;

/// <summary>
/// Settings and workflow tools for MCP server
/// </summary>
public class SettingsTools
{
    public string[] GetToolDefinitionsJson() => [
        @"{
          ""name"": ""list_workflows"",
          ""title"": ""List Workflows"",
          ""description"": ""Lists all configured XerahS workflows with their capture modes and after-capture actions."",
          ""inputSchema"": {
            ""type"": ""object"",
            ""properties"": {}
          }
        }",
        @"{
          ""name"": ""get_settings"",
          ""title"": ""Get Settings"",
          ""description"": ""Reads XerahS settings. Optionally scoped to a specific settings category."",
          ""inputSchema"": {
            ""type"": ""object"",
            ""properties"": {
              ""category"": {
                ""type"": ""string"",
                ""enum"": [""capture"", ""upload"", ""history"", ""general""],
                ""description"": ""If omitted, returns all settings (excluding secrets).""
              }
            }
          }
        }"
    ];

    public Task<string> ListWorkflowsAsync()
    {
        // STUB: needs integration with SettingsManager.WorkflowsConfig
        return Task.FromResult(@"{
          ""workflows"": [
            {
              ""id"": ""default-region"",
              ""name"": ""Region Capture"",
              ""job"": ""RectangleRegion"",
              ""capture_mode"": ""region"",
              ""after_capture"": [""save_to_file""],
              ""after_upload"": [""copy_url_to_clipboard""]
            },
            {
              ""id"": ""default-fullscreen"",
              ""name"": ""Full Screen"",
              ""job"": ""PrintScreen"",
              ""capture_mode"": ""fullscreen"",
              ""after_capture"": [""save_to_file"", ""upload""],
              ""after_upload"": [""copy_url_to_clipboard""]
            },
            {
              ""id"": ""default-window"",
              ""name"": ""Window Capture"",
              ""job"": ""ActiveWindow"",
              ""capture_mode"": ""window"",
              ""after_capture"": [""save_to_file""],
              ""after_upload"": []
            }
          ],
          ""count"": 3,
          ""note"": ""STUB: needs integration with SettingsManager.WorkflowsConfig""
        }");
    }

    public Task<string> GetSettingsAsync(string? category)
    {
        // STUB: needs integration with SettingsManager
        string settings;

        switch (category)
        {
            case "capture":
                settings = @"{
                  ""default_capture_mode"": ""region"",
                  ""show_cursor"": true,
                  ""capture_delay_ms"": 0,
                  ""image_format"": ""png"",
                  ""jpeg_quality"": 90,
                  ""screenshot_folder"": ""/home/user/Pictures/XerahS""
                }";
                break;
            case "upload":
                settings = @"{
                  ""default_destination"": ""imgur"",
                  ""destinations"": [""imgur"", ""imgur_anon"", ""dropbox""],
                  ""copy_url_after_upload"": true,
                  ""shorten_url"": false
                }";
                break;
            case "history":
                settings = @"{
                  ""save_history"": true,
                  ""history_file_format"": ""json"",
                  ""max_history_items"": 1000,
                  ""backup_history"": true
                }";
                break;
            case "general":
                settings = @"{
                  ""show_tray_icon"": true,
                  ""start_with_system"": false,
                  ""language"": ""en"",
                  ""theme"": ""dark""
                }";
                break;
            default:
                settings = @"{
                  ""capture"": {
                    ""default_capture_mode"": ""region"",
                    ""show_cursor"": true,
                    ""capture_delay_ms"": 0
                  },
                  ""upload"": {
                    ""default_destination"": ""imgur"",
                    ""copy_url_after_upload"": true
                  },
                  ""history"": {
                    ""save_history"": true,
                    ""max_history_items"": 1000
                  },
                  ""general"": {
                    ""show_tray_icon"": true,
                    ""language"": ""en""
                  }
                }";
                break;
        }

        return Task.FromResult(@"{
          ""category"": """ + (category ?? "all") + @""",
          ""settings"": " + settings + @",
          ""note"": ""STUB: needs integration with SettingsManager (secrets excluded)""
        }");
    }
}
