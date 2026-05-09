# XIP0076 OpenClaw Native Plugin Export

**Status**: Draft  
**Priority**: High  
**Area**: AI Integration | OpenClaw | CLI | Plugin Packaging | Automation  
**Related**: XIP0063, XIP0064  
**Created**: 2026-05-09  

## Summary

XerahS should ship a first-party way to generate or publish a native OpenClaw plugin whose primary job is simple and reliable: let OpenClaw upload content through XerahS and return a shareable URL.

XIP0063 made the XerahS CLI usable by OpenClaw through direct command execution. XIP0064 added a dedicated MCP server. This XIP proposes the next step: a thin OpenClaw-native plugin package that makes upload-to-URL workflows discoverable inside OpenClaw's plugin registry, exposes declared upload tools through `api.registerTool(...)`, and optionally registers an `openclaw xerahs ...` command group through `api.registerCli(...)`.

The plugin should remain a Node/TypeScript OpenClaw plugin. It should invoke the installed `xerahs` executable as a child process instead of trying to load .NET assemblies into OpenClaw. The success path is an agent handing the plugin a file path or text payload and receiving a structured result containing the final URL.

## Context

OpenClaw's plugin system is a Node runtime. Native plugins are discovered through `package.json` metadata and an `openclaw.plugin.json` manifest. Runtime behavior is registered by a plugin entry module that exports `definePluginEntry(...)`.

The local OpenClaw source at:

- `C:\Users\liveu\source\repos\openclaw\openclaw`

confirms several hard requirements that matter for XerahS:

- Plugin tools registered with `api.registerTool(...)` must be declared in `openclaw.plugin.json` under `contracts.tools`.
- Plugin CLI registrations should provide `descriptors` so OpenClaw can discover and route command roots without eagerly loading the full plugin runtime.
- OpenClaw looks for `cli-metadata.ts`, `cli-metadata.js`, `cli-metadata.mjs`, or `cli-metadata.cjs` for non-activating CLI metadata loads.
- `api.registerCliBackend(...)` is for local AI model CLIs such as Codex CLI and Gemini CLI. XerahS is not an inference backend and should not use that registration path.
- The plugin manifest must include a `configSchema`, even when the plugin has no required configuration.

This means XerahS should not extend its `.xsdp` uploader plugin packager for OpenClaw. `.xsdp` packages are XerahS uploader plugins. OpenClaw needs a separate native plugin layout.

## Goals

- Generate or ship an OpenClaw-native plugin for XerahS upload-to-URL workflows.
- Make XerahS upload capability visible through `openclaw plugins list`, `openclaw plugins inspect xerahs --json`, and `openclaw plugins doctor`.
- Expose file upload and text upload as first-class OpenClaw agent tools that return the uploaded URL.
- Provide diagnostic and bootstrap helpers only to support reliable upload setup.
- Register an optional `openclaw xerahs ...` CLI command group using descriptor-backed lazy registration for upload and diagnostics.
- Keep OpenClaw startup lightweight by separating CLI metadata from full runtime registration.
- Preserve XerahS as the system of record for uploader selection, upload execution, history recording, and settings.
- Avoid duplicating the XerahS MCP server contract inside the plugin.

## Non-Goals

- Do not make OpenClaw load XerahS .NET assemblies directly.
- Do not replace `xerahs-mcp`.
- Do not convert XerahS into an OpenClaw CLI backend provider.
- Do not turn the first plugin milestone into a general-purpose XerahS automation surface.
- Do not expose unsafe desktop capture or recording tools as always-on default agent tools.
- Do not expose history, workflow execution, settings mutation, editor, OCR, capture, or recording tools in the initial upload-focused milestone except where needed for upload diagnostics.
- Do not bundle uploader credentials, API keys, OpenClaw config files, or user secrets inside the generated plugin.
- Do not reuse `.xsdp` as an OpenClaw plugin format.

## Proposed User Experience

Add a XerahS CLI command:

```powershell
xerahs openclaw plugin export --output C:\Users\liveu\source\repos\xerahs-openclaw-plugin
```

The command writes a native OpenClaw plugin directory. During development, the user installs it with:

```powershell
openclaw plugins install -l C:\Users\liveu\source\repos\xerahs-openclaw-plugin
openclaw plugins enable xerahs
openclaw plugins inspect xerahs --runtime --json
openclaw plugins doctor
openclaw gateway restart
```

The plugin then contributes:

- `openclaw xerahs doctor`
- `openclaw xerahs bootstrap`
- `openclaw xerahs upload ...`
- OpenClaw agent tools focused on upload: `xerahs_upload_file`, `xerahs_upload_text`, `xerahs_doctor_uploaders`, and `xerahs_bootstrap_uploaders`.

The plugin invokes the configured XerahS CLI executable, defaults to `xerahs`, and allows an override through plugin config:

```json5
{
  plugins: {
    entries: {
      xerahs: {
        enabled: true,
        config: {
          command: "C:/Program Files/XerahS/xerahs.exe",
          timeoutMs: 120000
        }
      }
    }
  }
}
```

## Generated Plugin Layout

The export command should generate this minimum layout:

```text
xerahs-openclaw-plugin/
  package.json
  openclaw.plugin.json
  index.ts
  cli-metadata.ts
  src/
    cli.ts
    config.ts
    runner.ts
    tools.ts
```

### package.json

The generated `package.json` should declare a native OpenClaw entry point:

```json
{
  "name": "@xerahs/openclaw-plugin",
  "version": "0.1.0",
  "private": true,
  "description": "OpenClaw plugin for XerahS CLI automation",
  "type": "module",
  "dependencies": {
    "typebox": "1.1.37"
  },
  "peerDependencies": {
    "openclaw": ">=2026.5.6"
  },
  "peerDependenciesMeta": {
    "openclaw": {
      "optional": true
    }
  },
  "openclaw": {
    "extensions": [
      "./index.ts"
    ]
  }
}
```

When this plugin is eventually published instead of linked locally, the package should ship built JavaScript output and use `openclaw.runtimeExtensions` according to OpenClaw package install rules.

### openclaw.plugin.json

The manifest must declare tool ownership before runtime registration:

```json
{
  "id": "xerahs",
  "name": "XerahS",
  "description": "XerahS desktop capture, upload, and workflow automation for OpenClaw.",
  "activation": {
    "onStartup": false,
    "onCapabilities": ["tool"],
    "onCommands": ["xerahs"]
  },
  "contracts": {
    "tools": [
      "xerahs_doctor_uploaders",
      "xerahs_bootstrap_uploaders",
      "xerahs_upload_file",
      "xerahs_upload_text"
    ]
  },
  "commandAliases": [
    {
      "name": "xerahs"
    }
  ],
  "uiHints": {
    "command": {
      "label": "XerahS CLI command",
      "help": "Path or command name used to invoke the XerahS CLI."
    },
    "timeoutMs": {
      "label": "Command timeout",
      "help": "Maximum time in milliseconds for child xerahs commands."
    }
  },
  "configSchema": {
    "type": "object",
    "additionalProperties": false,
    "properties": {
      "command": {
        "type": "string"
      },
      "timeoutMs": {
        "type": "number",
        "minimum": 1000
      },
      "allowCaptureTools": {
        "type": "boolean"
      },
      "allowRecordingTools": {
        "type": "boolean"
      }
    }
  }
}
```

The initial `contracts.tools` list is intentionally upload-only plus setup diagnostics. Indexing, ReClip, capture, and recording should be introduced only after the upload-to-URL flow is stable and after a second scope review.

### cli-metadata.ts

The generated `cli-metadata.ts` should register descriptors only:

```ts
import { definePluginEntry } from "openclaw/plugin-sdk/plugin-entry";

export default definePluginEntry({
  id: "xerahs",
  name: "XerahS",
  description: "XerahS desktop capture, upload, and workflow automation for OpenClaw.",
  register(api) {
    api.registerCli(() => {}, {
      descriptors: [
        {
          name: "xerahs",
          description: "Run XerahS upload, diagnostic, and workflow helpers",
          hasSubcommands: true
        }
      ]
    });
  }
});
```

This follows OpenClaw's CLI metadata path and avoids importing the full command runner during root command discovery.

### index.ts

The runtime entry should register tools and the real CLI command registrar:

```ts
import { definePluginEntry } from "openclaw/plugin-sdk/plugin-entry";
import { resolveXerahSPluginConfig } from "./src/config.js";
import { registerXerahSCli } from "./src/cli.js";
import { createXerahSTools } from "./src/tools.js";

export default definePluginEntry({
  id: "xerahs",
  name: "XerahS",
  description: "XerahS desktop capture, upload, and workflow automation for OpenClaw.",
  register(api) {
    const config = resolveXerahSPluginConfig(api.pluginConfig);

    for (const tool of createXerahSTools(config)) {
      api.registerTool(tool);
    }

    api.registerCli(
      ({ program }) => {
        registerXerahSCli(program, config);
      },
      {
        descriptors: [
          {
            name: "xerahs",
            description: "Run XerahS upload, diagnostic, and workflow helpers",
            hasSubcommands: true
          }
        ]
      }
    );
  }
});
```

