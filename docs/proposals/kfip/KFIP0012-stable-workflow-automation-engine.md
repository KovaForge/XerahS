# KFIP0012: Stable Workflow Automation Engine

**Status**: Proposed
**Priority**: P1
**Area**: Workflow | Automation | Stability | AfterCapture
**Created**: 2026-06-21
**Related**: KFIP0005 (Social Sharing Workflows), KFIP0009 (Share-Ready Enhancements), KFIP0010 (Compression-Resilient Capture), KFIP0011 (OCR-to-Clipboard)
**Owner**: KovaForge
**Co-Authors**: Nadia (research), Vladislava (review)

---

## Summary

X/Twitter screen capture users consistently report workflow friction as their top pain point: redundant dialogs interrupting capture flow, manual steps between capture and sharing, and crashes that destroy in-progress work. While KFIP0005–0011 added social-specific features, no KFIP addresses the foundational stability and automation layer that makes capture workflows reliable and repeatable. This KFIP proposes a Workflow Automation Engine with three pillars: **Crash-Resilient Capture Sessions** (recover in-progress work), **Conditional Task Pipelines** (smart automation based on content/context), and **One-Touch Workflow Shortcuts** (reduce UI friction to zero for common flows).

---

## Problem Statement

### The Workflow Friction Tax

User research from X reveals a consistent pattern: users love ShareX/XerahS power but hate the friction. Every capture involves decision fatigue and manual steps:

| Step | Current Behavior | User Pain |
|------|-----------------|-----------|
| Pre-capture | Select region, trigger hotkey | Hotkey conflicts, region selection errors |
| Post-capture | Multiple dialogs (save? edit? upload? which uploader?) | "I have to select 'Save to photos' every time" |
| Annotation | Open editor, manually annotate | Brush editing "kinda bad without" improvements |
| Sharing | Copy to clipboard, open X, paste, write alt text | No native "Share to X" integration |
| Recovery | Crash = lost capture | "Crashed a bunch, it happens tho with new apps" |

### Evidence from X Research

- **Workflow interruption**: Users criticize "clunky interfaces that interrupt the flow" — redundant confirmation dialogs and post-capture steps feel unnecessary.
- **Crash frustration**: New apps/features cause crashes during capture; users lose work with no recovery path.
- **Manual repetition**: "I have to select 'Save to photos' every time" — repetitive manual choices for every capture.
- **Sharing friction**: "can't share clips easily" — no direct path from capture to X/Twitter post.
- **Stability concerns**: Linux/macOS port called "absolutely sucks" by some users; reliability varies by platform.

### XerahS Team Context

Per official X posts, XerahS next priorities are:
- **Linux stability**
- **Workflow automation**

This KFIP directly addresses the second priority while contributing to the first through crash resilience.

---

## Goals

1. **Crash-Resilient Capture Sessions**: Automatic recovery of in-progress captures after crashes or interruptions
2. **Conditional Task Pipelines**: Smart automation that adapts based on content type, context, and user patterns
3. **One-Touch Workflow Shortcuts**: Zero-friction capture flows for common scenarios (social sharing, documentation, quick save)
4. **Workflow History & Replay**: Browse, search, and re-run previous capture workflows

## Non-Goals

- No cloud sync or cross-device workflows (local only)
- No AI-generated capture decisions (rule-based only)
- No video/GIF workflow automation (screenshots only for v1)
- No full macro recording (workflow composition via UI only)
- No automatic posting to social platforms (user initiates all sharing)

---

## Proposed Solution

### 1. Crash-Resilient Capture Sessions

Persist capture state atomically throughout the pipeline, enabling recovery after crashes.

**Architecture:**

