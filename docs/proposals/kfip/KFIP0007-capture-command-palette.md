# KFIP0007: Capture Command Palette — Unified Quick-Invoke Interface for All Capture Modes

**Status**: Proposed
**Priority**: P1
**Area**: Capture UX | Hotkeys | Workflow Automation | Accessibility
**Created**: 2026-05-10
**Related**: KFIP0002 (Smart Region Capture Profiles), KFIP0003 (X/Twitter Context Detection), KFIP0005 (Social Sharing Workflows), KFIP0006 (Named Region Shortcuts), XIP0071 (XerahS Spotlight Assistant)
**Owner**: KovaForge
**Co-Authors**: Milena (research), Nadia (analysis)

---

## Summary

Users currently navigate XerahS capture modes through a fixed global hotkey scheme: `Ctrl+Shift+1` for full-screen, `Ctrl+Shift+2` for region, `Ctrl+Shift+3` for window, `Ctrl+Shift+4` for last-region, and so on. This scheme is fast for users who remember it, but opaque for everyone else, unscalable as features multiply, and completely unaware of context. This KFIP proposes a **Capture Command Palette** — a quick-invoke overlay (default: `Ctrl+Alt+Space` / `Cmd+Alt+Space`) that lets users type or select any capture mode, named region (KFIP0006), social preset (KFIP0005), or workflow in one unified interface, with context-aware suggestions ranked by confidence.

---

## Problem Statement

### The Hotkey Ceiling

Existing XerahS captures rely on a growing list of global hotkeys. This creates three concrete problems:

1. **Discoverability**: New users don't know hotkeys exist. Settings panels list them, but there's no in-context way to explore capabilities at the moment of capture intent.
2. **Cognitive load**: Even power users max out around 5–7 memorable shortcuts. KFIP0006 adds per-region hotkeys, KFIP0005 adds social presets, KFIP0004 adds plugin workflows — the shortcut space is already saturated.
3. **Context blindness**: Pressing `Ctrl+Shift+2` always opens the same region selector, even when the user is viewing a tweet and would benefit from an `x-tweet` preset with thread-aware hints (KFIP0003).

### Evidence from User Research

**XIP0070 user research**: "Intelligent Capture & Workflow Automation" ranked as a top-5 need. Users want the app to *suggest* the right capture mode rather than requiring them to know it exists.

**XIP0071 (XerahS Spotlight Assistant)**: Proposes a macOS-Spotlight-style launcher for XerahS commands. KFIP0007 converges with XIP0071's intent but scopes specifically to the **capture pipeline** — modes, regions, presets, and workflows — rather than the broader "open settings, open media library, run diagnostics" scope.

**CleanShot X competitive analysis** (2026): CleanShot X ($29, macOS) consistently wins user preference because its capture menu "just shows you everything you can do, right when you need it." XerahS has no equivalent quick-invoke surface.

**ShareX issue #8345** (2026-01): User wants to start a capture directly from the Ruler tool — but the only way to access rulers is through the main menu. "There should be a fast way to jump between tools without going back to the tray icon."

**KFIP0005 review finding #13**: "Users won't find presets without a prompt. There's no indication in the generic capture flow that social presets exist." A command palette solves this by surfacing presets at invoke time.

### Current Gaps in XerahS

| Gap | Description |
|-----|-------------|
| No unified quick-invoke | Every capture mode has its own hotkey; no single entry point |
| No fuzzy search | Cannot type "window" to find "Capture Window" — must memorize shortcuts |
| No context ranking | App knows when a tweet is visible (KFIP0003) but doesn't surface `x-tweet` preset at invoke time |
| Named regions buried | KFIP0006's saved regions live in a settings tab; no quick way to invoke them from the capture overlay |
| Plugin workflows invisible | KFIP0004 uploaders and workflows are hidden until configured |

---

## Goals

