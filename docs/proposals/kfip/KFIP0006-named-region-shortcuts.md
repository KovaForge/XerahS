# KFIP0006: Named Region Shortcuts — Persistent Capture Regions for Repetitive Workflows

**Status**: Proposed
**Priority**: P1
**Area**: Region Capture | Workflow Automation | Hotkeys | UX
**Created**: 2026-05-03
**Related**: KFIP0002 (Smart Region Capture Profiles), KFIP0005 (Social Sharing Workflows), XIP0070 (User Research — Top Screen Capture Needs)
**Owner**: KovaForge
**Co-Authors**: Milena (research)

---

## Summary

Users with repetitive capture targets — specific UI panels, code windows, tweet cards, chat panes — currently redraw the same region manually every time. Even when "pre-configured region" settings exist, invoking them without a dedicated hotkey or naming scheme requires navigating through menus. This KFIP proposes **Named Region Shortcuts**: a first-class system for saving, naming, hotkey-binding, and invoking specific screen regions in one action, eliminating the repetitive drag for good.

---

## Problem Statement

### The Repetitive Drag Problem (Unresolved by KFIP0002)

KFIP0002 introduced *smart detection* — the app suggests regions based on visual heuristics. But for many users, the target region is *stable and known*, not something the app needs to detect:

- "I capture the same tweet column 10 times a day"
- "I always need my terminal window (1200×800 at coordinates 0, 100)"
- "I document the same code block in VS Code every morning"

Detection fails these users because there is nothing to *detect* — they know exactly what they want, and they want it *now*.

### Evidence from User Research and Issues

**ShareX issue #7898** (2025-04): User wants to automate capture of *known* regions via command line, but `-RectangleRegion` opens a GUI instead of accepting coordinates. User manually defines multi-region captures, then has to stitch them externally. *"I know the regions that I need to capture and I could like to do it without having to select them."*

**ShareX issue #8345** (2026-01): User wants the Ruler tool to define a region, then feed that exact region into capture/record without redrawing. Currently Ruler and Capture are disconnected tools. *"It would be immensely useful to be able to start a 'Predefined Capture / Record' directly from ruler mode."*

**ShareX issue #8220** (2025-10): User sets a "pre-configured region" in the Capture menu, but when they run the OCR workflow, the region is ignored — they still have to drag-select manually. The pre-configured region setting exists but doesn't wire into all capture flows.

**XIP0070 user research**: "I capture tweets maybe 10 times a day — every time I'm drawing the same rough rectangle." The research identifies "Intelligent Capture & Workflow Automation" as a top-5 need. Named regions are the missing primitive — you can't automate a workflow if you can't name its trigger.

### Current Gaps in XerahS

| Gap | Description |
|-----|-------------|
| No named regions | Regions are anonymous; no way to organize or search them |
| No per-region hotkeys | Pre-configured region exists in settings but is global, not per-region |
| Workflow disconnection | Ruler tool and Region Capture are separate; no data passing |
| No quick-invoke UI | No command palette or popup for invoking saved regions by name |
| Pre-configured region limited | One global pre-configured region per hotkey; no multi-region sets |

---

## Goals

- Users can save a screen region by name, assign it a hotkey, and capture it in one action
- Regions are persistent and survive restarts
- Each region can optionally specify a monitor (for multi-monitor setups)
- Users can invoke regions via hotkey OR via a quick popup (name search)
- Saved regions can include an optional anchor app window, so the region recalibrates when the window moves
- Integration with the AfterCapture pipeline so named regions trigger full workflows

## Non-Goals

- No automatic region learning or ML-based suggestion ( KFIP0002 owns that)
- No multi-region capture in a single invocation for v1 (each hotkey = one region)
- No coordinate input via command line for v1 (future via CLI/XIP0063)
- No visual region editor beyond the basic capture-crosshair UI

---

## Proposed Solution

### 1. Named Region Model

```csharp
public sealed class NamedRegion
{
    public string Id { get; init; }           // GUID
    public string Name { get; init; }         // "Tweet Column", "Terminal", "VS Code Block"
    public Rect Region { get; init; }          // Absolute screen coordinates
    public int MonitorIndex { get; init; }    // 0-based, -1 = any monitor
    public string? AnchorWindowTitle { get; init; }  // Optional window filter
    public string? AnchorProcessName { get; init; }   // Optional process filter
    public DateTime CreatedAt { get; init; }
    public DateTime LastUsed { get; set; }
    public int UseCount { get; set; }
}
```

