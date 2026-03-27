# XIP0057 Send-to Post-v1 Defaults and Folder Policies
**Status**: PROPOSED
**Priority**: Medium
**Audit date**: 2026-03-27
**Related**: XIP0055

---

## Problem Statement
XIP0055 introduces the critical v1 correction: Send-to no longer implies upload, and users must choose an explicit action before XerahS starts work.

That fixes the main UX failure, but it intentionally leaves three second-order problems unsolved:

1. Users who always take the same action still pay the prompt cost on every Send-to invocation.
2. Folder-containing Send-to selections still need a more explicit, user-visible policy for how non-index actions treat directories.
3. Multi-item image actions can become mechanically correct but operationally noisy when they open or pin many items at once.

These are good v1 tradeoffs because they keep the first release scoped and safe. They should still be addressed as a follow-up so Send-to grows into a stable, configurable intake surface instead of remaining a one-off prompt.

---

## Goals
1. Add an explicit, reversible way to remember Send-to choices without hiding intent.
2. Make folder handling transparent for `Upload now` and `Open in Upload Content`.
3. Improve batch ergonomics for image-editor and pin-to-screen Send-to actions.
4. Preserve the distinction between `Send to XerahS` and `Upload with XerahS`.

## Non-Goals
- Replacing the Send-to prompt with silent behavior by default.
- Reworking the entire upload pipeline or workflow catalog.
- Changing watch-folder, clipboard, or shell context-menu semantics outside Send-to.

---

## Proposed Design
### 1) Remembered Send-to choice with explicit scope
Add an optional `Remember this choice` control to the Send-to prompt introduced by XIP0055.

Remembered choices should be stored with an explicit scope so the behavior remains understandable:

- `All files`
- `All folders`
- `Mixed files and folders`
- `Image-only files`

Rules:
- No remembered choice is applied unless the current selection matches the stored scope.
- The UI must always surface where the remembered rule came from and provide a one-click reset path.
- `Upload with XerahS` remains unaffected; remembered Send-to choices apply only to `--send-to` invocations.

Settings UX:
- Add a small Send-to section under integration/task settings that lists current remembered rules.
- Provide `Clear remembered Send-to choices`.

### 2) Explicit folder policy for non-index actions
V1 can safely expand folders conservatively or skip them, but post-v1 should make the rule explicit and configurable.

Add a Send-to folder policy for actions that are not `Index folder`:

- `Do not expand folders`
- `Include top-level files`
- `Include files recursively`

The prompt should show a short scope summary when folders are present, for example:

- `Upload now will include 12 top-level files from 3 folders.`
- `Open in Upload Content will ignore folders with the current policy.`

This removes ambiguity for mixed selections and makes logs match what users actually approved.

### 3) Batch execution policy for image actions
When Send-to receives many image files, `Open in Image Editor` and `Pin to Screen` need pacing and visibility.

Add a batch execution policy:

- `Open all immediately`
- `Open sequentially`
- `Confirm before opening more than N items`

For pin-to-screen batches:
- show a completion toast or summary with the number of successfully pinned images
- surface skipped or failed files once, instead of one failure per file when possible

### 4) Prompt summary and diagnostics hardening
Extend Send-to diagnostics so one batch decision can always be reconstructed from the log:

- remembered rule used or not used
- folder policy used
- number of file items resolved from folders
- number of editor/pin actions started, skipped, or failed

---

## Acceptance Criteria
1. Users can optionally remember a Send-to choice and clear it later.
2. Remembered Send-to choices are scoped and never affect `Upload with XerahS`.
3. Folder-containing Send-to selections show an explicit folder policy for non-index actions.
4. Batch image actions offer a predictable execution policy and avoid uncontrolled window bursts.
5. Logs clearly show the rule and folder policy used for each Send-to batch.

---

## Suggested Tests
1. Remembered choice applies only when the incoming selection matches the stored scope.
2. Clearing remembered Send-to choices removes all automatic routing.
3. Folder policy `Do not expand folders` results in zero folder-derived upload-content or upload items.
4. Folder policy `Include top-level files` excludes nested files.
5. Folder policy `Include files recursively` includes nested files and logs the resolved count.
6. Batch execution policy respects the configured threshold before opening many editor windows.

---

## Key Files Expected to Change / Be Added
1. `src/desktop/app/XerahS.UI/Views/SendToPromptWindow.axaml`
2. `src/desktop/app/XerahS.UI/ViewModels/SendToPromptViewModel.cs`
3. `src/desktop/app/XerahS.UI/Services/AvaloniaUIService.cs`
4. `src/desktop/app/XerahS.App/Program.cs`
5. `src/desktop/app/XerahS.App/SendToIntegrationCoordinator.cs`
6. `src/desktop/core/XerahS.Core/SendTo/*`
7. `src/platform/XerahS.Platform.Abstractions/IUIService.cs`
8. `src/desktop/app/XerahS.UI/Views/ApplicationSettingsView.axaml`

---

## Verification Commands
```powershell
dotnet build src/desktop/app/XerahS.sln -m:1
dotnet test tests/XerahS.Tests/XerahS.Tests.csproj -m:1
```
