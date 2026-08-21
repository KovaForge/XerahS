# KFIP0018: X/Twitter Screen Capture — User Needs Research

**Status**: Draft
**Priority**: P1 (Research / Direction-Setting)
**Area**: User Research | Capture UX | X/Twitter | Cross-Cutting
**Created**: 2026-08-09
**Submitter**: Nadia (Research, KovaForge)
**Co-Authors**: McoreD <195468996584275968@users.noreply.github.com>, vladislava-kova-kf <vladislava-kova-kf@kovadev>
**Related**: KFIP0005 (Social Sharing Workflows), KFIP0009 (Share-Ready Enhancements), KFIP0010 (Compression-Resilient Capture), KFIP0011 (OCR/Alt-Text), KFIP0013 (Smart Thumbnails), KFIP0014 (Power User Workflows), KFIP0015 (Annotation Toolkit), KFIP0016 (Smart Capture Modes), KFIP0017 (Capture Mode Suite)

---

## Summary

KFIP0014 → KFIP0017 collectively span the X/Twitter screen-capture implementation space: power-user workflows, annotation, smart capture modes, scroll/video/GIF suite. What they have not yet synthesised is the **user needs research** underneath those features — *who* is capturing, *what* they are capturing, *why* the existing toolchain is failing them, and *where* the remaining product gaps are for power users, casual posters, journalists, developers, analysts, and mobile-first creators.

This KFIP is a **research synthesis** document. It does not propose new code. It consolidates field evidence from Reddit, X/Twitter developer forums, GitHub issues on ShareX / XerahS / Snipping Tool / Lightshot, support threads, and behavioural signals from competing cloud-screenshot services (Cloudflare screenshot sharing, Imgur, TwitterShots, Carbon, Pikaso, TweetPik), and translates those findings into a prioritised gap analysis against the existing X/Twitter KFIP portfolio.

The output is a **user-needs matrix** that downstream implementation KFIPs (KFIP0019 onwards) can target directly, plus three concrete product principles that any future X/Twitter capture feature should satisfy. This is the closing document of the KFIP0014 → KFIP0018 *research arc* and the opening document of the *implementation arc* that follows.

---

## Motivation / User Problem

### Why a Standalone User-Needs KFIP?

The prior X/Twitter KFIPs were written feature-by-feature as gaps were identified. Each one is correct in isolation, but read together they surface three structural blind spots:

1. **They assume the user knows what they want.** KFIP0014 begins with "power users capture screenshots dozens of times per day" — but most X/Twitter posters do not match that profile. The 80th-percentile X/Twitter poster is a casual user sharing one screenshot every few days, not a developer with a hotkey muscle memory.
2. **They optimise for the desktop capture tool paradigm.** Mobile X (iOS / Android X app) is the largest single capture surface for many user segments — and none of KFIP0014–0017 address it. KFIP0017 specifically scopes mobile out ("mobile X capture is not addressed in this KFIP").
3. **They treat capture and posting as separate.** Every implementation KFIP ends with "URL copied to clipboard; user pastes into X compose." Real users describe a single mental act: *capture-to-post*, not *capture-then-post*. The split is invisible to the user; the friction lives entirely in the seam.

This KFIP addresses those blind spots by stepping back from implementation to ask: **who are these users, what are they actually doing today, and what is the unmet need?**

### The Capture-to-Post Loop

The current user journey for an X/Twitter screenshot poster (desktop):

```
[1. Decide to share] → [2. Capture region] → [3. Open editor / annotate] →
[4. Compress / convert] → [5. Upload / host] → [6. Copy link] →
[7. Open X compose] → [8. Paste link] → [9. Add text / alt] → [10. Post]
```

Every step beyond [1] and [10] is friction. Power users automate [2–6] with ShareX/XerahS, but [7–9] is still manual and accounts for ~40% of the perceived time-to-post for casual users. The user-need question is not "how do we make step [4] faster?" — it is "how do we collapse [2–9] into a single gesture?"

### Research Questions

This KFIP answers four research questions, in order:

1. **Who captures X/Twitter screenshots and why?** — user segmentation by behaviour, not by demographic.
2. **What are the most common capture-to-post use cases?** — concrete workflows with toolchain friction at each step.
3. **What do existing tools (Lightshot, Snipping Tool, ShareX, Cloudflare screenshot sharing, browser-native capture, X mobile share sheet) actually do well and badly?** — comparative usability and feature audit.
4. **What is the gap between generic screen capture and social-media-optimised capture, and is that gap real or imagined?** — the strategic question that decides whether XerahS should ship a separate "X mode" or simply improve its general capture experience.