**Anchor-based dynamic regions** (optional, for window-anchored captures):
If `AnchorProcessName` is set, the region is computed relative to the anchored window's bounds each time — capturing the same *relative* area even if the window moves or switches monitors. This handles users who keep windows in consistent positions but not pixel-perfect ones.

### 2. Region Storage

```csharp
public interface INamedRegionRepository
{
    Task<IReadOnlyList<NamedRegion>> GetAllAsync();
    Task<NamedRegion?> GetByIdAsync(string id);
    Task SaveAsync(NamedRegion region);
    Task DeleteAsync(string id);
    Task<RenameAsync(string id, string newName);  // For quick rename without full re-capture
}
```

Storage: JSON file at `%APPDATA%/XerahS/named-regions.json` (Windows), `~/Library/Application Support/XerahS/named-regions.json` (macOS), `~/.config/xerahs/named-regions.json` (Linux).

### 3. Save Region During Capture

When the region capture overlay is active:

```
User drags region
        │
        ▼
[Release mouse] → [Preview overlay appears]
                      │
                      ├─ [Save as Named Region] button  ← NEW
                      ├─ [Copy] [Annotate] [Upload]
                      └─ [Cancel]
```

Clicking **"Save as Named Region"**:
1. Opens a small inline dialog: text field for name + monitor selector
2. On confirm: saves to repository, shows brief toast "Region 'Tweet Column' saved"
3. Optionally prompts to assign a hotkey immediately after naming

**Quick-save flow (no dialog):**
- If user presses `Ctrl+S` (or `Cmd+S`) while region preview is shown: saves with auto-generated name "Region N" and opens the rename hotkey prompt next.
- This enables muscle-memory region saving without breaking flow.

### 4. Hotkey Binding Per Region

In Task Settings → **Named Regions** tab:

```
┌─ Named Regions ─────────────────────────────────────────────┐
│                                                              │
│  [+ New Region]          [Import]  [Export]                  │
│                                                              │
│  🔍 Filter regions...                                       │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ 🪟 Tweet Column              Ctrl+Shift+1    [Edit][Del]│ │
│  │    Monitor 1 • 680×900 @ (1240, 0)                     │ │
│  │    Last used: 2 hours ago                              │ │
│  ├────────────────────────────────────────────────────────┤ │
│  │ 🪟 Terminal Window             Ctrl+Shift+2    [Edit][Del]│ │
│  │    Anchored to: alacritty       1200×800 @ (0, 100)   │ │
│  │    Last used: Today                                    │ │
│  ├────────────────────────────────────────────────────────┤ │
│  │ 🪟 VS Code Block               Ctrl+Shift+3    [Edit][Del]│ │
│  │    Monitor 0 • 800×600 @ (100, 200)                   │ │
│  │    Last used: Yesterday                                │ │
│  └────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
```

**Hotkey assignment:**
- Each region can have its own global hotkey
- Conflicts are detected and warned before saving
- Unassigned regions are still invokable via the quick-invoke popup

### 5. Quick-Invoke Popup

Pressing a configurable hotkey (default: `Ctrl+Shift+Space` / `Cmd+Shift+Space`) opens the **region launcher**:

```
┌─ Capture Region ──────────────────────────┐
│ 🔍 Type to filter...                     │
│                                          │
│  ↵ Tweet Column          Ctrl+Shift+1   │
│  ↵ Terminal Window       Ctrl+Shift+2   │
│  ↵ VS Code Block         Ctrl+Shift+3   │
│  ──────────────────────────────────────  │
│  + Save current region as new...         │
└──────────────────────────────────────────┘
```

- Fuzzy search by name
- Arrow keys navigate; Enter captures immediately
- Shows the hotkey hint so users learn the shortcut
- "Save current region" option: if a region capture is active (overlay showing), saves that region without needing to open settings

### 6. One-Shot Capture via Hotkey

When a region hotkey is pressed:

```
Hotkey triggered (e.g., Ctrl+Shift+1)
        │
        ▼
Resolve region (fixed or dynamic via anchor)
        │
        ▼
Execute capture silently (no overlay, no preview step)
        │
        ▼
Run AfterCapture pipeline (annotate, OCR, upload, copy...)
        │
        ▼
Show brief system notification (optional, configurable)
```

**Silent vs. Confirm mode:**
Users can configure per-region whether it shows a 1-second preview before running AfterCapture tasks. Default: silent for named regions (they trust their own saved region).

### 7. Region Anchoring (Dynamic Regions)

For anchored regions:

