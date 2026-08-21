# XIP0053 Avalonia Compiled Bindings and Template Hardening

**Status**: Complete
**Version**: v0.22.257

**Priority**: High  
**Audit date**: 2026-03-17  
**Completion review**: 2026-03-19  
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

## Pre-migration Audit Findings (XerahS baseline)

The findings in this section describe the baseline state observed at audit time before the XIP0053 implementation work.

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

Binding correctness note for Avalonia command migration:

- `#ElementName.Property` is an Avalonia binding path feature and should be used with Avalonia `Binding` / compiled binding paths.
- Do **not** combine `#ElementName` paths with `ReflectionBinding`; that WPF-compatible reflection parser treats `#...` as a literal segment and command lookup can fail silently at runtime.
- In views with root `x:DataType` set to a view-model type, `{Binding SomeCommand}` resolves against that view-model. Window-level commands (for example in `MainWindow`) must either:
  - bind via an explicit window element path, or
  - set a narrow local scope `DataContext` to the window when appropriate (for example menu shell wiring).
- If command properties are exposed from a `Window`/`UserControl` code-behind surface, instantiate those command objects before `InitializeComponent()` so first-pass binding evaluation can resolve them.
- For `MainWindow` menu migration and equivalent compiled-binding shell surfaces in this codebase's current Avalonia/tooling setup, `MenuItem Click="..."` XAML event wiring is not a safe fallback for migration fixes (compiler can reject it with AVLN3000). Use command bindings for menu actions.

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

Benefits:

- **Early failure over late failure**: binding/path mistakes become build errors instead of runtime surprises.
- **Immediate migration inventory**: enabling defaults exposed the real set of breakpoints across desktop UI and editor surfaces, which made follow-up work concrete instead of speculative.
- **AOT/perf alignment**: this moves projects toward Avalonia's recommended compiled-binding path and away from reflection-heavy defaults.

### Phase 2 - Type all templates with `x:DataType`

Audit and update untyped templates in:

- desktop views (`TaskSettingsPanel`, `DestinationSettingsView`, `WorkflowsView`, etc.)
- image editor dialogs/controls

Rule:

- Every template with bindings gets an `x:DataType`.
- If data is truly polymorphic/dynamic, document and use `ReflectionBinding` intentionally.

Benefits:

- **Safer refactors**: renaming view-model properties now fails at compile time where templates are typed.
- **Cleaner intent**: each template advertises its expected data shape, improving readability and onboarding.
- **Controlled dynamic exceptions**: dynamic bindings remain possible, but only where explicitly declared, reducing accidental runtime binding drift.

### Phase 3 - Replace default reflection `ViewLocator`

Move from convention-only runtime lookup to explicit mapping:

- Prefer explicit `DataTemplate` registrations in `App.axaml` / feature-level template collections.
- Keep a narrow compatibility fallback during migration only.
- Log any fallback usage so remaining dynamic paths are visible.

Benefits:

- **Deterministic view resolution**: common navigation paths no longer depend only on string-based `Type.GetType` conventions.
- **Reduced runtime fragility**: namespace/name refactors are less likely to break screen resolution unexpectedly.
- **Incremental safety**: keeping fallback behavior preserves compatibility while explicit mappings are expanded.

### Phase 4 - Command-first interaction pass (targeted)

Prioritize high-use shell actions:

- Convert suitable `Click="..."` paths in `MainWindow` and similar views to commands.
- Keep code-behind handlers for truly view-specific behavior (focus, animation, control-only plumbing).
- For element-scoped command sources (for example `#MainWindowRoot.NavigateMenuCommand`), keep Avalonia `Binding` syntax and do not switch those paths to `ReflectionBinding`.
- For mixed command sources (window commands + view-model commands) in the same menu:
  - make command source ownership explicit,
  - avoid relying on implicit `DataContext` for shell menu actions,
  - ensure command property initialization order is deterministic (`commands -> InitializeComponent -> control discovery`),
  - require a runtime smoke check of at least 3 tool-launch entries before merge.

Benefits:

- **Higher testability**: command-backed actions are easier to exercise in view-model tests than code-behind event handlers.
- **Better MVVM consistency**: application behavior is centralized in view models/services rather than split across many views.
- **Lower coupling**: UI event wiring becomes thinner, which reduces regression risk during UI redesigns.