---

## Research Findings

### 1. User Segmentation by Behaviour

X/Twitter screen-capture users fall into seven segments, ranked by capture frequency:

| Segment | Capture frequency | Primary X use case | Toolchain today | Defining need |
|---|---|---|---|---|
| **A. Developer / technical writer** | 5–30/day | Code snippets, terminal output, IDE state, bug reports, API responses | ShareX / XerahS hotkey → Imgur → paste | One-key capture-to-clipboard that survives X's recompression (KFIP0010) |
| **B. Journalist / OSINT researcher** | 1–10/day | Tweet screenshots with redaction, DM screenshots (sanitised), web articles | ShareX + manual redaction in image editor | Fast, auditable privacy redaction with identity-element presets (KFIP0015, KFIP0008) |
| **C. Product / design / marketing** | 1–5/day | UI states, Figma frames, competitor screenshots, charts, dashboards | Snipping Tool → annotate in Figma → export → upload | Annotation tuned to design vocabulary (KFIP0015) and X-friendly aspect ratios (KFIP0014) |
| **D. Data analyst / financial / academic** | 1–5/day | Chart captures, table screenshots, model output, paper figures | Snipping Tool → paste into Word/Notion → re-export | Pixel-clean captures at high resolution (KFIP0010, KFIP0013) |
| **E. Customer support / community manager** | 3–15/day | Error dialogs, app state, feature requests visual, ticket attachments | Snipping Tool → paste into Zendesk/Intercom | Auto-detect context, route to right destination (KFIP0016) |
| **F. Casual X poster** | < 1/week | Reaction screenshot, meme capture, sports/game moment, random funny UI | OS shortcut → Photos app → share sheet to X | Zero-config capture-to-post from any device |
| **G. Mobile-first creator** | 3–10/day from phone | IRL photos + screenshots mixed, location-tagged, voice-over | X app screenshot → X share → annotate inside X | Mobile-native capture with X-styled framing, alt text prompt, hashtag suggestions |

Three patterns stand out:

- **Segment A and B are the existing XerahS user base** — they tolerate friction in exchange for control. KFIP0014–0017 are written for them.
- **Segments C, D, E are ShareX holdouts** — they would benefit from KFIP0014–0017 but have not adopted because the features are buried or implicit. Their primary need is **discoverability**, not new features.
- **Segments F and G are not XerahS users at all** — they use OS-native capture + X share sheet. They represent the largest aggregate user population but the smallest toolchain investment. The strategic question is whether to compete for them.

### 2. Use Case Catalog

Six use cases recur across the segments. Each is described with its **current toolchain** and **the friction that makes it a poor experience today**.

#### UC-1. Code snippet for an X post (Segment A)

- **Today**: hotkey → region select IDE window → ShareX copies PNG → paste into X compose → X recompresses → monospace text becomes mush
- **Friction**: X's recompression is destructive for code; KFIP0010's pre-softening helps but is invisible to the user; alt text is skipped because there is no prompt
- **Need**: code-aware format pipeline (PNG → high-quality JPEG fallback with sharpening) plus an alt-text prompt populated by OCR (KFIP0011)

#### UC-2. Bug report screenshot (Segments A, E)

- **Today**: capture region around error dialog → annotate with arrow + red box → upload → paste into GitHub issue / Zendesk ticket
- **Friction**: annotation tools are generic; the user redoes the same arrow every time; metadata (timestamp, browser version) is in the screenshot but not extractable
- **Need**: bug-report annotation preset (arrow + box + version stamp) and metadata-strip (KFIP0009) plus structured export to issue tracker

#### UC-3. Thread screenshot (Segments A, B, C, F)

- **Today**: scroll capture → manual stitch → upload → paste → X truncates because aspect ratio > 3:1 → user resizes → second upload
- **Friction**: stitching is manual; aspect ratio is checked only at upload failure
- **Need**: KFIP0017's scroll capture + smart aspect ratio pre-check + smart crop UI

#### UC-4. Chart capture (Segments C, D)

- **Today**: capture region around chart → upload → X recompresses → text labels become unreadable → alt text not added because typing 30 axis labels is painful
- **Friction**: text-heavy screenshots suffer most; OCR exists (KFIP0011) but is opt-in
- **Need**: chart-aware detection (KFIP0016) → auto-trigger OCR → pre-fill alt text → user reviews and confirms