```csharp
public Rect ResolveRegion(NamedRegion region)
{
    if (string.IsNullOrEmpty(region.AnchorProcessName))
        return region.Region;  // Static region

    var window = FindWindowByProcess(region.AnchorProcessName);
    if (window == null) return region.Region;  // Fallback

    // Relative offset from anchored window
    var offsetX = region.Region.X - region.AnchorWindowBounds.X;
    var offsetY = region.Region.Y - region.AnchorWindowBounds.Y;

    // Return region at same relative offset within current window bounds
    return new Rect(
        window.Bounds.X + offsetX,
        window.Bounds.Y + offsetY,
        region.Region.Width,
        region.Region.Height
    );
}
```

**Use case**: User pins their terminal to the top-left. The anchored region "Terminal" captures the same 1200×800 relative area even after moving the window.

### 8. Multi-Region Capture (Future Extension)

Not in v1, but the data model supports it: a **Region Group** containing multiple NamedRegions invoked sequentially. This enables the ShareX issue #7898 workflow: capture region A, then region B, then auto-stitch. Documented here to prevent premature model design that blocks this extension later.

---

## Technical Design

### Data Flow

```
Named Region Hotkey Pressed
        │
        ▼
NamedRegionService.Resolve(regionId)
        │
        ├─ Static region → return Rect
        └─ Anchored region → query window bounds → compute relative rect
        │
        ▼
CaptureJobProcessor.CaptureRegion(resolvedRect, silent: true)
        │
        ▼
AfterCapture pipeline (existing infrastructure)
        │
        ▼
Notification + history entry
```

### Integration Points

| Component | Integration |
|-----------|-------------|
| `TaskSettings` | New `NamedRegions` tab with list + hotkey binding |
| `CaptureJobProcessor` | `CaptureRegion(Rect rect, bool silent)` — existing overload |
| `NamedRegionService` | New service: resolve, save, delete, enumerate |
| `HotkeyService` | Per-region hotkey registration with conflict detection |
| `RegionCaptureOverlay` | "Save as Named Region" button in preview step |
| `INotificationService` | Post-capture confirmation notification |
| `HistoryService` | Tag history entries with `NamedRegionId` for analytics |

### File Structure

```
src/desktop/core/XerahS.Core/
├── Services/
│   ├── INamedRegionService.cs
│   └── NamedRegionService.cs
├── Models/
│   └── NamedRegion.cs
└── Storage/
    ├── INamedRegionRepository.cs
    └── JsonNamedRegionRepository.cs

src/desktop/app/XerahS.UI/
├── ViewModels/
│   └── NamedRegionsViewModel.cs
├── Views/
│   ├── NamedRegionsSettingsView.axaml
│   └── RegionLauncherPopup.axaml
└── Services/
    └── RegionLauncherService.cs  (popup invocation)
```

---

## UX Design Details

### First-Time Flow

1. User performs a region capture (any workflow)
2. During preview, notices the "Save as Named Region" button
3. Clicks it, names the region, optionally assigns a hotkey
4. Toast: "Saved. Press Ctrl+Shift+1 to capture this region anytime."
5. Region appears in Named Regions settings

### Returning User Flow

1. User presses `Ctrl+Shift+1`
2. Captures silently, runs AfterCapture pipeline
3. 1-second toast: "Terminal captured. Link copied to clipboard."

### Quick-Invoke Flow

1. User presses `Ctrl+Shift+Space`
2. Region launcher popup appears
3. Types "tweet" → narrows to "Tweet Column"
4. Presses Enter → silent capture

### Settings Flow

- **Named Regions tab**: Full CRUD for regions, hotkey binding per region, anchor configuration
- **Import/Export**: JSON for backup and sharing
- **Conflict detection**: Warns if a hotkey is already assigned; shows what's using it

---

## Acceptance Criteria

### Functional

- [ ] User can save a region during capture with a name, no extra steps beyond naming
- [ ] Saved regions persist across app restarts
- [ ] Each region can have an assigned global hotkey
- [ ] Pressing a region's hotkey captures that exact screen area silently
- [ ] Region launcher (`Ctrl+Shift+Space`) shows searchable list of saved regions
- [ ] Anchored regions correctly recompute when the anchor window moves
- [ ] Named region captures integrate with AfterCapture pipeline (OCR, upload, copy, etc.)
- [ ] User can export/import regions as JSON

### Quality

- [ ] Hotkey press to capture complete in <200ms (excluding actual capture + upload time)
- [ ] Region launcher opens in <50ms
- [ ] No duplicate captures if hotkey is pressed rapidly
- [ ] Conflicts between region hotkeys and other app hotkeys are detected and warned