- Single hotkey opens a searchable palette of all capture modes, named regions, social presets, and workflows
- Fuzzy type-to-filter with keyboard navigation (arrows + Enter)
- Context-aware suggestions: when a tweet window is detected, `x-tweet` preset ranks higher
- Learn from usage: frequently-used modes rise in the default list
- Accessible via keyboard only; screen-reader compatible
- Sub-100ms open time on target machines

## Non-Goals

- No natural language processing or LLM integration (this is fuzzy string matching, not AI)
- No general app launcher scope — that belongs to XIP0071; palette is capture-specific
- No mouse-drag region selection inside the palette (that's the existing overlay)
- No visual theme builder or screenshot editor — palette is an invoke mechanism, not a tool
- No plugin management UI — only invoking already-installed workflows

---

## Proposed Solution

### 1. Command Palette UI

Default invocation: `Ctrl+Alt+Space` / `Cmd+Alt+Space`

```
┌─ XerahS Capture ────────────────────────────────────────────┐
│ 🔍 capture mode...                                         │
│                                                              │
│  ── Quick Access ───────────────────────────────────────── │
│  📸 Region Capture                        Ctrl+Shift+2     │
│  🖼️ Window Capture                        Ctrl+Shift+3     │
│  🖥️ Full Screen Capture                   Ctrl+Shift+1     │
│                                                              │
│  ── Named Regions (KFIP0006) ──────────────────────────── │
│  📌 Tweet Column                          Ctrl+Shift+T     │
│  📌 Terminal Window                       Ctrl+Shift+4     │
│                                                              │
│  ── Social Presets (KFIP0005) ────────────────────────── │
│  🐦 X/Twitter Tweet — 4:5, auto-upload    Ctrl+Shift+5     │
│  💼 LinkedIn Post — 1.91:1                                 │
│                                                              │
│  ── Context Suggestions ───────────────────────────────── │
│  🐦 x.com detected → Tweet Capture (92% confidence)        │
│                                                              │
│  ── Workflows ──────────────────────────────────────────── │
│  🔗 OCR → Clipboard                                          │
│  ☁️ Upload to Imgur → Copy Link                              │
└──────────────────────────────────────────────────────────────┘
```

**Interaction model:**
- Type to filter across all sections simultaneously
- Arrow keys navigate results; Enter executes immediately
- `Esc` dismisses without action
- `Tab` cycles through sections
- Mouse click also works (click = select + execute)
- Holding the invocation hotkey after opening keeps the palette pinned; releasing dismisses

### 2. Context-Aware Ranking

The palette uses the `TweetCaptureDetector` from KFIP0003 (and future context detectors) to boost relevant items:

```csharp
public sealed class PaletteRanking
{
    public string ItemId { get; init; }
    public string Label { get; init; }
    public PaletteItemType Type { get; init; }  // Mode, Region, Preset, Workflow
    public double Score { get; init; }           // Combined relevance score
    public string? ContextHint { get; init; }    // "x.com detected"
}
```

**Scoring formula:**
```
Score = (0.4 × RecencyWeight) + (0.3 × ContextBoost) + (0.2 × FrequencyWeight) + (0.1 × AlphabeticalBias)
```

- **RecencyWeight**: Normalized 0–1 based on time since last use (recent = higher)
- **ContextBoost**: 1.0 if context detector matches (e.g., x.com window detected + tweet preset = boost), 0 otherwise
- **FrequencyWeight**: Normalized 0–1 based on total invocation count
- **AlphabeticalBias**: Small tiebreaker so results are stable when other weights are equal

**Example**: User has XerahS open with `x.com` in foreground:
- `x-tweet` preset gets ContextBoost = 1.0 → jumps to top
- "Tweet Column" named region gets ContextBoost = 0.7 → rises but below the preset
- Full-screen capture gets ContextBoost = 0 → ranks by recency/frequency only

### 3. Category System

Items are grouped into collapsible sections:

| Category | Source | Collapsible |
|----------|--------|-------------|
| Quick Access | Core capture modes | No |
| Named Regions | KFIP0006 repository | Yes |
| Social Presets | KFIP0005 preset service | Yes |
| Context Suggestions | KFIP0003 detector | Yes |
| Workflows | AfterCapture task chains | Yes |
| Plugins | KFIP0004 installed plugins | Yes |

Users can reorder categories in settings. Default order prioritizes what's most useful for new users (modes first) while keeping power-user items (regions, workflows) one arrow-key away.

### 4. Keyboard Shortcuts in Palette

While the palette is open:

| Key | Action |
|-----|--------|
| `Enter` | Execute selected item |
| `Esc` | Dismiss palette |
| `↑` / `↓` | Navigate results |
| `Tab` | Next category |
| `Shift+Tab` | Previous category |
| `Ctrl+Enter` | Execute without running AfterCapture (raw capture only) |
| `Alt+Enter` | Execute with preview step (skip silent mode for regions/workflows) |

### 5. Settings Integration

**Task Settings → Command Palette tab:**

- Toggle palette enabled/disabled
- Change invocation hotkey (default: `Ctrl+Alt+Space`)
- Reorder categories (drag-and-drop list)
- Toggle per-category visibility
- Configure "Show on first launch" — when enabled, palette auto-opens on first run after install
- Usage statistics: top-5 most-invoked items, reset button

### 6. Accessibility

- Full keyboard navigation (no mouse required)
- Screen reader announces: "Capture Command Palette. X results. Use arrow keys to navigate, Enter to select, Escape to dismiss."
- High contrast mode: palette respects system theme
- Reduced motion: no animated transitions
- VoiceOver/NVDA compatible with item labels and category headings
- Minimum font size 14px (configurable in settings)

---

## Technical Design

### Architecture

```
CaptureCommandPaletteService
├── IPaletteDataProvider (abstraction for item sources)
│   ├── CaptureModeProvider (full-screen, region, window, last-region)
│   ├── NamedRegionProvider (KFIP0006 repository)
│   ├── SocialPresetProvider (KFIP0005 preset service)
│   ├── ContextSuggestionProvider (KFIP0003 detector)
│   ├── WorkflowProvider (AfterCapture task chains)
│   └── PluginProvider (KFIP0004 installed plugins)
├── FuzzyMatcher (type-to-filter)
├── PaletteRanker (scoring + context boost)
└── PaletteUI (Avalonia popup window)
```

### Data Providers

```csharp
public interface IPaletteDataProvider
{
    string CategoryName { get; }
    int DisplayOrder { get; }
    bool IsCollapsible { get; }
    Task<IReadOnlyList<PaletteItem>> GetItemsAsync();
}

public sealed class PaletteItem
{
    public string Id { get; init; }
    public string Label { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }        // Emoji or icon key
    public string? HotkeyHint { get; init; }   // e.g., "Ctrl+Shift+2"
    public Action Execute { get; init; }        // Delegate to invoke
    public double DefaultScore { get; init; }   // Base priority before ranking
}
```

### Fuzzy Matching

Implementation uses a lightweight fuzzy string matcher (no external dependencies):

```csharp
public static class FuzzyMatcher
{
    public static double Score(string query, string target);
    // Returns 0.0 (no match) to 1.0 (exact match)
    // Subsequence matching: "cap" matches "Capture Window" at positions 0,3,4
    // Character proximity bonus: adjacent matches score higher
    // Case-insensitive
}
```

Algorithm: longest common subsequence with proximity weighting. O(n×m) where n = query length, m = target length. For 50+ items with queries under 20 chars, this is sub-millisecond.

### Context Integration

```csharp
// Pseudo-code for ranking with context
foreach (var item in allItems)
{
    double score = item.DefaultScore;
    score *= (0.4 * recencyScore(item) + 0.2 * frequencyScore(item));

    var contextBoost = contextDetector.GetBoostForItem(item);
    if (contextBoost > 0)
        score += 0.3 * contextBoost;

    item.FinalScore = score;
}
return allItems.OrderByDescending(x => x.FinalScore);
```

### File Structure

```
src/desktop/core/XerahS.Core/
├── Services/
│   ├── CaptureCommandPaletteService.cs
│   └── IPaletteDataProvider.cs
├── Providers/
│   ├── CaptureModeProvider.cs
│   ├── NamedRegionProvider.cs
│   ├── SocialPresetProvider.cs
│   ├── ContextSuggestionProvider.cs
│   ├── WorkflowProvider.cs
│   └── PluginProvider.cs
├── Fuzzy/
│   └── FuzzyMatcher.cs
└── Models/
    ├── PaletteItem.cs
    └── PaletteRanking.cs

src/desktop/app/XerahS.UI/
├── Views/
│   └── CaptureCommandPaletteView.axaml
├── ViewModels/
│   └── CaptureCommandPaletteViewModel.cs
└── Settings/
    └── CommandPaletteSettingsView.axaml
```

---

## UX Design Details

### First-Time Flow

1. User installs XerahS, presses `Ctrl+Alt+Space` for the first time
2. Palette opens with a brief onboarding overlay: "Type to search. Press Enter to capture."
3. Default items shown: the 4 core capture modes + "Capture Region" highlighted
4. After first use, onboarding dismisses permanently

### Power User Flow

1. User has 3 named regions + 2 social presets configured
2. Presses `Ctrl+Alt+Space`
3. Types "tweet" → palette filters to "Tweet Column" region + "X/Twitter Tweet" preset
4. Presses Enter → captures and runs configured AfterCapture pipeline
5. Total time: ~2 seconds from hotkey press to capture complete

### Context-Aware Flow

1. User is viewing a tweet in their browser
2. Presses `Ctrl+Alt+Space`
3. Palette opens with "🐦 x.com detected → Tweet Capture (92% confidence)" at the top
4. User presses Enter → captures the tweet with auto-detected region hints from KFIP0003
5. AfterCapture runs (e.g., upload to Imgur, copy link)

---

## Acceptance Criteria

### Functional

- [ ] Palette opens within 100ms of hotkey press (measured on reference hardware)
- [ ] Fuzzy filtering works across all item labels and descriptions
- [ ] Keyboard navigation (arrows, Enter, Esc, Tab) works without mouse
- [ ] Context suggestions appear when KFIP0003 detector returns confidence > 0.7
- [ ] Named regions from KFIP0006 appear in the palette automatically
- [ ] Social presets from KFIP0005 appear in the palette automatically
- [ ] Executing an item triggers the correct capture mode or workflow
- [ ] Palette dismisses cleanly on Esc or outside click
- [ ] Category reordering persists across app restarts

### Quality

- [ ] Fuzzy match scoring produces intuitive results for common queries
- [ ] No duplicate items in palette (each item has unique ID)
- [ ] Palette is fully navigable with screen readers (VoiceOver, NVDA, Orca)
- [ ] High contrast and reduced motion preferences are respected
- [ ] Palette window stays within screen bounds on multi-monitor setups

### Edge Cases

- [ ] Zero providers registered: palette shows "No capture items configured" with setup hint
- [ ] Provider throws exception: item is silently skipped; error logged; palette still opens
- [ ] Hotkey conflict: detected and reported in settings (same system as KFIP0006)
- [ ] Rapid double-invocation: second press while palette is open dismisses it (toggle behavior)
- [ ] Category with zero items: hidden until items are available

---

## Phased Implementation

### Phase 1: Core Palette Infrastructure

- [ ] `IPaletteDataProvider` interface and `PaletteItem` model
- [ ] `CaptureModeProvider` with the 4 core capture modes
- [ ] `FuzzyMatcher` implementation
- [ ] Basic Avalonia popup window with text input and result list
- [ ] Global hotkey registration (`Ctrl+Alt+Space`)
- [ ] Tests: fuzzy matching accuracy, hotkey registration, palette open/close lifecycle

### Phase 2: Provider Integration

- [ ] `NamedRegionProvider` (KFIP0006 integration)
- [ ] `SocialPresetProvider` (KFIP0005 integration)
- [ ] `WorkflowProvider` (AfterCapture task chains)
- [ ] Category grouping in UI
- [ ] Keyboard navigation (arrows, Enter, Esc, Tab)
- [ ] Tests: provider data correctness, category rendering, keyboard flow

### Phase 3: Context-Aware Ranking

- [ ] `ContextSuggestionProvider` (KFIP0003 integration)
- [ ] Scoring formula implementation (recency + frequency + context)
- [ ] Usage tracking (invocation count, last-used timestamp)
- [ ] "Context Suggestions" category with confidence display
- [ ] Tests: ranking correctness with synthetic context data

### Phase 4: Settings + Polish

- [ ] Command Palette settings tab (hotkey, category order, visibility toggles)
- [ ] `Ctrl+Enter` raw capture and `Alt+Enter` preview modifiers
- [ ] First-launch onboarding hint
- [ ] Screen reader labels and ARIA-like semantics
- [ ] Multi-monitor boundary handling
- [ ] Tests: settings persistence, accessibility audit, multi-monitor layout

---

## Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Palette open latency exceeds 100ms | Users perceive it as slow, abandon it | Pre-warm providers on app startup; lazy-load heavy providers; benchmark on CI |
| Too many items overwhelm the UI | Palette becomes a settings panel, not a quick-invoke | Cap visible results at 15; collapse categories by default; fuzzy filter reduces noise |
| Context detector false positives erode trust | User dismisses suggestions and never re-enables | KFIP0003 confidence thresholds (0.7+); user can dismiss per-session with "Don't suggest this again" |
| Hotkey conflicts with other apps | Palette doesn't open, user thinks it's broken | Settings shows conflict status; allow custom hotkey; warn at startup if conflict detected |
| Avalonia popup focus stealing on Linux | Palette opens but keyboard input goes to wrong window | Test on GNOME/KDE/Wayland; use proper window activation APIs per platform |
| Feature creep: palette becomes a general launcher | Scope expands beyond capture, delaying delivery | Explicitly scoped to capture pipeline; general launcher belongs to XIP0071 (separate proposal) |

---

## Open Questions

1. **Should the palette also surface recent captures from history?** Useful for "I just captured that, let me do it again" — but history access might belong to a separate Media Library feature (XIP0031). Defer to Phase 4 at earliest.

2. **Should context suggestions auto-execute (confidence > 0.95)?** This would be a "smart capture" mode. Risky for first release — trust is fragile. Keep it suggestion-only; add auto-execute as a settings toggle after user validation.

3. **Should plugin providers be allowed to add custom palette items?** Yes, via KFIP0004 plugin API. Phase 4+ work. Requires a `PaletteItemSchema` for plugins to declare their items.

4. **Should the palette replace the existing hotkey scheme?** No. Existing hotkeys are faster for power users who know them. The palette complements, not replaces. Both coexist.

---

## Success Metrics

- **Adoption**: >60% of users open the palette at least once per week within 30 days of release
- **Efficiency**: Average time from palette open to capture execution <5 seconds (vs. 10+ seconds navigating menus)
- **Discovery**: >30% of palette invocations result in items users had never triggered before
- **Retention**: Users who enable context suggestions use the palette 2× more frequently than those who don't
- **Accessibility**: 100% keyboard-navigable; screen reader compatibility verified on VoiceOver, NVDA, and Orca

---

## Related Work

- **KFIP0002**: Smart Region Capture Profiles — palette surfaces profiles alongside modes
- **KFIP0003**: X/Twitter Context Detection — powers the "Context Suggestions" category
- **KFIP0005**: Social Sharing Workflows — presets appear in palette for quick invocation
- **KFIP0006**: Named Region Shortcuts — regions appear in palette with fuzzy search
- **KFIP0004**: Community Plugin Registry — plugin workflows can be surfaced as palette items
- **XIP0071**: XerahS Spotlight Assistant — shares the launcher pattern but at a broader app scope; KFIP0007 is the capture-specific slice
