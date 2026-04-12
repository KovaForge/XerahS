# XIP0071 XerahS Spotlight Assistant

**Status**: Draft
**Priority**: High
**Area**: AI Integration | UX | Automation | Privacy
**Created**: 2026-04-12
**Related**: XIP0064 (XerahS MCP Server), XIP0068 (Re-Editing Saved Annotated Screenshots), XIP0069 (AfterCapture OCR Integration), XIP0070 (User Research - Top Screen Capture Needs)

---

## Summary

Add an in-app natural-language assistant to XerahS, summoned by a keyboard shortcut in the style of Apple Spotlight.

The feature is not a general chatbot and is not an external automation endpoint. It is a first-party command overlay for XerahS-owned actions and XerahS-owned data. Users should be able to ask for things like "give me the local file path of my last 5 screenshots", "OCR the latest screenshot", or "upload the latest capture", and have XerahS perform the matching local workflow through approved internal tools.

The assistant should be local-first and privacy-first. It should prefer structured metadata and tool results over sending screenshot pixels or arbitrary files to a model. External AI providers are optional and user-configured through bring-your-own-key settings.

---

## Problem Statement

XerahS already has many power-user capabilities: capture, history, annotation, OCR, upload destinations, workflows, and MCP-accessible runtime operations. The problem is discoverability and speed. Users often know the outcome they want, but not the exact menu, hotkey, history filter, workflow, or tool chain needed to reach it.

Examples:

- "Give me the local file path of the last 5 screenshots."
- "Copy the path of the latest screenshot."
- "OCR the latest screenshot and copy the text."
- "Open the most recent capture in the editor."
- "Upload the last capture to my default destination."
- "Find screenshots from yesterday that mention invoice."

These are XerahS tasks, not open-ended web assistant tasks. A natural-language command overlay can make existing capabilities feel faster without compromising the app's local-first posture.

---

## Goals

- Provide a summonable in-app assistant overlay with a configurable keyboard shortcut.
- Treat the assistant as a natural-language command palette for XerahS, not a general chatbot.
- Route assistant actions through approved XerahS services and tool contracts.
- Support user-provided AI provider API keys without bundling a mandatory cloud service.
- Keep metadata-only requests metadata-only; do not send screenshot pixels or file contents to AI providers unless the user explicitly requests an image/content analysis action.
- Require confirmation for external sharing, uploads, destructive operations, or any action that sends image/file content to an AI provider.
- Reuse existing XerahS runtime surfaces where practical, including the XIP0064 MCP/tool contract, without exposing this feature as a new external control channel.

---

## Non-Goals

- Building a general-purpose chatbot inside XerahS.
- Allowing external assistants to control the desktop app through this XIP.
- Scanning arbitrary filesystem locations outside XerahS history and configured capture/output folders.
- Automatically uploading, deleting, overwriting, or sharing files without user confirmation.
- Sending raw screenshot pixels, clipboard contents, file contents, uploader secrets, or API keys to an AI provider by default.
- Replacing existing menus, workflow editors, or settings pages.
- Implementing fully autonomous multi-step agents in the first version.

---

## User Experience

### Summoning

XerahS should expose a configurable assistant hotkey, with a default candidate of `Ctrl+Shift+Space` on desktop platforms unless it conflicts with an existing XerahS or OS shortcut.

When invoked, XerahS opens a lightweight centered overlay:

- single-line natural-language input
- recent commands or suggested examples
- inline results area
- explicit action buttons for follow-up actions such as Copy, Open, Reveal, Upload, or Run OCR

The overlay should close on `Esc` and should preserve the user's current app context. It should not feel like launching a separate chat application.

### Example Flow: Last 5 Screenshots

User prompt:

```text
give me local file path of last 5 screenshots
```

Expected behavior:

1. Assistant classifies the request as a history lookup.
2. XerahS calls a safe internal history query with image file filtering, newest-first ordering, and `limit=5`.
3. The model receives only the minimum tool schema and safe result metadata needed to format the answer.
4. The overlay returns the five local paths with a `Copy paths` action.
5. No screenshot pixels are sent to the model.
6. No arbitrary filesystem scan is performed.

### Example Flow: OCR Latest Screenshot

User prompt:

```text
OCR latest screenshot and copy the text
```

Expected behavior:

1. Assistant resolves the latest image capture from XerahS history.
2. XerahS runs OCR through the local OCR service when available.
3. The recognized text is shown in the overlay and copied to clipboard if the user prompt clearly requested copying.
4. If OCR would require a cloud model, XerahS asks for explicit confirmation before sending image content.

