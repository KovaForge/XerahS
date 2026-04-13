namespace XerahS.McpServer.Prompts;

public record PromptTemplate(string Name, string Description, Argument[] Arguments);
public record Argument(string Name, string Description, bool Required);

/// <summary>
/// MCP prompt templates for common XerahS workflows.
/// </summary>
public static class PromptTemplates
{
    public static PromptTemplate[] GetPrompts() => [
        new PromptTemplate("capture_and_annotate", "Two-step capture then annotate workflow", [
            new Argument("user_description_of_what_to_capture_and_annotate", "Description of what to capture and how to annotate it", true)
        ]),
        new PromptTemplate("batch_capture_report", "Capture multiple regions and compile a report", [
            new Argument("region_list", "List of regions to capture (descriptions or coordinates)", true)
        ]),
        new PromptTemplate("upload_workflow", "Standard screenshot-to-URL workflow", [
            new Argument("user_request_describing_what_to_capture_and_annotate", "User's request describing what to capture and annotate", true),
            new Argument("destination_id_or_default", "Destination ID for upload, or 'default' to use the configured default", false)
        ])
    ];

    public static string GetPromptTemplate(string name)
    {
        return name switch
        {
            "capture_and_annotate" => """
                You are working with XerahS, a screen capture tool. Follow these steps:

                1. Use the `capture_region` tool to initiate a region capture.
                2. Wait for the user to select a region in the XerahS overlay.
                3. The capture file path will be returned.
                4. Use `annotate_image` with `auto_save=true` and the annotations derived from the user's request.
                5. Report the final annotated file path.

                Input: {{user_description_of_what_to_capture_and_annotate}}
                """,

            "batch_capture_report" => """
                Use XerahS to capture the following screen regions in sequence and compile a report of all captured images:

                Regions to capture:
                {{region_list}}

                For each region:
                1. Use `capture_region` with the appropriate monitor/index hint.
                2. Record the returned file path.
                3. Use `get_history_item` to retrieve metadata.
                4. If OCR is available, include extracted text.

                Output format: A structured markdown report with file paths, timestamps, and extracted text for each capture.
                """,

            "upload_workflow" => """
                Standard screenshot-to-URL workflow:

                1. `capture_region` - get the screenshot.
                2. `annotate_image` (auto_save=true) - apply requested annotations.
                3. `upload_file` - upload to the specified destination.
                4. Return the URL from step 3.

                Input: {{user_request_describing_what_to_capture_and_annotate}}
                Destination: {{destination_id_or_default}}
                """,

            _ => throw new ArgumentException($"Unknown prompt template: {name}")
        };
    }
}