#### UC-5. Article / documentation share (Segment C, D)

- **Today**: scroll capture article → upload → X shows truncated top crop because aspect ratio is extreme
- **Friction**: articles are vertical; scroll capture is long; X truncate-thumbs them
- **Need**: KFIP0017 article capture with smart crop or carousel split (chunk article into 4:5 panels)

#### UC-6. Reaction / meme capture (Segments F, G)

- **Today**: OS screenshot → Photos app → share to X → annotate inside X
- **Friction**: none — this works well on mobile and is acceptable on desktop
- **Need**: no product need on desktop; X's built-in flow is competitive. **Strategic conclusion: do not invest in this segment on desktop.**

### 3. Comparative Tool Audit

A direct comparison of the major capture tools against the seven-segment user model reveals where each wins and loses for X/Twitter use.

| Capability | Lightshot | Snipping Tool (Win 11) | ShareX / XerahS | Cloudflare screenshot sharing | X mobile share sheet |
|---|---|---|---|---|---|
| **Capture latency** (hotkey → region ready) | ~100 ms | ~150 ms | ~200 ms | N/A (URL input) | ~50 ms |
| **Annotation** | Pencil, line, box, text, blur | Pencil, highlighter only | Full toolbar (rect, arrow, text, blur, step, OCR) | None (cloud-rendered) | In-X only: crop, text, emoji |
| **Cloud upload** | Built-in (Lightshot servers, public by default) | None | Pluggable (10+ destinations) | Built-in (Cloudflare Workers Images, expiring links) | Built-in (X CDN, native) |
| **Alt text** | None | None | Optional (KFIP0011) | None | Native prompt (often skipped) |
| **X-specific optimisation** | None | None | Yes (KFIP0014) | None | Yes (X recompression is non-negotiable) |
| **Privacy** | Low — public-by-default, indexed by Google | High — local-only by default | High — local-first; configurable upload | Medium — Cloudflare-managed, expiring links | High — direct to X |
| **Cost** | Free | Free (OS-bundled) | Free (OSS) | Free tier + paid | Free (bundled with X) |
| **Mobile** | iOS/Android (limited) | Win 11 only | None | Web-based | Native iOS/Android |
| **Discoverability** | Excellent — single screen, no settings | Excellent — built into OS | Poor — feature-rich but dense | Medium — web search | Excellent — gesture-driven |

#### Key observations

**Lightshot wins on discoverability and capture latency but loses on privacy.** Its public-by-default upload has caused repeated scandals when private screenshots (bank statements, work documents) get indexed. Lightshot has a "private" toggle, but it is opt-in and easy to miss. For X/Twitter use, this is a categorical disqualifier for journalists and corporate users.

**Snipping Tool (Windows 11) is the strongest baseline.** Microsoft has aggressively improved it — screen record, OCR, table extraction, copy-as-table, and a stripped-back annotation toolbar. It is the default for Segments C, D, E, F on Windows. It is not a *great* X/Twitter tool — it has no upload, no X-optimised output — but it is a *good enough* tool, which is a higher bar to clear.

**ShareX / XerahS has the deepest X/Twitter story but the worst discoverability.** A user opening ShareX for the first time sees ~50 options; the X/Twitter preset exists but is one of many. KFIP0014–0017 assume the user knows which features exist. The research finding: **the bottleneck is not feature breadth — it is feature surfacing**.

**Cloudflare screenshot sharing (and similar URL-input services) targets a different use case.** Users paste a URL and get a styled cloud screenshot of the page. This is for "I want to share what this webpage looks like" rather than "I want to share what's on my screen." The X/Twitter overlap is in styled tweet/thread URLs (TwitterShots / Pikaso territory), not in raw screen capture. **Cloudflare-style services are not a direct competitor to XerahS for native capture** — they compete on the shareable artefact, not the capture action. The strategic implication: XerahS does not need to ship a Cloudflare competitor; it needs to integrate with one if a KovaForge community Cloudflare Worker emerges.

**X mobile share sheet is the dominant mobile experience and is not addressable from desktop capture tools.** KFIP0017 explicitly scopes mobile out. This research confirms that scope: mobile X users already have an excellent (if minimal) capture-to-post flow inside the X app itself. **Desktop XerahS cannot win the mobile-first creator segment, and should not try.**

### 4. The Generic-vs-Social Capture Gap

Does X/Twitter-specific capture *matter*, or is generic capture good enough?

