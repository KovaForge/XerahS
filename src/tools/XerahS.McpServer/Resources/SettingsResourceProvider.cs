namespace XerahS.McpServer.Resources;

/// <summary>
/// Provides settings-related MCP resources
/// </summary>
public class SettingsResourceProvider
{
    public string[] GetResourceTemplatesJson() => [
        @"{
          ""uri"": ""xerahs://settings/capture"",
          ""name"": ""Capture Settings"",
          ""mimeType"": ""application/json"",
          ""description"": ""Capture-related settings""
        }",
        @"{
          ""uri"": ""xerahs://settings/upload"",
          ""name"": ""Upload Settings"",
          ""mimeType"": ""application/json"",
          ""description"": ""Upload destination settings (no secrets)""
        }",
        @"{
          ""uri"": ""xerahs://settings/history"",
          ""name"": ""History Settings"",
          ""mimeType"": ""application/json"",
          ""description"": ""History management settings""
        }",
        @"{
          ""uri"": ""xerahs://settings/general"",
          ""name"": ""General Settings"",
          ""mimeType"": ""application/json"",
          ""description"": ""General application settings""
        }",
        @"{
          ""uri"": ""xerahs://settings/destinations"",
          ""name"": ""Upload Destinations"",
          ""mimeType"": ""application/json"",
          ""description"": ""Available upload destinations (names only, no secrets)""
        }"
    ];

    public string ReadResourceJson(string uri)
    {
        // STUB: needs integration with SettingsManager
        var path = uri.Replace("xerahs://settings/", "");

        string settings;
        switch (path)
        {
            case "capture":
                settings = @"{
                  ""default_capture_mode"": ""region"",
                  ""show_cursor"": true,
                  ""capture_delay_ms"": 0,
                  ""image_format"": ""png"",
                  ""jpeg_quality"": 90,
                  ""screenshot_folder"": ""/home/user/Pictures/XerahS"",
                  ""show_quick_capture_menu"": true,
                  ""play_sound_after_capture"": false
                }";
                break;
            case "upload":
                settings = @"{
                  ""default_destination"": ""imgur"",
                  ""destinations_configured"": [""imgur"", ""imgur_anon""],
                  ""copy_url_after_upload"": true,
                  ""shorten_url"": false,
                  ""open_url_after_upload"": false
                }";
                break;
            case "history":
                settings = @"{
                  ""save_history"": true,
                  ""history_file_format"": ""json"",
                  ""max_history_items"": 1000,
                  ""backup_history"": true,
                  ""history_folder"": ""/home/user/.config/XerahS/History""
                }";
                break;
            case "general":
                settings = @"{
                  ""show_tray_icon"": true,
                  ""start_with_system"": false,
                  ""language"": ""en"",
                  ""theme"": ""dark"",
                  ""show_startup_window"": false,
                  ""minimize_to_tray"": true
                }";
                break;
            case "destinations":
                settings = @"{
                  ""destinations"": [
                    { ""id"": ""imgur"", ""name"": ""Imgur"", ""type"": ""image_host"", ""configured"": true },
                    { ""id"": ""imgur_anon"", ""name"": ""Imgur (Anonymous)"", ""type"": ""image_host"", ""configured"": true },
                    { ""id"": ""dropbox"", ""name"": ""Dropbox"", ""type"": ""cloud_storage"", ""configured"": false },
                    { ""id"": ""google_drive"", ""name"": ""Google Drive"", ""type"": ""cloud_storage"", ""configured"": false }
                  ]
                }";
                break;
            default:
                throw new ArgumentException($"Unknown settings category: {path}");
        }

        return $@"{{
          ""uri"": ""{uri}"",
          ""mimeType"": ""application/json"",
          ""settings"": {settings},
          ""note"": ""STUB: needs integration with SettingsManager (secrets excluded)""
        }}";
    }
}
