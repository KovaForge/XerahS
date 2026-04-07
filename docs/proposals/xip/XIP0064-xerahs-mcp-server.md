# XIP0064 XerahS MCP Server — Model Context Protocol Integration

**Status**: PROPOSED
**Priority**: High
**Area**: AI Integration | Extensibility | Interoperability
**Related**: XIP0063 (XerahS CLI OpenClaw compatibility)

---

## Summary

Expose XerahS as a Model Context Protocol (MCP) server so AI agents can invoke screen capture, annotation, upload, and history operations natively — without shell scripts, CLI wrappers, or platform-specific automation. MCP turns XerahS into a first-class AI tool alongside file systems and databases.

---

## What is MCP?

The Model Context Protocol (MCP) is an open standard introduced by Anthropic (late 2024) that defines how AI applications connect to external data sources and tools. It functions as "USB-C for AI integrations" — a universal plug that works across all LLM providers and AI frameworks.

**Core architecture:**
```
AI Application (Host)
    ├── Client 1 ←→ MCP Server (e.g. Files & Git)
    ├── Client 2 ←→ MCP Server (e.g. Database)
    └── Client N ←→ MCP Server (XerahS)  ← this proposal
```

MCP is built on **JSON-RPC 2.0** over **stdio** (local) or **HTTP + SSE** (remote). It defines three primitives:

| Primitive | Control | Description | MCP primitive type |
|---|---|---|---|
| Tools | Model-controlled | Functions the LLM can invoke | `tools/call` |
| Resources | Application-controlled | Structured data the app exposes | `resources/list` + `resources/read` |
| Prompts | User-controlled | Pre-defined templates | `prompts/list` |

---

## Why XerahS Needs an MCP Server

Today, AI agents that want to capture a screenshot must shell out to `xerahs --cli capture` with fragile argument parsing. There is no structured way for an AI to:
- Discover what capture modes XerahS supports
- Read capture history programmatically
- Trigger a capture with specific parameters and get back the file path or URL
- Annotate an existing image
- Check upload destination status

An MCP server fixes this by making XerahS a first-class citizen in any MCP-compatible AI stack (Claude Desktop, Cursor, OpenClaw, etc.).

### Use cases

1. **OpenClaw agent instructs XerahS to capture** — `vladislava` tells XerahS "capture this window" and gets back the file path
2. **AI-powered annotation pipeline** — agent reads an image, determines what annotations to add, calls `annotate_image` tool
3. **Automated upload workflow** — agent captures → annotates → uploads → returns URL, fully autonomous
4. **History queries** — agent asks "show me screenshots from yesterday" via the history resource
5. **CI/CD screenshot testing** — headless agent triggers capture, waits for result, validates output

---

## Proposed Design

### Phase 1 — Core MCP Server (this XIP)

#### Transport Layer

Use **stdio transport** (local, zero-config). XerahS spawns as a child process; the AI host communicates via JSON-RPC messages on stdin/stdout. This is the standard MCP local transport and requires no network configuration.

```
AI Host (OpenClaw / Claude Desktop / etc.)
    └── stdio spawn → XerahS.McpServer
                          └── JSON-RPC 2.0
```

#### Server Identity

```json
{
  "name": "xerahs",
  "version": "0.22.0",
  "capabilities": {
    "tools": { "listChanged": true },
    "resources": { "subscribe": true, "listChanged": true },
    "prompts": { "listChanged": true }
  }
}
```

---

### Tools (Model-Controlled)

These are the functions the LLM can invoke. Each tool maps to a XerahS operation.

#### Capture Tools

**`capture_region`** — interactive region capture
```json
{
  "name": "capture_region",
  "title": "Capture Screen Region",
  "description": "Opens the XerahS region selector overlay. User selects an area; returns the saved file path. Blocks until capture completes or is cancelled.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "workflow_id": {
        "type": "string",
        "description": "Optional workflow UUID to apply after capture (uses default if omitted)"
      },
      "monitor": {
        "type": "integer",
        "description": "Monitor index to capture (0 = primary, 1 = secondary). If omitted, all monitors are shown in the selector."
      }
    }
  }
}
```
Result: `{ "file_path": "/home/user/Pictures/XerahS/capture_2026-04-08_001.png", "url": null }`

