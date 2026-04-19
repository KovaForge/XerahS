# KFIP0003: X/Twitter Context Detection Hardening

**Status**: Draft
**Priority**: P1
**Area**: Region Capture | Social Media | Detection | UX Hardening
**Created**: 2026-04-19
**Related**: KFIP0002 (Smart Region Capture Profiles & Social Media Screenshot Automation), XIP0070 (User Research — Top Screen Capture Needs), XIP0071 (XerahS Spotlight Assistant)
**Co-Authors**: Milena (research), Nadia (analysis), Sofia (design), TBD (implementation)

---

## Summary

Power users capturing posts on X/Twitter want the app to recognize where they are without forcing brittle DOM scraping or noisy auto-selection. This KFIP narrows the problem: harden URL and window-title detection for compose, single-post, and timeline contexts, expose safe confidence-ranked hints, and keep the UI conservative so false positives do not poison trust.

This is a follow-up slice from KFIP0002. Instead of pretending we can solve generic smart capture in one shot, we ship a concrete, testable foundation for X/Twitter-aware capture suggestions.

---

## Problem Statement

Current tweet-aware capture logic is too thin for reliable product use:

- it only recognizes a subset of x.com URLs
- it does not treat `twitter.com` legacy links as first-class input
- it lacks a region hint for viewing a tweet, not just composing one
- it does not offer a structured way to rank safe suggestions by confidence
- it leaves UX policy implicit, which invites noisy overlays and bad first impressions

Users doing research, journalism, support, and social media work need something dependable. The first release should be boringly correct, not magically ambitious.

---

## Proposed Solution

### Scope

Ship an X/Twitter context detector that:

1. Supports both `x.com` and `twitter.com`
2. Detects three conservative contexts:
   - compose
   - single tweet view
   - home timeline
3. Returns structured region hints for compose and tweet view
4. Produces confidence-ranked suggestions for downstream UI selection
5. Keeps timeline confidence below direct-target contexts so the UI can avoid noisy auto-picks

### Non-Goals

- no DOM parsing
- no browser extension
- no auto-scroll / thread stitching
- no ML or OCR
- no automatic capture without user confirmation

### Proposed API Shape

```csharp
public sealed class TweetRegionHint
{
    public string ProfileId { get; init; }
    public string Name { get; init; }
    public float Confidence { get; init; }
    public int RelativeTop { get; init; }
    public int RelativeLeft { get; init; }
    public int RelativeWidth { get; init; }
    public int RelativeHeight { get; init; }
}

public interface ITweetCaptureDetector
{
    bool IsTweetComposeWindow(string? url, string? windowTitle);
    bool IsTweetViewWindow(string? url, string? windowTitle);
    bool IsTimelineWindow(string? url, string? windowTitle);
    TweetRegionHint? DetectComposeRegion(string? url, string? windowTitle);
    TweetRegionHint? DetectTweetViewRegion(string? url, string? windowTitle);
    IReadOnlyList<TweetRegionHint> GetSuggestedRegions(string? url, string? windowTitle);
}
```

### Acceptance Criteria

- `x.com` and `twitter.com` URLs are both supported
- compose URLs return a high-confidence compose hint
- single-post URLs return a high-confidence tweet-view hint
- home timeline URLs return only lower-confidence contextual suggestion(s)
- unsupported URLs return no suggestions
- confidence ordering is deterministic and covered by tests
- build stays green and tests pass or remain clean under existing project constraints

---

## Critical Review

*Review by Nadia*

This scope is finally sane. The main risk is over-reading weak signals from window titles when URLs are absent. Mitigation: URL match is primary, title-only heuristics should stay conservative and never outrank direct URL evidence.

Also, do not let timeline detection become a UI spam machine. A home feed is context, not intent. If the user is on the timeline, the app may offer a soft suggestion, but it should not behave like it found a precise target.

---

## Design Feedback

*Feedback by Sofia*

The overlay should reward confidence, not enthusiasm.

- Compose and single-tweet views may show a primary suggestion chip.
- Timeline context should show a secondary suggestion only, visually quieter than a direct match.
- When no reliable match exists, show nothing and keep manual capture feeling fast.
- Label suggestions with plain language like `Tweet composer` or `Tweet content`, not internal profile IDs.
- Keyboard selection should follow confidence order, but the first visible choice must always be the safest one.
- Accessibility matters: any surfaced suggestion needs readable labels and must not depend on color alone.

---

## Implementation Plan

### Stage 1
- expand URL support to `twitter.com`
- add tweet-view region hints
- add deterministic suggestion ranking

### Stage 2
- tighten service defaults and profile metadata to match detector output
- keep curated defaults immutable

### Stage 3
- add focused tests for supported URLs, unsupported URLs, hint shapes, and ranking

---

## Success Metric

Users can open a compose window or a single tweet on X/Twitter and consistently get a sane suggestion without false confidence or noisy overlay behavior.
