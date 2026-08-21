# Avalonia Compiled Binding Guidelines

These rules are required for `XerahS.UI` and `ShareX.ImageEditor` presentation XAML.

## Required Rules

1. Enable compiled bindings by default at project level (`AvaloniaUseCompiledBindingsByDefault=true`).
2. Every `DataTemplate` and `TreeDataTemplate` must declare `x:DataType`.
3. If a template is intentionally dynamic, declare `x:DataType="x:Object"` and use `ReflectionBinding` only for the dynamic path.
4. Do not add broad `x:CompileBindings="False"` at view root scope.
5. Keep dynamic opt-outs as narrow as possible and document why near the binding.

## Menu/Flyout Binding Pattern

For flyout/popover bindings that reference parent view models, prefer:

- strongly typed item templates (`x:DataType` on the menu/template item), and
- `ReflectionBinding` only on parent `DataContext` hops that are runtime-only.

## Local Verification Before Push

Run these commands from repository root:

```bash
python3 build/ci/check_compiled_bindings_guardrails.py --repo-root .
dotnet build src/desktop/app/XerahS.UI/XerahS.UI.csproj -warnaserror
dotnet build ShareX.ImageEditor/src/ShareX.ImageEditor/ShareX.ImageEditor.csproj -warnaserror
```

## Debugging Binding Issues

- Keep `BuildAvaloniaApp().LogToTrace()` enabled for development diagnostics.
- Treat AVLN binding diagnostics as build blockers in migrated surfaces.
- Prefer fixing type scopes over suppressing with global reflection fallbacks.