---

**`capture_window`** — window capture
```json
{
  "name": "capture_window",
  "title": "Capture Single Window",
  "description": "Captures a specific window by title. Opens window picker if title is omitted.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "window_title": {
        "type": "string",
        "description": "Substring match on window title. If omitted, shows window picker."
      },
      "include_decoration": {
        "type": "boolean",
        "default": true,
        "description": "Include the window title bar and borders."
      }
    }
  }
}
```

---

**`capture_full_screen`** — full screen capture
```json
{
  "name": "capture_full_screen",
  "title": "Capture Full Screen",
  "description": "Captures all monitors or a specific monitor.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "monitor": {
        "type": "integer",
        "description": "Monitor index (0 = primary). If omitted, captures all monitors as a stitched image."
      }
    }
  }
}
```

---

**`capture_scrolling`** — scrolling capture
```json
{
  "name": "capture_scrolling",
  "title": "Scrolling Capture",
  "description": "Activates XerahS scrolling capture mode. User selects a region then scrolls manually. Returns the stitched result.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "scroll_direction": {
        "type": "string",
        "enum": ["down", "up", "left", "right"],
        "default": "down",
        "description": "Expected scroll direction"
      },
      "max_frames": {
        "type": "integer",
        "default": 50,
        "description": "Maximum frames before auto-stop"
      }
    }
  }
}
```

---

#### Annotation Tools

**`annotate_image`** — annotate an existing image
```json
{
  "name": "annotate_image",
  "title": "Annotate Image",
  "description": "Opens XerahS image editor with the specified image pre-loaded for annotation.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "image_path": {
        "type": "string",
        "description": "Absolute path to the image file to annotate",
        "required": true
      },
      "annotations": {
        "type": "array",
        "description": "Optional list of annotations to apply automatically before opening the editor (for fully automated workflows)",
        "items": {
          "type": "object",
          "properties": {
            "type": {
              "type": "string",
              "enum": ["arrow", "rectangle", "ellipse", "line", "text", "freehand", "blur", "pixelate", "step"]
            },
            "params": {
              "type": "object",
              "description": "Annotation-specific parameters (coordinates, color, text, etc.)"
            }
          }
        }
      },
      "auto_save": {
        "type": "boolean",
        "default": false,
        "description": "If true, applies annotations and saves without showing the editor UI"
      }
    }
  }
}
```

**Annotation `params` by type:**
```json
// Arrow
{ "x1": 10, "y1": 20, "x2": 100, "y2": 60, "color": "#FF5733", "thickness": 2 }

// Rectangle
{ "x": 10, "y": 20, "width": 200, "height": 150, "color": "#FF5733", "fill": false, "thickness": 2 }

// Ellipse
{ "x": 10, "y": 20, "width": 200, "height": 150, "color": "#FF5733", "fill": false, "thickness": 2 }

// Text
{ "x": 10, "y": 20, "text": "Label", "color": "#FF5733", "font_size": 16, "font_family": "Segoe UI" }

// Blur
{ "x": 10, "y": 20, "width": 100, "height": 50, "radius": 15 }

// Pixelate
{ "x": 10, "y": 20, "width": 100, "height": 50, "pixel_size": 8 }
```

---

#### Upload Tools

**`upload_file`** — upload a file to configured destination
```json
{
  "name": "upload_file",
  "title": "Upload File",
  "description": "Uploads a file to the configured default (or specified) upload destination.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "file_path": {
        "type": "string",
        "description": "Absolute path to the file to upload",
        "required": true
      },
      "destination": {
        "type": "string",
        "description": "Destination ID (e.g. 'imgur', 'imgur_anon', 'dropbox'). Uses default if omitted."
      }
    }
  }
}
```

