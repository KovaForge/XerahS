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

## Provider Research Update - 2026-04-12

Initial provider research should shape the implementation around provider adapters, not one hard-coded request format:

- OpenAI recommends the Responses API for new agent-like applications, including native multimodal support and tool use. Its function-calling guide also recommends strict schemas and keeping the number of functions small for accuracy. Source: https://platform.openai.com/docs/guides/responses-vs-chat-completions and https://platform.openai.com/docs/guides/function-calling
- MiniMax documents text generation models that can produce conversation output and tool calls, and supports HTTP, Anthropic SDK compatibility, and OpenAI SDK compatibility. Source: https://platform.minimaxi.com/docs/api-reference/api-overview
- Kimi documents Kimi K2.5 as OpenAI API-format compatible using `base_url="https://api.moonshot.ai/v1"`, supports multimodal input, and includes an OpenAI-style tool-calling loop. Source: https://platform.kimi.ai/docs/guide/kimi-k2-5-quickstart
- Gemini uses the Gemini API `generateContent` endpoint and its own function-declaration/tool-config model. Gemini API REST calls use the `x-goog-api-key` header. Source: https://ai.google.dev/gemini-api/docs/function-calling and https://ai.google.dev/gemini-api/docs/api-key
- Anthropic uses the Messages API with `x-api-key`, `anthropic-version`, and JSON content headers. Claude tool use returns structured client-side tool calls that the application executes and feeds back as tool results. Source: https://docs.anthropic.com/en/api/overview and https://docs.anthropic.com/en/docs/agents-and-tools/tool-use/overview

Design implication: XerahS should define one internal assistant request/tool model, then adapt it per provider. OpenAI-compatible providers can share adapter code only where their tool-call and multimodal behavior actually matches. Gemini and Anthropic should have dedicated adapters because their request/response envelopes differ.

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
- Support a unified provider selection flow for OpenAI, MiniMax, Kimi, Gemini, and Anthropic.
- Let the user select a provider from a dropdown, input an API key, optionally select or override the model ID, validate the key, and set that provider as active.
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

▲ Nadia clarification resolved: Q33, Q34, Q35, Q36, Q37, Q38, Q39, Q40

XerahS should expose a configurable assistant hotkey:
- **Windows/Linux**: `Ctrl+Shift+Space`
- **macOS**: `Cmd+Shift+Space`
- **Fallback**: Double-tap `Ctrl` (Ctrl, Ctrl within 300ms) when conflicts detected on any platform

Hotkey conflict detection: Attempt to register the default shortcut. If registration fails, try the fallback. If fallback fails, prompt user to configure manually. Show a "Test Shortcut" button in settings. (Nadia Q33, Q34)

When invoked, XerahS opens a lightweight centered overlay:

- single-line natural-language input
- recent commands or suggested examples
- inline results area
- explicit action buttons for follow-up actions such as Copy, Open, Reveal, Upload, or Run OCR

**Overlay behavior**:
- Available when XerahS is tray-only, minimized, or in editor
- NOT available during modal dialogs or active capture (would interfere with workflow)
- If overlay opens during another XerahS workflow, commands attach to current editor/capture state if applicable
- Opens within **150ms** of hotkey press. Defer off UI thread: provider list loading (lazy), validation status checks (async), model metadata fetch (background), history preloading (on first open)
- Input stays enabled during in-flight requests so user can Esc to cancel

