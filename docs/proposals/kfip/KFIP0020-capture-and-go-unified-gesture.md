# KFIP0020: Capture-and-Go Unified Gesture (X/Twitter Posting Loop)

**Status**: Draft
**Priority**: P1 (Implementation-Ready)
**Area**: Implementation | Capture UX | X/Twitter | Cross-Cutting
**Created**: 2026-09-13
**Submitter**: Nadia (Research, KovaForge)
**Co-Authors**: McoreD <195468996584275968@users.noreply.github.com>, vladislava-kova-kf <vladislava-kova-kf@kovadev>
**Related**: KFIP0008 (Privacy Redaction), KFIP0009 (Share-Ready Workflows), KFIP0010 (Compression-Resilient Capture), KFIP0011 (OCR / Alt-Text), KFIP0013 (Smart Thumbnails), KFIP0014 (Power User Workflows), KFIP0015 (Annotation Toolkit), KFIP0018 (User-Needs Research), **KFIP0019 (User-Needs Refresh — scope source)**, **KFIP0021 (Context-Leak Detection — downstream)**, KFIP0022 (Cross-Device Handoff — downstream), KFIP0023 (Capture UX Refresh — downstream)

---

## Summary

KFIP0019 closed three of eight open questions from KFIP0018 with new evidence and named **four implementation successors**: KFIP0020 (this proposal), KFIP0021, KFIP0022, KFIP0023. KFIP0020 is the **first implementation-grade KFIP in the post-research arc** and proposes the **Capture-and-Go** feature: a single one-tap gesture that compresses a region capture to X's optimal in-feed image (KFIP0010), uploads via the configured uploader, copies the public link to the system clipboard, and surfaces a ready-to-paste handoff in the X share intent.

The **default handoff is private local + clipboard**: the screenshot is *not* posted to X, only the link. Public-share one click away from the tray; **public-by-default is explicitly forbidden by product policy** (KFIP0019 §"Threat 3 — Public-by-default upload"). KFIP0020 also closes one of the two remaining KFIP0018 open questions ("Should KFIP0019 target Segments A/B or C/D/E?" — answered: mid-tier, because they are discoverability-blocked, not feature-blocked).

The output is: a single user-initiated gesture, three new code paths (capture-tray handler, command-palette action, optional keyboard binding), a new workflow destination in the AfterCaptureWindow, and a default shortcut (Ctrl+Shift+X on Windows/Linux, Cmd+Shift+X on macOS) that collapses **four sequential steps** of KFIP0018's capture-to-post loop into one.

---

## Motivation / User Problem

### Why Capture-and-Go, and Why Now?

KFIP0018 §"Capture-to-Post Loop" identified a 10-step loop with friction distributed across all steps. KFIP0019 updated this model with new evidence (screensnap.pro 2026-05; worktime.com 2026-Q3; allblogs.in 2026-06) showing **two-step elimination is more impactful than uniform optimization**: collapsing **steps [4] (compress) and [8] (paste link)** for power users while surfacing **a one-tap [3] (editor) + [8] (paste link) combo** for casual users.

Three concrete forces make KFIP0020 the right next KFIP:

1. **X recompression pipeline is now fully specified.** Screensnap.pro's 2026-05 measurements (PNG → JPEG forced conversion, 30–60% file-size reduction, 1200×675 optimal in-feed, downsampling above ~1600×900) turn the pre-softening step from a heuristic problem (KFIP0010 days) into a deterministic function. KFIP0020 consumes these numbers directly and turns them into a one-step pipeline instead of a multi-pass workflow users discover by trial-and-error.
2. **Mid-tier segments (C/D/E from KFIP0018) remain discoverability-blocked, not feature-blocked.** KFIP0019 §"Mid-Tier Segment Deep Dive" reproduced this finding with new data: Segment C (product/design/marketing) uses the OS Snipping Tool because XerahS's X/Twitter-specific affordances (KFIP0010, KFIP0014, KFIP0017) are buried inside after-capture workflows. A one-tap Capture-and-Go surface, paired with KFIP0023's first-run tour, surfaces them.
3. **Threat-model evidence has hardened the public-share default stance.** KFIP0019 §"Threat 3 — Public-by-default upload" elevated the public-share default from "configurable preference" to **"categorical product-policy red line"**. KFIP0020 must enforce this at the gesture level: clipboard-only is the default; public-share requires a deliberate second action.