### Phase 5 - Verification and guardrails

- Build with warnings-as-errors for affected projects where feasible.
- Add CI checks for common binding regressions.
- Add a lightweight lint/checklist: "new templates must include `x:DataType`."
- Enable/verify binding trace logging in development to catch remaining dynamic issues quickly.

Benefits:

- **Prevents regression backslide**: guardrails make it hard to accidentally reintroduce reflection-only patterns.
- **Faster PR feedback**: contributors get actionable failures in CI instead of delayed runtime bug reports.
- **Sustained migration velocity**: explicit checks let the team continue phase-by-phase hardening without losing prior gains.

---

## Implementation Status (2026-03-17)

### Phase 1 - Turn on compiled-binding defaults

Status: **Implemented**

- Enabled `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>` in:
  - `src/desktop/app/XerahS.UI/XerahS.UI.csproj`
  - `ShareX.ImageEditor/src/ShareX.ImageEditor/ShareX.ImageEditor.csproj`

Benefits realized:

- Binding issues now fail at build time during normal development.
- Migration work became concrete and trackable from compiler output.

### Phase 2 - Type templates with `x:DataType` and explicit dynamic bindings

Status: **Implemented**

- Added `x:DataType` to all `DataTemplate` declarations in `XerahS.UI` and `ShareX.ImageEditor` presentation surfaces.
- Added explicit type annotations for templates that already had `DataType` but lacked compiled binding typing metadata.
- Preserved explicit dynamic template boundaries by using `x:Object` where the item shape is intentionally enum/object-driven.

Benefits realized:

- Stronger template safety across the entire migrated template surface area.
- Fewer hidden runtime binding failures in navigation, settings, tools, and editor dialogs.

### Phase 3 - Replace default reflection `ViewLocator` behavior

Status: **Implemented**

- `ViewLocator` now uses explicit type-to-view mappings first, with convention fallback retained for compatibility.

Benefits realized:

- More deterministic view resolution for common navigation paths.
- Lower risk from namespace/type renames that previously depended on string conventions only.

### Phase 4 - Command-first interaction pass

Status: **Implemented**

- Converted main shell menu navigation/open/exit wiring in `MainWindow` to command bindings (`NavigateMenuCommand`, `OpenImageMenuCommand`, `ExitMenuCommand`) with explicit command-source scoping.
- Converted dynamic workflow menu execution to command-based dispatch (`RunWorkflowFromMenuCommand`) instead of per-item click events.
- Confirmed menu item command paths that use `#MainWindowRoot` are expressed as Avalonia `Binding` paths, not `ReflectionBinding`.
- Corrected a post-migration regression by making menu command source ownership explicit in XAML:
  - shell menu command source is scoped to `MainWindow`,
  - editor/view-model commands are explicitly bound to `MainWindow.DataContext`.
- Corrected a cross-platform startup regression where menu commands were instantiated after `InitializeComponent()`:
  - moved menu-command initialization earlier in the `MainWindow` constructor,
  - validated that first-pass bindings resolve and tool menu launches work on startup.

Benefits realized:

- High-use shell actions now follow a command-first pattern, improving consistency with MVVM command flows.
- Menu action behavior is easier to reason about and test because dispatch paths are centralized.
- Avoided the WPF-compatibility pitfall where `ReflectionBinding` does not interpret Avalonia `#ElementName` path semantics.
- Added explicit command source boundaries, removing an implicit-binding ambiguity that caused Tools menu actions to stop opening tool windows during migration.
- Added constructor-order guardrail so command-backed menus remain available immediately at window startup.

### Phase 5 - Verification and guardrails

Status: **Implemented**

- Added CI workflow: `.github/workflows/compiled-bindings-guardrails.yml`.
- Added template typing guard script: `build/ci/check_compiled_bindings_guardrails.py`.
- CI now enforces:
  - `x:DataType` presence on `DataTemplate` / `TreeDataTemplate` in hardened surfaces.
  - `dotnet build -warnaserror` for:
    - `src/desktop/app/XerahS.UI/XerahS.UI.csproj`
    - `ShareX.ImageEditor/src/ShareX.ImageEditor/ShareX.ImageEditor.csproj`