## Initial Tool Contract

The initial contract optimizes for one agent workflow:

1. Check whether XerahS uploaders are ready.
2. If needed, run safe uploader bootstrap.
3. Upload a file or generated text.
4. Return a machine-readable payload containing the final URL.

### xerahs_doctor_uploaders

Runs:

```powershell
xerahs doctor uploaders --json
```

Purpose:

- Let OpenClaw inspect whether XerahS has usable uploader defaults.
- Return structured diagnostic output.
- Avoid changing local configuration.

### xerahs_bootstrap_uploaders

Runs:

```powershell
xerahs bootstrap uploaders
```

Purpose:

- Apply safe first-use uploader defaults.
- Be idempotent.
- Return success/failure with captured stdout/stderr.

This tool mutates XerahS configuration and should be described accordingly.

### xerahs_upload_file

Runs:

```powershell
xerahs upload <file> --json
```

Parameters:

- `path` string, required
- `asFile` boolean, optional
- `name` string, optional

Purpose:

- Upload a local file through XerahS and return a shareable URL.
- Preserve XerahS uploader routing, history, and configured defaults.

Expected successful tool result:

```json
{
  "url": "https://example.com/uploaded/file.png",
  "filename": "file.png",
  "size": 12345,
  "type": "image/png"
}
```

Safety:

- The plugin should not bypass OpenClaw's own file access policy.
- The path is passed to the XerahS CLI, which performs its own validation.
- Tool output should include parsed JSON plus raw diagnostics only when needed.

### xerahs_upload_text

Runs:

```powershell
xerahs upload --text <text> --name <name> --json
```

Parameters:

- `text` string, required
- `name` string, optional, default `upload.txt`

Purpose:

- Let agents upload generated markdown, reports, logs, or snippets without creating temporary files themselves.
- Return the final shareable URL as structured JSON.

## Deferred Tool Contract

These tools are intentionally deferred:

- `xerahs_index_directory`
- `xerahs_reclip_status`
- `xerahs_capture_full_screen`
- `xerahs_capture_region`
- `xerahs_capture_window`
- `xerahs_record_screen`
- `xerahs_upload_clipboard`

Reasons:

- They are not required for the primary upload-and-return-URL workflow.
- They can expose local desktop contents.
- They may require interactive portals or desktop session state.
- They need clear OpenClaw tool descriptions so agents understand privacy and side effects.
- They may need explicit plugin config gates such as `allowCaptureTools` and `allowRecordingTools`.

## XerahS CLI Changes

Implement the export command inside:

- `src/desktop/cli/XerahS.CLI/Commands/OpenClawCommand.cs`

Proposed command tree:

```text
xerahs openclaw manifest
xerahs openclaw plugin export --output <directory> [--force]
xerahs openclaw plugin print --format files
```

`manifest` should remain backward compatible.

`plugin export` should:

- refuse to overwrite existing files unless `--force` is provided;
- create parent directories when safe;
- write deterministic file contents for stable tests;
- emit JSON when a future global `--json` option is available;
- avoid writing secrets or user-specific config into the generated plugin;
- include the current XerahS version in generated plugin metadata where available.

`plugin print` is optional. It would print the generated file list and planned contents without touching disk, useful for diagnostics and docs.

## Security Model

The plugin is a local bridge from OpenClaw to XerahS. Treat it as privileged local automation.

Security requirements:

- Do not embed credentials in generated files.
- Do not read OpenClaw config directly from XerahS.
- Do not write OpenClaw config directly from XerahS.
- Keep all XerahS invocations non-interactive unless the tool description explicitly says the command may open UI.
- Put mutating tools behind clear names and descriptions.
- Use bounded command timeouts.
- Capture stdout and stderr separately.
- Redact likely secrets from error payloads before returning them to the agent.
- Prefer `--json` XerahS commands and fail closed when JSON parsing fails for tools that require structured output.

The generated OpenClaw plugin should not attempt to bypass OpenClaw's own tool allow/deny policy. Operators can still restrict plugin tools through OpenClaw `tools.allow`, `tools.deny`, and `tools.profile`.

