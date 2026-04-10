# XIP-0069: User Research — Top 5 Screen Capture Needs

**Status:** Proposed  
**Created:** 2026-04-10  
**Author:** Milena Petrova (Researcher, KovaForge)  
**Research Model:** moonshot/kimi-k2.5

---

## Summary

Synthesized research from X/Twitter discussions, Reddit threads, Hacker News comments, Linux community feedback, and competitive analysis of ShareX alternatives to identify the top 5 user needs for a modern screen capture application in 2025.

---

## Problem Statement

Screen capture tools have evolved from simple screenshot utilities to complex workflow enablers. However, users consistently report friction points across existing solutions — particularly around cross-platform consistency, annotation workflows, Linux/Wayland compatibility, and modern sharing expectations. Understanding these pain points is critical for prioritizing Xerahs feature development.

---

## Research Methodology

Sources analyzed:
- AlternativeTo.net, OpenAlternative.co user reviews and feature comparisons
- ClickUp, Axis Intelligence 2025 screenshot tool comparisons
- Reddit r/software, r/linuxquestions community discussions
- Hacker News screen capture tool threads
- GitHub issues (Flameshot, OBS Studio, xdg-desktop-portal)
- Arch Linux Forums, KDE Bugzilla Wayland/PipeWire reports
- OpenSourceFeed Wayland troubleshooting guides
- ItsFOSS, LinuxVox Linux screenshot tool reviews

---

## Top 5 User Needs

### 1. Instant Annotation with Non-Destructive Editing

**The Need:** Users want to capture, annotate, and refine screenshots in a single fluid workflow — without switching apps or losing the ability to edit annotations later.

**Evidence:**
- Windows 11's Snipping Tool added "Quick Markup" (live on-screen annotation) and "Capture & Notes" specifically because users demanded inline annotation
- Ksnip gained popularity specifically for its ability to modify annotations even after saving
- ShareX users cite annotation tools as the primary reason for recommendation
- Common complaint: "If I miss adding annotations while taking the screenshot, there's no built-in image editor to help"

**Key Features Users Expect:**
- Arrows, shapes, text, highlights, blur/pixelation
- Step numbering for tutorials
- Watermarks and logos
- Undo/redo support
- Ability to re-open and edit saved annotations

---

### 2. First-Class Linux/Wayland Support

**The Need:** Linux users — especially on modern Wayland compositors — need screen capture that "just works" without manual portal configuration, permission dialogs that break workflows, or crashes.

**Evidence:**
- ~60% of Linux users report issues with screen sharing on Wayland (Linux User Group survey)
- Common issues: PipeWire crashes in OBS, black frames, choppy recordings, missing window selection
- Flameshot — beloved on X11 — "refused to work" on dual-monitor Wayland setups
- Users struggle with xdg-desktop-portal configuration across different compositors (GNOME, KDE, wlroots/Hyprland)

**Key Requirements:**
- Native Wayland support without XWayland fallbacks for capture
- Proper multi-DPI/multi-monitor handling
- Reliable PipeWire integration
- Clear permission flows that don't interrupt workflows
- Support for wlroots, GNOME, and KDE portal backends

---

### 3. Cross-Platform Consistency

**The Need:** Users work across Windows, macOS, and Linux. They want the same capture experience, hotkeys, and workflows regardless of OS — not a "best on Windows, afterthought on Linux" situation.

**Evidence:**
- ShareX is consistently rated #1 but is Windows-only — this is its "biggest weakness"
- Flameshot (Linux-first) and CleanShot X (Mac-only) fragment the ecosystem
- Users express frustration when switching OSes requires learning entirely new tools
- Cross-platform tools like Greenshot get recommended specifically for this need

**Key Requirements:**
- Feature parity across Windows, macOS, and Linux
- Consistent keyboard shortcuts
- Unified configuration/import/export
- Same annotation tools and effects on all platforms

---

### 4. Intelligent Capture & Workflow Automation

