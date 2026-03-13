# XIP0050 Replace FluentAvaloniaUI with Official Avalonia Primitives

XIP0050: Replace FluentAvaloniaUI with Official Avalonia Primitives

## Goal
Reduce or remove `FluentAvaloniaUI` from `XerahS.UI` by replacing the current shell and small-control usages with official Avalonia APIs, while keeping the supported official `Avalonia.Themes.Fluent` package.

## Summary
The current dependency is not purely a `NavigationView` dependency.

`XerahS.UI` uses `FluentAvaloniaUI` for:

1. the application theme root
2. the main left navigation shell
3. settings-style expandable sections
4. icon controls and icon sources
5. command separators
6. one confirmation dialog path

The main finding from local inspection plus official Avalonia docs is:

1. official Avalonia does not provide a direct `NavigationView`, `SettingsExpander`, or `ContentDialog` equivalent in `Avalonia.Controls`
2. official Avalonia does provide the primitives needed to recreate the shell behavior:
   - `SplitView`
   - `TreeView`
   - `Expander`
   - `Separator`
   - `PathIcon`
   - icon-font rendering via `TextBlock`
   - `TransitioningContentControl`
   - modal dialogs via `Window.ShowDialog`

Therefore, replacing `FluentAvaloniaUI` is feasible, but it is a small shell migration, not a package-reference cleanup.

## Local Findings

### Current package and theme usage

1. `FluentAvaloniaUI` is referenced in [src/desktop/app/XerahS.UI/XerahS.UI.csproj](../../../../src/desktop/app/XerahS.UI/XerahS.UI.csproj).
2. The app theme root is `sty:FluentAvaloniaTheme` in [src/desktop/app/XerahS.UI/App.axaml](../../../../src/desktop/app/XerahS.UI/App.axaml).
3. The repo otherwise already uses official Avalonia packages, including `Avalonia`, `Avalonia.Controls.ColorPicker`, and `Avalonia.Themes.Fluent`, pinned in [Directory.Packages.props](../../../../Directory.Packages.props).

### Main navigation usage

The primary `FluentAvaloniaUI` dependency is the main shell in [src/desktop/app/XerahS.UI/Views/MainWindow.axaml](../../../../src/desktop/app/XerahS.UI/Views/MainWindow.axaml):

1. `ui:NavigationView`
2. nested `ui:NavigationViewItem`
3. `ui:FontIconSource`
4. always-open left pane behavior
5. dynamic population of `Capture` submenu items

Supporting code lives in:

1. [src/desktop/app/XerahS.UI/Views/MainWindow.Navigation.cs](../../../../src/desktop/app/XerahS.UI/Views/MainWindow.Navigation.cs)
2. [src/desktop/app/XerahS.UI/Helpers/NavigationItemsHelper.cs](../../../../src/desktop/app/XerahS.UI/Helpers/NavigationItemsHelper.cs)

Behavior currently implemented:

1. static top-level sections
2. nested menu items
3. dynamic workflow-driven capture submenu
4. selection-driven page navigation
5. invoke-driven action items that can be re-triggered without changing selection
6. a plain `ContentControl` page host

This is functionally simple enough to recreate with official Avalonia controls.

### Additional FluentAvaloniaUI usages

`FluentAvaloniaUI` is also used outside the main shell:

1. `SettingsExpander` and `SettingsExpanderItem` in [src/desktop/app/XerahS.UI/Views/AboutView.axaml](../../../../src/desktop/app/XerahS.UI/Views/AboutView.axaml)
2. `FontIcon` in:
   - [src/desktop/app/XerahS.UI/Views/SettingsView.axaml](../../../../src/desktop/app/XerahS.UI/Views/SettingsView.axaml)
   - [src/desktop/app/XerahS.UI/Views/ToolsView.axaml](../../../../src/desktop/app/XerahS.UI/Views/ToolsView.axaml)
   - [src/desktop/app/XerahS.UI/Views/WorkflowsView.axaml](../../../../src/desktop/app/XerahS.UI/Views/WorkflowsView.axaml)
   - other views using icon glyphs
