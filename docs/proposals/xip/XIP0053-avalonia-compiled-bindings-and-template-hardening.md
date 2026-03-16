# XIP0053 - Avalonia Compiled Bindings and Template Hardening

**Status**: Proposed  
**Priority**: High  
**Audit date**: 2026-03-17  
**Related**: XIP0041, XIP0052

---

## Problem Statement

XerahS has adopted many MVVM and Avalonia patterns, but the binding surface is still only partially compile-checked.  
Based on Avalonia docs guidance on `x:DataType`, `x:CompileBindings`, and project-wide compiled bindings, the current state creates avoidable runtime risk:

1. Several UI projects do not enable compiled bindings by default in `.csproj`.
2. Many `DataTemplate` declarations still omit `x:DataType`.
3. `Application.DataTemplates` still relies on a reflection-based `ViewLocator` that resolves views by naming convention at runtime.

This combination means binding and view-resolution mistakes can pass build and fail only at runtime.

---

## Avalonia Guidance Used

This proposal is grounded in Avalonia docs returned from the `avalonia-docs` MCP:

- `x:DataType` is required for compiled bindings and enables compile-time binding validation.
- `x:CompileBindings="True"` can enable compiled bindings in a scope.
- `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>` enables project-wide compiled binding behavior.
- `ReflectionBinding` should be used intentionally for dynamic scenarios, not as accidental fallback.

Key doc families used:

- XAML directives (`x:DataType`, `x:CompileBindings`)
- XAML compilation / compiled bindings
- Data template guidance

---

## Code Audit Findings (XerahS)

### 1) Project-wide compiled bindings are not enabled in key UI projects

- `src/desktop/app/XerahS.UI/XerahS.UI.csproj` has no `AvaloniaUseCompiledBindingsByDefault` property.
- `ShareX.ImageEditor/src/ShareX.ImageEditor/ShareX.ImageEditor.csproj` has no `AvaloniaUseCompiledBindingsByDefault` property.

Impact:

- Bindings can remain reflection-based unless each view/template is individually hardened.
- Typo-level binding regressions are caught later than needed.

### 2) Many `DataTemplate` blocks still omit `x:DataType`

Representative examples:

- `src/desktop/app/XerahS.UI/Views/TaskSettingsPanel.axaml`
- `src/desktop/app/XerahS.UI/Views/DestinationSettingsView.axaml`
- `src/desktop/app/XerahS.UI/Views/WorkflowsView.axaml`
- `src/desktop/app/XerahS.UI/Views/ApplicationSettingsView.axaml` (mixed state: some templates typed, some untyped)
- `ShareX.ImageEditor/src/ShareX.ImageEditor/Presentation/Views/EditorView.axaml`
- `ShareX.ImageEditor/src/ShareX.ImageEditor/Presentation/Controls/*PickerDropdown.axaml`

Impact:

- Binding path errors in templates are not compile-validated.
- AOT-readiness and binding performance improvements are not fully realized.

### 3) Runtime reflection view resolution remains a default path

- `src/desktop/app/XerahS.UI/App.axaml` registers `<local:ViewLocator/>` in `Application.DataTemplates`.
- `src/desktop/app/XerahS.UI/ViewLocator.cs` resolves types using string replacement + `Type.GetType` + `Activator.CreateInstance`.

Impact:

- Broken naming conventions can fail at runtime.
- View wiring remains implicit and harder to refactor safely.
- Compile-time template validation coverage is diluted by late-bound view construction.

### 4) Event-driven UI is still mixed with command-driven MVVM

Representative example:

- `src/desktop/app/XerahS.UI/Views/MainWindow.axaml` has many `Click="..."` handlers mixed with command bindings.

This is not always wrong (some are shell/navigation concerns), but the current ratio suggests more app actions can move to command bindings for testability and consistency.

---

## Goals