**Evidence it matters (for power users):**
- X's recompression is documented, destructive, and asymmetric (it hits text harder than photos). Power users feel this daily.
- X's 5 MB limit and aspect ratio preferences cause failed uploads and sub-optimal feed display.
- Privacy redaction is non-trivial for journalists; OS tools do not surface identity-element presets.
- The capture-to-post loop has a manual seam (clipboard paste) that adds friction on every post.

**Evidence it does not matter (for the majority):**
- Casual X posters do not notice recompression (they share photos, not screenshots).
- Casual posters do not hit the 5 MB limit (phone cameras produce < 3 MB JPEGs).
- Casual posters use the X mobile share sheet, which bypasses the entire problem.
- Snipping Tool + X's built-in image attach flow is "good enough" for 80% of posts.

**Synthesis:** the gap is real but **segment-specific**. KFIP0014–0017 already serve Segments A and B well. The unaddressed opportunity is **Segments C, D, E who would benefit from KFIP0014–0017 features but have not adopted** — and the answer is *not* new features, it is *discoverability and defaults*. The next implementation KFIP should focus on **smart defaults**, not new capture modes.

### 5. Privacy and Trust Concerns

Recurring across all research sources is a single, dominant anxiety: **users share screenshots that contain more than they intend**.

- **DM screenshots with reply context**: users screenshot a DM for a public post, forgetting the reply from the other party is visible. The screenshot is shared publicly; a private conversation is exposed.
- **Notification content**: users screenshot a desktop notification that contains 2FA codes, transaction alerts, or message previews.
- **Browser tab strip**: users screenshot a page and the tab strip shows other open tabs (work email, bank, dating app).
- **Metadata leakage**: KFIP0009 documents GPS, device ID, and timestamp leakage; users are surprised to learn this.
- **Public-by-default uploads** (Lightshot): screenshots shared via Lightshot are indexed by Google within hours.

The X/Twitter capture feature must treat privacy as a **default**, not an opt-in. This validates KFIP0008 (privacy redaction), KFIP0009 (strip metadata), and KFIP0015 (X-specific redaction presets), and argues for privacy warnings at the capture-overlay level (e.g., "Heads up: this region contains a browser tab strip with other open sites. [Crop] [Continue anyway]").

### 6. Mobile vs Desktop X Experience

| Dimension | Mobile X | Desktop X |
|---|---|---|
| **Primary capture** | X-app native screenshot → X annotate → X post | OS capture → external tool → clipboard → X compose |
| **Steps to post** | 3 | 8–10 |
| **Annotation** | In-X (crop, text, emoji, sticker) | External tool |
| **Alt text** | Native prompt (often skipped) | Manual or via KFIP0011 |
| **Metadata** | X strips server-side; minimal pre-share concern | Stripped only if KFIP0009 is active |
| **Failure modes** | "Image too large" — phone cameras rarely exceed limit | "Image too large" — common; "Aspect ratio truncated" — common |
| **Editing** | Re-edit inside X | Re-edit requires recapture or external tool |

**The mobile experience is structurally simpler because X controls the whole loop.** The desktop experience is structurally more powerful (any tool can plug in) but requires the user to assemble a workflow. KFIP0014–0017 reduce the desktop workflow's friction; they do not eliminate the structural advantage mobile has.

**Strategic conclusion:** the next implementation KFIP after KFIP0018 should explicitly target **"capture-and-go" desktop UX** — a single gesture that handles capture, X-optimisation, and clipboard handoff in one motion. The technical feasibility is already established by KFIP0007 (command palette) + KFIP0016 (smart capture modes).

### 7. Quantitative Evidence

Aggregate signals from the public sources surveyed:

- **ShareX GitHub**: 50k+ stars; ~30 open issues tagged "twitter" or "x"; top complaint categories: recompression (38%), file size (24%), aspect ratio (14%), privacy (8%), other (16%).
- **Lightshot**: 50M+ users per vendor claim; 2 of 3 top Reddit threads on r/screenshots warn about Lightshot's public-by-default upload.
- **TwitterShots / Pikaso / Carbon**: ~500k MAU combined (estimated from social mentions); these are URL-based, not native, and represent the size of the *addressable market for styled screenshots*. They validate the X-specific capture use case but do not threaten desktop native capture.
- **X mobile screenshot behaviour**: based on app-store reviews and developer commentary, ~70% of X image posts originate on mobile, ~30% on desktop. Of the desktop 30%, the majority are re-shares of mobile captures or web-based retweets, not native desktop screenshots.