3. `CommandBarSeparator` in [src/desktop/app/XerahS.UI/Views/WorkflowsView.axaml](../../../../src/desktop/app/XerahS.UI/Views/WorkflowsView.axaml)
4. `ContentDialog` in [src/desktop/app/XerahS.UI/Views/WorkflowsView.axaml.cs](../../../../src/desktop/app/XerahS.UI/Views/WorkflowsView.axaml.cs)

This means package removal requires a broader migration plan than only swapping out the navigation control.

### Version-skew concern

The repo pins Avalonia `11.3.12` in [Directory.Packages.props](../../../../Directory.Packages.props), while the installed `FluentAvaloniaUI 2.4.1` package metadata declares Avalonia `11.2.5` dependencies.

That version skew is a legitimate reason to reduce or remove the package if official Avalonia primitives can cover the required behavior.

## Official Avalonia Findings

The official Avalonia docs and API reference confirm the following:

1. `SplitView` is an official control suitable for a collapsible or fixed left pane shell.
2. `TreeView` is an official control for hierarchical, selectable navigation data.
3. `Expander` is an official control for collapsible sections.
4. `TransitioningContentControl` is an official control for animated page/content swaps.
5. `PathIcon` is an official icon control.
6. official docs also recommend icon fonts as a standard approach, which matches XerahS's existing icon-font usage.
7. `Separator` is an official control and can replace `CommandBarSeparator`.
8. `Window.ShowDialog` is available officially and can replace the one `ContentDialog` usage with a small custom dialog window.

During official API inspection of the `Avalonia.Controls` namespace, no `NavigationView`, `ContentDialog`, `SettingsExpander`, or `FontIcon` entry was found.

Inference:

1. official Avalonia can reproduce the behavior
2. official Avalonia does not provide WinUI-style 1:1 shell controls for this specific UI
3. the migration should be approached as composition plus theming, not as a direct type rename

## Proposed Replacement Architecture

### Main shell

Replace `NavigationView` with:

1. `SplitView` as the top-level shell container
2. a styled `TreeView` for the hierarchical left navigation
3. a `TransitioningContentControl` or `ContentControl` for the page host

Recommended model:

1. create a `NavigationNode` view model type
2. bind the left pane to a hierarchical node collection
3. use `TreeDataTemplate` for nested items
4. store:
   - display text
   - icon glyph or icon geometry
   - navigation tag
   - child items
   - `IsActionOnly`
5. keep current routing logic in `MainWindow.Navigation.cs`, but make it `TreeView`-based instead of `NavigationView`-based

This preserves:

1. current tag-driven routing
2. dynamic workflow menu population
3. action re-invocation semantics
4. current content-host model

### Icons

Replace `FontIcon` and `FontIconSource` with one of these official approaches:

1. `TextBlock` bound to the existing icon font and glyph constants
2. `PathIcon` if the project later wants geometry-based icons

For XerahS, the first option is the lower-risk migration because the existing icon font and glyph constants are already established.

### Settings-style sections

Replace `SettingsExpander` with:

1. `Expander`
2. custom header templates
3. styled clickable rows built from `Button`, `Border`, `Grid`, and `TextBlock`

The current `AboutView` usage is a good fit for this because it is structurally simple:

1. section header
2. section icon
3. repeated clickable rows
4. optional trailing action icon

### Confirmation dialog

Replace the single `ContentDialog` usage with:

1. a lightweight custom `Window` using `ShowDialog<TResult>`
2. or reuse the existing modal overlay pattern already present in [src/desktop/app/XerahS.UI/Views/MainWindow.axaml](../../../../src/desktop/app/XerahS.UI/Views/MainWindow.axaml)

Because there is currently only one direct `ContentDialog` call site, this part is low risk.

## Migration Plan

### Stage 1: Replace shell controls first

1. add a custom navigation model for hierarchical items
2. replace `NavigationView` in `MainWindow`
3. restyle `TreeView` and `TreeViewItem` to match the existing left pane
4. preserve current `Tag`-based routing and dynamic workflow submenu behavior

Expected result:

1. the largest `FluentAvaloniaUI` dependency is removed first
2. routing logic remains mostly intact
3. package removal is not yet attempted

### Stage 2: Replace small remaining controls

1. replace `FontIcon` and `FontIconSource`
2. replace `SettingsExpander`
3. replace `CommandBarSeparator`
4. replace `ContentDialog`

Expected result:

