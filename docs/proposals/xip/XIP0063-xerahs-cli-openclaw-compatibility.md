# XIP0063 XerahS CLI OpenClaw Compatibility Enhancement

XIP0063: XerahS CLI — OpenClaw Integration & Headless Upload Support

## Priority
**HIGH** — Enables autonomous OpenClaw ↔ XerahS workflow for the KovaForge AI agent team

## Assignee
**Viktor Hale** (implementation), **Vladislava Kova** (COO, coordination)

## Branch
`feature/cli-openclaw-compatibility`

## Status
Draft

## Context

OpenClaw (AI agent orchestration platform) needs to hand off files to XerahS for upload and retrieval of shareable URLs — without a GUI, display, or human interaction. The current XerahS CLI is designed primarily around screen capture workflows with GUI dialogs. This XIP proposes targeted enhancements to make the CLI scriptable and headless-friendly for agentic use.

---

## Source Reference

- XerahS CLI source: `~/Documents/GitHub/XerahS/src/desktop/cli/XerahS.CLI/`
- CLI binary (self-contained, built): `~/Documents/GitHub/XerahS/src/desktop/cli/XerahS.CLI/bin/Release/net10.0/linux-x64/xerahs`
- OpenClaw workspace: `~/.openclaw/workspace/`
- Relevant XIPs: XIP0021 (verify-recording CLI), XIP0034 (watch-folder daemon)

---

## Current State

### What Already Works

| Command | Status | Notes |
|---------|--------|-------|
| `xerahs list workflows` | ✅ Works | Lists 8 workflows |
| `xerahs capture screen --upload` | ✅ Works | Captures screen, uploads via configured uploader |
| `xerahs capture region --region x,y,w,h --upload` | ✅ Works | Region capture with upload |
| `xerahs record` | ✅ Works | Screen recording (requires display or Xvfb) |
| `xerahs run <workflow-id>` | ✅ Works | Executes existing workflows by ID |
| `xerahs config show` | ✅ Works | Shows configuration summary |
| `xerahs config path` | ✅ Works | Shows config file paths |
| `xerahs --version` | ✅ Works | |

### What Is Missing

| Feature | Priority | Status |
|---------|----------|--------|
| `xerahs upload <file>` — upload arbitrary file by path | **HIGH** | ⚠️ Implemented but blocked by missing uploader credentials |
| `xerahs upload --text <string>` — upload string as text file | **HIGH** | ❌ Missing |
| `xerahs upload --pipe` — upload from stdin | **HIGH** | ❌ Missing |
| `xerahs upload --clipboard` — upload clipboard contents | **MEDIUM** | ❌ Missing |
| `xerahs capture` subcommands without display | **MEDIUM** | ⚠️ Capture works headless on Linux with Xvfb |
| `--json` / `--output json` — machine-readable output | **MEDIUM** | ❌ Missing |
| `--quiet` / `--silent` — suppress notifications | **LOW** | ❌ Missing |

---

## Recommendations

### REC-001: Commit `upload` Command Skeleton (Already Implemented)

**Branch:** `feature/cli-openclaw-compatibility`

**What was built in this session:**
- `Commands/UploadCommand.cs` — new command implementing `xerahs upload <file-path>`
- `Program.cs` — registered `UploadCommand.Create(taskManager)` in the root command tree
- Build passes, CLI help shows `upload` command correctly
- Command correctly uses `IDesktopTaskManager.StartFileTask()` and waits for completion

**What is blocked:**
Upload fails at runtime because no File uploader has credentials configured:

```
[NOTIFICATION] Upload Failed
  All uploaders failed for category File and fallback.
```

The `auto` provider routes `.md` → File category → all file uploaders fail without auth.

**Proposed change:**
Commit the `UploadCommand.cs` skeleton now so Viktor has a foundation to build on. Do not block on uploader credentials.

