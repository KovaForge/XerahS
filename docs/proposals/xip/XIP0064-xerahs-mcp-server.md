# XIP0064: XerahS MCP Server

**Status**: Implemented
**Priority**: High
**Area**: AI Integration | Automation | Interoperability
**Updated**: 2026-04-10

## Summary

XerahS ships a dedicated MCP server executable, `xerahs-mcp`, that exposes screen capture, annotation, upload, history, workflow, and settings operations over the Model Context Protocol.

The implementation is not a CLI wrapper. It boots the real XerahS runtime headlessly and routes MCP calls into the same capture, history, uploader, and settings systems used by the desktop application.

The current implementation is a user-desktop MCP server. It must run on the same machine whose screen, clipboard, history, settings, and uploader configuration are being controlled. Cloudflare, GitHub Pages, and other edge/static hosts can publish discovery metadata or proxy to a reachable `xerahs-mcp` HTTP process, but they do not execute the XerahS runtime.

## Goals

- Make XerahS discoverable and callable from MCP-compatible hosts.
- Reuse the real XerahS runtime instead of inventing a second automation stack.
- Support both local stdio transport and user-controlled HTTP transport with the same tool contract.
- Expose only MCP-safe settings data; do not leak uploader secrets or raw API keys.

## Non-Goals

- Reproducing the full interactive editor UI over MCP.
- Providing a shared Cloudflare-hosted XerahS runtime.
- Turning GitHub Pages into the MCP execution host.
- Letting remote users control capture, clipboard, history, settings, or uploader secrets without a running XerahS desktop/runtime backend.
- Streaming live capture frames.
- Exposing every XerahS feature before the contract is stable.

## Implemented Scope

### Transport

`xerahs-mcp` supports:

- `--mcp` or `--mcp-server` for stdio JSON-RPC.
- `--transport http --port <port>` for HTTP JSON-RPC plus SSE.

Stdio mode is the intended default for local MCP hosts. HTTP mode is for a user-controlled desktop session or dedicated desktop/VM host that runs the same XerahS runtime over a network-reachable endpoint.

The HTTP server exposes:

- `POST /mcp/`
- `GET /mcp/events/`
- `GET /health`

### Runtime Boot

The MCP server bootstraps XerahS through `ShareXBootstrap.InitializeAsync(...)` using headless UI and toast services. This gives MCP access to:

- platform capture services
- clipboard services
- OCR when available
- history storage
- workflow configuration
- uploader instances and defaults
- desktop task execution for uploads

### Authentication

HTTP transport requires a bearer token.

- The token is stored in `ApplicationConfig.McpApiKey`.
- If the key is missing, the runtime generates one and saves it.
- MCP-safe settings output only exposes whether a key exists and a masked preview.

## Tool Contract

### Capture

#### `capture_region`

Interactive region capture.

Input:

```json
{
  "workflow_id": "optional workflow id",
  "monitor": 0
}
```

Behavior:

- Opens the real XerahS region selector.
- Waits for the user to choose a region.
- Saves the capture through the standard XerahS file pipeline.
- If `monitor` is supplied, the final region is constrained to that monitor.

#### `capture_window`

Captures the foreground window or the first window whose title contains `window_title`.

Input:

```json
{
  "window_title": "Firefox",
  "include_decoration": true
}
```

Behavior:

- `include_decoration=false` captures only the client area.
- The response includes the resolved window title.

#### `capture_full_screen`

Captures all monitors as a single image, or a single monitor when `monitor` is provided.

#### `capture_scrolling`

Runs scrolling capture against the active window when the platform supports it.

Input:

```json
{
  "scroll_direction": "down",
  "max_frames": 50
}
```

Important notes:

- The current runtime uses XerahS scrolling capture services and returns the stitched result.
- `scroll_direction` and `max_frames` are accepted and returned for contract stability, but the current capture engine is not hard-capped by `max_frames`.

### Annotation

#### `annotate_image`

Headless annotation pipeline for an existing image.

Input:

```json
{
  "image_path": "C:/path/to/image.png",
  "annotations": [
    {
      "type": "rectangle",
      "params": {
        "x": 10,
        "y": 20,
        "width": 200,
        "height": 120,
        "color": "#ff3b30",
        "thickness": 4
      }
    }
  ],
  "auto_save": true
}
```

