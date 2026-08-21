# XIP0057 Send-to Post-v1 Defaults and Folder Policies

**Status**: Complete
**Version**: v0.22.257

**Priority**: Medium
**Audit date**: 2026-03-27
**Related**: XIP0055

---

## Implementation Update (2026-05-12)

XIP0057 has shipped as a production Send-to follow-up on top of XIP0055.

### Shipped behavior
- The Send-to prompt now supports `Remember this choice` with scoped rules for all files, all folders, mixed selections, and image-only files.
- Remembered choices are persisted in application settings and are only consumed by `--send-to` handling; `Upload with XerahS` remains separate.
- Application settings now show the remembered Send-to rules and provide `Clear remembered Send-to choices`.
- Folder-containing Send-to batches expose an explicit folder policy: do not expand folders, include top-level files, or include files recursively.
- `Upload now` and `Open in Upload Content` both use the same folder expansion resolver, so they no longer diverge on folder input.
- Send-to file uploads continue through the normal task name-pattern path, and native file uploader dispatch now passes the generated `TaskInfo.FileName` into stream upload instead of falling back to the source file name.
- Upload Content shows the resolved upload-name preview for queued files.
- Image editor and pin-to-screen Send-to batches now carry an explicit batch execution policy; remembered image batch rules that exceed the confirmation threshold are stopped with diagnostics/toast instead of opening an uncontrolled burst.
- Diagnostics now log decision source, remembered scope, folder policy, batch policy, folder-derived file counts, failed folder enumeration count, and generated-name policy.

### Key files
- `src/platform/XerahS.Platform.Abstractions/SendToModels.cs`
- `src/desktop/core/XerahS.Core/SendTo/SendToPolicyResolver.cs`
- `src/desktop/core/XerahS.Core/Models/ApplicationConfig.cs`
- `src/desktop/app/XerahS.App/SendToIntegrationCoordinator.cs`
- `src/desktop/app/XerahS.UI/ViewModels/SendToPromptViewModel.cs`
- `src/desktop/app/XerahS.UI/Views/SendToPromptWindow.axaml`
- `src/desktop/app/XerahS.UI/Services/AvaloniaUIService.cs`
- `src/desktop/app/XerahS.UI/Services/UploadContentToolService.cs`
- `src/desktop/app/XerahS.UI/ViewModels/UploadContentViewModel.cs`
- `src/desktop/app/XerahS.UI/Views/UploadContentWindow.axaml`
- `src/desktop/app/XerahS.UI/ViewModels/SettingsViewModel.Integration.cs`
- `src/desktop/app/XerahS.UI/Views/ApplicationSettingsView.axaml`
- `src/desktop/core/XerahS.Core/Tasks/Processors/UploadJobProcessor.cs`

### Tests and verification
- Added `tests/XerahS.Tests/SendTo/SendToPolicyResolverTests.cs`.
- Extended `tests/XerahS.Tests/SendTo/SendToIntegrationCoordinatorTests.cs`.
- `dotnet test tests\XerahS.Tests\XerahS.Tests.csproj --filter SendTo -m:1` passed: 12 tests.
- `dotnet build src\desktop\XerahS.sln -m:1` passed with 0 warnings and 0 errors.
- `dotnet test tests\XerahS.Tests\XerahS.Tests.csproj -m:1` compiled and ran, but failed in pre-existing/environment-sensitive coverage outside XIP0057 on this Windows host: SQLite history cleanup locked `history.db`, Linux/macOS clipboard path expectations, and `/bin/sh` notification process tests.

### Remaining hardening
- A future polish pass can replace the remembered large-image-batch toast stop with an interactive confirmation dialog when the app can safely present one from a remembered/background Send-to invocation.
- A future UX pass can add editable default folder/batch policies to settings; the shipped settings surface lists and clears remembered rules, while defaults are initialized to safe values.

---

## Problem Statement
XIP0055 introduces the critical v1 correction: Send-to no longer implies upload, and users must choose an explicit action before XerahS starts work.

That fixes the main UX failure, but it intentionally leaves four second-order problems unsolved:

1. Users who always take the same action still pay the prompt cost on every Send-to invocation.
2. Folder-containing Send-to selections still need a more explicit, user-visible policy for how non-index actions treat directories.
3. Upload-capable Send-to actions can bypass normal XerahS file naming behavior and leak source/provider placeholder names into the final URL, such as `_pb.jpg`, instead of using the configured name pattern.
4. Multi-item image actions can become mechanically correct but operationally noisy when they open or pin many items at once.

These are good v1 tradeoffs because they keep the first release scoped and safe. They should still be addressed as a follow-up so Send-to grows into a stable, configurable intake surface instead of remaining a one-off prompt.

---

