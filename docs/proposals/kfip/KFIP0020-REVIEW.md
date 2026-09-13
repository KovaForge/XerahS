# KFIP0020 — Review Notes (Nadia + Mikhail)

**Reviewer team**: Nadia (Research / Product) + Mikhail (Implementation / Engineering)
**Date**: 2026-09-13
**Target**: `docs/proposals/kfip/KFIP0020-capture-and-go-unified-gesture.md` (commit e8573d0cb)

---

## Verdict

**Status**: ✅ Approved with feedback (Implementation Eligible)

The KFIP is grounded in KFIP0019's evidence base (screensnap.pro 2026-05, worktime 2026-Q3, allblogs 2026-06), follows the established KFIP prose shape, and targets a real discoverability gap in the mid-tier segment C/D/E. Both Nadia (scope) and Mikhail (engineering surface) recommend the proposal proceed to staged implementation, with the feedback below folded into the implementation PRs.

---

## Nadia — Product / Research Feedback

### Critical (must address)

1. **§B "Workflow Pipeline" — Add explicit citation that this is KFIP0010's pre-softening target distribution.** Currently references KFIP0010 in the "Forces" section but the §B pseudo-code does not name the source. Implementation must `// Source: KFIP0010 §"Pre-softening target"` at the top of `PreSoftenXProfile`.

2. **§A.2 "Capture Tray" — Tray button labels must not contain the word "Share" unless public-share is configured.** Threat-3 product-policy red-line per KFIP0019. Capture-and-Go's clipboard-only default must literally read "Capture & Copy Link" — not "Capture & Share". ✅ KFIP0020 already enforces this; reaffirming for implementation.

3. **§A.4 "Public-Share Opt-In" — The X OAuth check must read current state on every tray show, not at app startup.** A user revoking X OAuth mid-session must cause the tray to re-disable the button on next show. Implementation must use a per-show check, not a cached bool.

### Product (should address)

4. **§C "Privacy Default" — Recommend adding `Settings → Capture → After Capture Action → First-time public-share opt-in` dialog text exactly as proposed in §A.4.** The dialog copy is good — preserve verbatim.

5. **§D "Discovery" — The inline hint text near Quick-Share should read `Ctrl/Cmd+Shift+X for one-tap Copy Link`. The verb is "Copy Link", not "Share" or "Post".** Consistent with Threat-3 red-line.

6. **§B "PrivacyGate Apply" — Privacy-redaction preset must be applied BEFORE pre-softening, not after.** Privacy-redaction works on raw bitmaps (text masking, blur); pre-softening works on downsampled JPEGs (where redaction would lose fidelity). Implementation must order PrivacyGate → PreSoften → Upload.

### Editorial

7. **§"Why Capture-and-Go, and Why Now?" — The third force (Threat-model default stance) deserves more emphasis. Recommend moving it from third to first position.** Optional; non-blocking.

8. **§"References" — Add cite for the KFIP0019 §"Threat 3" red-line as a primary reference, not buried in §A.2.** Editorial; non-blocking.

---

## Mikhail — Engineering / Implementation Feedback

### Critical (must address)

1. **§B "Workflow Pipeline" — `PreSoftenXProfile` must be a pure function over a bitmap stream, with no XerahS.Core or XerahS.Common dependency in the test path.** Implementation must live in a dedicated namespace `XerahS.Core.Media.Soften` and the project file must keep it conditionally-compilable so the test class `[Trait("Category","Pure")]` can run without the Avalonia UI shim.

2. **§B "Workflow Pipeline" — `CaptureAndGoOptions` must round-trip via Newtonsoft.Json (existing XerahS settings serialization).** Follow the `ApplicationConfig` serialization pattern (custom converters, ordinal case sensitivity, TypeNameHandling.Auto). Do NOT introduce System.Text.Json — codebase standard is Newtonsoft.

3. **§E "Acceptance Tests" — Tests 1–4 (`PreSoften_*`) are pure-function tests. They must live in `tests/XerahS.Tests/Media/PreSoftenXProfileTests.cs`, not `Workflows/CaptureAndGoWorkflowTests.cs`.** Workflow tests depend on the Avalonia UI dispatcher; pure-function tests must not.