```csharp
public interface ICaptureSessionService
{
    Task<SessionState> CreateSessionAsync(CaptureRequest request);
    Task PersistCheckpointAsync(SessionState state, PipelineStage stage);
    Task<SessionState?> RecoverSessionAsync(Guid sessionId);
    Task<IReadOnlyList<SessionState>> ListRecoverableSessionsAsync();
    Task CompleteSessionAsync(Guid sessionId);
}

public class SessionState
{
    public Guid SessionId { get; init; }
    public CaptureRequest OriginalRequest { get; init; } = null!;
    public PipelineStage LastCompletedStage { get; init; }
    public string? RawCapturePath { get; init; }
    public string? ProcessedImagePath { get; init; }
    public TaskMetadata Metadata { get; init; } = new();
    public DateTime CreatedAt { get; init; }
    public int RecoveryAttempts { get; init; }
}

public enum PipelineStage
{
    Initialized,
    RegionSelected,
    CapturedRaw,
    MetadataStripped,
    Redacted,
    OcrCompleted,
    Formatted,
    Saved,
    Completed
}
```

**Checkpoint Strategy:**

| Stage | Persisted Data | Recovery Action |
|-------|---------------|-----------------|
| Initialized | Request + region selection | Re-show region selector or auto-capture same region |
| CapturedRaw | Raw bitmap to temp file | Resume from AfterCapture tasks |
| MetadataStripped/Redacted | Processed bitmap | Resume from remaining tasks |
| Saved | Final file path | Offer to re-share/copy |

**Recovery UX:**

- On app startup: detect incomplete sessions, show recovery toast: "3 captures interrupted. Recover?"
- Recovery dialog shows thumbnails + stage: "OCR pending" / "Ready to save" / "Upload not completed"
- One-click: **[Resume]** **[Discard]** **[Save As...]**
- Auto-cleanup: completed sessions archived for 7 days, then deleted

**Crash Handling:**

- Every pipeline stage wrapped in `try/catch` with checkpoint
- Unhandled exceptions trigger emergency checkpoint before app termination
- Platform-specific: macOS `NSApplicationCrashReporter` integration, Linux `systemd-coredump` awareness

### 2. Conditional Task Pipelines

Replace static AfterCapture task flags with conditional rules that adapt to content and context.

**Rule Engine:**

```csharp
public interface IWorkflowRuleEngine
{
    Task<WorkflowPipeline> BuildPipelineAsync(CaptureContext context, TaskMetadata metadata);
}

public class WorkflowPipeline
{
    public IReadOnlyList<PipelineStep> Steps { get; init; } = Array.Empty<PipelineStep>();
    public bool AutoExecute { get; init; } // true = no dialogs, false = show after-capture
}

public class PipelineStep
{
    public string StepId { get; init; } = "";
    public AfterCaptureTasks Task { get; init; }
    public ICondition? Condition { get; init; }
    public Dictionary<string, object> Parameters { get; init; } = new();
}

public interface ICondition
{
    string ConditionId { get; }
    Task<bool> EvaluateAsync(CaptureContext context, TaskMetadata metadata);
}
```

**Built-in Conditions:**

| Condition | Evaluates | Use Case |
|-----------|-----------|----------|
| `ContentIsTextHeavy` | OCR confidence >80% + edge density >40% | Auto-select PNG format |
| `TargetPlatformIsX` | X/Twitter window detected (KFIP0003) | Auto-strip metadata, suggest alt text |
| `FileSizeExceeds` | Estimated size > threshold | Auto-compress or warn |
| `ContainsSensitiveContent` | PII patterns detected in OCR | Auto-suggest redaction |
| `PreviousWorkflowUsed` | User consistently applies same tasks | Offer to save as shortcut |
| `TimeSinceLastCapture` | <5 seconds | Batch mode suggestions |

**Rule Examples:**

```yaml
# Social share workflow
workflow: "x-share-ready"
triggers:
  - condition: TargetPlatformIsX
steps:
  - task: StripMetadata
  - task: SmartFormatSelect
    condition: ContentIsTextHeavy
    parameters: { preferFormat: PNG }
  - task: CompressForPlatform
  - task: DoOCR
  - task: GenerateAltText
    condition: ContentIsTextHeavy
  - task: CopyFilePathToClipboard
autoExecute: true
```

```yaml
# Quick documentation workflow
workflow: "quick-doc"
triggers:
  - hotkey: "Ctrl+Shift+2"
steps:
  - task: SaveToFolder
    parameters: { folder: "~/Screenshots/Docs", format: PNG }
  - task: CopyFilePathToClipboard
  - task: ShowNotification
    parameters: { message: "Saved to Docs folder" }
autoExecute: true
```