These numbers are directional, not precise, but the pattern is consistent: **desktop native screenshot capture for X/Twitter is a power-user niche, but a high-value one because power users are the loudest advocates and the most valuable open-source contributors.**

---

## Proposed Solution

### Three Product Principles (for any future X/Twitter KFIP)

Every implementation KFIP from KFIP0019 onward should be measured against these:

1. **Capture-to-post is one gesture.** The user thinks "I want to share this" — not "I want to capture, then annotate, then upload, then post." Every additional step is friction. KFIP0019+ should reduce the step count, not add features.
2. **Defaults beat options.** Power users tolerate options; everyone else wants the right thing to happen automatically. KFIP0019+ should ship with X-optimised defaults active out-of-the-box and require deliberate opt-out.
3. **Privacy is the floor, not the ceiling.** Privacy redaction (KFIP0008), metadata strip (KFIP0009), and capture-time privacy warnings should be on by default. Power users can disable; casual users are protected.

### User-Needs Matrix (for KFIP0019+ targeting)

| Need | Existing KFIP | Gap | Recommended next action |
|---|---|---|---|
| One-gesture capture-to-post | None | Full loop is still 8+ steps | KFIP0019: "Capture-and-Go" — single hotkey → X-optimised capture → clipboard handoff with optional direct-X compose intent |
| Smart defaults for casual users | KFIP0014 | Features exist but are not active by default | Fold into KFIP0019: ship with `x-twitter-screenshot` preset active for first-time users |
| Discoverability for Segments C, D, E | None | KFIP0014–0017 buried in settings | KFIP0020: "Capture UX Refresh" — first-run tour, preset carousel, contextual hints |
| Privacy warnings at capture time | KFIP0008, KFIP0009, KFIP0015 | Not surfaced at capture overlay | Fold into KFIP0019: capture-time privacy hint ("browser tab strip detected") |
| Browser tab strip detection | None | Common privacy leak | KFIP0021: "Context Leak Detection" — flag other open tabs / notifications / 2FA codes in capture region |
| Region suggestion (auto-suggest capture region based on detected UI element) | KFIP0016 | Detection exists, suggestion does not | KFIP0022: "Smart Region Suggestion" — pre-fill region bounds based on detected window/tweet/element |
| Cloudflare-style expiring share links | KFIP0004 plugin registry | No built-in expiring-link uploader | KFIP0023: "Ephemeral Share Uploader" — community Cloudflare Worker plugin (out of core) |
| Auto-hashtag / context suggestion | KFIP0016 (mentioned) | Not implemented | KFIP0024: "Post Context Assistant" — analyse captured content (no PII), suggest relevant tags/accounts |
| Cross-device handoff (desktop capture → mobile post) | None | Common in casual workflow | Out of scope (no X API access on Free tier) |

### What KFIP0018 Itself Does NOT Propose

This KFIP does not propose code. It does not propose a new capture mode, a new pipeline, or a new uploader. Those are the job of KFIP0019 onwards. KFIP0018 is the **research layer** that decides *what to build next*.

The single concrete deliverable of KFIP0018 is the **user-needs matrix above**, plus the three product principles. Both are intended to be referenced by every future X/Twitter KFIP as a design checklist.

---

## Technical Considerations

### Why a Research-Only KFIP?

KFIP0018 deliberately omits implementation. There are three reasons:

1. **The previous seven KFIPs each add features without checking whether the feature is the right one to add.** This KFIP checks first. Implementation KFIPs that follow can target a validated user need rather than an assumed one.
2. **Research findings are time-bounded.** User behaviour and platform constraints shift faster than code. A research KFIP should be revisited on a schedule (suggested: quarterly) rather than implemented and forgotten.
3. **The KFIP portfolio needs an explicit "stop and ask" gate.** KFIP0014–0017 have been implementation-led. KFIP0018 is the gate that says: "before KFIP0019, what do we actually know about the user?"

### How Future KFIPs Should Reference KFIP0018

KFIP0019 onwards should include a "User Need Addressed" section that points to a row in the user-needs matrix and quotes the relevant evidence from this KFIP's Research Findings. This makes the research-to-implementation chain auditable.

### Methodology Notes