- Added contributor guideline: `developers/guidelines/AVALONIA_COMPILED_BINDING_GUIDELINES.md` and linked it from `developers/README.md`.

Benefits realized:

- Guardrails now prevent silent regressions in template typing and binding hygiene.
- CI catches typed-binding regressions earlier and with explicit failure signals.

---

## Exit Criteria for Temporary `x:CompileBindings="False"`

`x:CompileBindings="False"` is treated as a temporary migration aid and should be removed when all are true:

1. The view has a stable `x:DataType` at root and all template scopes.
2. Any dynamic binding path is explicitly marked with `ReflectionBinding`.
3. The project builds cleanly without adding new AVLN binding errors.
4. No runtime regressions are observed in smoke tests for that view.
5. The opt-out is narrowed to smallest practical scope (never broad/global unless unavoidable).

Current note:

- All prior `x:CompileBindings="False"` temporary opt-outs in targeted surfaces have been removed.
- Dynamic binding edges that remain are now explicitly represented through typed scopes and narrow `ReflectionBinding` usage where needed.

---

## Acceptance Criteria

1. `XerahS.UI` and `ShareX.ImageEditor` compile with project-wide compiled bindings enabled.
2. New and migrated templates include `x:DataType` (except documented dynamic exceptions).
3. `App.axaml` no longer depends on reflection `ViewLocator` as the primary view resolution mechanism.
4. No functional regression in core flows (capture, editor, upload, settings, history).
5. A short contributor guideline exists for template typing and binding defaults.
6. Main menu tool actions (`Tools > Clipboard Viewer`, `Tools > Hash Checker`, `Tools > Index Folder`) open expected windows/views in runtime smoke tests.

---

## Completion Review (2026-03-19)

This XIP is considered complete based on the current codebase and guardrail audit.

- `XerahS.UI` and `ShareX.ImageEditor` both enable project-wide compiled bindings and build cleanly with `-warnaserror`.
- The compiled-binding guardrail script passes and the CI workflow exists to enforce typed templates and warning-free builds for the hardened surfaces.
- All `DataTemplate` / `TreeDataTemplate` usage under the guarded `XerahS.UI` and `ShareX.ImageEditor/Presentation` surfaces is typed with `x:DataType`; remaining dynamic cases are explicit narrow boundaries such as `x:Object` or targeted `ReflectionBinding`.
- `ViewLocator` now resolves known mappings explicitly first, with convention-based reflection retained only as compatibility fallback.
- Main shell menu wiring in `MainWindow` uses command bindings, and menu command instances are initialized before `InitializeComponent()`.

Verification performed for this review:

- `python build\ci\check_compiled_bindings_guardrails.py --repo-root .`
- `dotnet build src\desktop\app\XerahS.UI\XerahS.UI.csproj -warnaserror -m:1`
- `dotnet build ShareX.ImageEditor\src\ShareX.ImageEditor\ShareX.ImageEditor.csproj -warnaserror -m:1`

Remaining note:

- This review did not re-run interactive UI smoke tests; completion is based on the current static audit plus successful guardrail builds.

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
- Tools menu launches (minimum): Clipboard Viewer, Hash Checker, Index Folder
- Capture -> edit -> upload flow
- Destination/provider settings editing
- History grid/list rendering and context actions
- ImageEditor tool dialogs and effect parameter panels

### Command-source regression checks (required for menu refactors)

- Verify each menu command binding's owner explicitly (`MainWindow` vs view-model `DataContext`).
- Verify command properties used by XAML are initialized before `InitializeComponent()`.
- Reject PRs that fix menu command regressions by switching to `ReflectionBinding` for `#ElementName` paths.
- Reject PRs that rely on `MenuItem Click="..."` as fallback in hardened compiled-binding surfaces.

### Diagnostics

- Verify binding warnings are reduced compared to baseline.
- Verify no runtime "Not Found: ...View" from `ViewLocator` fallback in normal app navigation.

---

## Expected Outcome

After XIP0053, XerahS will retain existing UX behavior while moving a large portion of UI correctness checks to compile time.  
This aligns with Avalonia best practices, improves performance predictability, and materially lowers refactor risk for both human contributors and AI-assisted edits.