### 3. One-Touch Workflow Shortcuts

Pre-built, zero-dialog workflows triggered by hotkey or context.

**Shortcut Registry:**

```csharp
public interface IWorkflowShortcutService
{
    Task ExecuteShortcutAsync(string shortcutId, CaptureContext context);
    IReadOnlyList<WorkflowShortcut> GetAvailableShortcuts(CaptureContext? context = null);
    Task SaveUserShortcutAsync(WorkflowShortcut shortcut);
}

public class WorkflowShortcut
{
    public string ShortcutId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string? Hotkey { get; init; }
    public WorkflowPipeline Pipeline { get; init; } = null!;
    public bool IsSystemDefined { get; init; }
    public string? Icon { get; init; }
}
```

**System Shortcuts (v1):**

| Shortcut | Trigger | Pipeline | Dialogs |
|----------|---------|----------|---------|
| `x-instant-share` | Hotkey + X context | Strip → Format → OCR → AltText → Copy path | 0 (toast only) |
| `quick-save-png` | Hotkey (user-defined) | Save PNG → Copy path → Toast | 0 |
| `quick-save-jpg` | Hotkey (user-defined) | Save JPEG 90 → Copy path → Toast | 0 |
| `copy-with-ocr` | Hotkey | OCR → Copy text → Toast | 0 |
| `annotate-then-share` | Hotkey | Open editor → On close: Format → Copy path | Editor only |

**Shortcut Learning:**

- Monitor user patterns: "User always applies StripMetadata + SmartFormatSelect + CopyPath for X captures"
- Suggest new shortcut: "You often share to X. Create 'X Share' shortcut?"
- One-click save with suggested hotkey

### 4. Workflow History & Replay

Browse past captures with full context and re-run workflows.

**History Model:**

```csharp
public class WorkflowHistoryEntry
{
    public Guid EntryId { get; init; }
    public DateTime CapturedAt { get; init; }
    public string ThumbnailPath { get; init; } = "";
    public string? FinalImagePath { get; init; }
    public WorkflowPipeline AppliedPipeline { get; init; } = null!;
    public TaskMetadata Metadata { get; init; } = new();
    public CaptureContext Context { get; init; } = new();
    public string? SharedToPlatform { get; init; }
    public bool IsPinned { get; set; }
    public List<string> Tags { get; init; } = new();
}
```

**History Browser UI:**

- Grid view: thumbnails + capture time + workflow name
- Filters: date range, platform, workflow type, tags
- Search: OCR text within captures (reuse KFIP0001)
- Actions per entry:
  - **[Re-capture same region]** — quick re-capture with same dimensions
  - **[Re-run workflow]** — apply same pipeline to new capture
  - **[Copy to clipboard]** — re-copy file path
  - **[Open in editor]** — annotate again
  - **[Share again]** — re-trigger share flow
  - **[Pin]** — keep in quick access

**Quick Access Panel:**

- Pinned captures + recent 10
- One-click re-capture: "Last X post screenshot" → captures same region

---

## Technical Design

### New Services

```
src/desktop/core/XerahS.Core/
├── Services/
│   ├── ICaptureSessionService.cs
│   ├── CaptureSessionService.cs          [SQLite-backed session persistence]
│   ├── IWorkflowRuleEngine.cs
│   ├── WorkflowRuleEngine.cs             [YAML/JSON rule evaluation]
│   ├── IWorkflowShortcutService.cs
│   ├── WorkflowShortcutService.cs        [Shortcut registry + execution]
│   ├── IWorkflowHistoryService.cs
│   └── WorkflowHistoryService.cs         [History persistence + search]
│
├── Models/
│   ├── SessionState.cs
│   ├── WorkflowPipeline.cs
│   ├── PipelineStep.cs
│   ├── WorkflowShortcut.cs
│   └── WorkflowHistoryEntry.cs
│
└── Conditions/
    ├── ICondition.cs
    ├── ContentIsTextHeavyCondition.cs
    ├── TargetPlatformIsXCondition.cs
    ├── FileSizeExceedsCondition.cs
    └── PreviousWorkflowUsedCondition.cs
```