### Example Flow: Upload Latest Capture

User prompt:

```text
upload the latest capture
```

Expected behavior:

1. Assistant resolves the latest capture from history.
2. XerahS shows a confirmation step naming the file and destination.
3. Only after confirmation, XerahS uploads through the configured uploader system.
4. The resulting URL is shown with a `Copy URL` action.

---

## Architecture

### 1. Assistant Overlay

Add a desktop UI surface for the assistant:

- `AssistantOverlayWindow` or equivalent lightweight top-level view
- `AssistantViewModel` for input, result state, pending confirmations, and action buttons
- keyboard shortcut registration in the existing hotkey/settings infrastructure
- recent prompt history stored locally, with an option to disable history

The overlay should use normal XerahS styling and accessibility conventions. It should be fast to open and should avoid starting heavy AI provider initialization on the UI thread.

### 2. Assistant Orchestrator

Introduce a small orchestration service, for example `IAssistantService`, responsible for:

- accepting the raw user prompt
- resolving deterministic commands that do not need an AI model
- invoking the selected AI provider only when needed
- validating model-selected tool calls against an allowlist
- executing approved XerahS tools
- returning a structured assistant response to the UI

The orchestrator should make the trust boundary explicit. AI output is never executed directly as code or shell commands. The model may only request known XerahS actions with typed arguments.

### 3. Tool Surface

Initial tools should be intentionally small:

| Tool | Purpose | Confirmation |
|---|---|---|
| `history.search` | Query XerahS history by type, date, text, and limit | No for metadata-only results |
| `history.latest` | Get the latest capture or latest image capture | No |
| `clipboard.copy_text` | Copy assistant result text, paths, or OCR text | No when requested directly |
| `file.reveal` | Reveal a known history item in the OS file manager | No |
| `editor.open_image` | Open a known history image in the XerahS editor | No |
| `ocr.run` | Run OCR on a known history image | No for local OCR; yes for cloud OCR |
| `upload.file` | Upload a known file through configured XerahS uploaders | Yes |

The tool names above are planning names. Implementation may map them to existing services or to the XIP0064 MCP runtime contract if that remains the cleanest internal boundary.

### 4. Provider Layer

Add a provider abstraction, for example `IAssistantModelProvider`, with support for:

- provider ID
- model ID
- text-only tool-calling request
- optional image/content analysis request
- token and cost metadata where available
- cancellation

Initial providers can be staged. The first implementation only needs one provider to validate the architecture, but settings should not bake in a single vendor forever.

Candidate providers:

- OpenAI-compatible endpoint
- OpenRouter
- Ollama or another local OpenAI-compatible endpoint

API keys should be stored in the OS credential store where practical. If XerahS must fall back to app config on a platform, the UI should clearly label the storage behavior.

### 5. Privacy and Consent Guard

Add a central policy layer, for example `IAssistantPrivacyGuard`, to classify requests and enforce consent:

- metadata-only local query
- local file operation on known XerahS history item
- clipboard write
- external upload/share
- cloud AI text request
- cloud AI image/file-content request
- destructive operation

The guard should produce user-facing confirmation text before risky actions. It should also provide audit-friendly structured events for logs without recording secrets or prompt contents by default.

---

## Implementation Plan

### Phase 0 - Product and Safety Contract

- Define assistant scope as in-app only.
- Write the initial allowlisted tool contract.
- Decide default shortcut and conflict behavior.
- Define consent categories and confirmation copy.
- Decide where provider keys are stored on Windows, macOS, and Linux.
- Add tests for the privacy guard's classification rules.

Exit criteria:

- XerahS has a documented assistant trust boundary.
- No implementation path requires arbitrary filesystem search or shell execution.

### Phase 1 - Spotlight Shell and Deterministic Commands

- Add the overlay UI and view model.
- Add settings for enabling/disabling the assistant and configuring its shortcut.
- Implement deterministic local command routing for a small command set:
  - last N screenshots
  - latest screenshot path
  - copy latest screenshot path
  - open latest screenshot
  - reveal latest screenshot
- Use XerahS history services for all file lookup.
- Do not require an AI provider for Phase 1.

Exit criteria:

- A user can press the shortcut and ask for the last 5 screenshot paths.
- The answer comes from XerahS history.
- The result can be copied from the overlay.
- No model call is required for the core example.

### Phase 2 - BYOK Provider Settings and AI Intent Routing