A "Capture-and-Go" feature was implicit in KFIP0009 §"Quick-Share Action" and KFIP0014 §"One-Tap Post". KFIP0020 consolidates both into one feature, anchored to the KFIP0019 evidence base.

### The Open Questions KFIP0020 Closes

KFIP0019 left five open questions (KFIP0018's eight minus three KFIP0019 answered). KFIP0020 closes two:

1. **"Should the implementation arc target power users or mid-tier segments first?"** — Answered: **mid-tier (C/D/E)**, because (a) aggregate population dwarfs power users, (b) the unmet need is *discoverability* (a lower-effort fix than new features), and (c) KFIP0014, KFIP0015, KFIP0016, KFIP0017 already saturate Segment A/B. KFIP0020 ships with (a) a discoverable gesture and (b) a discoverable keyboard binding.
2. **"How should XerahS handle the Lightshot-style public-by-default workflow?"** — Answered: **ship Capture-and-Go with `ClipboardOnly` as the explicit, *labeled* default**. The capture tray's action labels read "Capture & Copy Link" (default) and "Capture & Post to X" (opt-in, requires an X credentials check first). Public share is a single second click but never implicit.

The remaining three KFIP0019 open questions (capture-time context-leak detection, cross-device handoff, mobile-sharesheet parity) are deferred to KFIP0021, KFIP0022, and KFIP0023.

---

## Research Findings

### 1. The Four-Step Collapse — Updated Loop

KFIP0018's 10-step loop, with KFIP0020's collapsing arrows:

```
[1. Decide to share] → [2. Capture region] → [3. Annotate] →
  [4] CAPTURE-AND-GO ►►► [4. Compress + 5. Upload + 6. Copy link]
  [7. Open X compose] → [8] CAPTURE-AND-GO ►►► [8. Paste link]
  → [9. Add text / alt] → [10. Post]
```

For power users: KFIP0020 reduces steps [4]–[6] and step [8] from *explicit, separate* to *implicit, automated*. For mid-tier (C/D/E) users: KFIP0020 also collapses step [3] because the user can decline annotation through the AfterCaptureWindow with a single click (no annotated workflow required).

### 2. X Recompression Pipeline (KFIP0010 Adopted Verbatim)

KFIP0010 specified the pre-softening target distribution; KFIP0019 sharpened the numbers from screensnap.pro:

- **Single in-feed post**: 1200×675 (16:9) optimal
- **Two-image**: 1200×600 each (2:1)
- **Three-image**: 1200×675 lead + 1200×600 stacked
- **Four-image grid**: 1200×675 each
- **Twitter Card (`summary_large_image`)**: 1200×628 (1.91:1)
- **Format**: PNG/JPG both accepted; WebP re-encoded to JPG on display
- **Compression**: X applies 30–60% file-size reduction; downsamples above ~1600×900

KFIP0020 ships a single pipeline that picks the smallest of these variants that fits the captured region's aspect ratio with no more than one axis downsampled. The user never specifies dimensions — the gesture does.

### 3. Mid-Tier Segment User Journeys

#### Journey 1: Segment C — Product Designer Annotates a Figma Frame for X

- **Frequency**: 1–5/day
- **Current toolchain**: Snipping Tool → annotate in Figma/Canva → upload to Imgur → paste to X
- **KFIP0020 flow**: PrtScn → drag capture region → type caption in tray → Enter → link is in clipboard; Alt+Tab to X compose → Ctrl+V → done
- **Steps saved**: 7 → 4 (annotation is **optional** in Capture-and-Go; if the user types a caption in the tray, it is appended to the upload as `alt` text)

#### Journey 2: Segment D — Data Analyst Shares a Dashboard Capture

- **Frequency**: 1–5/day
- **Current toolchain**: Snipping Tool → paste into Notion/Word → upload to company Drive → share → paste to X
- **KFIP0020 flow**: PrtScn → drag region → Enter → link in clipboard → paste to X
- **Steps saved**: 6 → 3 (no editor detour, no Drive detour; uploader configured to company default)

#### Journey 3: Segment E — CS Manager Pastes a Ticket Screenshot to a Public X Reply

- **Frequency**: 1–10/day; some x.com staff threads
- **Current toolchain**: Mac system capture → Preview → File > Export > JPG → Imgur → X
- **KFIP0020 flow**: Cmd+Shift+4 → drag → tray: "Capture & Copy Link" (default) → Enter → cmd+V into x.com reply
- **Steps saved**: 8 → 4

The common pattern across all three journeys: **the bottleneck is the upload-and-copy step, not the capture itself.** KFIP0014 and KFIP0017 already optimised capture. KFIP0020 optimises the *after*-capture handoff.

### 4. Threat-Model Integration

KFIP0020 must integrate with three pre-existing privacy gates:

- **KFIP0008 (Privacy Redaction)**: Before the upload, if the user has the privacy-redaction preset enabled, KFIP0020 must apply it. If the preset detects unmasked PII (SSN, credit card, email-like patterns), the gesture surfaces a warning in the tray and offers "Continue anyway" / "Cancel" / "Edit in Redactor".
- **KFIP0009 (Share-Ready Workflows)**: After upload, the handoff step appends the configured X-compat URL-shortener (bit.ly, custom) only if the user has a configured X-account setting; otherwise it returns the raw upload URL.
- **KFIP0021 (downstream)**: Capture-time context-leak detection (notification overlays, browser tab-strip) is explicitly out of scope for KFIP0020. KFIP0020 must defer the leak warning to KFIP0021 if and when the user opts into that detection mode.

### 5. Why One Tap, Not Two?

A two-tap surface ("Capture" → "Process") was the original KFIP0019 sketch. Three pieces of evidence prefer one tap:

1. **Power users already have it.** ShareX and XerahS's existing workflows collapse to one tap in the AfterCaptureWindow's "Upload" button. KFIP0020 should not regress from this.
2. **Mid-tier users prefer lower cognitive load.** UX research (cited in KFIP0019 §"Mid-Tier Segment Deep Dive") consistently shows that mid-tier users abandon tools with >1 decision per capture.
3. **Cancellation is cheaper than selection.** A single tray that defaults to clipboard, with a single labelled Escape, is *simpler* than a menu of three choices ("Capture only", "Capture and copy", "Capture and post"). The user can always swap the default in Settings → Capture → After Capture Action.

---

## Proposed Design

### A. The Gesture Surface

KFIP0020 introduces three UI surfaces, all reading from the same backing service:

#### A.1 Keyboard Binding (the primary surface)

- **Windows / Linux**: `Ctrl+Shift+X` (named `CaptureAndGo`)
- **macOS**: `Cmd+Shift+X`
- **Customisable** under Settings → Keyboard → Capture → Capture-and-Go

The binding triggers a region-capture overlay (XerahS's existing RegionCapture component); on confirm, KFIP0020's workflow runs. **Esc cancels at any point.** Holding Shift during the capture-confirm step opens the AfterCaptureWindow instead of the tray — this is the discoverability escape hatch for power users.

#### A.2 Capture Tray (the secondary surface for non-keyboard paths)

When the user closes a region capture by clicking the capture-confirm cursor or pressing Enter, a small "capture tray" appears at the bottom-right of the screen with three buttons:

- **"Capture & Copy Link"** (default, pre-selected — keyboard Enter)
- **"Capture & Post to X"** (opt-in — requires X auth, see A.4)
- **"Capture & Edit"** (opens AfterCaptureWindow — the discovery-surface for KFIP0010/KFIP0014)

The tray auto-dismisses after 8 seconds; the user can dismiss it explicitly with Esc. **The "Copy Link" button is the default and is the literal text on the button — not "Capture & Share".** Per KFIP0019 §"Threat 3", the verb "share" implies public, and "share" is forbidden by default.

#### A.3 Command Palette (KFIP0007's palette)

KFIP0007's capture-command-palette already supports post-capture actions. KFIP0020 adds a `Capture-and-Go (Clipboard)` action and a `Capture-and-Go (Public X)` action to the palette. The palette's pre-selected default is `Capture-and-Go (Clipboard)`.

#### A.4 Public-Share Opt-In Gate

The "Capture & Post to X" button is **disabled** (greyed, with a tooltip) until the user has configured an X OAuth credential under Settings → Uploaders → X / Twitter. The first time the user clicks it, a confirmation dialog reads:

> "Capture-and-Go will upload your image to `your-configured-host` and submit a public X post on your behalf. The image will be public-by-URL once uploaded. Are you sure you want to enable this in the future?"

If "Yes", the button enables for the rest of the session. If "No", the button stays disabled. Settings → Capture → After Capture Action can globally override.

### B. The Workflow Pipeline

When the user confirms a region capture via one of the three surfaces, KFIP0020 runs:

```
  RegionCapture.Confirm(bounds, dpr)
       │
       ▼
  PreSoften(bounds)         ← KFIP0010 consumer:
       │                         pick smallest X-fit variant
       │                         (1200×675 / 1200×600 / 1200×628)
       │                         bound by detected aspect ratio;
       │                         downsample if > 1600 on any axis
       ▼
  PrivacyGate.Apply()       ← KFIP0008 consumer:
       │                         apply preset if enabled;
       │                         surface warning if PII detected
       ▼
  Uploader.Upload(image)    ← configured default uploader:
       │                         ImageUploadJob destination
       │                         (IMGuru, S3, custom, etc.)
       ▼
  ClipboardHandoff(link)    ← copy final URL (raw or shortened) to clipboard;
       │                      capture tray surfaces "View link"
       ▼
  Tray.Show(result)         ← auto-dismiss 8s; "Capture & Copy" highlighted
```

Implementation lives in:

- `src/desktop/app/XerahS.UI/Workflows/CaptureAndGoWorkflow.cs` (new)
- `src/desktop/app/XerahS.RegionCapture/CaptureTrayWindow.cs` (new)
- `src/desktop/core/XerahS.Core/Workflows/PreSoftenXProfile.cs` (new — pure function over KFIP0010 spec)
- `src/desktop/core/XerahS.Core/Workflows/CaptureAndGoOptions.cs` (new — config schema)

### C. The Privacy Default

Three settings under Settings → Capture → After Capture Action:

1. **"Default action"** (default: `CopyLink`)
2. **"Public-share confirmation"** (default: `Required every session`)
3. **"Auto-dismiss tray"** (default: 8 seconds; 0 = never)

The default action of `CopyLink` is **set in code** as well as config — even after a config wipe, XerahS boots into `CopyLink`. This is the architectural enforcement of KFIP0019 §"Threat 3" red-line.

### D. Discovery & First-Run (Rolls into KFIP0023)

KFIP0023 will surface Capture-and-Go in a first-run tour. KFIP0020 itself **must add an inline discoverability hint** in the AfterCaptureWindow: a small text near the existing Quick-Share buttons reading `⌘/Ctrl+Shift+X for one-tap Capture-and-Go`. This is a minimal, no-decision hint, not a tour.

### E. Acceptance Test Surface

KFIP0020 must ship **six new unit tests** in `tests/XerahS.Tests/Workflows/CaptureAndGoWorkflowTests.cs`:

1. `PreSoften_TallPortrait_1200x675_WithinBounds`
2. `PreSoften_WideBanner_1200x600_WithinBounds`
3. `PreSoften_TwitterCard_1200x628_ForAspectRatio1_91`
4. `PreSoften_Downsamples4K_DefaultsTo1200x675`
5. `ClipboardHandoff_RespectsShortener_WhenConfigured`
6. `PublicShareButton_Greyed_WhenXOAuthMissing`

And one integration test in `tests/XerahS.Tests/Workflows/CaptureAndGoEndToEndTests.cs`:

7. `CaptureAndGo_Run_FromConfirmToClipboard_CopiesImageLink`

---

## Implementation Plan

### Stage 1 — Pipeline Skeleton (3 days, P1 deliverable)

- Add `PreSoftenXProfile` (pure function, no IO).
- Add `CaptureAndGoOptions` (config schema, defaults from §C above).
- Add six unit tests for `PreSoftenXProfile` (decided: tests 1–4 above).
- Build green. PR: `xerahs-feature: KFIP0020 stage1 pipeline skeleton`.

### Stage 2 — Tray + Clipboard Handoff (2 days, P1 deliverable)

- Add `CaptureTrayWindow` (Avalonia Window, auto-dismiss timer).
- Add `ClipboardHandoff` service (uses `TextCopy` NuGet, same as existing XerahS clipboard code).
- Wire to existing RegionCapture.Confirm event.
- Add tests 5 (clipboard + shortener) and 7 (end-to-end).
- Build green. PR: `xerahs-feature: KFIP0020 stage2 tray and clipboard`.

### Stage 3 — Capture-and-Go Workflow Wiring (2 days, P1 deliverable)

- Add `CaptureAndGoWorkflow` orchestrator.
- Wire keyboard binding `Ctrl/Cmd+Shift+X` to the workflow (uses KFIP0006's shortcut infrastructure).
- Wire command-palette entries (KFIP0007).
- Add test 6 (public-share greyed).
- Build green. PR: `xerahs-feature: KFIP0020 stage3 wiring`.

### Stage 4 — Privacy + Redaction Gate (1 day, P1 deliverable)

- Wire `PrivacyGate.Apply` using KFIP0008's preset match.
- Wire public-share opt-in dialog flow.
- Settings page additions under Settings → Capture.
- Build green. PR: `xerahs-feature: KFIP0020 stage4 privacy gate`.

### Stage 5 — Discoverability Hint (0.5 day, P1 deliverable)

- Add inline hint in AfterCaptureWindow.
- Update KFIP0023 backlog entry to roll KFIP0020's hint into the first-run tour.
- Build green. PR: `xerahs-feature: KFIP0020 stage5 discoverability hint`.

### Stage 6 — Integration & Smoke (1 day, P1 deliverable)

- Manual smoke on Windows, Linux, macOS — minimum one capture each platform.
- Update `docs/CHANGELOG.md` with `[Feature] Capture-and-Go unified gesture (KFIP0020)`.
- Update `docs/proposals/ieip/` with Implementation Evidence Item `IEIP0020`.
- PR: `xerahs-feature: KFIP0020 stage6 docs and smoke`.

**Total effort**: ~9.5 working days across one engineer. Parallelisation: Stage 2 and Stage 3 can be sequenced; Stages 4–6 must follow Stage 3.

### Fallback / Prioritised Trim

If a stage slips, in this order:

1. **Stage 5 (discoverability hint) can be deferred to KFIP0023 entirely.** KFIP0020 ships without the inline hint; KFIP0023 picks it up.
2. **Stage 4 (privacy gate) can be deferred if KFIP0008 already gates from outside the workflow.** If `PrivacyGate.Apply` exists and is invoked from the regular upload path, KFIP0020 inherits it — Stage 4 reduces to "no code".
3. **Test 7 (end-to-end) can be marked `[Explicit]` if environment-specific (e.g., macOS clipboard behaviour in CI differs).** Tests 1–6 are mandatory.

If Stages 1–4 cannot ship, do not ship KFIP0020 at all — release a stub workflow that opens the AfterCaptureWindow with the default Quick-Share highlighted. This is the rollback plan.

---

## Acceptance Tests

- **Build**: `dotnet build --configuration Release` succeeds with 0 errors and 0 warnings on Windows, Linux, macOS CI.
- **Unit tests**: `dotnet test --configuration Release` runs Tests 1–6 plus the existing 1548; all pass.
- **Manual smoke**:
  - Windows: PrtScn → drag → Ctrl+Shift+X (or default tray) → link in clipboard. Paste in browser address bar shows the uploaded image.
  - macOS: Cmd+Shift+4 → drag → Cmd+Shift+X → link in clipboard.
  - Linux: PrtScn (KDE/GNOME) → drag → Ctrl+Shift+X → link in clipboard.
- **Privacy regression**: Configure random-uploader hosting only (no X auth); confirm "Capture & Post to X" stays greyed in the tray.
- **Public-share negative path**: Configure X auth but disable public-share confirmation; confirm the button stays disabled.
- **KFIP0019 threat-model regression**: Capture a region containing a credit-card-shaped 16-digit number with the privacy-redaction preset enabled; confirm the gesture surfaces the PII warning and lets the user cancel before the upload runs.

---

## Risks

| Risk | Probability | Severity | Mitigation |
|------|-------------|----------|------------|
| KFIP0010 pre-softening spec drifts from X platform changes | Low | Medium | KFIP0020 reads the spec from a versioned config file (config schema §C.1) updated quarterly via KFIP0019 refresh |
| Public-share opt-in is bypassed by a creative keystroke | Low | High | Test 6 enforces button-disabled state at the UI level; tray reads `IsXOAuthConfigured` on every show, not at session start |
| Clipboard hijack (other app replaces clipboard mid-flow) | Very low | Low | XerahS already overwrites clipboard on completion; KFIP0020 logs the final URL to the user tray as visual confirm |
| Tray window blocks capture region selection | Medium | Medium | Tray is positioned bottom-right with `HitTestable=false` after 8s; capture overlay takes Z-order precedence |
| KFIP0008 privacy-redaction preset false-positive blocks legitimate captures | Low | Low | Tray's "Continue anyway" button is one click; default Cancel is one click |

---

## Open Questions

1. **Should Capture-and-Go respect user-configured quality (PNG vs JPG) or always downsample to JPG?** KFIP0020 §B pre-softening defaults to JPG because screensnap.pro data shows X re-encodes anyway. A user-configurable override (`Settings → Capture → Capture-and-Go Format`) is the recommended escape hatch — to be validated against KFIP0023's UX surfacing budget.
2. **Should the tray persist across displays in multi-monitor setups, or mirror to the active monitor?** KFIP0020 defaults to the active monitor's bottom-right; multi-monitor support needs separate UX work (deferred).
3. **Where should auto-post to X (the opt-in second button) target — x.com compose URL or an installed X desktop client?** KFIP0020 Stage 3 wires both via pluggable `IExternalPoster`; default is x.com compose URL.

The remaining five KFIP0018 / three KFIP0019 / three KFIP0020 open questions are tracked in the post-arc backlog (KFIP0021, KFIP0022, KFIP0023, mobile parity).

---

## References

- KFIP0008 (Privacy Redaction — downstream consumer for §B)
- KFIP0009 (Share-Ready Workflows — handoff style reference)
- KFIP0010 (Compression-Resilient Capture — pre-softening source spec)
- KFIP0011 (OCR / Alt-Text — alt-text integration in §B upload step)
- KFIP0013 (Smart Thumbnails — thumbnail generation downstream of KFIP0020)
- KFIP0014 (Power User Workflows — discoverability-comparison reference)
- KFIP0015 (Annotation Toolkit — §A.2 "Capture & Edit" button)
- KFIP0016 (Smart Capture Modes)
- KFIP0017 (Capture Mode Suite)
- KFIP0018 (User-Needs Research — original segment analysis)
- KFIP0019 (User-Needs Refresh — scope source, threat-model §C)
- KFIP0021 (Context-Leak Detection — downstream)
- KFIP0022 (Cross-Device Handoff — downstream)
- KFIP0023 (Capture UX Refresh — discoverability partner)
- Screensnap.pro 2026-05 ("Twitter X Image Size 2026: All Dimensions")
- Worktime.com 2026-Q3 ("Top 5 screen sharing privacy mistakes")
- Allblogs.in 2026-06 ("Browser tab leak in screenshots")

---

## Changelog

- 2026-09-13 — KFIP0020 drafted (replaces the abandoned KFIP0020 stub of 2026-09-06).
