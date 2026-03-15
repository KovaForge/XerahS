# XerahS Lessons Learnt

This document serves as a centralized knowledge base for technical challenges, architectural decisions, and platform-specific quirks encountered during the development of XerahS.

When a task produces a durable correction or preventive rule, capture it here or in the closest topic-specific lessons file using this format:

```md
- Never ...; always ... because ...
```

Promote only repository-wide policy changes to `AGENTS.md`.

## table of Contents

1.  [UI & Theming](#ui--theming)
2.  [Build & Configuration](#build--configuration)
3.  [Plugin System](#plugin-system)
4.  [Android / Avalonia](#android--avalonia)

---

## UI & Theming

- Never fix post-migration dark-surface regressions one view at a time; always start by fixing the first painted host surface (`SurfaceWindow` / `PageView`) and use a separate `OverlayWindow` base for transparent cases, then extend `src/desktop/app/XerahS.UI/Themes/ThemeResources.axaml` only for missing neutral compatibility brushes because Avalonia templates can still fall back to black even when child layouts look correct.
- Never assume explicit `TextBox.Background` is enough for read-only previews; always map the `TextControl*ReadOnly` and related Fluent resource keys in `src/desktop/app/XerahS.UI/Themes/ThemeResources.axaml` because Avalonia's read-only text templates can bypass the normal editable text brushes and fall back to black.
- Never use outer `Margin` on the first child of a `UserControl` to create themed gutters; always use a painted root `Border` with `Padding` because `UserControl` itself does not own a background and transparent gutter space will fall through to the host surface.
- Never rely on `VerticalScrollBarVisibility="Visible"` by itself when a scrollbar must stay fully shown; always pair it with `AllowAutoHide="False"` and prefer setting that once in `src/desktop/app/XerahS.UI/Themes/ThemeResources.axaml` because the Fluent `ScrollViewer` template can still collapse the bar until hover.
- Never rely on `Classes="accent"` being added manually to every new button; always make accent the default in `src/desktop/app/XerahS.UI/Themes/ThemeResources.axaml` and use semantic opt-out classes such as `NoAccent`, `SettingsRow`, or `ColorSwatchButton` because Avalonia Fluent keeps ordinary buttons neutral unless the app supplies a shared default.
- Never duplicate semantic control classes like `section-header`, `caption`, `readonly`, or status colors inside individual views; always define them once in `src/desktop/app/XerahS.UI/Themes/ThemeResources.axaml` and back them with palette tokens in `ShareX.ImageEditor/src/ShareX.ImageEditor/Presentation/Theming/ShareXTheme.axaml` because local copies stop whole-app theme changes from propagating consistently.
- Never bind workflow-edit dialogs directly to the live `WorkflowSettings` instance; always edit a working copy, apply it only on `OK`, and show the real job separately from the custom description because otherwise `Cancel` is not real and workflow names can silently drift away from the task they actually execute.

### ContextMenu vs. ContextFlyout

**Issue**: The old warning against `ContextMenu` was specific to `FluentAvaloniaTheme`. XerahS now uses the official Avalonia `FluentTheme`, so standard `ContextMenu` rendering is no longer blocked by that theme-specific limitation.

**Solution**: Use `ContextMenu` for ordinary context menus. Keep `ContextFlyout` with `MenuFlyout` for cases that need richer flyout behavior, shared popup content, or a flyout attached to a non-standard host.

**❌ Incorrect**:
```xml
<!-- Standard ContextMenu (may be invisible) -->
<Border.ContextMenu>
    <ContextMenu>
        <MenuItem Header="Action" Command="{Binding MyCommand}"/>
    </ContextMenu>
</Border.ContextMenu>
```

**✅ Correct**:
```xml
<!-- Use ContextFlyout with MenuFlyout -->
<Border.ContextFlyout>
    <MenuFlyout>
        <MenuItem Header="Action" Command="{Binding MyCommand}"/>
    </MenuFlyout>
</Border.ContextFlyout>
```

### Binding in DataTemplates with Flyouts

**Issue**: When using `ContextFlyout` or `ContextMenu` inside a `DataTemplate`, bindings to the parent logic (ViewModel) fail because Popups/Flyouts exist in a separate visual tree, detached from the `DataTemplate`'s hierarchy.

**Solution**: Use the `$parent[UserControl]` reflection binding syntax to reach the main view's DataContext.

```xml
<DataTemplate x:DataType="local:MyItem">
    <Border>
        <Border.ContextFlyout>
            <MenuFlyout>
                <!-- Bind to parent UserControl's DataContext -->
                <MenuItem Header="Edit" 
                          Command="{Binding $parent[UserControl].DataContext.EditCommand}"
                          CommandParameter="{Binding}"/>
            </MenuFlyout>
        </Border.ContextFlyout>
    </Border>
</DataTemplate>
```

**Key Points**:
- Use `$parent[UserControl].DataContext` to access the View's ViewModel from within a flyout.
- `CommandParameter="{Binding}"` passes the current data item (the DataTemplate's DataContext).
- For shared flyouts, define them in `UserControl.Resources` and reference via `{StaticResource}`.

### WebView Helper

**Context**: Rendering HTML content within the application (e.g., for Indexer previews).

**Issue**: The standard `WebView.Avalonia` package is insufficient on its own for desktop applications. It provides the controls but may lack the necessary desktop-specific native bindings or initialization logic required for Windows/Linux/macOS.

**Solution**: You must reference **`WebView.Avalonia.Desktop`** in addition to the base package.

**❌ Incorrect**:
```xml
<PackageReference Include="WebView.Avalonia" Version="11.0.0.1" />
```

**✅ Correct**:
```xml
<PackageReference Include="WebView.Avalonia" Version="11.0.0.1" />
<PackageReference Include="WebView.Avalonia.Desktop" Version="11.0.0.1" />
```

Without the `.Desktop` package, the `WebView` control may fail to initialize or render, often silently or with generic "type not found" errors when using reflection to locate it.

### RegionCapture and ImageEditor Resource Contracts

- Never leave RegionCapture UI smoke coverage at compile-only; always load `AnnotationToolbar` and `OverlayWindow` in Avalonia headless tests because ImageEditor submodule updates can break icon/font resources at runtime without breaking the build.
- Never use Avalonia's fake headless drawing for icon-font smoke tests; always use Skia-backed headless mode (`UseSkia()` and `UseHeadlessDrawing = false`) because glyph resource failures only surface when the font pipeline is actually exercised.
- Never let feature work alter or bypass existing `ShareX.ImageEditor` theme resources, variants, or bindings unless the task explicitly targets them; always treat theme behavior and visual resource contracts as non-regression requirements because unrelated UI changes can silently break dark/light presentation across the editor.
- Never collapse Linux modern region-capture failure and user cancellation into the same `null` outcome; always preserve cancellation separately and fall back to the XerahS overlay only for unsupported or failing backends because otherwise `UseModernCapture=true` can block X11 region capture on older desktops.
- Never force `UseModernCapture=false` for every Linux `CaptureRectAsync`; always scope that downgrade to the overlay fallback flow because direct rect capture on capable X11 desktops should preserve the native portal path.
- Never move the XDG portal to the front of every X11 region-capture waterfall; always require a desktop-native backend signal (for example KDE, GNOME, LXQt, or XApp) because generic GTK-backed X11 portal sessions can still hang or misroute captures.
- Never define Tmds.DBus proxy interfaces as nested or inaccessible types; always expose them as top-level public interfaces because the dynamic proxy assembly cannot implement inaccessible interfaces.
- Never trust region-capture modifier updates to key events alone; always resample the current `KeyModifiers` from pointer movement/release while dragging because modifier-only transitions can be missed under pointer capture and leave the selection geometry stuck in the wrong mode.
- Never advertise Linux selector modes that the current session cannot actually honor, and never let an explicit selector silently fall through to a different interactive backend; always filter the UI using live selector diagnostics and keep `Automatic` as the only cross-backend fallback mode because otherwise specific selector choices become misleading and bug reports get polluted by fallback behavior.


---

## Build & Configuration

### Windows TFM & CsWinRT Behavior (Net10.0-windows)

**Context**: When implementing modern Windows features using `Microsoft.Windows.CsWinRT` in a project targeting .NET 8/9/10.

**Issue**: Using the generic `net10.0-windows` TFM combined with a separate `<TargetPlatformVersion>10.0.19041.0</TargetPlatformVersion>` property works for **individual** project builds but fails during **full solution** builds with "Windows Metadata not provided" errors. This is due to a transitive dependency resolution issue in the CsWinRT targets file.

**Solution**: Use the **explicit TFM** string which combines the framework and the platform version.

**❌ Incorrect configuration for solution builds**:
```xml
<TargetFramework>net10.0-windows</TargetFramework>
<TargetPlatformVersion>10.0.19041.0</TargetPlatformVersion>
```

**✅ Correct configuration**:
```xml
<TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
```

This forces the build system to include the correct Windows SDK reference assemblies natively, avoiding the metadata resolution failure. This is required for reliable solution-wide builds when using WinRT APIs like `Windows.Graphics.Capture`.

- Never assume `npm ci` can always clear `ShareX.VideoEditor/frontend/node_modules` on Windows; always delete that folder and rerun the build when `ENOTEMPTY` appears because file locks in `node_modules` can make the first clean fail even though the project itself is valid.
- Never let `XerahS.App` or `XerahS.CLI` publish transitive `ShareX.VideoEditor/frontend/dist` assets directly; always remove those `ResolvedFileToPublish` entries and copy the Web UI once after `Publish` because duplicate Video Editor frontend publish items trigger `NETSDK1152` on Windows and macOS release packaging.

---

## Plugin System

### Pure Dynamic Loading

**Context**: Implementing a plugin architecture where extensions are loaded at runtime without compile-time references.

**Lessons Learned**:
1.  **Don't mix paradigms**: Attempts to mix static compilation (direct project references) with dynamic loading (`AssemblyLoadContext`) cause type identity conflicts. Types loaded via ALC are distinct from the "same" types loaded via normal reference, even if the DLL is identical.
2.  **Keep contexts alive**: The `PluginLoader` must maintain a static reference to the created `AssemblyLoadContexts`. If these are garbage collected, the plugin assemblies will be unloaded, causing crashes or missing functionality.
3.  **Share framework dependencies**: Plugins must not ship with their own copies of framework assemblies (e.g., `Avalonia.dll`, `CommunityToolkit.Mvvm`). The `PluginLoadContext` must be configured to return `null` for these shared assemblies, forcing the runtime to resolve them from the Host application's context. This ensures that `Plugin.Button` is compatible with `Host.Button`.
4.  **Templating limitations**: In Avalonia, overriding `ControlTemplate` in a plugin requires careful Command wiring, as standard resource lookup chains may specific to the load context.
5.  **Plugin TFM must match Host TFM**: Plugin projects must use the **exact same Target Framework Moniker (TFM)** as the host application. If the host targets `net10.0-windows10.0.19041.0` on Windows, plugins must also use conditional TFM matching:

    ```xml
    <!-- Plugins must match host TFM exactly -->
    <TargetFramework Condition="'$(OS)' == 'Windows_NT'">net10.0-windows10.0.19041.0</TargetFramework>
    <TargetFramework Condition="'$(OS)' != 'Windows_NT'">net10.0</TargetFramework>
    ```

    **Why**: Plugin build targets that copy outputs to the host's bin folder (e.g., `$(TargetFramework)\Plugins\`) will use the plugin's TFM in the path. If the plugin targets `net10.0` but the host outputs to `net10.0-windows10.0.19041.0`, plugins end up in the wrong folder and fail to load at runtime. This causes provider settings UI to not appear.

---

## Android / Avalonia

### Avalonia Android: App Stuck at "Initializing..." or Blank Screen

**Context**: XerahS.Mobile.Ava (Avalonia UI on Android) showed a perpetual loading screen or blank screen even though initialization and navigation logic ran correctly.

**Root cause**: In `MainActivity.OnCreate`, code was setting `parent.Content = null` where `parent` was the host `ContentControl` that contains Avalonia's `MainView`. That removed the entire Avalonia UI from the visual tree, so nothing (loading view or main view) was visible.

**Lesson**: Do **not** clear the content of the control that hosts `ISingleViewApplicationLifetime.MainView`. If the app seems stuck on loading or blank but logs show init and navigation completing, look for platform code (e.g. in the Activity) that modifies the host's `Content`.

**MAUI**: MAUI has no equivalent host-Content bug. For MAUI white screen / loading not visible, defer starting `InitializeCoreAsync` by ~150 ms in `MainActivity.OnCreate` so the loading page can render before background init runs. See [android_avalonia_init_fix.md](android_avalonia_init_fix.md#maui-equivalent-no-host-content-bug).

---

## Image / Preview Ownership

### Clone Task Bitmaps Before `UpdatePreview`

**Context**: `ShareX.ImageEditor.Presentation.ViewModels.MainViewModel.UpdatePreview` is used to show captured task images in the desktop editor surface.

**Lesson**: Treat `UpdatePreview` as an ownership transfer. Do not read from or keep sharing the same `SKBitmap` after calling it. `UpdatePreview` can trigger property-change flows that dispose or replace the supplied bitmap during the same call. When the source bitmap still belongs to a task or another component, clone it first and hand the clone to the view model.

### ImageEditor Host Export Wiring

- Never partially wire a hosted component's host-facing commands/events; always audit the full host contract and connect every supported action because UI enablement and behavior can depend on subscriber presence, making omissions look like broken features instead of integration gaps.
- Never put OS-specific wallpaper lookup inside `ShareX.ImageEditor` view models; always expose it through `ShareX.ImageEditor.Hosting` and implement the real lookup in `XerahS.Platform.Abstractions` because the editor is shared across hosts and platforms.
- Never use the XerahS `[vX.Y.Z]` commit prefix when committing inside `ShareX.ImageEditor` or other shared library submodules; always use `[Type] Use concise description` there because those libraries are versioned independently of the XerahS app.

---

## Linux Capture UX

### Separate Linux Selector Preference From `UseModernCapture`

**Context**: Linux region capture can succeed through several different interactive selectors depending on the session stack: XerahS overlay, XDG portal, desktop-native D-Bus selectors, or `slurp`.

**Lesson**: Do not treat `UseModernCapture` as the only Linux UX decision. Keep it as the broad capture-engine toggle, but layer any user-facing Linux selector choice on top as a more specific preference. Runtime code should:

- allow explicit selector preferences to opt into a native selector even when `UseModernCapture` is off for the general workflow,
- preserve safe overlay fallback on X11 when the chosen native path is unavailable or fails,
- stamp overlay follow-up rect/fullscreen captures with `LinuxRegionSelectorPreference = XerahSOverlay` so later Linux crop steps stay on the legacy path instead of accidentally re-entering portal/native logic,
- expose live diagnostics in the UI (`session`, `portal backend`, `available selectors`, `automatic will prefer`) so users can make informed choices without understanding the full Linux capture stack.

### Drain Portal Hotkey Rebind Work Before Dispose

**Context**: Editing workflows or hotkeys on Wayland can trigger debounce-driven portal rebinds while the `WaylandPortalHotkeyService` is also being torn down.

**Lesson**: Never dispose portal hotkey D-Bus state while debounce or rebind work can still be running. Mark the service as disposed first, cancel the debounce token, and wait for in-flight rebind tasks to drain before releasing the connection, session, or semaphore. Otherwise workflow edits can surface unobserved `ObjectDisposedException` failures against `Tmds.DBus.Connection`.