Supported annotation types:

- `arrow`
- `rectangle`
- `ellipse`
- `line`
- `text`
- `freehand`
- `blur`
- `pixelate`
- `step`

Important notes:

- MCP annotation is headless and always writes a new annotated file.
- It does **not** launch the interactive image editor.
- `auto_save` is accepted for compatibility but does not change the headless behavior.

### Upload

#### `upload_file`

Uploads a file through the configured XerahS uploader system.

`destination` may be:

- an uploader instance ID
- a provider ID
- a display name
- omitted, in which case XerahS resolves the default destination for the file category

#### `upload_clipboard`

Uploads clipboard content when the clipboard currently contains:

- an image
- text
- a file drop list

### History

#### `query_history`

Queries stored history by:

- free-text query
- `from_date`
- `to_date`
- `file_type`
- `limit`

Supported `file_type` values:

- `image`
- `video`
- `text`
- `file`
- `all`

#### `get_history_item`

Returns full metadata for a single history item.

Important note:

- History item IDs are SQLite row IDs serialized as strings.
- They are not UUIDs.

### Settings and Workflows

#### `list_workflows`

Returns configured workflows with:

- `id`
- `name`
- `job`
- `capture_mode`
- `after_capture`
- `after_upload`
- `enabled`
- `pinned_to_tray`

#### `get_settings`

Returns MCP-safe settings for:

- `capture`
- `upload`
- `history`
- `general`
- `integration`

When `category` is omitted, all MCP-safe categories are returned.

## Resource Contract

The server supports the following resource families:

- `xerahs://history/{id}`
- `xerahs://history/thumb/{id}`
- `xerahs://history/search?q={query}`
- `xerahs://capture/latest`
- `xerahs://workflows`
- `xerahs://workflows/{id}`
- `xerahs://settings/capture`
- `xerahs://settings/upload`
- `xerahs://settings/history`
- `xerahs://settings/general`
- `xerahs://settings/integration`
- `xerahs://destinations`
- `xerahs://monitors`

Important notes:

- `xerahs://history/thumb/{id}` currently returns a base64 blob of the stored file for that history item. It is not a separately rendered thumbnail pipeline.
- Settings resources expose summaries and safe metadata only.

## Prompt Contract

The server exposes prompt templates and supports both:

- `prompts/list`
- `prompts/get`

Shipped prompts:

- `capture_and_annotate`
- `batch_capture_report`
- `upload_workflow`

## Discovery Manifest

The public discovery manifest is published at:

- `https://xerahs.com/.well-known/mcp/manifest.json`

That manifest advertises the optional public HTTP endpoint:

- `https://mcp.xerahs.com/mcp/`
- `https://mcp.xerahs.com/mcp/events/`

GitHub Pages or Cloudflare hosts the manifest and documentation. It is not the execution host for the MCP server itself.

The public endpoint is a deployment slot, not a standalone hosted MCP runtime. It is only functional when configured to proxy to a running `xerahs-mcp --transport http` backend. If no backend is configured, MCP clients should use local stdio mode.

## Implementation Notes

The MCP server lives in:

- `src/tools/XerahS.McpServer`

Key implementation points:

- real runtime integration is centralized in `Runtime/XerahSMcpRuntime.cs`
- HTTP auth reads the saved `McpApiKey`
- Cloudflare Worker code, when used, is a proxy/discovery layer only; it cannot run desktop capture, clipboard, history, uploader, or settings operations without a reachable XerahS backend
- SSE now sends plain JSON event payloads rather than custom base64 payloads
- tests are unit tests over the JSON-RPC server surface rather than machine-specific spawned-process tests

## Deferred Work

The following items remain intentionally out of scope for this XIP revision:

- richer progress notifications for long-running capture operations
- frame streaming for scrolling capture
- audio and video capture tools
- interactive editor hosting over MCP
- subscription semantics beyond the current resource and prompt discovery surface

## Rationale

This XIP is intentionally narrower than the original draft. The previous version described behavior that was still stubbed, encoded incorrectly, or underspecified. The implemented contract above matches the real server behavior and is the baseline for future expansion.
