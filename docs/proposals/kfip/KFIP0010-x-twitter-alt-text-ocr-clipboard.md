# KFIP0010: X/Twitter Accessibility Drafts — OCR-to-Clipboard for Image Descriptions

**Status**: Proposed
**Priority**: P2
**Area**: AfterCapture | OCR | Clipboard | Accessibility | Social Sharing
**Created**: 2026-06-07
**Related**: KFIP0001 (AfterCapture OCR), KFIP0003 (X/Twitter Context Detection), KFIP0005 (Social Sharing Workflows), KFIP0009 (Share-Ready Enhancements)
**Owner**: KovaForge
**Co-Authors**: Nadia (research), Vladislava (review)

---

## Summary

X supports image descriptions on x.com, iOS, and Android, with up to 1000 characters per image. That is good for accessibility, but it still leaves screenshot-heavy users doing boring manual prep after capture: open the screenshot, run OCR somewhere else, copy the text, then trim it into an image description. XerahS already has OCR and clipboard plumbing, but they are disconnected in the standard capture flow. This KFIP adds a small but high-leverage bridge: a first-class AfterCapture task that copies recognized OCR text to the clipboard immediately after a successful capture OCR pass.

This is not "AI alt text." It is the boring, dependable first slice that gets users from screenshot to editable description draft faster.

---

## Problem Statement

Users capturing posts, replies, support screenshots, error dialogs, and long text snippets for X/Twitter accessibility currently hit a dumb gap:

1. Capture image
2. Run OCR somewhere
3. Copy OCR text manually
4. Paste into X's image description field
5. Clean up obvious OCR noise

The underlying primitives already exist in XerahS:

- `AfterCaptureTasks.DoOCR`
- OCR result persistence on `TaskMetadata.OcrText`
- clipboard services across desktop platforms

What does not exist is the obvious automation between them.

### Evidence

- X Help currently documents image descriptions on X and notes that each image can include a description, up to 1000 characters, on x.com and mobile apps.
- X Help also documents photo posting as a core posting workflow, which means screenshot posters are already inside an image-first composition path.
- XerahS already exposes OCR in the capture pipeline and assistant flows, so the missing step is orchestration, not capability.

### Why This Matters for X/Twitter Users

- Journalists and researchers screenshot text-heavy posts and need a quick accessibility draft.
- Social/support users screenshot UI states and want the visible text ready to paste into the description field.
- Accessibility-conscious posters should not need a second tool just to copy recognized text.

---

## Goals

- Add a dedicated AfterCapture flag that copies OCR text to the clipboard after a successful OCR run
- Keep the behavior deterministic and local, with no AI summarization and no network dependency
- Expose the option in task settings and the after-capture dialog
- Make the feature useful for X/Twitter alt-text prep, while keeping the implementation generic

## Non-Goals

- No automatic posting to X/Twitter
- No generative summarization, rewriting, or compression of OCR text
- No image-description quality scoring
- No automatic enablement based on X/Twitter context in v1
- No new OCR engine work

---

## Proposed Solution

### 1. New AfterCapture Task Flag

Add a new `AfterCaptureTasks.CopyOcrTextToClipboard` flag.

Behavior:

- Runs only when `AfterCaptureTasks.DoOCR` is also enabled
- Runs only when OCR succeeds and the normalized result text is non-empty
- Copies the recognized text to the clipboard
- Leaves the capture pipeline running normally after the copy

If OCR fails, returns whitespace, or clipboard service is unavailable, the task is skipped quietly with debug logging.

### 2. UI Exposure

Expose the new flag in:

- Task settings panel
- After-capture dialog

Suggested label:

- `Copy OCR text to clipboard`

This keeps the option understandable without pretending it creates polished alt text automatically.

### 3. Ordering

Execution order inside the capture pipeline:

1. OCR runs
2. OCR result is normalized and stored
3. Clipboard copy runs if enabled
4. Remaining tasks continue

This preserves the current OCR behavior and makes clipboard copy a pure follow-up action.

---

## Technical Design

### Enum

```csharp
[Flags]
public enum AfterCaptureTasks
{
    // ...existing flags...
    DeleteFile = 1 << 20,
    CopyOcrTextToClipboard = 1 << 21
}
```

### Processor Behavior

Extend `CaptureJobProcessor.PerformOCRAsync` or its immediate call site:

```csharp
if (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.DoOCR))
{
    await PerformOCRAsync(info);

    if (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.CopyOcrTextToClipboard))
    {
        TryCopyOcrTextToClipboard(info.Metadata?.OcrText);
    }
}
```

Rules:

- Trim and validate OCR text before copying
- Require `PlatformServices.Clipboard` to be non-null
- Never throw if clipboard copy fails

### Tests

Add coverage for:

- OCR text is copied when OCR succeeds and the flag is enabled
- OCR text is not copied when OCR output is whitespace
- OCR text is not copied when the flag is enabled without `DoOCR`
- Clipboard-unavailable path does not throw or corrupt OCR persistence
- New enum value remains a distinct bit flag

---

## UX Notes

- The feature is intentionally literal. Users get recognized text, not a "smart description."
- For X/Twitter image descriptions, literal OCR is still useful because the user can edit it in place rather than starting from nothing.
- The checkbox should live near `OCR text recognition` because it depends on that task.

---

## Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Users assume the clipboard text is finished alt text | Misleading accessibility expectations | Label it as OCR text, not alt text generation |
| Clipboard overwrite surprises users | Minor annoyance | Keep opt-in in v1 |
| OCR noise produces ugly drafts | Users paste junk | User remains in control; this is a draft aid only |
| Feature is enabled without OCR | Confusing no-op | UI keeps the option adjacent to OCR; processor safely skips |

---

## Acceptance Criteria

- [ ] New `AfterCaptureTasks.CopyOcrTextToClipboard` flag exists as a unique bit
- [ ] When `DoOCR` and `CopyOcrTextToClipboard` are both enabled, successful OCR copies recognized text to the clipboard
- [ ] Whitespace-only OCR results do not overwrite clipboard text
- [ ] Clipboard-unavailable environments do not throw
- [ ] Task settings UI exposes the option
- [ ] After-capture dialog exposes the option
- [ ] Build and tests pass with the new flag enabled in regression coverage

---

## Rollout

### Phase 1

- Add enum flag
- Add processor support
- Add regression tests
- Add task-settings checkbox
- Add after-capture dialog checkbox

### Phase 2

- Consider context-aware enablement for X/Twitter presets from KFIP0005/KFIP0009
- Consider surfacing a post-OCR toast like `OCR text copied`

---

## Success Metric

The feature is successful if users who already enable OCR can turn on one extra checkbox and immediately paste recognized text into X's image-description field without using another tool.

---

## References

- X Help: image descriptions / accessibility for photos
- X Help: posting photos and image constraints on X
- Existing XerahS OCR capture pipeline and clipboard services