1. Enable compiled-binding defaults in all Avalonia-first UI projects.
2. Ensure `DataTemplate` usage is typed (`x:DataType`) unless intentionally dynamic.
3. Reduce runtime reflection view resolution from the default application path.
4. Improve binding safety, startup predictability, and refactor confidence.
5. Preserve current UX behavior while tightening compile-time guarantees.

---

## Non-Goals

- Rewriting all screens in one PR.
- Removing all code-behind events immediately.
- Blocking dynamic scenarios that genuinely require `ReflectionBinding`.
- Forcing a new navigation architecture during this XIP.

---

## Proposal

### Phase 1 - Turn on compiled-binding defaults

Add:

```xml
<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
```

to:

- `src/desktop/app/XerahS.UI/XerahS.UI.csproj`
- `ShareX.ImageEditor/src/ShareX.ImageEditor/ShareX.ImageEditor.csproj`

Then run compile, fix typed-binding errors, and explicitly mark truly dynamic paths with `ReflectionBinding`.

### Phase 2 - Type all templates with `x:DataType`

Audit and update untyped templates in:

- desktop views (`TaskSettingsPanel`, `DestinationSettingsView`, `WorkflowsView`, etc.)
- image editor dialogs/controls

Rule:

- Every template with bindings gets an `x:DataType`.
- If data is truly polymorphic/dynamic, document and use `ReflectionBinding` intentionally.

### Phase 3 - Replace default reflection `ViewLocator`

Move from convention-only runtime lookup to explicit mapping:

- Prefer explicit `DataTemplate` registrations in `App.axaml` / feature-level template collections.
- Keep a narrow compatibility fallback during migration only.
- Log any fallback usage so remaining dynamic paths are visible.

### Phase 4 - Command-first interaction pass (targeted)

Prioritize high-use shell actions:

- Convert suitable `Click="..."` paths in `MainWindow` and similar views to commands.
- Keep code-behind handlers for truly view-specific behavior (focus, animation, control-only plumbing).

### Phase 5 - Verification and guardrails

- Build with warnings-as-errors for affected projects where feasible.
- Add CI checks for common binding regressions.
- Add a lightweight lint/checklist: "new templates must include `x:DataType`."
- Enable/verify binding trace logging in development to catch remaining dynamic issues quickly.

---

## Acceptance Criteria

1. `XerahS.UI` and `ShareX.ImageEditor` compile with project-wide compiled bindings enabled.
2. New and migrated templates include `x:DataType` (except documented dynamic exceptions).
3. `App.axaml` no longer depends on reflection `ViewLocator` as the primary view resolution mechanism.
4. No functional regression in core flows (capture, editor, upload, settings, history).
5. A short contributor guideline exists for template typing and binding defaults.

---

## Risks and Mitigations

### Risk 1 - Initial compile error spike after enabling defaults

Mitigation:

- Roll out project-by-project.
- Start with desktop UI, then ImageEditor.
- Use temporary targeted `ReflectionBinding` only where needed.

### Risk 2 - Dynamic plugin/config views resist strict typing

Mitigation:

- Keep narrow dynamic boundaries explicit.
- Document each exception at template site.

### Risk 3 - Migration churn across many AXAML files

Mitigation:

- Batch by feature module (history, settings, workflows, editor dialogs).
- Keep behavior-only PRs separate from binding-hardening PRs.

---

## Test Plan

### Build-time

- `dotnet build` for affected projects after each phase.
- Confirm compiled-binding errors are resolved, not suppressed broadly.

### Runtime smoke checks

- Main shell navigation and menu actions
- Capture -> edit -> upload flow
- Destination/provider settings editing
- History grid/list rendering and context actions
- ImageEditor tool dialogs and effect parameter panels

### Diagnostics

- Verify binding warnings are reduced compared to baseline.
- Verify no runtime "Not Found: ...View" from `ViewLocator` fallback in normal app navigation.

---

## Expected Outcome

After XIP0053, XerahS will retain existing UX behavior while moving a large portion of UI correctness checks to compile time.  
This aligns with Avalonia best practices, improves performance predictability, and materially lowers refactor risk for both human contributors and AI-assisted edits.
