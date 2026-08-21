---
# XerahS MCP Server

Local Model Context Protocol server for XerahS screen capture, annotation, upload, history, workflows, and safe settings access.

`xerahs-mcp` runs the real XerahS desktop runtime. It must run on the same machine whose screen, clipboard, history, settings, and uploader configuration are being controlled.

Cloudflare or another edge service can publish discovery metadata or proxy HTTP requests to a reachable `xerahs-mcp` process, but it cannot run XerahS itself.

## Building

dotnet build -c Release

## Local MCP Usage

Use stdio mode for local MCP clients:

```powershell
xerahs-mcp --mcp
```

HTTP mode is available for user-controlled desktop hosts:

```powershell
xerahs-mcp --mcp-server --transport http --port 7890
```

HTTP mode still requires an interactive desktop session and the required OS capture, clipboard, and accessibility permissions.
---