### Database Schema

```sql
-- Session recovery (ephemeral, cleaned on completion)
CREATE TABLE capture_sessions (
    session_id TEXT PRIMARY KEY,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    last_stage INTEGER NOT NULL,
    raw_capture_path TEXT,
    processed_path TEXT,
    metadata_json TEXT,
    recovery_count INTEGER DEFAULT 0
);

-- Workflow history (persistent, user data)
CREATE TABLE workflow_history (
    entry_id TEXT PRIMARY KEY,
    captured_at DATETIME NOT NULL,
    thumbnail_path TEXT NOT NULL,
    final_image_path TEXT,
    pipeline_json TEXT NOT NULL,
    metadata_json TEXT,
    context_json TEXT,
    shared_to_platform TEXT,
    is_pinned BOOLEAN DEFAULT 0,
    tags_json TEXT
);
CREATE INDEX idx_history_captured ON workflow_history(captured_at);
CREATE INDEX idx_history_pinned ON workflow_history(is_pinned);

-- Workflow shortcuts
CREATE TABLE workflow_shortcuts (
    shortcut_id TEXT PRIMARY KEY,
    display_name TEXT NOT NULL,
    hotkey TEXT UNIQUE,
    pipeline_json TEXT NOT NULL,
    is_system_defined BOOLEAN DEFAULT 0,
    icon TEXT
);
```

### Integration Points

| Component | Integration |
|-----------|-------------|
| `CaptureJobProcessor` | Checks for recoverable sessions on startup; persists checkpoints after each stage |
| `AfterCaptureDialog` | Skipped if `WorkflowPipeline.AutoExecute = true` |
| `CaptureCommandPaletteService` (KFIP0007) | `WorkflowShortcutProvider` contributes palette items |
| `IImageContentAnalyzer` (KFIP0010) | Used by `ContentIsTextHeavyCondition` |
| `IPlatformContextDetector` (KFIP0003) | Used by `TargetPlatformIsXCondition` |
| `OCRService` (KFIP0001) | Used for history search + text-heavy detection |
| `IPlatformServices.Clipboard` | Used by copy steps in shortcuts |

### Pipeline Execution Order

```
1. Detect/Create Session
2. Load or build WorkflowPipeline (rules + shortcuts)
3. For each PipelineStep:
   a. Evaluate Condition (if any)
   b. If true, execute AfterCaptureTask with Parameters
   c. Persist checkpoint
4. Complete session, archive to history
5. If AutoExecute: show toast summary
   Else: show AfterCaptureDialog with pre-selected options
```

---

## Acceptance Criteria

### Functional

- [ ] `CaptureSessionService` persists session state after every pipeline stage
- [ ] App startup detects incomplete sessions and offers recovery UI
- [ ] Recovery successfully resumes from any pipeline stage
- [ ] `WorkflowRuleEngine` evaluates YAML-defined rules correctly
- [ ] Built-in conditions (ContentIsTextHeavy, TargetPlatformIsX, etc.) work reliably
- [ ] System shortcuts execute with zero dialogs (toast only)
- [ ] User can create custom shortcuts via UI
- [ ] Workflow history persists captures with thumbnails
- [ ] History search finds captures by OCR text content
- [ ] Re-capture same region works within 5px of original

### Stability

- [ ] Simulated crash at any stage allows clean recovery
- [ ] Multiple rapid captures don't corrupt session database
- [ ] History database handles 10,000+ entries without performance degradation
- [ ] Session cleanup removes completed sessions after 7 days
- [ ] No memory leaks from long-running capture sessions

### Performance

- [ ] Session checkpoint persistence <50ms
- [ ] Rule evaluation <100ms for 10 conditions
- [ ] History search returns results in <500ms for 1000 entries
- [ ] Shortcut execution adds <200ms to capture pipeline

### Edge Cases

