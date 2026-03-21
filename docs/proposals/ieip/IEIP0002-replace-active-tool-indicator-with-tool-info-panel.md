# IEIP0002: Replace Active Tool Indicator with Tool Info Panel

## Status
- Status: Drafted on March 21, 2026.
- Type: UX enhancement for `ShareX.ImageEditor`.
- Scope: `ShareX.ImageEditor` toolbar presentation and adapter surface only.
- Related issue: `ShareX/ShareX.ImageEditor#7`

## Summary
Replace the current icon-only active tool indicator in `AnnotationToolbar` with a distinct Tool Info panel that is visually separated from the primary tool/action buttons and shows the current tool or selected annotation type together with relevant properties such as color, thickness, font, effect strength, and dimensions.

## Motivation
- The current leading icon in the second toolbar row reads like a button even though it is informational.
- The phrase "Active Tool" is misleading when Select mode is active but the user has a rectangle, text box, or other annotation selected.
- Important context is currently split across picker controls and tooltips, so the user cannot quickly read what the selected tool or annotation is configured to do.
- The current placement makes informational context compete visually with editing actions.

## Current State
- `AnnotationToolbar.axaml` starts the second row with a `40x40` icon-only border bound to `ActiveToolIcon` with a tooltip bound to `ActiveToolName`.
- `IAnnotationToolbarAdapter` exposes `ActiveToolIcon`, `ActiveToolName`, and tool-option visibility flags, but it does not expose a structured tool-info model or any dimensions/bounds summary.
- `MainViewModel.ToolOptions.cs` already maps Select mode plus selected annotation into a more specific display icon and name, which means the UI is already trying to describe the selected annotation type rather than only the currently pressed toolbar button.

## Goals
- Replace the misleading icon chip with a dedicated Tool Info surface.
- Rename the concept from "Active Tool" to "Tool Info" or "Current Tool".
- Keep the main toolbar focused on choosing tools and invoking actions.
- Show only the properties relevant to the current tool or selected annotation.
- Surface live dimensions or bounds when drawing or when an annotation is selected.
- Preserve existing commands, pickers, keyboard shortcuts, and editing flows.

## Non-Goals
- No redesign of the annotation data model.
- No removal of existing color, width, font, or effect picker controls.
- No changes to editor commands, persistence, rendering output, or history behavior.
- No backend contract changes outside the `ShareX.ImageEditor` toolbar/presentation layer unless strictly needed for dimensions or selection summaries.

## Proposal

### 1. Replace the indicator with a Tool Info panel
- Remove the current icon-only leading block from the second toolbar row.
- Introduce a `ToolInfoPanel` surface that displays:
  - tool icon
  - tool or selected annotation name
  - a small set of relevant summary chips or values
- The panel should look informational rather than clickable.

### 2. Separate context from actions
- Visually detach the Tool Info panel from the tool/action button strip.
- Preferred direction:
  - a slim vertical rail anchored near the canvas edge
- Responsive fallback:
  - a compact horizontal info panel at the start of the options row when space is limited
- The main toolbar remains responsible for tool selection and commands only.

### 3. Show only relevant metadata
- Rectangle, ellipse, line, arrow, freehand, highlight:
  - stroke color
  - fill color when applicable
  - thickness
  - width and height while drawing or when selected
- Text and speech balloon:
  - text color
  - font size
  - bold, italic, underline
  - shadow
  - bounds when selected
- Blur, pixelate, magnify, spotlight, smart eraser:
  - effect strength
  - bounds when drawing or selected
- Select mode:
  - if nothing is selected, show compact "Select" state only
  - if an annotation is selected, show that annotation's tool type and relevant properties instead

### 4. Add live dimensions and selection summary
- While an annotation is being drawn, update the Tool Info panel with live width and height values.
- When an annotation is selected, show its current bounds summary.
- Dimensions should be read-only informational values, not inline editors in the first implementation.

### 5. Extend the adapter with structured info rather than more ad-hoc labels
- Keep `ActiveToolIcon` and `ActiveToolName` only as compatibility shims if needed during migration.
- Preferred end state:
  - expose a small Tool Info view model or adapter contract with fields such as:
    - title
    - icon
    - primary color visibility/value
    - secondary color visibility/value
    - thickness visibility/value
    - font size visibility/value
    - strength visibility/value
    - width visibility/value
    - height visibility/value
    - style toggles summary
- This avoids pushing more display logic into XAML triggers and tooltips.

## Implementation Plan

### Phase 1: Terminology and presentation split
- Rename the UI concept from Active Tool to Tool Info or Current Tool.
- Replace the current icon-only block with a non-clickable info panel container.
- Keep the existing pickers and option buttons functional and unchanged.

### Phase 2: Structured info surface
- Add a tool-info model to `IAnnotationToolbarAdapter` and `EditorToolbarAdapter`.
- Populate title/icon and property summary fields from `MainViewModel.ToolOptions.cs`.
- Bind the new panel to structured values instead of relying on tooltip-only context.

### Phase 3: Live dimensions
- Feed drawing/selection bounds into the adapter.
- Show width and height readouts for active drawing operations and selected annotations.

### Phase 4: Layout refinement
- Evaluate the preferred detached vertical rail layout.
- Keep a responsive fallback to a horizontal layout if canvas width is constrained.

## Verification
- Build:
  - `ShareX.ImageEditor/src/ShareX.ImageEditor/ShareX.ImageEditor.csproj`
- Manual smoke scenarios:
  - switch between all annotation tools and confirm the Tool Info title/icon stays correct
  - Select mode with no selection shows compact select state
  - Select mode with a selected annotation shows that annotation type rather than "Select"
  - stroke/fill/text properties stay accurate as tool options change
  - text styling states are reflected correctly
  - width and height update while drawing supported shapes
  - width and height update when selecting an existing annotation
  - no command, undo/redo, or picker regressions

## Risks
- Adding dimensions to the toolbar adapter may create unwanted coupling between transient drawing state and the presentation layer if the data shape is not kept narrow.
- A detached vertical rail could consume valuable canvas space on smaller widths if responsive fallback behavior is not designed early.
- Mixing informative values and editable controls in one panel could make the surface noisy; the first implementation should prioritize readable summaries over adding new editing affordances.

## Open Questions
- Should the panel title be `Tool Info` or `Current Tool`?
- Should dimensions be shown for all tools with geometry, or only for selection and active drawing?
- Should the first implementation remain horizontal for lower risk, then move to a vertical rail once the information model is proven?