### Edge Cases

- [ ] Region saved on disconnected monitor gracefully shows error on invoke with option to reassign
- [ ] Anchor window not found: falls back to static region or shows notification
- [ ] Overlapping hotkeys: most recently assigned wins; user is warned
- [ ] Region renamed: hotkey binding preserved

---

## Phased Implementation

### Phase 1: Core Storage + Capture Flow

- [ ] `NamedRegion` model and `INamedRegionRepository` + JSON implementation
- [ ] `NamedRegionService` with save, resolve, list, delete
- [ ] "Save as Named Region" button in region capture preview
- [ ] Basic Task Settings tab (list view, name, region coordinates, delete)
- [ ] Silently invoke saved region via direct method call (no hotkey yet)
- [ ] Tests: save/load roundtrip, resolve static vs. anchored

### Phase 2: Per-Region Hotkeys

- [ ] Hotkey registration per NamedRegion (conflicts detected)
- [ ] Hotkey column in settings list with inline binding UI
- [ ] Silent capture on hotkey invoke
- [ ] Post-capture notification (toast)
- [ ] Tests: hotkey registration, conflict detection, rapid-fire guard

### Phase 3: Quick-Invoke Popup

- [ ] Global hotkey `Ctrl+Shift+Space` → region launcher popup
- [ ] Fuzzy search by region name
- [ ] Keyboard navigation (arrows + Enter)
- [ ] "Save current region" shortcut in launcher (for active captures)
- [ ] Tests: search accuracy, latency, keyboard flow

### Phase 4: Dynamic Anchoring + Polish

- [ ] Anchor-based region resolution (window-relative)
- [ ] Monitor selector in save dialog
- [ ] Import/export JSON
- [ ] Per-region "show preview" toggle
- [ ] Use count and last-used tracking in settings
- [ ] Tests: anchor resolution across window moves, multi-monitor

---

## Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Monitor disconnect invalidates saved coordinates | Medium | Store monitor ID (if stable) + warn on reconnect; option to reassign |
| Hotkey conflicts with other apps | Low | Detect at assignment time; recommend avoiding bare F-keys |
| Anchor window detection fails on Wayland | Medium | Graceful fallback to static region; don't crash |
| Users accumulate dozens of stale regions | Low | Sort by last-used; highlight unused regions (>30 days) for cleanup |

---

## Open Questions

1. **Should a region's hotkey compete with a global capture hotkey?** If `Ctrl+Shift+1` is a named region hotkey, and `Ctrl+Shift+1` is also a global "start region capture" hotkey — which wins? Recommendation: Named region hotkeys are additive; they only fire when no capture overlay is active (so they don't intercept normal capture workflows).

2. **Max regions?** Recommend a soft limit of 50 with a warning — beyond that, search becomes slow and the UI gets unwieldy. No hard block.

3. **Should regions have optional descriptions/tags?** Useful for organization. Defer to Phase 2 if requested.

4. **What about capture sequence (multi-region in one shot)?** Documented as a v2 extension. A NamedRegionGroup containing ordered regions, invoked by one hotkey. Don't design into the model now — just ensure the individual NamedRegion model doesn't preclude this later.

---

## Related Work

- **KFIP0002**: Smart Region Capture Profiles — complementary; detection suggests regions for unknown targets; named regions cover known recurring targets
- **KFIP0005**: Social Capture Workflows — named regions are the trigger primitive for social workflows; a "Tweet Column" named region + social preset = one-shot social capture
- **XIP0070**: User research identifies "capture tweets 10x/day with same rectangle" as a key pain point — named regions solve this directly
- **ShareX issue #7898**: Multi-region automation via command line; our named region hotkeys achieve the same goal with better UX
- **ShareX issue #8345**: Ruler-to-capture flow; our "Save as Named Region" button in preview connects the two tools

---

## Sources

- ShareX GitHub issue #7898 — Command line option for capturing multiple regions (2025-04-24)
- ShareX GitHub issue #8345 — Ruler tool integration with capture/record (2026-01-24)
- ShareX GitHub issue #8220 — Pre-configured region with OCR workflow (2025-10-22)
- XIP0070 — User Research: Top 5 Screen Capture Needs (KovaForge, 2026-04-10)
- KFIP0002 — Smart Region Capture Profiles (KovaForge, 2026-04-12)
- KFIP0005 — Screen Capture Workflow Enhancements for Social Sharing (KovaForge, 2026-04-26)