# XerahS MCP Server — Usage Guide

## Building

```bash
cd src/tools/XerahS.McpServer
dotnet build -c Release
```

## Running

```bash
dotnet exec xerahs-mcp-server
```

## Protocol

JSON-RPC 2.0 over stdio. Each line is a complete JSON object.

## Initialize

-> `{ "jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {} }`
<- `{ "jsonrpc": "2.0", "id": 1, "result": { "protocolVersion": "2024-11-05", ... } }`

## Tools

### capture_region

Opens the XerahS region selector overlay. User selects an area; returns the saved file path. Blocks until capture completes or is cancelled.

```json
{
  "name": "capture_region",
  "inputSchema": {
    "type": "object",
    "properties": {
      "workflow_id": { "type": "string", "description": "Optional workflow UUID to apply after capture" },
      "monitor": { "type": "integer", "description": "Monitor index (0 = primary)" }
    }
  }
}
```

### capture_window

Captures a specific window by title. Opens window picker if title is omitted.

```json
{
  "name": "capture_window",
  "inputSchema": {
    "type": "object",
    "properties": {
      "window_title": { "type": "string", "description": "Substring match on window title" },
      "include_decoration": { "type": "boolean", "default": true, "description": "Include title bar and borders" }
    }
  }
}
```

### capture_full_screen

Captures all monitors or a specific monitor.

```json
{
  "name": "capture_full_screen",
  "inputSchema": {
    "type": "object",
    "properties": {
      "monitor": { "type": "integer", "description": "Monitor index (0 = primary). If omitted, captures all monitors stitched." }
    }
  }
}
```

### capture_scrolling

Activates XerahS scrolling capture mode. User selects a region then scrolls manually. Returns the stitched result.

```json
{
  "name": "capture_scrolling",
  "inputSchema": {
    "type": "object",
    "properties": {
      "scroll_direction": { "type": "string", "enum": ["down", "up", "left", "right"], "default": "down" },
      "max_frames": { "type": "integer", "default": 50, "description": "Maximum frames before auto-stop" }
    }
  }
}
```

### annotate_image

Opens XerahS image editor with the specified image pre-loaded for annotation.

```json
{
  "name": "annotate_image",
  "inputSchema": {
    "type": "object",
    "properties": {
      "image_path": { "type": "string", "description": "Absolute path to the image file", "required": true },
      "annotations": {
        "type": "array",
        "description": "Optional list of annotations to apply automatically",
        "items": {
          "type": "object",
          "properties": {
            "type": { "type": "string", "enum": ["arrow", "rectangle", "ellipse", "line", "text", "freehand", "blur", "pixelate", "step"] },
            "params": { "type": "object" }
          }
        }
      },
      "auto_save": { "type": "boolean", "default": false, "description": "If true, applies annotations and saves without showing the editor UI" }
    },
    "required": ["image_path"]
  }
}
```

### query_history

Searches XerahS capture history with optional filters.

```json
{
  "name": "query_history",
  "inputSchema": {
    "type": "object",
    "properties": {
      "query": { "type": "string", "description": "Free-text search (filename, OCR text)" },
      "from_date": { "type": "string", "format": "date", "description": "Start date (ISO 8601)" },
      "to_date": { "type": "string", "format": "date", "description": "End date (ISO 8601)" },
      "file_type": { "type": "string", "enum": ["image", "video", "text", "all"], "default": "all" },
      "limit": { "type": "integer", "default": 20, "maximum": 100 }
    }
  }
}
```

### get_history_item

Retrieves full details for a specific history item.

```json
{
  "name": "get_history_item",
  "inputSchema": {
    "type": "object",
    "properties": {
      "id": { "type": "string", "description": "History item UUID", "required": true }
    },
    "required": ["id"]
  }
}
```

### list_workflows

Lists all configured XerahS workflows with their capture modes and after-capture actions.

```json
{
  "name": "list_workflows",
  "inputSchema": { "type": "object", "properties": {} }
}
```

### get_settings

Reads XerahS settings. Optionally scoped to a specific settings category.

```json
{
  "name": "get_settings",
  "inputSchema": {
    "type": "object",
    "properties": {
      "category": { "type": "string", "enum": ["capture", "upload", "history", "general"], "description": "If omitted, returns all settings (excluding secrets)" }
    }
  }
}
```

### upload_file

Uploads a file to the configured default (or specified) upload destination.

```json
{
  "name": "upload_file",
  "inputSchema": {
    "type": "object",
    "properties": {
      "file_path": { "type": "string", "description": "Absolute path to the file to upload", "required": true },
      "destination": { "type": "string", "description": "Destination ID (e.g. 'imgur', 'dropbox'). Uses default if omitted." }
    },
    "required": ["file_path"]
  }
}
```

### upload_clipboard

Reads the current clipboard contents (image or text) and uploads to the configured destination.

```json
{
  "name": "upload_clipboard",
  "inputSchema": {
    "type": "object",
    "properties": {
      "destination": { "type": "string", "description": "Destination ID. Uses default if omitted." }
    }
  }
}
```

## Resources

- `xerahs://history/` — capture history
- `xerahs://settings/` — XerahS settings
- `xerahs://workflows/` — workflow configurations

## Prompts

- `capture_and_annotate` — Two-step capture then annotate workflow
- `batch_capture_report` — Capture multiple regions and compile a report
- `upload_workflow` — Standard screenshot-to-URL workflow