Result: `{ "url": "https://imgur.com/abc123", "filename": "capture.png", "size_bytes": 102400 }`

---

**`upload_clipboard`** — upload clipboard contents
```json
{
  "name": "upload_clipboard",
  "title": "Upload Clipboard",
  "description": "Reads the current clipboard contents (image or text) and uploads to the configured destination.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "destination": {
        "type": "string",
        "description": "Destination ID. Uses default if omitted."
      }
    }
  }
}
```

---

#### History Tools

**`query_history`** — search capture history
```json
{
  "name": "query_history",
  "title": "Query Capture History",
  "description": "Searches XerahS capture history with optional filters.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "query": {
        "type": "string",
        "description": "Free-text search (matches filename, OCR text if indexed)"
      },
      "from_date": {
        "type": "string",
        "format": "date",
        "description": "Start date (ISO 8601)"
      },
      "to_date": {
        "type": "string",
        "format": "date",
        "description": "End date (ISO 8601)"
      },
      "file_type": {
        "type": "string",
        "enum": ["image", "video", "text", "all"],
        "default": "all"
      },
      "limit": {
        "type": "integer",
        "default": 20,
        "maximum": 100
      }
    }
  }
}
```

Result:
```json
{
  "items": [
    {
      "id": "uuid",
      "file_path": "/home/user/Pictures/XerahS/capture_2026-04-08_001.png",
      "thumbnail_url": "file:///home/user/Pictures/XerahS/thumb_2026-04-08_001.png",
      "created_at": "2026-04-08T09:00:00Z",
      "file_size_bytes": 102400,
      "ocr_text": "optional extracted text",
      "tags": []
    }
  ],
  "total_count": 47,
  "has_more": true
}
```

---

**`get_history_item`** — get a single history item
```json
{
  "name": "get_history_item",
  "title": "Get History Item",
  "description": "Retrieves full details for a specific history item.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "id": {
        "type": "string",
        "description": "History item UUID",
        "required": true
      }
    }
  }
}
```

---

#### Settings / Workflow Tools

**`list_workflows`** — list available workflows
```json
{
  "name": "list_workflows",
  "title": "List Workflows",
  "description": "Lists all configured XerahS workflows with their capture modes and after-capture actions."
}
```

**`get_settings`** — read XerahS settings
```json
{
  "name": "get_settings",
  "title": "Get Settings",
  "description": "Reads XerahS settings. Optionally scoped to a specific settings category.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "category": {
        "type": "string",
        "enum": ["capture", "upload", "history", "general"],
        "description": "If omitted, returns all settings (excluding secrets)."
      }
    }
  }
}
```

---

### Resources (Application-Controlled)

These are URI-addressable data items the AI host can read.

#### Resource URI Scheme

```
xerahs://history/{uuid}           — single history item (JSON)
xerahs://history/thumb/{uuid}    — thumbnail image
xerahs://history/search?q={q}    — search template
xerahs://settings/{category}     — settings snapshot (JSON)
xerahs://workflows               — all workflows (JSON)
xerahs://workflows/{id}          — single workflow (JSON)
xerahs://capture/latest          — most recent capture metadata
xerahs://monitors                — monitor configuration
xerahs://destinations            — upload destinations (names only, no secrets)
```

#### Example: `xerahs://history/{uuid}`
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "file_path": "/home/user/Pictures/XerahS/capture_2026-04-08_001.png",
  "file_url": "file:///home/user/Pictures/XerahS/capture_2026-04-08_001.png",
  "thumbnail_path": "/home/user/Pictures/XerahS/thumb_capture_2026-04-08_001.png",
  "capture_type": "region",
  "capture_width": 1920,
  "capture_height": 1080,
  "created_at": "2026-04-08T09:00:00Z",
  "file_size_bytes": 204800,
  "file_hash_md5": "a1b2c3d4e5f6...",
  "upload_url": "https://imgur.com/abc123",
  "ocr_text": "optional extracted text",
  "window_title": "Mozilla Firefox",
  "application_name": "firefox",
  "tags": [],
  "annotations_applied": ["arrow", "text"],
  "workflow_id": "default-region"
}
```

---

### Prompts (User-Controlled)

Pre-defined prompt templates for common AI workflows.

**`capture_and_annotate`** — two-step capture then annotate
```
You are working with XerahS, a screen capture tool. Follow these steps:

1. Use the `capture_region` tool to initiate a region capture.
2. Wait for the user to select a region in the XerahS overlay.
3. The capture file path will be returned.
4. Use `annotate_image` with `auto_save=true` and the annotations
   derived from the user's request.
5. Report the final annotated file path.

Input: {user_description_of_what_to_capture_and_annotate}
```

**`batch_capture_report`** — capture multiple regions and compile a report
```
Use XerahS to capture the following screen regions in sequence and compile
a report of all captured images:

Regions to capture:
{region_list}

For each region:
1. Use `capture_region` with the appropriate monitor/index hint
2. Record the returned file path
3. Use `get_history_item` to retrieve metadata
4. If OCR is available, include extracted text

Output format: A structured markdown report with file paths, timestamps,
and extracted text for each capture.
```

**`upload_workflow`** — capture, annotate, upload, return URL
```
Standard screenshot-to-URL workflow:

1. `capture_region` — get the screenshot
2. `annotate_image` (auto_save=true) — apply requested annotations
3. `upload_file` — upload to the specified destination
4. Return the URL from step 3

Input: {user request describing what to capture and annotate}
Destination: {destination_id or 'default'}
```

---

## Implementation

### Project Structure

```
src/
  tools/
    XerahS.McpServer/
      XerahS.McpServer.csproj     — self-contained dotnet tool / executable
      Program.cs                   — entry point, JSON-RPC over stdio loop
      Server/
        XerahSMcpServer.cs         — main server class, implements JsonRpcHandler
        Capabilities.cs             — capability declarations
      Tools/
        CaptureTools.cs             — capture_region, capture_window, capture_full_screen, capture_scrolling
        AnnotationTools.cs          — annotate_image
        UploadTools.cs              — upload_file, upload_clipboard
        HistoryTools.cs             — query_history, get_history_item
        SettingsTools.cs            — list_workflows, get_settings
      Resources/
        HistoryResourceProvider.cs
        SettingsResourceProvider.cs
        WorkflowResourceProvider.cs
      Transport/
        StdioServer.cs             — stdio JSON-RPC transport
      JsonRpc/
        JsonRpcRequest.cs
        JsonRpcResponse.cs
        JsonRpcError.cs
```

### Transport: JSON-RPC over Stdio

MCP uses stdio as the primary local transport. The server reads JSON-RPC requests from `Console.In` and writes responses to `Console.Out`. This is identical to how LSP (Language Server Protocol) works.

```csharp
// Pseudocode — stdio message loop
while ((var line = Console.ReadLine()) != null)
{
    var request = JsonSerializer.Deserialize<JsonRpcRequest>(line);
    var response = await HandleRequestAsync(request);
    Console.WriteLine(JsonSerializer.Serialize(response));
}
```

### Capability Negotiation

On connection, the host sends `initialize` with its capabilities. The server responds with its capabilities:

```json
// Server → Client (initialize response)
{
  "protocolVersion": "2024-11-05",
  "serverInfo": { "name": "xerahs", "version": "0.22.0" },
  "capabilities": {
    "tools": { "listChanged": true },
    "resources": { "subscribe": true, "listChanged": true },
    "prompts": { "listChanged": true }
  }
}
```

### Startup Integration

**Option A — Dedicated MCP mode:**
```bash
xerahs --mcp-server
# or
xerahs --mcp
```

XerahS starts in headless MCP server mode. No UI is shown. Communicates via stdio. Exit when the AI host closes the connection (EOF on stdin).

**Option B — Auto-spawn (Claude Desktop / AI host integration):**
Add to `~/.config/claude/claude_desktop_config.json`:
```json
{
  "mcpServers": {
    "xerahs": {
      "command": "xerahs",
      "args": ["--mcp"]
    }
  }
}
```

### Security Considerations

1. **No auto-execute for destructive tools** — annotation tools that overwrite files should require user confirmation on first use
2. **Scope tools to active session** — tools operate on the current user's XerahS config, not arbitrary system paths
3. **No secret exposure** — upload API keys, tokens, and credentials are never returned via `get_settings` or `resources`
4. **Human-in-the-loop for upload** — `upload_file` surfaces the URL but requires the user to have configured the destination; AI cannot exfiltrate to an unknown destination
5. **Sandbox** — MCP server inherits the same sandbox flags as the main XerahS app (no elevated execution)

### Error Handling

All tools return a `JsonRpcError` on failure:
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "error": {
    "code": -32603,
    "message": "Capture cancelled by user",
    "data": { "reason": "user_aborted" }
  }
}
```