**The Need:** Power users want capture to trigger automatic post-processing: OCR, resizing, format conversion, uploads, and sharing — without manual steps.

**Evidence:**
- ShareX's automated workflows "reduced bug documentation time by 60%"
- Users want: "capture → auto-upload → get link in clipboard" as a single action
- Flameshot GitHub issue #4623: Users requesting `--post-capture-command` for custom processing
- Common workflow: screenshot → annotate → upload to Imgur/S3 → copy URL → paste in chat

**Key Features:**
- Customizable post-capture actions
- OCR text extraction
- Auto-resize for different platforms
- Direct upload to 80+ destinations (Imgur, S3, Dropbox, etc.)
- Auto-generated shareable links
- Conditional workflows (e.g., different destinations based on window title)

---

### 5. Privacy-First, Local-First Architecture

**The Need:** Users increasingly want control over their data. They want local processing, optional cloud features, and transparency about what leaves their device.

**Evidence:**
- Open source screenshot tools (Flameshot, Ksnip, ShareX) consistently preferred
- Screenity markets itself as "privacy-focused — your recordings stay local"
- Cap offers "local recording mode" and "your storage, your rules — connect your own S3"
- Users specifically seek tools that "process images entirely on your device"
- Concerns about screenshots of sensitive content reaching external servers

**Key Requirements:**
- Local processing by default
- Optional (not mandatory) cloud features
- Self-hostable upload destinations
- Open source for auditability
- Clear data boundaries — no unexpected uploads

---

## Alternatives Considered

| Approach | Pros | Cons |
|----------|------|------|
| Windows-only (ShareX model) | Maximum features, deep OS integration | Excludes Mac/Linux users; fragmentation |
| Web-based tools | Cross-platform, no install | Requires internet; privacy concerns; limited features |
| OS-built-in tools | Zero install, native feel | Feature-poor; inconsistent across OSes |
| Browser extensions | Convenient for web content | Can't capture desktop/system-level content |
| Multiple single-platform tools | Best-in-class per platform | Workflow fragmentation; learning curve |

**Selected Approach:** Xerahs already targets cross-platform with Avalonia — this research validates that direction and identifies specific gaps to close.

---

## Proposed Solution

Based on this research, Xerahs should prioritize:

1. **Annotation System:** Complete the annotation editor with non-destructive layer support (re-editing saved annotations — XIP-0068 already addresses this)

2. **Linux Hardening:** Continue Wayland portal stability work (XIP-0029, XIP-0046, XIP-0047, XIP-0058, XIP-0061) — this is a competitive differentiator

3. **Workflow Engine:** Implement post-capture job chains that match ShareX's automation capabilities (XIP-0005, XIP-0007 touch on this)

4. **Privacy Defaults:** Ensure all processing is local-by-default; cloud uploads require explicit user configuration

5. **Cross-Platform Parity:** Avoid platform-specific feature gaps; test annotation and capture features equally on all three OSes

---

## Review

**Reviewer:** Nadia Valeva (Analyst, KovaForge)  
**Date:** 2026-04-10  
**Status:** ✅ Validated — Research aligns with active development priorities

### Feasibility Assessment

| Need | Feasibility | Complexity | Current State |
|------|-------------|------------|---------------|
| 1. Instant Annotation | **High** | Medium | Core infrastructure exists (`ShareX.ImageEditor` integrated). XIP-0068 (re-editing) is in draft — this closes the gap. |
| 2. Linux/Wayland | **Medium** | High | Active work in XIP-0029, XIP-0046, XIP-0047, XIP-0058, XIP-0061. Portal stability is the hard problem; no magic bullet here. |
| 3. Cross-Platform | **High** | Medium | Avalonia baseline already committed. Risk is feature parity drift — needs automated testing, not just "works on my machine." |
| 4. Workflow Automation | **High** | Low-Medium | XIP-0005 (UI) and XIP-0007 (backend) are complete. Upload pipeline exists. OCR and conditional workflows are the remaining gaps. |
| 5. Privacy-First | **High** | Low | Architectural decision, not a feature. Already aligned — local processing is default, cloud is opt-in. |