### Engineering (should address)

4. **§B "Workflow Pipeline" — Clipboard handoff must use the existing `src/platform/XerahS.Platform.Windows/ClipboardHelpersEx.cs` pattern, not raw `TextCopy`.** Codebase standard is platform-abstraction-aware. For Linux/macOS, follow the `XerahS.Platform.*` project pattern.

5. **§A.1 Keyboard binding — `Ctrl+Shift+X` (Windows/Linux) and `Cmd+Shift+X` (macOS) need a hotkeys registration that reads from `ApplicationConfig`.** Existing `HotkeyService` already handles this. Implementation should add `Hotkey = CaptureAndGo` alongside existing `Hotkey = RegionCapture`.

6. **§A.4 Public-share gate — `IsXOAuthConfigured` must read from `SettingsManager.GetUploaderConfig("twitter")` or equivalent.** Do not cache; do not construct a custom X account check.

7. **§E Test 7 "CaptureAndGo_Run_FromConfirmToClipboard" — This is an integration test. Tag with `[Trait("Category","Integration")]` so it can be skipped in the PR-builder smoke loop and run only in dedicated integration jobs.** Keep Tests 1–6 unit; Test 7 integration-only.

### Risk (FYI — work in progress, not blocking)

8. **§"Risks" — `Clipboard hijack` row: TextCopy on Linux uses xclip / xsel. If neither is installed, XerahS already falls back to writing to a clipboard file under /tmp.** This is an existing XerahS.Platform.Linux behavior; KFIP0020 inherits it. No new code required.

9. **§"Risks" — `Multi-monitor tray placement` row: Avalonia's `WindowStartupLocation` does not natively support per-monitor anchoring.** Recommend using the existing `XerahS.UI/Helpers/WindowPlacement.cs` helper, not introduce a new tray-placement service in this KFIP.

### Stage Sequencing

- **Stages 1–2 are sequenced (pure function → tray).** ✅ Matches the KFIP's plan.
- **Stage 3 (wiring) can run in parallel with Stage 4 (privacy gate)** because the gate is invoked from the workflow's named step, not the workflow's main path.
- **Stage 5 (discoverability hint) is the lowest priority** — confirmed deferrable to KFIP0023 if Stage 3 takes longer than budget.

---

## Action Items (Folded into Implementation)

| # | Action | Stage | Owner |
|---|--------|-------|-------|
| N1 | `PreSoftenXProfile` file-level comment cites KFIP0010 | Stage 1 | Mikhail |
| N2 | Tray button "Capture & Copy Link" verbatim | Stage 2 | Mikhail |
| N3 | `IsXOAuthConfigured` per-show check | Stage 4 | Mikhail |
| M1 | Pure-function project structure, decouple from Core/Common for tests | Stage 1 | Mikhail |
| M2 | Newtonsoft serialization round-trip for `CaptureAndGoOptions` | Stage 1 | Mikhail |
| M3 | `Media/PreSoftenXProfileTests.cs` test path, not `Workflows/` | Stage 1 | Mikhail |
| M4 | Use `ClipboardHelpersEx` platform abstraction | Stage 2 | Mikhail |
| M5 | Hotkey registration via existing `HotkeyService` | Stage 3 | Mikhail |
| M6 | Use `WindowPlacement` helper for tray | Stage 2 | Mikhail |

---

## Approval

Both reviewers (Nadia + Mikhail) approve KFIP0020 for staged implementation. The actions in the table above are not blocking on the Stage 1 PR (which is pure-function only), but Stages 2+ must address them in their respective PRs.

**Approved by**:
- Nadia Valeva (Research, KovaForge)
- Mikhail Volkov (Engineering, KovaForge)

**Date**: 2026-09-13
**KFIP**: docs/proposals/kfip/KFIP0020-capture-and-go-unified-gesture.md
**Commit**: e8573d0cb (xerahs-kfip: KFIP0020 Capture-and-Go unified gesture)