**Positioning (multi-monitor/DPI)**:
- Open on monitor containing mouse cursor at hotkey press time
- Centered horizontally, 100px from top edge
- Width: 600px fixed, max-height: 400px (results scroll, window doesn't stretch)
- High-DPI: Scale by `DPI / 96`, round to even pixels
- Multi-monitor: Track monitor change; if monitor disconnected, move to primary
- Z-order: Topmost when open, normal when unfocused

**Esc key behavior**:
- Pending confirmation → dismiss confirmation, return to input
- In-flight provider/tool call → cancel request, show "Cancelled" status, keep overlay open
- Input has text → clear input (first Esc), close overlay (second Esc within 2s)
- Input empty → close overlay immediately (Nadia Q38)

**Keyboard accessibility**:
- Tab/Shift+Tab cycles: input → results → action buttons → close button → input
- Enter submits from input; Ctrl+Enter forces new line in multiline
- Arrow keys navigate result list (Down = next, Up = previous, Home = first, End = last)
- Focus visible indicator: 2px `#0A84FF` outline, high contrast, instant (no animation)
- Screen reader: `aria-live="polite"` on results, `aria-live="assertive"` on errors

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

▲ Nadia clarification resolved: Q42, Q44, Q45, Q47, Q48

Initial tools are intentionally small:

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

#### Exact Typed Arguments and Result Payloads

`history.search`:
- Args: `{ query?: string, dateFrom?: ISO8601, dateTo?: ISO8601, type?: 'screenshot'|'recording'|'all', limit: number(1-50) }`
- Result: `{ items: HistoryItem[], total: number }`
- HistoryItem: `{ id: string, filePath: string, fileName: string, capturedAt: ISO8601, type: string, ocrText?: string, exists: boolean }`

`history.latest`:
- Args: `{ type?: 'screenshot'|'recording'|'all', limit: number(1-10) }`
- Result: `{ items: HistoryItem[], total: number }`

`clipboard.copy_text`:
- Args: `{ text: string }`
- Result: `{ success: boolean }`

`file.reveal`:
- Args: `{ filePath: string }`
- Result: `{ success: boolean, error?: string }`

`editor.open_image`:
- Args: `{ filePath: string }`
- Result: `{ success: boolean, editorInstanceId?: string }`

`ocr.run`:
- Args: `{ filePath: string, language?: string }`
- Result: `{ text: string, confidence: number, engine: string }`

`upload.file`:
- Args: `{ filePath: string, destination?: string }`
- Result: `{ success: boolean, url?: string, error?: string }`

#### Schema Versioning

Tool schemas are versioned under `schemaVersion: "1.0.0"` in `AssistantToolSchema.json`. Provider prompts include version in system message. Breaking changes bump major version; additive changes bump minor. (Nadia Q45)

#### Multi-Step Plans

Rejected entirely in Phase 1. Assistant executes single tool call per response. Multi-step plans (tool A → tool B) are not supported. Phase 2 may add chained workflows with explicit user consent between steps. (Nadia Q47)

#### Approved MCP Tools (XIP0064)

`history.search`, `history.latest`, `clipboard.copy_text`, `clipboard.copy_image`, `file.reveal`, `file.open`, `editor.open_image`, `ocr.run`, `upload.file`. All enforce privacy/path allowlist rules. History tools return only paths within configured capture folders. File tools validate paths against history tool results (no arbitrary paths). (Nadia Q42)

### 4. Provider Layer

Add a provider-neutral abstraction, for example `IAssistantModelProvider`, with support for:

- provider ID
- display name
- provider family or protocol
- model ID
- text-only tool-calling request
- optional image/content analysis request
- token and cost metadata where available
- cancellation

The internal XerahS assistant request should be provider-independent:

```csharp
public sealed record AssistantModelRequest(
    string ProviderId,
    string ModelId,
    IReadOnlyList<AssistantMessage> Messages,
    IReadOnlyList<AssistantToolDefinition> Tools,
    AssistantPrivacyScope PrivacyScope,
    bool AllowImageContent);
```

The model provider should return a provider-independent result:

```csharp
public sealed record AssistantModelResult(
    AssistantModelResultKind Kind,
    string? Text,
    IReadOnlyList<AssistantToolCall> ToolCalls,
    AssistantUsage? Usage,
    string? ProviderRequestId);
```

Provider adapters translate that internal model into each provider's transport and tool format. This avoids leaking provider-specific envelopes into the assistant orchestrator and privacy guard.

Initial provider support:

| Provider | Adapter strategy | Key/header expectation | Initial model default |
|---|---|---|---|
| OpenAI | Native OpenAI adapter using Responses API for new agentic work; Chat Completions compatibility only if required by SDK/runtime constraints | Bearer API key | `gpt-5.4` |
| MiniMax | Dedicated MiniMax adapter; prefer the documented compatible API path that best matches tool calling after implementation validation | MiniMax API key | `MiniMax-M2.7` |
| Kimi | OpenAI-compatible adapter using Moonshot/Kimi base URL and Kimi model IDs | Bearer API key against `https://api.moonshot.ai/v1` | `kimi-k2.5` |
| Gemini | Dedicated Gemini adapter using `generateContent`, function declarations, and function calling config | `x-goog-api-key` | `gemini-3.1-flash` |
| Anthropic | Dedicated Anthropic Messages adapter using client-side tools and tool-result loop | `x-api-key` plus `anthropic-version` | `claude-sonnet-4-6` |

> Model defaults owned by Product Owner (Vladislava Kova). Engineering Lead (Mikhail Orlov) executes updates. Review quarterly or when providers announce deprecations. (Nadia Q4)

#### Provider Model Lists

Combination approach: hard-code curated default list per provider (10–15 models max, known tool-capable), fetch from provider APIs on demand when user opens model selector (cache 24h), allow user-entered custom model IDs with a "Custom" option. Never block user from entering a model ID not in the cached list. (Nadia Q5)

#### Validation Behavior

Always issue a minimal tool-capable request (1 token max, single tool, 10s timeout). Model-list/status endpoints don't prove key validity for completion calls. UI must warn: "Validating your API key will send a minimal request that may incur a small charge (<$0.001)." Checkbox: "Don't warn me again for this provider." (Nadia Q6, Q8)

#### Timeout, Cancellation, Retry, Rate-Limit

- Validation timeout: 10s hard limit
- Model call timeout: 30s for text, 60s if image input
- Cancellation: user-initiated aborts HTTP request, discards partial response
- Retry: max 2 retries for 5xx errors, 0 retries for 4xx client errors, exponential backoff 1s/2s
- Rate-limit: honor `Retry-After` header if present; otherwise back off 5s and retry once (Nadia Q7)

#### BaseUrl and SSRF Protection

BaseUrl editable for all providers (UI shows field only when Advanced toggle enabled). SSRF protections required:
- Block private IP ranges: 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 127.0.0.0/8, ::1/128, fc00::/7, fe80::/10
- Block localhost variants: `localhost`, `*.localhost`
- Block non-HTTP schemes: `file://`, `gopher://`, `ftp://` (allow only http/https)
- Enforce TLS 1.2+ for HTTPS
- User must explicitly confirm when BaseUrl doesn't match known provider domains (Nadia Q11, Q12)

#### Secret Storage

- Windows: Windows Credential Manager
- macOS: Keychain (kSecClassGenericPassword)
- Linux: Secret Service API / GNOME Keyring / KDE Wallet
- Fallback: AES-256-GCM encrypted file in app config directory
- Secret key pattern: `xerahs:assistant:provider:{ProviderId}:apikey` (Nadia Q9, Q10)

#### SDK vs Raw HTTP

Raw HTTP preferred. Use `HttpClient` with provider-specific request/response models. Avoid SDK dependencies to minimize platform constraints and binary size. OpenAI SDK acceptable if already in repo; evaluate others case-by-case. Zero additional native dependencies beyond existing. (Nadia Q68)

#### Provider-Specific Configurable Differences

Store per-provider in `ProviderConfiguration.json`:
- `MaxTokensParameterName`: `"max_tokens"` vs `"max_output_tokens"` (Gemini)
- `ToolChoiceFormat`: `"auto"` vs `"required"` vs object format
- `SystemMessageRole`: `"system"` vs `"developer"` (OpenAI o1)
- `ImageUrlFormat`: data URI vs separate image object
- `StreamDelimiter`: newline vs SSE vs JSON lines
- `RateLimitHeaders`: `x-ratelimit-remaining` vs `x-ratelimit-limit`
- `AuthHeader`: `Authorization: Bearer` vs `Authorization: Api-Key` (Nadia Q69)

Model defaults must be checked against provider docs during implementation because model IDs change over time. The settings UI should let users override the model ID instead of requiring a code change for every provider model update.

### 5. Provider Settings UX

Add an Assistant Providers settings section to the global application settings UI.

Placement:

- The settings belong in `src/desktop/app/XerahS.UI/Views/ApplicationSettingsView.axaml`.
- The first planned location is the existing `Integration` tab, near the current MCP Server section, because these settings configure app-wide AI provider integration rather than a capture workflow, uploader destination, or per-task behavior.
- The bindings should live on the existing `SettingsViewModel` surface, with supporting models/services added as needed for provider selection, key validation, and active-provider state.
- The settings should not be placed in task settings, after-capture settings, workflow settings, or the assistant overlay itself. The overlay can link to these settings when no active provider is configured.

Controls:

- Provider dropdown: OpenAI, MiniMax, Kimi, Gemini, Anthropic.
- API key input: masked by default, reveal button optional, never logged.
- Model dropdown or text field: prefilled default, editable for advanced users.
- Endpoint/base URL field only where needed:
  - hidden for fixed-provider defaults
  - shown for Kimi/Moonshot and any OpenAI-compatible override
  - optional advanced setting for local/proxy endpoints later
- `Validate key` action that performs a minimal provider-specific test request without sending screenshots, file contents, or history data.
- `Set active` action that stores the selected provider configuration as the current assistant provider.
- masked provider status in UI, such as `OpenAI - active - key ending in abcd`.
- `Remove key` and `Deactivate provider` actions.

Provider configuration should be split between non-secret settings and secret storage:

```csharp
public sealed record AssistantProviderSettings(
    string ProviderId,
    string DisplayName,
    string ModelId,
    string? BaseUrl,
    bool IsActive,
    bool IsConfigured,   // derived: !string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(ModelId)
    bool IsValidated,   // separate flag for validation status
    DateTimeOffset? LastValidatedAt);
```

`IsConfigured` derives from secret-store presence AND non-empty model ID — NOT from validation status. `IsValidated` is a separate flag. Users may set a provider active before validation completes, with a warning shown. Key removal while provider is active requires confirmation. (Nadia Q73–Q75)

The API key itself should live in OS-backed secret storage where practical. App config should only store the provider ID, model ID, base URL, active state, masked key preview, and validation metadata.

### 6. Provider Capability Matrix

The assistant should not assume every provider can do every task. Add provider capability metadata:

| Capability | OpenAI | MiniMax | Kimi | Gemini | Anthropic |
|---|---|---|---|---|---|
| Text intent routing | Yes | Yes | Yes | Yes | Yes |
| Tool/function calling | Yes | Yes, via compatible APIs | Yes, OpenAI-style | Yes, Gemini function declarations | Yes, Claude tool use |
| Image input | Yes, model-dependent | Model-dependent | Yes for Kimi K2.5 | Yes, model-dependent | Yes, model-dependent |
| Streaming | Later | Later | Later | Later | Later |
| Local deterministic commands without key | Not needed | Not needed | Not needed | Not needed | Not needed |

XerahS should gate UI affordances and privacy prompts based on these capabilities. For example, if the active model is text-only, image-content analysis prompts should fail with a clear message instead of trying to upload pixels.

Initial providers can be staged. The first implementation only needs two providers to validate the architecture, but settings should not bake in a single vendor forever. One of the first two providers should use an OpenAI-compatible envelope and one should use a non-OpenAI envelope.

### 7. Privacy and Consent Guard

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

## Design Specifications

▲ Sofia Novak clarification resolved: Q1–Q10 (full UI/UX specifications)

This section defines the visual and interaction design for the XerahS Assistant overlay. Design philosophy: **command overlay, not chatbot**. Think Spotlight meets a well-designed developer tool — fast, scoped, trustworthy.

### Color Palette

| Role | Hex | Usage |
|---|---|---|
| Background | `#1C1C1E` | Overlay surface |
| Surface elevated | `#2C2C2E` | Input field, cards |
| Surface hover | `#3A3A3C` | Hovered states |
| Border subtle | `#48484A` | Card borders, dividers |
| Text primary | `#F5F5F7` | Headings, filenames |
| Text secondary | `#98989D` | Timestamps, metadata |
| Text muted | `#636366` | Placeholders, disabled |
| Accent blue | `#0A84FF` | Primary actions, focus rings |
| Accent blue hover | `#409CFF` | Button hover |
| Success green | `#30D158` | Provider active dot |
| Warning yellow | `#FFD60A` | Provider configured-not-validated, privacy notices |
| Error red | `#FF453A` | Error states, validation failures |

### Typography

```
Font stack: -apple-system, BlinkMacSystemFont, "Segoe UI Variable", "Segoe UI", system-ui, sans-serif

Sizes:
  Overlay title:     15px / 600 weight / #F5F5F7
  Input text:        16px / 400 weight / #F5F5F7
  Input placeholder: 16px / 400 weight / #636366
  Card title:       14px / 500 weight / #F5F5F7
  Card metadata:    12px / 400 weight / #98989D
  Button text:      13px / 500 weight / #F5F5F7
  Caption/hint:     11px / 400 weight / #636366
  Error message:    13px / 400 weight / #FF453A

Monospace (paths, code): "JetBrains Mono", "Cascadia Code", "SF Mono", "Consolas", monospace
```

### Spacing System — 8pt Grid

| Value | Usage |
|---|---|
| 4px | Icon-to-text gap, tight internal padding |
| 8px | Default padding between sibling elements |
| 12px | Card internal padding (top/bottom) |
| 16px | Card horizontal padding, section gaps |
| 24px | Header-to-content gap, modal sections |
| 32px | Overlay edge-to-content |

### Overlay Window

| Property | Value |
|---|---|
| Width | `600px` fixed |
| Height | `auto`, max `400px` |
| Border radius | `12px` |
| Shadow | `0 8px 32px rgba(0,0,0,0.28), 0 2px 8px rgba(0,0,0,0.12)` |
| Backdrop | `rgba(20,20,20,0.72)` blur `20px` over desktop |

### Header Layout

```
┌─────────────────────────────────────────────────┐
│ [●]  XerahS Assistant           [Esc: close] [×] │  ← 40px height, 16px horizontal padding
│ ────────────────────────────────────────────── │
│ [  Ask anything...                          ]   │  ← Input: 48px height, 10px radius, #2C2C2E bg
│ ────────────────────────────────────────────── │
│ [ results / suggestions / empty state          ]│  ← Scrollable results area, max ~280px
└─────────────────────────────────────────────────┘
```

### Input Field

| Property | Value |
|---|---|
| Height | 48px |
| Radius | 10px |
| Background | `#2C2C2E` |
| Border (default) | 1px solid `#48484A` |
| Border (focused) | 2px solid `#0A84FF` + `0 0 0 3px rgba(10,132,255,0.25)` shadow |
| Padding | 0 16px |
| Placeholder | "Ask anything..." in `#636366` |
| Icon | Search icon (16px, `#636366`) left edge, 12px gap |

### Result Cards

**Single result card** (latest screenshot):
```
ResultCard (gap: 8px between cards, padding: 12px 16px)
├── Thumbnail: 48×48px, radius 6px, object-fit cover, #3A3A3C placeholder
├── TextBlock
│   ├── Filename: 14px / 500 / #F5F5F7 (middle ellipsis if > 28 chars)
│   └── Timestamp: 12px / 400 / #98989D ("Jan 15, 2:30 PM" / relative < 24h)
└── ActionBar: [Copy path] [Open] [Reveal] [Upload] — ghost buttons, 13px
```

**Last 5 screenshots**: Vertical stack of 5 compact rows, no thumbnails.
```
Each row: filename (14px, truncated) | timestamp (right-aligned) | [Copy] [Reveal] icon buttons only
Caption: "Showing 5 of 5 recent captures" — #636366
```

**Action buttons** (ghost style):
| State | Background | Text Color |
|---|---|---|
| Default | transparent | `#98989D` |
| Hover | `#3A3A3C` | `#F5F5F7` |
| Active | `#48484A` | `#0A84FF` |
| Disabled | transparent | `#636366` | opacity 0.4 |

**Path result display**:
```
Font: JetBrains Mono / Cascadia Code / Consolas, 13px, #F5F5F7
Background: #2C2C2E, padding 12px 16px, radius 8px
Max height: 160px (scrollable)
Tooltip on filename hover: full path, monospace, no truncation, max-width 400px
Copied! badge: #30D158 text, rgba(48,209,88,0.12) background, 2px 8px padding, 1.5s visible
```

### Confirmation Dialog (Modal)

| Property | Value |
|---|---|
| Width | 440px |
| Radius | 14px |
| Padding | 24px |
| Shadow | `0 16px 48px rgba(0,0,0,0.36)` |

```
┌─────────────────────────────────────────────────┐
│  [Icon]  Confirmation Title                     │  ← 18px / 600
│          Body text                                │  ← 14px / 400 / #F5F5F7
│  ┌───────────────────────────────────────────┐  │
│  │ File: screenshot_2025-01-15.png            │  │  ← #2C2C2E, 8px radius
│  │ Size: 240 KB                               │  │
│  │ To:   Imgur (imgur.com)                   │  │
│  └───────────────────────────────────────────┘  │
│  ⚠️ Privacy notice (12px / #FFD60A)             │
│  [ ] Remember this choice for today (13px)      │
│              [Cancel]        [Confirm]          │
└─────────────────────────────────────────────────┘
```

**Buttons**: [Cancel] ghost (#98989D), [Confirm] filled (#0A84FF, white text). Tab order: Cancel → Confirm.

**Metadata card**: Label (11px / 500 / #636366 uppercase) stacked above value (13px / 400 / #F5F5F7).

### Provider Status Indicator

| Property | Value |
|---|---|
| Dot size | 8×8px circle |
| Position | Header, left of title text |
| Green `#30D158` | Provider active and validated (pulse: opacity 1→0.7→1, 2s ease-in-out infinite) |
| Yellow `#FFD60A` | Configured but not validated |
| Red `#FF453A` | No provider configured |
| Clickable | Opens provider settings, pointer cursor, underline on hover |
| Tooltip | 400ms delay, max 200px wide, #3A3A3C background |

Tooltip content:
- Green: `"{Provider} — active\nLast validated: {date}"`
- Yellow: `"{Provider} — configured\nNot yet validated"`
- Red: `"No AI provider\nClick to configure"`

### Hotkey Conflict Toast

| Property | Value |
|---|---|
| Position | Bottom center, 32px from bottom edge |
| Width | min 280px, max 480px |
| Duration | 5 seconds, then auto-dismiss |
| Background | `#2C2C2E` |
| Border | 1px solid `#48484A` |
| Non-blocking | User can still interact with XerahS |
| Animation | Slide up + fade in (200ms ease-out), fade out (150ms) |

```
⚠️  Shortcut conflict detected.
    Ctrl+Shift+Space conflicts with {app}.
    [Open Settings]
```

### Error States

```
Card: background #2C2C2E, border 1px solid #FF453A, left-accent 3px solid #FF453A, radius 8px
Icon: error-circle, 16px, #FF453A, left of text
Title: 14px / 600 / #F5F5F7
Body: 13px / 400 / #FF453A
Retry: ghost button, #0A84FF, right side of card
Multiple errors: "2 errors" label above stack, 8px gap, 50ms stagger fade-in
```

**Error messages** (Nadia Q76):
- No recent captures: "No recent captures found. Try taking a screenshot first."
- File unavailable: "File no longer available. It may have been moved or deleted."
- OCR not configured: "OCR is not configured. Enable OCR in Settings > Capture."
- No uploader: "No upload destination configured. Add a destination in Settings > Upload."
- Provider not configured: "Assistant provider not configured. Add an API key in Settings > Assistant."
- Unsupported capability: "Model {ModelId} may not support {capability}. Try a different model."
- Cancelled: "Action cancelled. You can retry if needed."

### Loading State

**Skeleton cards** (NOT spinner-only — shows layout structure immediately):
```
Background: #2C2C2E, border 1px solid #3A3A3C, radius 8px, padding 12px 16px
Skeleton element: #3A3C3E shimmer → #3A3A3C → #2C2C2E, 1.4s ease-in-out
Header row: 48×48px thumbnail placeholder + text bars (60% width 14px, 40% width 12px)
Action row: 3 ghost button placeholders, 32×24px
Number of skeletons: 1 (latest) / 5 (last-5)
```

**Provider loading indicator**: Small inline spinner (12×12px, `#0A84FF`, 0.8s linear infinite) + "Thinking..." / "Processing..." (12px / #98989D).

### Keyboard Navigation (Visual Spec)

| Element | Focus Ring | Navigation |
|---|---|---|
| Input | 2px border `#0A84FF` | Enter submits, Esc closes |
| Result card | 2px `#0A84FF` outline, 2px offset | Arrow keys navigate list |
| Action button | 2px `#0A84FF` outline | Tab/Shift+Tab |
| Confirmation | Standard focus | Enter confirms, Esc cancels |

**Screen reader announcements**:
- Result list: `"5 results. Use arrow keys to navigate."`
- Result item: `"{filename}, {timestamp}, {n} actions available: Copy path, Open, Reveal"`
- Confirmation: `"Confirmation required: {action}. Press Enter to confirm, Escape to cancel."`
- Error: `"Error: {message}. Retry available."` (aria-live="assertive")

### Settings Panel — Provider Configuration

```
Provider section:
  Header: Provider icon (24×24) + Display name (15px / 600) + [Remove] link
  Provider dropdown: 40px height, #2C2C2E bg, full width, 8px radius
  API key input: masked ●●●●●●●●, reveal eye icon, 40px height
  Validation badge: inline right of API key (spinner / ✓ valid / × invalid)
  BaseUrl field: shown only when Advanced toggle enabled
  Model selector: dropdown with capability icons (🖼️ image, 🔧 tools)
  "Set Active" toggle: right side, 40×20px
  Validation warning: yellow (#FFD60A) banner when not validated but active
  "Validate Key" button: ghost, #0A84FF border, cost warning shown first
```

**Model dropdown capability icons**:
- 🖼️ (cyan `#0A84FF`) — Image input supported
- 🔧 (blue `#0A84FF`) — Tool/function calling
- 📡 (purple `#BF5AF2`) — Streaming (grayed in Phase 1)

---

## Implementation Plan

### Phase 0 - Product and Safety Contract

- Define assistant scope as in-app only.
- Write the initial allowlisted tool contract.
- Decide default shortcut and conflict behavior.
- Define consent categories and confirmation copy.
- Define the provider-neutral request/result contracts.
- Decide where provider keys are stored on Windows, macOS, and Linux.
- Define provider capability metadata for OpenAI, MiniMax, Kimi, Gemini, and Anthropic.
- Add tests for the privacy guard's classification rules.

Exit criteria:

- XerahS has a documented assistant trust boundary.
- No implementation path requires arbitrary filesystem search or shell execution.
- Provider support can be added without changing the assistant orchestrator or tool contracts.

### Phase 1 - Spotlight Shell and Deterministic Commands

▲ Milena clarification resolved: Q1, Q2, Q3

- Add the overlay UI and view model.
- Add settings for enabling/disabling the assistant and configuring its shortcut.
- Implement deterministic local command routing for the full Phase 1 command set:
  - last N screenshots (N ≤ 10)
  - latest screenshot path
  - copy latest screenshot path
  - open latest screenshot
  - reveal latest screenshot
- **Shipping scope**: The full deterministic set above ships, not a single command. The "last 5 screenshot paths" acceptance criterion is the proof point, not the entire scope.
- **Provider scaffolding**: Provider settings UI and provider-neutral `IAssistantModelProvider` interfaces ship as architecture scaffolding only. No AI provider adapter ships in Phase 1.
- Use XerahS history services for all file lookup.
- Do not require an AI provider for Phase 1.

**First two providers (Phase 2)**): OpenAI (GPT-4o-mini) + Ollama (local Llama 3.x) to prove both OpenAI-compatible and non-OpenAI envelopes. (Milena Q3)

Exit criteria:

- A user can press the shortcut and ask for the last 5 screenshot paths.
- The answer comes from XerahS history.
- The result can be copied from the overlay.
- No model call is required for the core example.

### Phase 2 - BYOK Provider Settings and AI Intent Routing

▲ Milena clarification resolved: Q64, Q65
▲ Nadia clarification resolved: Q22, Q23, Q24, Q25, Q26, Q27, Q28, Q50, Q51, Q52, Q53, Q66, Q67, Q70, Q71, Q76, Q77, Q78, Q79, Q81, Q82

- Add assistant provider settings UI with provider dropdown, API key input, model selection/override, validation, and `Set active`.
- Implement the global settings UI in `src/desktop/app/XerahS.UI/Views/ApplicationSettingsView.axaml`, with bindings on `SettingsViewModel`.
- Add initial provider records for OpenAI, MiniMax, Kimi, Gemini, and Anthropic.
- Add provider/key validation without exposing full keys in UI or logs.
- Implement provider adapters behind `IAssistantModelProvider`.
- Implement OpenAI and Ollama (local Llama 3.x) as first two providers to prove both OpenAI-compatible and non-OpenAI envelopes.
- Add model routing only after deterministic command matching fails or needs disambiguation.
- Validate all model-requested tool calls against typed schemas and the privacy guard.
- Add cancellation and timeout handling.
- Add prompt history (opt-in, disabled by default, 30-day/100-entry limit, SQLite at `~/.config/xerahs/assistant/history.db`)
- Store normalized intents + executed actions in history, not raw prompts

**"No provider configured" state**: Overlay shows message "AI provider not configured. XerahS Assistant can only run local commands without a provider." + list of available deterministic commands + "Configure AI Provider" button. Overlay header always shows provider status dot (green/yellow/red) — clicking opens provider settings. (Milena Q64, Q65)

**History query semantics**:
- "Known XerahS history item": database rows where `Source IN ('capture','editor','uploader')` AND `Status='completed'` AND `FilePath IS NOT NULL`
- "Screenshot" scope: region, window, fullscreen captures only. Excludes: annotated screenshots, OCR outputs, GIFs, screen recordings, imported images
- "Latest capture": most recent successful file-producing capture regardless of type
- Maximum N = 10 for `history.latest`; return fewer than N without warning if fewer exist
- Timezone for date phrases: OS local timezone (user override: `local | utc | capture`)
- "Find screenshots from yesterday that mention invoice": requires pre-indexed OCR text, not on-demand OCR
- Missing/inaccessible files: return `exists: false` flag; UI shows strikethrough or "(unavailable)" badge

**Overlay during capture**: Block capture while overlay is open. Close overlay with 50ms delay before capture proceeds. Overlay window excluded from capture APIs (`WS_EX_TOOLWINDOW` on Windows, `NSWindowStyleMask.NonactivatingPanel` on macOS). (Nadia Q81, Q82)

**Offline mode**: Disable assistant provider calls when offline or when `Settings.Privacy.OfflineMode` is enabled. Show "Assistant requires internet connection." message. (Nadia Q71)

**Network/proxy/certificate failures**:
- Network timeout: "Connection timed out. Check your internet connection."
- Proxy failure: "Proxy configuration error. Check proxy settings in System Settings."
- Certificate failure: "SSL certificate error. Check system date/time and certificate trust."
- All failures: log detailed error (without key), show user-friendly message, allow retry. (Nadia Q70)

Exit criteria:

- Users can configure an AI provider key from a provider dropdown and set one provider as active.
- API keys are masked, removable, and never stored in plain assistant transcripts or logs.
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

▲ Mikhail/Nadia/Milena clarification resolved: Q14–Q21, Q59–Q60, Q62, Q80, Q83

1. XerahS history metadata may be used for local answers.
2. Screenshot pixels are not sent to AI providers for metadata-only tasks.
3. API keys are never included in prompts, logs, tool results, MCP-safe settings, or assistant transcripts.
4. Uploads and external sharing require confirmation.
5. Destructive actions remain in tool schemas (for future compatibility) but are blocked by the privacy guard in all phases.
6. The assistant may only operate on XerahS-known files unless a later XIP explicitly expands the scope.
7. Prompt history is opt-in, disabled by default. 30-day / 100-entry retention. Stored in `~/.config/xerahs/assistant/history.db`.
8. The UI must make it clear when a cloud model will receive text, image, or file content.
9. Raw prompts are forbidden in all logs, crash reports, and telemetry — even in debug builds.
10. Audit/logging sinks record only: `ActionType`, `ToolName`, `Timestamp`, `DurationMs`, `ResultStatus`, `ItemCount`. Forbidden: `FilePath`, `OcrText`, `PromptText`, `UserInput`, `ApiKey`, `ModelResponse`.

### Confirmation Copy (exact)

| Action | Confirmation Required | Copy |
|---|---|---|
| Metadata lookup (list, file info) | No | Silent operation |
| Clipboard write (direct) | No | Silent operation |
| Clipboard write (model-inferred) | Yes | "Copy '{text preview}' to clipboard?" |
| Reveal/Open | Yes | "Reveal file in folder? `{path}`" |
| Local OCR | No | Silent operation |
| Cloud OCR / image analysis | Yes | "Send image to `{Provider}` for analysis? Image: `{filename}` ({size})" |
| Upload | Yes | "Upload `{filename}` ({size}) to `{Destination}`?" |
| Destructive | Yes | "Permanently delete `{filename}`? This cannot be undone." |

### Consent Persistence

Users may persist consent per-action-type for 24 hours via "Remember this choice for today" checkbox. Permanent consent is not permitted. (Milena Q19)

### Mandatory Privacy Guard Cases

Beyond the acceptance criteria, the following cases are mandatory:
- Clipboard writes where text > 1000 chars or contains URLs
- File reveal/open for files outside known history items
- Cloud text requests where input > 500 chars or contains URLs/paths
- Network operations (uploads)
- Batch operations on > 5 items
- Files modified > 30 days ago
- Unknown tool calls (block with error, don't execute)

---

## Open Questions

The following questions have been resolved through the design review clarification loop (Milena Petrova, Nadia Valeva, Sofia Novak — 2026-04-12). ▲ denotes resolved items.

- ▲ What should the default shortcut be on Windows, macOS, and Linux? → Windows/Linux: `Ctrl+Shift+Space`. macOS: `Cmd+Shift+Space`. Fallback: double-tap `Ctrl` on all platforms (Mikhail/Nadia Q33, Q34)
- ▲ Should the overlay be available when XerahS is only running in the tray? → Available when tray-only, minimized, or in editor. NOT available during modal dialogs or active capture (Nadia Q35)
- ▲ Should Phase 1 include a deterministic parser only, or a small built-in command grammar with suggestions? → Fixed regex patterns with named capture groups, hot-reloadable from `AssistantCommandPatterns.json`. No grammar engine (Nadia Q28)
- ▲ Which two providers should ship first to validate both OpenAI-compatible and non-OpenAI-compatible adapters? → OpenAI (GPT-4o-mini) + Ollama (local Llama 3.x) — Milena Q3
- ▲ Should prompt history be enabled by default or opt-in? → Opt-in, disabled by default. 30-day / 100-entry retention limit. Stored in local SQLite (Milena Q31)
- ▲ Should local model support be a first-class Phase 2 requirement or a Phase 3/4 addition? → Design local model support into the provider abstraction now, even if not exposed in UI until later (Nadia Q13)
- ▲ How should the assistant display paths on platforms where history items may be remote, deleted, or moved? → Return `exists: false` flag for missing files; UI shows strikethrough or "(unavailable)" badge (Nadia Q25)
- ▲ Should model lists be hard-coded defaults, fetched from provider APIs, or both? → Combination: hard-coded curated defaults (10-15 models max), fetched from provider APIs on demand with 24h cache, user can enter custom model ID (Nadia Q5)
- ▲ Should provider validation use a real minimal model request or only an authenticated model-list/status endpoint when available? → Always issue a minimal tool-capable request. Model-list endpoints don't prove key validity for completion calls (Nadia Q6)

**Remaining open questions:**
- (none — all clarification questions resolved in Round 1)

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
- The implementation plan includes a provider-neutral API architecture for OpenAI, MiniMax, Kimi, Gemini, and Anthropic.
- Unit tests cover command routing for the initial deterministic commands.
- Unit tests cover privacy guard behavior for metadata-only lookup, upload confirmation, and cloud image/content requests.