### Critical Observations

**1. The Linux Problem is Undersold**

The research cites "~60% of Linux users report Wayland issues" but doesn't quantify the *impact* on XerahS adoption. If we're positioning Linux as a first-class platform (we are), the portal work isn't a nice-to-have — it's a retention risk. The XIPs addressing this are scattered; consider consolidating into a single Linux Hardening epic.

**2. "Intelligent Capture" Needs Definition**

"Workflow automation" spans everything from "copy to clipboard" to "conditional uploads based on window title." The research conflates these. XIP-0005/0007 cover basic after-capture jobs. OCR, conditional logic, and 80+ upload destinations are *not* implemented. Don't let stakeholders assume "ShareX parity" is done.

**3. Non-Destructive Editing: Two Different Problems**

The research bundles "inline annotation during capture" with "re-edit saved annotations." These have different technical profiles:
- Inline: Solved (region capture overlay works today)
- Re-edit: Requires XIP-0068 sidecar implementation — still draft, not scheduled

Clarify this distinction in prioritization discussions.

**4. Missing: Performance Baselines**

Users don't mention capture latency, memory usage, or cold-start time — but these are competitive differentiators. ShareX is fast. If we're slower, the feature checklist doesn't matter. Recommend adding perf benchmarks to the roadmap.

### Alignment with Roadmap

| XIP-0069 Need | Roadmap Item | Status |
|---------------|--------------|--------|
| Annotation | Phase 7: E2E verification matrix for annotation tools | In Progress |
| Linux/Wayland | XIP-0029, XIP-0046-0061 | Active |
| Cross-platform | Phase 7: macOS on-device validation | Blocked (stubs remain) |
| Workflow | XIP-0005, XIP-0007 | ✅ Complete |
| Privacy | Architectural default | ✅ Aligned |

**Verdict:** The research validates the current roadmap. No course correction needed. However, the gap between "core workflow complete" (Phase 7 status) and "ShareX parity" (user expectation) is larger than this XIP implies. Manage expectations accordingly.

### Recommendations

1. **Scope XIP-0068 for next cycle** — Non-destructive editing is the biggest unmet need with clear user demand.
2. **Consolidate Linux portal XIPs** — Five separate XIPs for Linux stability is fragmentation. Bundle and prioritize.
3. **Define "automation" tiers** — Distinguish between shipped (upload), planned (OCR), and future (conditional workflows) to avoid scope creep.
4. **Add competitive benchmarking** — Measure cold-start, capture-to-clipboard latency against ShareX and Flameshot.

### Bottom Line

This is solid research that confirms what the roadmap already targets. The risk isn't misalignment — it's *timing*. Users want Linux stability and re-editable annotations *now*. The roadmap has them *eventually*. Close that gap or someone else will.

---

## References

- [OpenAlternative — ShareX Alternatives](https://openalternative.co/alternatives/sharex)
- [ClickUp — 10 Best ShareX Alternatives](https://clickup.com/blog/sharex-alternative/)
- [Axis Intelligence — 15 Best Screenshot Tools 2025](https://axis-intelligence.com/best-screenshot-tools-2025/)
- [ItsFOSS — Ksnip Experience](https://itsfoss.com/news/ksnip-experience/)
- [OpenSourceFeed — Wayland Common Problems](https://www.opensourcefeed.org/insights/wayland-common-problems-fixes/)
- [GitHub — Flameshot Issue #4623](https://github.com/flameshot-org/flameshot/issues/4623)
- [GitHub — OBS Studio PipeWire Issue #11673](https://github.com/obsproject/obs-studio/issues/11673)
- [KDE Bugzilla — Screen Recording on NVIDIA Wayland](https://bugs.kde.org/show_bug.cgi?id=477130)
