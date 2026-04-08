namespace XerahS.McpServer.Tools;

/// <summary>
/// Annotation tools for MCP server
/// </summary>
public class AnnotationTools
{
    public string[] GetToolDefinitionsJson() => [
        @"{
          ""name"": ""annotate_image"",
          ""title"": ""Annotate Image"",
          ""description"": ""Opens XerahS image editor with the specified image pre-loaded for annotation."",
          ""inputSchema"": {
            ""type"": ""object"",
            ""properties"": {
              ""image_path"": {
                ""type"": ""string"",
                ""description"": ""Absolute path to the image file to annotate"",
                ""required"": true
              },
              ""annotations"": {
                ""type"": ""array"",
                ""description"": ""Optional list of annotations to apply automatically before opening the editor"",
                ""items"": {
                  ""type"": ""object"",
                  ""properties"": {
                    ""type"": {
                      ""type"": ""string"",
                      ""enum"": [""arrow"", ""rectangle"", ""ellipse"", ""line"", ""text"", ""freehand"", ""blur"", ""pixelate"", ""step""]
                    },
                    ""params"": {
                      ""type"": ""object"",
                      ""description"": ""Annotation-specific parameters""
                    }
                  }
                }
              },
              ""auto_save"": {
                ""type"": ""boolean"",
                ""default"": false,
                ""description"": ""If true, applies annotations and saves without showing the editor UI""
              }
            },
            ""required"": [""image_path""]
          }
        }"
    ];

    public Task<string> AnnotateImageAsync(string? imagePath, bool autoSave, int annotationsCount)
    {
        if (string.IsNullOrEmpty(imagePath))
        {
            return Task.FromResult(@"{ ""error"": ""image_path is required"" }");
        }

        // STUB: needs integration with annotation pipeline
        var outputPath = imagePath.Replace(".png", "_annotated.png");
        return Task.FromResult($@"{{
          ""input_path"": ""{imagePath}"",
          ""output_path"": ""{outputPath}"",
          ""auto_save"": {autoSave.ToString().ToLower()},
          ""annotations_applied"": {annotationsCount},
          ""note"": ""STUB: needs integration with annotation pipeline""
        }}");
    }
}
