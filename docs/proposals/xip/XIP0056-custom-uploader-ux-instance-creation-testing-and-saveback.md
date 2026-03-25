# XIP0056 Custom Uploader UX - Instance Creation, Testing, Save-Back
**Status**: PROPOSED
**Priority**: High
**Related**: XIP0024, XIP0045

---

## Problem Statement
XerahS currently models Custom Uploaders (`.sxcu` JSON files stored under `Plugins/`) as *Providers* that must later be added as *Instances* into a destination *Category*.

In the UI, users experience this as an awkward two-step workflow:
1. Click `Add Custom Uploader` to create/export a `.sxcu` definition.
2. Click `Add from Catalog` (per category) to create the runnable destination instances.

This creates UX friction compared to ShareX, where a custom uploader results in a directly usable configuration (and where users can quickly test/iterate using the URL output).

There is also a second mismatch:
- Inline editing of an instance updates the instance `SettingsJson` (and persists it into `uploader-instances.json`), but it does not clearly provide a way to update the underlying `.sxcu` definition. As a result, users can end up with changes that exist only as per-instance overrides, while the plugin catalog source remains unchanged.

---

## Current Implementation Notes (Code Audit)
### UI entry points
- `src/desktop/app/XerahS.UI/Views/DestinationSettingsView.axaml`
  - `Add Custom Uploader` (category-independent)
  - `Add from Catalog` (category-scoped, bound to `CategoryViewModel.AddFromCatalog`)
- `src/desktop/app/XerahS.UI/ViewModels/DestinationSettingsViewModel.cs`
  - `AddCustomUploader()` saves a new `.sxcu` into `Plugins/` and reloads the provider catalog.
  - It does not create destination instances for the newly saved custom uploader definition.
  - Legacy import path (`Import ShareX Config`) *does* auto-create instances via `AutoCreateCustomUploaderInstances(...)`.
- `src/desktop/app/XerahS.UI/ViewModels/CategoryViewModel.cs`
  - `AddFromCatalog()` opens `ProviderCatalogViewModel` for exactly one category.
- `src/desktop/app/XerahS.UI/ViewModels/ProviderCatalogViewModel.cs`
  - `AddSelected()` creates exactly one instance in the currently filtered category.

### Custom uploader editor
- `src/desktop/app/XerahS.UI/ViewModels/CustomUploaderEditorViewModel.cs`
  - Contains `TestUploaderAsync()` (but it is currently not exposed in `CustomUploaderEditorDialog.axaml`).
- `src/desktop/app/XerahS.UI/Views/CustomUploaderEditorDialog.axaml`
  - Header/actions include `Import...`, `Export...`, `Cancel`, `Save`.

### Instance editing vs `.sxcu` source
- `src/desktop/app/XerahS.UI/ViewModels/UploaderInstanceViewModel.cs`
  - For `custom_` providers, it uses `CustomUploaderEditorViewModel` inline.
  - Any edit syncs back into `Instance.SettingsJson` and `InstanceManager.UpdateInstance(...)`.
- `src/desktop/core/XerahS.Uploaders/CustomUploader/CustomUploaderProvider.cs`
  - Wraps a loaded `.sxcu` and exposes `FilePath`.

---

## Goals
1. Reduce the “do it twice” workflow:
   - After saving a Custom Uploader definition, users should be able to create instances immediately (optionally defaulting selected categories).
2. Provide an in-editor `Test` workflow that helps users validate URL/result parsing without needing to perform a real upload every time.
3. Offer a clear, intentional way to propagate instance edits back to the `.sxcu` definition (or clearly explain why instance overrides do not affect the definition).
4. Improve clarity about the difference between:
   - *Custom uploader definition* (the `.sxcu` provider in Plugins)
   - *Destination instance* (a per-category instance that runtime uses)

---

## Non-Goals
- This XIP does not attempt to replace the provider/instance architecture.
- This XIP does not require implementing a fully featured network “upload test” that depends on valid credentials and real HTTP endpoints.

---

## Proposed Design
### 1) Instance auto-creation after `Add Custom Uploader`
After the user hits `Save` in `CustomUploaderEditorDialog`, prompt (or inline-option) whether to create instances now.

Behavior proposal:
- Use the destination types selected inside the editor (`IsImageUploader`, `IsTextUploader`, `IsFileUploader`, etc.).
- Show checkboxes in the `Add Custom Uploader` completion flow:
  - `Create Image uploader instance`
  - `Create Text uploader instance`
  - `Create File uploader instance`
  - `Create URL shortener instance`
  - `Create URL sharing instance`
- When confirmed:
  - Ensure the custom uploader provider is loaded (already done via `ProviderCatalog.LoadCustomUploaders`).
  - Create `UploaderInstance` entries for each selected category (reuse `AutoCreateCustomUploaderInstances(...)` logic, or refactor it into a shared helper).
  - Optionally offer `Set Default` per category (or auto-set the first created instance).

