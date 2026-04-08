namespace XerahS.McpServer.Prompts;

/// <summary>
/// MCP prompt templates for common XerahS workflows
/// </summary>
public static class PromptTemplates
{
    public static IEnumerable<object> GetPrompts()
    {
        return new[]
        {
            new
            {
                name = "capture_and_annotate",
                description = "Two-step capture then annotate workflow",
                arguments = new[]
                {
                    new
                    {
                        name = "user_description_of_what_to_capture_and_annotate",
                        description = "Description of what to capture and how to annotate it",
                        required = true
                    }
                }
            },
            new
            {
                name = "batch_capture_report",
                description = "Capture multiple regions and compile a report",
                arguments = new[]
                {
                    new
                    {
                        name = "region_list",
                        description = "List of regions to capture (descriptions or coordinates)",
                        required = true
                    }
                }
            },
            new
            {
                name = "upload_workflow",
                description = "Standard screenshot-to-URL workflow",
                arguments = new[]
                {
                    new
                    {
                        name = "user_request_describing_what_to_capture_and_annotate",
                        description = "User's request describing what to capture and annotate",
                        required = true
                    },
                    new
                    {
                        name = "destination_id_or_default",
                        description = "Destination ID for upload, or 'default' to use configured default",
                        required = false
                    }
                }
            }
        };
    }

    public static string GetPromptTemplate(string name)
    {
        return name switch
        {
            "capture_and_annotate" => @"You are working with XerahS, a screen capture tool. Follow these steps:

1. Use the `capture_region` tool to initiate a region capture.
2. Wait for the user to select a region in the XerahS overlay.
3. The capture file path will be returned.
4. Use `annotate_image` with `auto_save=true` and the annotations derived from the user's request.
5. Report the final annotated file path.

Input: {{user_description_of_what_to_capture_and_annotate}}",

            "batch_capture_report" => @"Use XerahS to capture the following screen regions in sequence and compile a report of all captured images:

Regions to capture:
{{region_list}}

For each region:
1. Use `capture_region` with the appropriate monitor/index hint
2. Record the returned file path
3. Use `get_history_item` to retrieve metadata
4. If OCR is available, include extracted text

Output format: A structured markdown report with file paths, timestamps, and extracted text for each capture.",

            "upload_workflow" => @"Standard screenshot-to-URL workflow:

1. `capture_region` — get the screenshot
2. `annotate_image` (auto_save=true) — apply requested annotations
3. `upload_file` — upload to the specified destination
4. Return the URL from step 3

Input: {{user_request_describing_what_to_capture_and_annotate}}
Destination: {{destination_id_or_default}}",

            _ => throw new ArgumentException($"Unknown prompt template: {name}")
        };
    }
}