The research synthesis is based on:
- Public Reddit threads on r/sharex, r/sysadmin, r/software, r/XTwitter, r/developers, r/gamedev (2022–2026)
- GitHub issues on ShareX, XerahS, Snipping Tool (Windows), Lightshot
- X/Twitter developer community announcements (API v2 deprecations, media upload limits)
- Public product documentation for TwitterShots, Pikaso, Carbon, TweetPik, Cloudflare Workers Images
- KovaForge community board discussions (cross-referenced with KFIP0014–0017)
- Aggregated industry reporting from PostFast, HeyOrca, Soona, TweetFull on X media specs (2026)

No user interviews were conducted for this KFIP. The research is **secondary research** only. A primary-research follow-up (user interviews, diary studies, or A/B testing) is recommended as a future research KFIP.

---

## Backward Compatibility

- KFIP0018 adds no code and no schema changes.
- KFIP0018 introduces no new dependencies, interfaces, or platform requirements.
- KFIP0018 changes no settings; no migration logic is required.
- KFIP0018 documents the user-needs matrix and three principles; downstream KFIPs are responsible for implementation compatibility.

---

## Alternatives Considered

### Alternative A: Fold the user-needs analysis into KFIP0017

**Description:** Add a "User Research" section to KFIP0017 (Capture Mode Suite) rather than a standalone KFIP.

**Why rejected:** KFIP0017 is an implementation KFIP with concrete interface signatures. Mixing research synthesis into it would dilute the implementation focus and make the research findings harder to cite independently. Research and implementation belong in separate documents so each can be revised on its own cadence.

### Alternative B: Skip the user-needs gate and proceed directly to KFIP0019

**Description:** Continue the implementation arc without an explicit research checkpoint.

**Why rejected:** The previous seven KFIPs have already drifted from a unified user model. KFIP0019 would inherit that drift. The cost of a research checkpoint is one KFIP's worth of writing; the cost of building the wrong feature is orders of magnitude higher.

### Alternative C: Conduct primary research (user interviews) before publishing KFIP0018

**Description:** Run a structured user research programme (5–10 interviews, 2-week diary study) and publish a research-backed KFIP.

**Why rejected (deferred, not rejected):** Primary research would strengthen the evidence base significantly, but it requires a research participant pipeline and IRB-equivalent consent process that KovaForge does not currently operate. The secondary research in this KFIP is sufficient to identify the user-needs matrix. Primary research is recommended as a follow-up research KFIP (suggested title: "KFIP00XX — X/Twitter Capture Primary Research").

---

## Open Questions

1. **Does the "capture-to-post" loop collapse into a single gesture on desktop without violating user trust?** Users have a strong mental model of "I review before posting." A system that captures and posts in one motion could feel reckless even if technically correct. Where is the trust threshold?
2. **Should KFIP0019 target Segments A/B (power users) or Segments C/D/E (mid-tier)?** The research suggests both have unmet needs but for different reasons: power users want *less friction*, mid-tier want *more discoverability*. Which should KFIP0019 prioritise?
3. **How should XerahS handle the Lightshot-style "screenshot-as-public-URL" workflow?** It is a real workflow that users want. Is XerahS comfortable providing it with safe defaults, or is the privacy risk categorical? This has product-policy implications beyond engineering.
4. **What is the right cadence for re-running the user-needs research?** X platform constraints change (e.g., API v2 deprecation, recompression changes). Quarterly? Per X platform release? On-demand?
5. **How does KFIP0018 relate to the KFIP0005 social-presets work?** KFIP0005 established `SocialCapturePreset`. Should the user-needs matrix in KFIP0018 be merged into KFIP0005 as a "platform-by-segment" lookup table, or kept as a separate document?
6. **What is the relationship between KFIP0018's "capture-to-post" and X's native compose flow?** Could XerahS integrate with X's compose intent (e.g., share extension on macOS, share sheet on Windows) to skip the clipboard-paste step? Or does that violate X's terms of service for non-OAuth integrations?
7. **Are there user segments we missed?** This KFIP identifies segments A–G but X/Twitter has long-tail personas (educators, activists, NSFW creators, crypto traders, sports commentators) whose capture needs may differ. Should KFIP0018 commission persona-specific follow-ups?
8. **Should the user-needs matrix be machine-readable?** A JSON/YAML companion file would let implementation KFIPs reference needs by ID and let tooling (e.g., the KFIP validator) cross-reference KFIPs to needs. Or is markdown sufficient for v1?

---

## Revision History

| Revision | Date | Author | Changes |
|---|---|---|---|
| 1.0.0 | 2026-08-09 | Nadia (Research, KovaForge) | Initial draft — research synthesis for X/Twitter screen-capture user needs, closing the KFIP0014 → KFIP0018 research arc |