Expected UX impact:
- The “Add from Catalog” step becomes optional for the common case.

### 2) Add a real `Test` button in the custom uploader dialog
Add a `Test` button to `CustomUploaderEditorDialog.axaml` bound to `TestUploaderCommand` from `CustomUploaderEditorViewModel`.

Test proposal (minimum viable):
- Validate all required fields (already implemented via `ValidateAll()`).
- Resolve/preview:
  - final resolved request URL (based on syntax parsing with a dummy input/filename)
  - final URL parsing rules (show which pattern the user configured: `{json:...}`, `{regex:...}`, `{response}`, etc.)
  - show a “success criteria” message, rather than a stub “Save and test with an actual upload.”

Test proposal (phase 2, optional):
- Attempt a lightweight HTTP request only if:
  - URL scheme is supported (http/https)
  - user explicitly enables `Perform network test`
  - request payload is generated from dummy content

### 3) Instance edit save-back to `.sxcu`
Add an explicit command in the custom uploader inline editor (when editing a `custom_` provider instance) to:
- `Save changes to .sxcu definition`

Policy proposal:
- Overwrite the underlying `.sxcu` with the editor’s current state (`CustomUploaderEditorViewModel.ToItem()`).
- Confirm with a warning that the change affects other instances that use the same provider definition.

Implementation needs:
- Map the current provider to its `.sxcu` source via `CustomUploaderProvider.FilePath`.
- Write back using `CustomUploaderRepository.SaveToFile(item, filePath)`.
- Reload providers and refresh all destination categories.

Fallback policy:
- If `FilePath` cannot be resolved, disable the action and show a tooltip:
  - `This instance cannot be mapped back to a .sxcu source file.`

### 4) Optional: multi-category add from catalog
For custom uploaders (or any provider with multiple supported categories), enhance `ProviderCatalogViewModel.AddSelected()` to support:
- `Add to all supported categories`
- or a multi-select list of categories (UI can start with “all” to keep the change small).

This reduces “repeat Add from Catalog per category” even in catalog-driven workflows.

---

## Implementation Summary
### Stage A (UI and UX improvements without persistence model changes)
1. Wire `TestUploaderAsync()` into `CustomUploaderEditorDialog.axaml`.
2. After `AddCustomUploader()` saves `.sxcu`:
   - ask whether to create instances
   - create instances immediately using editor destination type selections
3. Update completion dialog text to reference auto-created instances (instead of only “available in catalog”).

### Stage B (optional persistence enhancement: save-back)
1. Add `Save to .sxcu` action in the inline custom uploader editor (inside `UploaderInstanceViewModel` UI).
2. Implement save-back:
   - resolve `CustomUploaderProvider.FilePath`
   - overwrite `.sxcu` using `CustomUploaderRepository.SaveToFile`
   - reload catalog and refresh categories
3. Add confirmation/warning UI.

### Stage C (optional catalog multi-add)
1. Extend provider catalog modal to allow adding instances to multiple categories at once (starting with “all supported categories”).

---

## Acceptance Criteria
1. A user can create a Custom Uploader and have ready-to-use destination instances created immediately for at least one selected destination type.
2. The custom uploader editor contains an accessible `Test` control that validates config and provides meaningful preview information.
3. Inline edits clearly either:
   - affect only the instance, or
   - can be saved back to the `.sxcu` definition via an explicit action.
4. No existing workflows (especially legacy ShareX config import) regress.

---

## Suggested Tests (Implementation Work)
1. Unit test or integration test:
   - create custom uploader definition, confirm auto-created instances per selected category
2. Regression test:
   - legacy import path continues to auto-create instances
3. Save-back test (if Stage B is implemented):
   - editing an instance and saving back overwrites the `.sxcu` and reloads the provider catalog

---

## Key Files Expected to Change / Be Added
1. `src/desktop/app/XerahS.UI/Views/CustomUploaderEditorDialog.axaml`
2. `src/desktop/app/XerahS.UI/ViewModels/CustomUploaderEditorViewModel.cs`
3. `src/desktop/app/XerahS.UI/ViewModels/DestinationSettingsViewModel.cs`
4. `src/desktop/app/XerahS.UI/ViewModels/CategoryViewModel.cs` (if multi-category add is implemented)
5. `src/desktop/app/XerahS.UI/ViewModels/ProviderCatalogViewModel.cs` (if multi-category add is implemented)
6. `src/desktop/app/XerahS.UI/ViewModels/UploaderInstanceViewModel.cs` (for save-back wiring)
7. `src/desktop/core/XerahS.Uploaders/PluginSystem/ProviderCatalog.cs` (reload/refresh helpers if needed)
8. `src/desktop/core/XerahS.Uploaders/CustomUploader/CustomUploaderProvider.cs` (if small helper methods are needed)

---

## Verification Commands
```powershell
dotnet build src/desktop/app/XerahS.sln -m:1
```