**Acceptance criteria:**
- `dotnet build src/desktop/cli/XerahS.CLI/XerahS.CLI.csproj -c Release` passes with 0 errors
- `xerahs upload --help` shows the command
- Commit message format: `[v0.22.0] [Enhancement] Add upload command to CLI — Vladislava Kova`

---

### REC-002: `--text` Flag — Upload String Content Directly

**Branch:** `feature/cli-openclaw-compatibility`

**Problem:**
OpenClaw agents generate markdown/text content (memory logs, reports) and need to upload it without writing to a temp file first. The current pipeline requires `echo "content" > /tmp/file.md && xerahs upload /tmp/file.md`.

**Proposed change:**

```bash
xerahs upload --text "Hello world" --name hello.txt
# or
echo "Hello world" | xerahs upload --pipe --name hello.txt
```

Implementation path:
1. Add `--text` option (`string?`) and `--pipe` option (`bool`) to `UploadCommand`
2. If `--text` is provided, write to a temp file then upload
3. If `--pipe` is provided, read `Console.OpenStandardInput()` into a temp file then upload
4. `--name` option sets the filename (defaults to `upload.txt` or infers from content-type)
5. Output the URL to stdout; errors to stderr; exit code 0 on success

**DO:**
- Clean up temp file after successful upload
- Preserve file extension from `--name` to ensure correct uploader routing (`.md` → text uploader, `.png` → image uploader)

**DO NOT:**
- Require `--name` — generate a sensible default (`upload.txt`, `upload.md`)
- Print anything except the URL to stdout on success (agents parse stdout)

**Acceptance criteria:**
```bash
echo "test" | xerahs upload --pipe
# → https://imgur.com/abc123 (or equivalent URL)
echo $?  # → 0
```

---

### REC-003: Configure Free Uploader for CLI Use

**Branch:** `feature/cli-openclaw-compatibility` (config-only change, no code)

**Problem:**
No file uploader is configured. `auto` routing fails for all file types. The `paste2` text uploader has no API key.

**Proposed fix — two options:**

**Option A (recommended for text/markdown): GitHub Gist**
- Free, PAT-based, no upload size limit for public gists
- Configure via Settings → Uploaders → GitHub Gist
- Requires: GitHub PAT with `gist` scope
- Note: Gist only handles Text category — `.md` files routed via `auto` may still try File first

**Option B (recommended for all files): Imgur anonymous**
- Free, anonymous image uploads up to 5MB
- Configure via Settings → Uploaders → Imgur → Account Type: Anonymous
- Works for images and falls back for other file types

**Option C (recommended for agents): Custom script uploader**
- Useless.se, 0x0.st, or similar frictionless paste services
- Configure as a custom uploader script in Settings → Custom Uploaders
- Pros: no auth, works for any file type
- Cons: depends on third-party service availability

**Recommended for OpenClaw:**
Use **Imgur Anonymous** for images + **GitHub Gist** (with PAT) for text, and document that agents should prefer `--text` for markdown content to ensure correct routing.

**Proposed change:**
Document the uploader setup process in the XIP. Viktor or McoreD configures the uploader. No code change required.

---

### REC-004: `--json` Output Mode for Machine Readability

**Branch:** `feature/cli-openclaw-compatibility`

**Problem:**
Agents need structured output, not human formatted text. Current CLI outputs free-text URLs and notifications.

**Proposed change:**

```bash
xerahs upload /path/to/file.png --json
# → {"url": "https://imgur.com/abc123", "filename": "file.png", "size": 1024, "type": "image/png"}

xerahs upload --text "hello" --json
# → {"url": "https://gist.github.com/...", "filename": "upload.txt", "size": 5, "type": "text/plain"}

xerahs capture region --region 0,0,1920,1080 --json
# → {"url": "https://imgur.com/xyz789", "width": 1920, "height": 1080, "format": "png"}
```

Implementation:
1. Add `--json` global option to the root command in `Program.cs`
2. Store `OutputFormat` enum: `Text`, `JSON`
3. When `--json` is set, serialize result objects as JSON to stdout
4. Errors always go to stderr as JSON: `{"error": "Upload failed: all uploaders failed"}`