## Testing Plan

### XerahS Tests

Add tests under:

- `tests/XerahS.Tests/Tools`

Recommended coverage:

- `OpenClawCommand` still emits the existing manifest shape.
- `plugin export` creates `package.json`, `openclaw.plugin.json`, `index.ts`, `cli-metadata.ts`, and `src/*`.
- Generated `openclaw.plugin.json` includes every tool registered by `index.ts`.
- `plugin export` refuses to overwrite by default.
- `plugin export --force` overwrites only files in the requested output directory.
- Generated files contain no absolute local paths, API keys, or machine-specific config.

### OpenClaw Validation

Manual validation against local OpenClaw source:

```powershell
xerahs openclaw plugin export --output C:\Users\liveu\source\repos\xerahs-openclaw-plugin --force
openclaw plugins install -l C:\Users\liveu\source\repos\xerahs-openclaw-plugin
openclaw plugins enable xerahs
openclaw plugins inspect xerahs --runtime --json
openclaw plugins doctor
```

Expected result:

- `plugins inspect` reports `xerahs` as loaded.
- The plugin shape is non-capability or tool/command oriented.
- Registered tools match `contracts.tools`.
- CLI command `openclaw xerahs --help` appears without full runtime load during root command discovery.

### End-to-End Smoke

After XerahS upload defaults are configured:

```powershell
openclaw agent --message "Use the XerahS plugin to diagnose uploaders, then upload a short markdown note."
```

Expected result:

- The agent can call `xerahs_doctor_uploaders`.
- The agent can call `xerahs_upload_text`.
- The final response includes the uploaded URL.

## Implementation Order

| Step | Work | Notes |
|------|------|-------|
| 1 | Add a generator service for OpenClaw plugin files | Keep pure and deterministic for tests. |
| 2 | Add `xerahs openclaw plugin export` | Use `System.CommandLine` patterns already in the CLI. |
| 3 | Add tests for file generation and overwrite behavior | No OpenClaw runtime required. |
| 4 | Validate upload tool registration against local OpenClaw | Use linked install first. |
| 5 | Run an upload-to-URL smoke test | `xerahs_upload_text` should return a URL. |
| 6 | Add generated plugin docs | Include install, enable, inspect, doctor, upload examples, and troubleshooting. |
| 7 | Consider publishing a maintained plugin package | Only after linked local export is stable. |

## Open Questions

1. Should XerahS generate a local-only plugin by default, or should the repo also contain a maintained `plugins/openclaw` package?
2. Should capture and recording tools require separate explicit OpenClaw config gates?
3. Should generated tools call `xerahs` directly or prefer `xerahs-mcp` HTTP when available?
4. Should the plugin provide OpenClaw Control UI descriptors once the basic tool bridge is stable?
5. Should XerahS publish this plugin to ClawHub, npm, or only generate it locally?

## Acceptance Criteria

- `xerahs openclaw plugin export --output <dir>` writes a valid native OpenClaw plugin.
- Generated `openclaw.plugin.json` has a valid `configSchema`.
- Every generated `api.registerTool(...)` name is declared in `contracts.tools`.
- Generated CLI command registration uses `descriptors`.
- Generated `cli-metadata.ts` exists and does not import the full runner implementation.
- Linked install through `openclaw plugins install -l <dir>` succeeds.
- `openclaw plugins inspect xerahs --runtime --json` reports the expected tools and CLI command.
- `openclaw plugins doctor` reports no plugin issues for the generated plugin.
- The generated plugin can upload text through XerahS and return a structured URL result when uploaders are configured.
- The generated plugin can upload a file through XerahS and return a structured URL result when uploaders are configured.

## Rationale

This design follows OpenClaw's real plugin contracts instead of treating OpenClaw as a generic shell runner. The plugin remains small, auditable, and native to OpenClaw, while XerahS keeps ownership of its .NET runtime and CLI behavior.

The separation between `cli-metadata.ts` and `index.ts` matters. It lets OpenClaw discover the `xerahs` command cheaply, while runtime tools and child-process invocation load only when the plugin is actually used.

The proposal deliberately starts with the smallest useful bridge: upload content and return a URL. Diagnostic and bootstrap helpers exist only to make that upload path reliable. Desktop capture, recording, indexing, and adjacent automation should be added later only if they serve a clear OpenClaw workflow and pass a separate safety review.