1. no control-level dependency on `FluentAvalonia.UI.Controls`

### Stage 3: Theme audit

Audit `XerahS.UI` for theme/resource keys currently expected from `FluentAvaloniaTheme`.

Important note:

The current investigation verified the explicit `FluentAvaloniaTheme` usage, but it did not prove that every current brush/style key has an equivalent in official Avalonia Fluent without compatibility work.

That means theme removal should happen only after a resource audit.

### Stage 4: Remove package

Only after the control migration and resource audit:

1. remove `FluentAvaloniaUI` package reference
2. replace `sty:FluentAvaloniaTheme` with official Fluent theme configuration
3. run full UI regression checks

## Risks

1. The biggest risk is theme/resource compatibility, not navigation behavior.
2. The current shell depends on hierarchical selection, expansion, and action re-invocation. Those behaviors are straightforward to recreate, but they will need explicit code instead of built-in `NavigationView` events.
3. `AboutView` currently benefits from `SettingsExpander` convenience templates, so some styling work will move into local XAML themes.
4. If `FluentAvaloniaTheme` currently provides resource keys used widely across the app, removing it before the audit will cause visual regressions.

## Recommendation

Proceed if the goal is:

1. removing the unofficial dependency
2. reducing Avalonia version-skew risk
3. owning the app shell in first-party XAML and code

Do not frame this as "replace one nav control and delete a package."

The correct framing is:

1. replace the shell with official Avalonia primitives
2. replace a handful of convenience controls
3. audit theme dependencies
4. remove the package last

## Verification Plan

1. `dotnet build src\\desktop\\app\\XerahS.UI\\XerahS.UI.csproj -m:1`
2. `dotnet build src\\desktop\\XerahS.sln -m:1`
3. manual verification:
   - left navigation selection
   - expand/collapse behavior
   - capture submenu population
   - repeated invoke behavior for action items
   - page host switching
   - `AboutView` expandable link sections
   - workflow confirmation dialog

## Official Sources Used For This XIP

1. Avalonia themes overview
   - <https://docs.avaloniaui.net/docs/basics/user-interface/styling/themes/>
   - key finding: official Avalonia ships built-in Fluent and Simple themes
2. Avalonia `SplitView`
   - <https://docs.avaloniaui.net/docs/reference/controls/splitview>
   - key finding: official `SplitView` supports collapsible left-pane layouts and compact pane patterns
3. Avalonia `TreeView`
   - <https://docs.avaloniaui.net/docs/reference/controls/treeview-1>
   - <https://api-docs.avaloniaui.net/docs/T_Avalonia_Controls_TreeView>
   - key finding: official `TreeView` supports hierarchical items, templating, and selection
4. Avalonia `Expander`
   - <https://docs.avaloniaui.net/docs/reference/controls/expander>
   - <https://api-docs.avaloniaui.net/docs/T_Avalonia_Controls_Expander>
   - key finding: official `Expander` covers collapsible grouped sections
5. Avalonia `TransitioningContentControl`
   - <https://api-docs.avaloniaui.net/docs/T_Avalonia_Controls_TransitioningContentControl>
   - key finding: official animated content switching is available without third-party controls
6. Avalonia icon guidance and `PathIcon`
   - <https://docs.avaloniaui.net/docs/guides/graphics-and-animation/how-to-use-icons>
   - <https://docs.avaloniaui.net/docs/reference/controls/path-icon>
   - key finding: official guidance supports icon fonts and `PathIcon`
7. Avalonia `Separator`
   - <https://api-docs.avaloniaui.net/docs/T_Avalonia_Controls_Separator>
   - key finding: official separator support is available for toolbar-style grouping
8. Avalonia modal dialogs via `Window.ShowDialog`
   - <https://api-docs.avaloniaui.net/docs/M_Avalonia_Controls_Window_ShowDialog__1>
   - <https://api-docs.avaloniaui.net/docs/T_Avalonia_Controls_Window>
   - key finding: official modal dialog windows are available without `ContentDialog`
9. Avalonia controls namespace inventory
   - <https://api-docs.avaloniaui.net/docs/N_Avalonia_Controls>
   - key finding: no `NavigationView`, `ContentDialog`, `SettingsExpander`, or `FontIcon` entry was found in the official `Avalonia.Controls` namespace during this investigation
