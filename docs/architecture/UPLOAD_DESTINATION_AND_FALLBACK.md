# Upload destination and fallback behavior

This note describes how XerahS chooses uploaders for a workflow destination, and where behavior differs between code paths.

## Auto vs a fixed destination

- **Auto** (the Auto provider in Destination Settings): The upload pipeline calls `TryUploadWithFallback`, which walks configured non-Auto instances in priority order for the relevant category, then may try other categories (for example Image to File). Debug output uses the `[UploadFallback]` prefix.
- **Any other destination**: Normally only that `UploaderInstance` is used. If it is missing or invalid, the app may fall back to the category default or show a configuration error, but it does not silently try unrelated uploaders the way Auto does.

## Image uploads: general upload vs capture upload

Behavior is **not identical** for image uploads:

| Path | Class / method | Fixed (non-Auto) image destination fails | Notes |
|------|----------------|------------------------------------------|--------|
| General uploads (clipboard, file drop, etc.) | `UploadJobProcessor.UploadWithPluginSystem` | After the primary image uploader fails, the code calls `TryUploadWithFallback` for other image uploaders (excluding the one that already failed). | Intended to recover when the chosen host is down while other image uploaders are configured. |
| Screen capture upload | `CaptureJobProcessor.TryUploadWithPluginSystem` | Returns the failure from `TryUploadWithInstance` only; **no** post-failure fallback to other image uploaders. | Stricter: the workflow’s destination is the only attempt. |

Both paths still use **Auto** the same way: `TryUploadWithFallback` from the start when Auto is selected.

## Related UI

The workflow editor **Task Settings → Upload → Destinations** combo documents Auto vs fixed destinations in a tooltip; see `TaskSettingsPanel.axaml`.

## Source references

- `src/desktop/core/XerahS.Core/Tasks/Processors/UploadJobProcessor.cs` — `UploadWithPluginSystem`, `TryUploadWithFallback`
- `src/desktop/core/XerahS.Core/Tasks/Processors/CaptureJobProcessor.cs` — `TryUploadWithPluginSystem`, `TryUploadWithFallback`