- [ ] Recovery when source window/region no longer exists shows graceful error
- [ ] Multiple recovery attempts for same session tracked and limited
- [ ] User can force-discard all recoverable sessions
- [ ] History handles missing/deleted image files (show placeholder)
- [ ] Concurrent shortcut execution properly queued (not interleaved)

---

## Phased Implementation

### Phase 1: Crash-Resilient Sessions

- [ ] `ICaptureSessionService` interface + SQLite implementation
- [ ] Checkpoint persistence in `CaptureJobProcessor`
- [ ] Recovery detection on startup
- [ ] Recovery UI (dialog with thumbnails)
- [ ] Session cleanup job

### Phase 2: Conditional Rules Engine

- [ ] `IWorkflowRuleEngine` + YAML rule parser
- [ ] Built-in condition implementations
- [ ] Rule editor UI (basic)
- [ ] Integration with KFIP0003 (X context detection)
- [ ] Integration with KFIP0010 (content analysis)

### Phase 3: Workflow Shortcuts

- [ ] `IWorkflowShortcutService`
- [ ] System-defined shortcuts (x-instant-share, quick-save, etc.)
- [ ] Hotkey registration + conflict detection
- [ ] Shortcut editor UI
- [ ] Shortcut learning suggestions

### Phase 4: History & Replay

- [ ] `IWorkflowHistoryService`
- [ ] History browser UI
- [ ] OCR-based search
- [ ] Re-capture and re-run actions
- [ ] Quick access panel

### Phase 5: Polish & Integration

- [ ] Command palette integration (KFIP0007)
- [ ] Telemetry: shortcut usage, recovery rate, history adoption
- [ ] User documentation
- [ ] Performance optimization

---

## Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Session database corruption | High | SQLite WAL mode, periodic integrity checks, backup before recovery |
| Rule engine complexity | Medium | Start with 5 built-in conditions, defer custom scripting to Phase 5 |
| Auto-execute surprises users | Medium | Visual toast feedback, easy undo (open history), per-shortcut opt-in |
| History storage bloat | Low | Configurable retention (default 90 days), thumbnail compression |
| Hotkey conflicts | Low | Conflict detection UI, suggest alternative hotkeys |
| Recovery false positives | Low | Track recovery success rate, allow "don't ask again for this session" |

---

## Open Questions

1. **Should shortcuts support conditional branching?** v1 uses linear pipelines; branching adds complexity. Defer to Phase 5 if user demand exists.

2. **Should history sync to cloud?** Not in scope for v1. Future consideration for multi-device workflows.

3. **Should we support workflow templates shared by the community?** Interesting for Phase 5: import/export YAML, community repository.

4. **How aggressive should shortcut learning be?** Balance helpful vs creepy. Start with explicit "Save as shortcut" only, add suggestions in Phase 5 based on pattern confidence >80%.

---

## Related Work

- **KFIP0001**: OCR used for text-heavy detection and history search
- **KFIP0003**: X context detection feeds `TargetPlatformIsXCondition`
- **KFIP0005**: Social presets integrate with workflow shortcuts
- **KFIP0007**: Command palette hosts shortcut actions
- **KFIP0008**: Redaction available as pipeline step
- **KFIP0009**: Metadata strip, size check as pipeline steps
- **KFIP0010**: Content analysis feeds `ContentIsTextHeavyCondition`
- **KFIP0011**: OCR-to-clipboard as pipeline step

---

## Success Metrics

- **Recovery success rate**: >90% of interrupted sessions successfully recovered
- **Shortcut adoption**: >40% of users create or use at least one shortcut within 30 days
- **Workflow automation rate**: >50% of captures use auto-execute workflows (no dialogs)
- **History usage**: >25% of users open history browser at least once per week
- **Capture-to-share time**: <5 seconds median for X-targeted captures using `x-instant-share` shortcut
- **Crash-to-recovery time**: <10 seconds from app restart to recovered capture

---

## References

- X user research: workflow friction, crash reports, sharing pain points (2026-06-21)
- XerahS team priorities: Linux stability, workflow automation (via @ShareX, 2026-05)
- ShareX Discord feedback threads on capture interruption and recovery