- Add assistant provider settings UI.
- Add provider/key validation without exposing full keys in UI or logs.
- Implement one provider path using strict tool calling.
- Add model routing only after deterministic command matching fails or needs disambiguation.
- Validate all model-requested tool calls against typed schemas and the privacy guard.
- Add cancellation and timeout handling.

Exit criteria:

- Users can configure an AI provider key.
- The assistant can translate flexible natural language into the existing Phase 1 tools.
- Invalid or unsupported tool requests fail safely with a plain explanation.

### Phase 3 - OCR and Upload Workflows

- Add `ocr.run` over the existing local OCR path.
- Add `upload.file` over the configured uploader system.
- Require confirmation before upload.
- Add result actions for copying OCR text and uploaded URLs.
- Add telemetry/logging hooks that record action categories, not private content.

Exit criteria:

- "OCR latest screenshot and copy the text" works with local OCR.
- "Upload latest capture" prompts before upload and then returns a URL.
- Cloud image analysis requires explicit consent before any image bytes leave the machine.

### Phase 4 - Rich XerahS Workflows

- Add workflow execution for safe configured workflows.
- Add annotation-oriented assistant actions where the editor/runtime supports typed operations.
- Add user aliases such as "bug report shot" or "copy last five paths".
- Add recent assistant commands and pinning.
- Add tests for multi-step action planning and rollback behavior where needed.

Exit criteria:

- The assistant can run useful multi-step XerahS workflows without becoming a general autonomous agent.
- Risky steps remain confirmable and visible to the user.

---

## Privacy Rules

1. XerahS history metadata may be used for local answers.
2. Screenshot pixels are not sent to AI providers for metadata-only tasks.
3. API keys are never included in prompts, logs, tool results, MCP-safe settings, or assistant transcripts.
4. Uploads and external sharing require confirmation.
5. Destructive actions are out of scope for the initial version.
6. The assistant may only operate on XerahS-known files unless a later XIP explicitly expands the scope.
7. Prompt history should be local and user-controllable.
8. The UI must make it clear when a cloud model will receive text, image, or file content.

---

## Open Questions

- What should the default shortcut be on Windows, macOS, and Linux?
- Should the overlay be available when XerahS is only running in the tray?
- Should Phase 1 include a deterministic parser only, or a small built-in command grammar with suggestions?
- Which provider should ship first for BYOK validation?
- Should prompt history be enabled by default or opt-in?
- Should local model support be a first-class Phase 2 requirement or a Phase 3/4 addition?
- How should the assistant display paths on platforms where history items may be remote, deleted, or moved?

---

## Initial Use Case Backlog

### History and File Lookup

- "Give me the local file path of my last 5 screenshots."
- "Copy the path of the latest screenshot."
- "Show screenshots from yesterday."
- "Find screenshots with OCR text containing invoice."
- "Reveal the latest capture in Explorer/Finder/files."

### OCR

- "OCR the latest screenshot."
- "OCR the latest screenshot and copy the text."
- "Find my last screenshot that contains this text."

### Upload and Sharing

- "Upload the latest capture."
- "Upload the last screenshot to my default image host and copy the URL."
- "Copy the latest uploaded URL again."

### Editing

- "Open the latest screenshot in the editor."
- "Open the last annotated screenshot for re-editing."
- "Blur emails in the latest screenshot." This requires a later typed annotation/content-analysis design and should not be in the first implementation slice.

---

## Risks

- The feature can become too broad if it is treated as a general assistant instead of a XerahS command overlay.
- Cloud provider integration can create privacy surprises if consent and data boundaries are not designed first.
- Model-generated tool calls can become unsafe if the allowlist is loose or accepts arbitrary paths.
- Hotkey handling can conflict with OS and desktop-environment shortcuts.
- Provider-specific APIs can leak into the app if the provider abstraction is designed too late.

---

## Acceptance Criteria for the First Shipping Slice

- The assistant is in-app only and summoned by a configurable shortcut.
- The overlay opens quickly and can be dismissed with `Esc`.
- The command "give me local file path of last 5 screenshots" returns up to five newest image capture paths from XerahS history.
- The result includes a copy action.
- The feature works without an AI API key for the initial deterministic commands.
- No arbitrary filesystem scan is performed.
- No screenshot pixels or file contents are sent to any AI provider for the first shipping slice.
- Unit tests cover command routing for the initial deterministic commands.
- Unit tests cover privacy guard behavior for metadata-only lookup, upload confirmation, and cloud image/content requests.