Error codes:
- `-32603` — Internal error (capture failed, upload failed)
- `-32602` — Invalid params (file not found, invalid UUID)
- `-32600` — Capture in progress (only one capture at a time)
- `-32500` — User cancelled
- `-32400` — XerahS not configured / no active session

---

## Non-Goals

- Replacing the XerahS CLI (XIP0063) — the MCP server is complementary, not a replacement
- Remote MCP (HTTP + SSE) — stdio-only for v1 (local AI hosts)
- Full annotation pipeline exposure — initial version exposes `annotate_image`; fine-grained annotation editing via the editor is out of scope
- Changing XerahS's own UI behavior — MCP is purely additive, no existing features are modified

---

## Deliverables (Phase 1)

| # | Deliverable | Description |
|---|---|---|
| 1 | `XerahS.McpServer` project | Self-contained dotnet tool project |
| 2 | Stdio JSON-RPC transport | MCP-compliant transport layer |
| 3 | `capture_*` tools | 4 capture tools |
| 4 | `annotate_image` tool | Annotation with auto-save |
| 5 | `upload_file` + `upload_clipboard` tools | Upload pipeline |
| 6 | `query_history` + `get_history_item` tools | History access |
| 7 | Settings + workflow resources | URI-addressable config |
| 8 | Prompt templates | `capture_and_annotate`, `upload_workflow` |
| 9 | MCP integration test | Spawn server, call tools, verify responses |
| 10 | Documentation | `docs/mcp/` — usage guide + tool reference |

---

## Open Questions

1. **Headless annotation**: `annotate_image` with `auto_save=true` needs to run the annotation pipeline without a display. Is SkiaSharp Avalonia annotation pipeline headless-capable, or does it require a window context?
2. **Capture in progress**: Should `capture_*` tools block (synchronous) or return immediately with a job ID (async)? MCP supports both patterns. Synchronous is simpler but will block the AI host's event loop.
3. **Multi-monitor UI**: `capture_region` needs to show the overlay on the correct monitor. How does the AI host communicate which monitor to target?
4. **Authorization**: Should each MCP connection require explicit user approval the first time a tool is used, or is the AI host trusted to authorize on behalf of the user?

---

## Future Phases (Out of Scope for This XIP)

- **Phase 2**: Remote MCP over HTTP + SSE for network-accessible AI hosts. Hosted at `https://xerahs.github.io/` (GitHub Pages, `xerahs.github.io` repo).
- **Phase 3**: Streaming frame updates for `capture_scrolling` (SSE notifications as each frame is captured)
- **Phase 4**: MCP client (XerahS as an MCP *host*) — allows XerahS to use other MCP servers (e.g. ask an AI to analyze a screenshot without leaving XerahS)
- **Phase 5**: Audio/video capture tools

---

*Author: Vladislava Kova (KovaForge COO) | Research: Milena Petrova (KovaForge Researcher)*