**Acceptance criteria:**
```bash
xerahs upload /path/to/file.png --json 2>/dev/null | jq .url
# → extracts URL reliably
```

---

### REC-005: Headless Capture Without Display

**Branch:** `feature/cli-openclaw-compatibility`

**Problem:**
`xerahs capture region` requires a display on Linux (Wayland/X11). In headless CI/agent environments this fails unless Xvfb is running.

**Proposed change:**

Add `--headless` flag that:
1. Uses the Wayland portal directly for capture (no overlay/region selector)
2. Uses the last-used or default region if none specified
3. Falls back to `slurp` (Wayland) or `scrot` (X11) for region selection if no `--region` provided

```bash
# In headless environment with Xvfb
xvfb-run -a xerahs capture region --region 0,0,1920,1080 --upload --exit-on-complete

# Or if portal is available headless:
xerahs capture region --region 0,0,1920,1080 --upload --exit-on-complete --headless
```

**Status:** Requires further investigation. Wayland portal capture may work headless if `xdg-desktop-portal` is running. Marked as **INVESTIGATE** for Viktor.

---

### REC-006: Quiet / Silent Mode

**Branch:** `feature/cli-openclaw-compatibility`

**Problem:**
XerahS prints `[NOTIFICATION]` messages to the terminal even when not in a capture workflow. These pollute agent output.

**Proposed change:**
Add `--quiet` / `-q` global flag that suppresses:
- `[NOTIFICATION]` messages
- Toast/progress output
- Sound playback triggers

Does NOT suppress:
- The final URL output (unless `--json` is used)
- Error messages

---

## Implementation Order

| Step | Action | Owner | Notes |
|------|--------|-------|-------|
| 1 | Commit `UploadCommand.cs` skeleton | Vladislava (COO) | Already done in this session |
| 2 | Configure Imgur Anonymous uploader | McoreD | Settings → Uploaders → Imgur → Anonymous |
| 3 | Implement `--text` and `--pipe` flags | Viktor | Build on existing `UploadCommand.cs` |
| 4 | Implement `--json` output mode | Viktor | Global option in `Program.cs` |
| 5 | Implement `--quiet` / `--silent` mode | Viktor | |
| 6 | Document OpenClaw integration in XIP | Vladislava (COO) | After Viktor ships |
| 7 | Test headless capture with Xvfb | Viktor | |

---

## Open Questions

| # | Question | Priority | Owner |
|---|----------|----------|-------|
| 1 | Does Imgur anonymous work for non-image files? | HIGH | Viktor |
| 2 | Should `--text` auto-detect markdown and use Gist instead of Imgur? | MEDIUM | Viktor |
| 3 | Does Wayland portal capture work headless without Xvfb? | MEDIUM | Viktor |
| 4 | Should the CLI ship as a separate `xerahs-cli` package from the GUI? | LOW | McoreD |

---

## Related Files

```
src/desktop/cli/XerahS.CLI/
├── Program.cs                         # Root command — add --json, --quiet
├── Commands/
│   ├── UploadCommand.cs                # ✅ Implemented this session
│   ├── CaptureCommand.cs               # ✅ Works
│   ├── WorkflowCommand.cs              # ✅ Works
│   ├── RecordCommand.cs                # ✅ Works
│   └── ListCommand.cs                 # ✅ Works
└── bin/Release/net10.0/linux-x64/xerahs  # Built self-contained binary
```

---

## Success Criteria

1. `xerahs upload vault/openclaw-context-fix.md` produces a shareable URL with no manual interaction
2. `xerahs upload --text "Hello" --json` outputs `{"url": "..."}` to stdout
3. All notification/noise output is suppressible with `--quiet`
4. CLI runs headless in a Docker container (no display) with `--headless`
5. OpenClaw agents can invoke XerahS upload purely via exec, get URL back, and post it to Discord/Telegram