## Goals
1. Add an explicit, reversible way to remember Send-to choices without hiding intent.
2. Make folder handling transparent for `Upload now` and `Open in Upload Content`.
3. Preserve XerahS file naming parity for Send-to actions that result in upload.
4. Improve batch ergonomics for image-editor and pin-to-screen Send-to actions.
5. Preserve the distinction between `Send to XerahS` and `Upload with XerahS`.

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

### 3) Upload naming parity with standard workflows
Any Send-to action that results in upload should resolve the upload name through the same naming policy used by normal XerahS uploads.

Observed symptom:

- the uploaded asset can retain a placeholder or source-derived name such as `_pb.jpg`
- the final URL therefore no longer reflects the configured XerahS name pattern

Post-v1 design rules:

- `Upload now` and `Open in Upload Content` must converge on the same naming resolution path before upload begins.
- If the source item does not already have an XerahS-generated upload name, XerahS should materialize a staged file or staged upload item whose name is derived from the active task settings name pattern.
- Generated names must respect the same collision handling rules used by normal XerahS uploads.
- Folder expansion policy and naming policy must compose cleanly: each resolved file gets its own generated upload name after folder expansion, not before.
- The Upload Content UI should surface the resolved upload name when feasible so users can verify what will be sent.

Diagnostics:

- log the original source path
- log whether a staging/materialization step occurred
- log the resolved upload name used for the outgoing upload

### 4) Batch execution policy for image actions
When Send-to receives many image files, `Open in Image Editor` and `Pin to Screen` need pacing and visibility.

Add a batch execution policy:

- `Open all immediately`
- `Open sequentially`
- `Confirm before opening more than N items`

For pin-to-screen batches:
- show a completion toast or summary with the number of successfully pinned images
- surface skipped or failed files once, instead of one failure per file when possible

### 5) Prompt summary and diagnostics hardening
Extend Send-to diagnostics so one batch decision can always be reconstructed from the log:

- remembered rule used or not used
- folder policy used
- naming policy used
- number of file items resolved from folders
- number of staged file names generated
- number of editor/pin actions started, skipped, or failed

---

## Acceptance Criteria
1. [Implemented] Users can optionally remember a Send-to choice and clear it later.
2. [Implemented] Remembered Send-to choices are scoped and never affect `Upload with XerahS`.
3. [Implemented] Folder-containing Send-to selections show an explicit folder policy for non-index actions.
4. [Implemented] Send-to uploads use the same effective naming policy as equivalent non-Send-to uploads.
5. [Implemented] `Upload now` and `Open in Upload Content` do not diverge in generated upload names for the same input and task settings.
6. [Implemented] Batch image actions offer a predictable execution policy and avoid uncontrolled window bursts.
7. [Implemented] Logs clearly show the rule, folder policy, and naming policy used for each Send-to batch.

---

## Suggested Tests
1. Remembered choice applies only when the incoming selection matches the stored scope.
2. Clearing remembered Send-to choices removes all automatic routing.
3. Folder policy `Do not expand folders` results in zero folder-derived upload-content or upload items.
4. Folder policy `Include top-level files` excludes nested files.
5. Folder policy `Include files recursively` includes nested files and logs the resolved count.
6. Send-to `Upload now` applies the configured XerahS name pattern before upload instead of leaking the source/provider placeholder name.
7. Send-to `Open in Upload Content` resolves the same upload name that `Upload now` would produce for the same input.
8. Generated Send-to upload names remain collision-safe when multiple files resolve from folders.
9. Batch execution policy respects the configured threshold before opening many editor windows.

---

## Key Files Expected to Change / Be Added
1. `src/desktop/app/XerahS.UI/Views/SendToPromptWindow.axaml`
2. `src/desktop/app/XerahS.UI/ViewModels/SendToPromptViewModel.cs`
3. `src/desktop/app/XerahS.UI/Services/AvaloniaUIService.cs`
4. `src/desktop/app/XerahS.App/Program.cs`
5. `src/desktop/app/XerahS.App/SendToIntegrationCoordinator.cs`
6. `src/desktop/core/XerahS.Core/SendTo/*`
7. `src/platform/XerahS.Platform.Abstractions/IUIService.cs`
8. `src/desktop/app/XerahS.UI/Services/UploadContentToolService.cs`
9. `src/desktop/app/XerahS.UI/ViewModels/UploadContentViewModel.cs`
10. `src/desktop/core/XerahS.Core/Tasks/*`
11. `src/desktop/app/XerahS.UI/Views/ApplicationSettingsView.axaml`

---

## Verification Commands
```powershell
dotnet build src/desktop/XerahS.sln -m:1
dotnet test tests/XerahS.Tests/XerahS.Tests.csproj -m:1
```
