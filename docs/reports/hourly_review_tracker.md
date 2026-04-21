# XerahS Hourly Review Tracker

Purpose: persistent coverage ledger for the hourly XerahS review cron.

Use this file to avoid re-reviewing the same subsystem blindly, track findings, and surface stale/high-risk areas that still need attention.

## Rules

- Every hourly review run must read this file before choosing an area.
- Prefer the stalest unreviewed or least-recently-reviewed subsystem that is not currently blocked by a larger prerequisite.
- After each run, append a new entry with the area reviewed, files inspected, findings, and whether a fix was landed.
- If no fix is landed, record the concrete blocker, not a vague "no issues found".
- Small safe fixes are preferred over broad speculative refactors.

## Coverage Table

| Area | Last Reviewed | Status | Last Outcome | Priority | Notes |
|---|---|---|---|---|---|
| Capture pipeline | 2026-04-21 18:23 GMT+8 | Reviewed | Fixed screen-recorder encoder dimension normalization so 1px region captures no longer collapse to 0x0 and break recording startup | High | Reviewed `ScreenRecorderService` capture sizing and start/stop test coverage; Release build passed with 0 warnings/errors, targeted screen-recorder tests passed, and full `dotnet test --configuration Release` now reports 8 unrelated existing failures in editor, preset-serialization, Linux portal policy, and region-capture UI smoke |
| OCR | 2026-04-21 06:40 GMT+8 | Reviewed | Fixed assistant clipboard-dependent OCR and copy actions to return recoverable errors with copy-back actions instead of throwing when clipboard services are unavailable | High | Follow-up review of assistant OCR/copy flows after the earlier OCR-options fix; Release build passed, targeted assistant tests passed, and full `dotnet test` still reports 10 unrelated existing failures |
| Editor integration | 2026-04-21 14:46 GMT+8 | Reviewed | Fixed history editor tests to disable constructor auto-load and preserve annotation sidecar metadata when refreshed items are cloned | High | Follow-up review of Avalonia editor session flow, history refresh, and headless UI service paths; full Release build passed with 0 warnings/errors, targeted history editor tests passed, and full `dotnet test --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false` still reports 9 unrelated existing failures |
| Uploader core | 2026-04-21 15:11 GMT+8 | Reviewed | Fixed legacy uploader-instance normalization so null file-type routing data no longer crashes duplication, validation, or destination selection flows | High | Reviewed plugin-system instance duplication/routing/config flows with legacy/null `FileTypeRouting` states; targeted InstanceManager regressions passed, full `dotnet test --no-build` now reports 9 unrelated existing failures, and full Release build hit SIGKILL/OOM pressure in this environment |
| Nextcloud uploader plugin | 2026-04-21 07:13 GMT+8 | Reviewed | Fixed credential-clear state reset so stale server profile metadata and capability flags no longer survive after disconnecting a Nextcloud account | Medium | Follow-up review of config view-model state reset after the earlier uploader/client pass; targeted Nextcloud tests passed, while full Release build/test hit SIGKILL/OOM pressure in this environment |
| FTP uploader plugin | 2026-04-21 20:09 GMT+8 | Reviewed | Fixed config JSON loading so missing or invalid FTP/FTPS/SFTP ports normalize back to protocol defaults instead of deserializing to 0 and failing validation | Medium | Reviewed config model/view-model load, validation, and protocol default handling; Release plugin build passed with 0 warnings/errors, targeted FTP config tests passed 7/7, and full `dotnet test --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false` still reports 8 unrelated existing failures in editor overlay/context-menu/schema-driven dialog coverage, image-effect preset serialization, Linux portal capture policy, and region-capture UI smoke |
| Imgur uploader plugin | 2026-04-21 22:09 GMT+8 | Reviewed | Fixed Imgur config loading so invalid enum values normalize back to safe UI defaults instead of leaving the view-model with out-of-range selection indexes | Medium | Reviewed config model/view-model plus uploader retry paths; Release build passed with 0 warnings/errors, targeted Imgur tests passed 5/5, and full `dotnet test --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false` still reports 8 unrelated existing failures in editor overlay/context-menu/schema-driven dialog coverage, image-effect preset serialization, Linux portal capture policy, and region-capture UI smoke |
| Settings/configuration | 2026-04-21 19:09 GMT+8 | Reviewed | Fixed custom uploader/workflow config path resolution so relative folders are normalized to absolute paths and whitespace-only overrides fall back to the default settings folder | High | Follow-up review of settings path resolution after the reset-path fix; Release build passed with 0 warnings/errors, targeted settings-path tests passed, and full `dotnet test --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false` still reports 8 unrelated existing failures in editor, preset-serialization, Linux portal policy, and region-capture UI smoke |
| Hotkeys/input | 2026-04-21 23:18 GMT+8 | Reviewed | Fixed hotkey edit/re-register cleanup so a failed native unregister no longer wipes the still-active hotkey's runtime trigger description before the old binding is actually released | Medium | Reviewed hotkey registration/unregistration and edit-time cleanup failure paths; Release build passed with 0 warnings/errors, targeted hotkey+Imgur regressions passed 11/11, and full `dotnet test --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false` still reports 8 unrelated existing failures in editor overlay/context-menu/schema-driven dialog coverage, image-effect preset serialization, Linux portal capture policy, and region-capture UI smoke |
| Notifications/toasts | 2026-04-21 01:45 GMT+8 | Reviewed | Fixed toast middle-click routing so non-drag middle releases now trigger the configured middle-click action instead of being ignored | Low | Reviewed native notification services plus Avalonia/headless toast paths; Release build passed, targeted toast/notification tests passed, and full `dotnet test` still reports 14 unrelated existing failures |
| Plugin loading/runtime | 2026-04-21 21:09 GMT+8 | Reviewed | Fixed custom uploader repository discovery cleanup so deleted or newly invalid definition files are evicted from the runtime cache instead of surviving until manual reload | High | Reviewed plugin discovery/loader/provider catalog plus custom-uploader repository caching paths; Release build passed with 0 warnings/errors, targeted custom-uploader/plugin-binding tests passed, and full `dotnet test --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false` still reports 8 unrelated existing failures in editor overlay/context-menu/schema-driven dialog coverage, image-effect preset serialization, Linux portal capture policy, and region-capture UI smoke |
| CLI / command surface | 2026-04-22 00:12 GMT+8 | Reviewed | Fixed CLI upload filename sanitization so `--name` values containing directory segments or `.` / `..` collapse to a safe leaf filename or fallback instead of leaking invalid path fragments into temp upload paths | Medium | Reviewed `UploadCommand` temp-file naming and CLI upload path handling; Release build passed with 0 warnings/errors, targeted upload-path regressions passed 9/9, and full `dotnet test --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false` still reports 8 unrelated existing failures in editor overlay/context-menu/schema-driven dialog coverage, image-effect preset serialization, Linux portal capture policy, and region-capture UI smoke |
| Platform-specific services | 2026-04-21 04:31 GMT+8 | Reviewed | Fixed macOS window search/foreground-handle reporting so matching the front window returns a usable non-zero sentinel handle instead of a false negative | Medium | Reviewed macOS window service front-window parsing/search/handle semantics; Release build passed, targeted macOS window tests passed, and full `dotnet test` still reports 14 unrelated existing failures |
| File/path handling | 2026-04-21 01:10 GMT+8 | Reviewed | Fixed screenshots parent-folder resolution so custom paths are normalized/expanded consistently, including `%TEMP%` and `%TMP%` tokens on Linux | High | Reviewed screenshot path resolution, folder-variable expansion, and settings-driven custom path flows; Release build passed, targeted screenshots-path tests passed, and full `dotnet test --no-build` still reports 14 unrelated existing failures |
| Tests / test discoverability | 2026-04-21 05:31 GMT+8 | Reviewed | Fixed the workflow-editor test factory to create a real `TaskSettingsViewModel`, removing three false-negative workflow editor failures from the Release suite | High | Reviewed test double/view-model construction paths; targeted workflow-editor regression tests pass, full `dotnet test --no-build` now reports 395 tests with 10 remaining unrelated failures, and full-solution Release build still hits SIGKILL/OOM pressure in this environment |

## Review Log

### 2026-04-22 00:12 GMT+8
- Area: CLI / command surface
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/cli/XerahS.CLI/Commands/UploadCommand.cs`
  - `src/desktop/core/XerahS.Common/Helpers/FileHelpers.cs`
  - `src/desktop/cli/XerahS.CLI/Properties/AssemblyInfo.cs`
  - `tests/XerahS.Tests/Tools/UploadCommandPathSanitizationTests.cs`
- Findings:
  - `UploadCommand` trusted `--name` too literally when staging temp upload files for `--text`, `--pipe`, or rename-on-upload flows.
  - Names like `nested/path/report.png`, `.` and `..` could propagate path fragments or invalid leaf values into temp staging logic, creating misleading filenames and opening the door to path-shape edge cases.
  - The regression tests needed explicit CLI internals visibility because the helper methods are intentionally non-public.
- Outcome:
  - Landed a bounded fix so upload temp-file staging now strips directory segments with `Path.GetFileName`, sanitizes only the leaf name, and falls back to the original filename when the sanitized result is empty, `.` or `..`.
  - Added focused regression coverage for null/blank/dot-segment names, nested path inputs, and the temp upload path staying inside the unique `xerahs-upload` temp directory.
  - Added `InternalsVisibleTo("XerahS.Tests")` for `XerahS.CLI` so the regression can exercise the non-public helper directly without widening the production API.
- Verification / blockers:
  - Parent repo upstream remained current this run: 0 upstream `develop` commits pending and no merge conflicts occurred.
  - `ShareX.ImageEditor` remotes were re-verified (`origin` = KovaForge, `upstream` = ShareX); the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, not detached, and no submodule commit or parent pointer update was required.
  - `dotnet build --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings and 0 errors.
  - Targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false --filter "FullyQualifiedName~UploadCommandPathSanitizationTests"` passed with 9/9 tests green.
  - Full `dotnet test --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false` still reports 8 unrelated existing failures in `EditorCloseConfirmationTests`, `EditorContextMenuSmokeTests`, `SchemaDrivenFilterCatalogTests`, `ImageEffectPresetSerializationTests`, `PortalCapturePolicyTests`, and `RegionCaptureUiSmokeTests`; those pre-existing suites are outside this bounded CLI upload fix.
- Follow-up:
  - The next CLI pass should inspect upload JSON/error output consistency and whether upload temp staging needs a dedicated helper/service shared with any future batch-upload path.

### 2026-04-21 23:18 GMT+8
- Area: Hotkeys/input
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/core/XerahS.Core/Hotkeys/WorkflowManager.cs`
  - `src/desktop/core/XerahS.Core/Hotkeys/HotkeySettings.cs`
  - `tests/XerahS.Tests/Hotkeys/WorkflowManagerTests.cs`
- Findings:
  - `WorkflowManager.RegisterHotkey` cleared `NativeTriggerDescription` before attempting to unregister an existing native binding.
  - If that unregister failed during an in-place hotkey edit, the old OS-level binding could still be active but the workflow lost the runtime metadata the UI uses to describe the live trigger.
  - The existing unregister-failure regression covered explicit removal, but not edit-and-reregister cleanup failures.
- Outcome:
  - Landed a bounded fix so re-registration aborts immediately when cleanup fails, preserving the existing mapping and trigger description until the old native binding is actually released.
  - Added a focused regression test proving failed cleanup during hotkey edits keeps the workflow mapped and its runtime trigger description intact.
- Verification / blockers:
  - Parent repo merged 1 upstream `develop` commit this run (`14f0687b`, docs/blog only) with no conflicts.
  - `ShareX.ImageEditor` remotes were re-verified (`origin` = KovaForge, `upstream` = ShareX); the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, not detached, and no submodule commit or parent pointer update was required.
  - `dotnet build --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings and 0 errors.
  - Targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false --filter "FullyQualifiedName~WorkflowManagerTests|FullyQualifiedName~ImgurConfigViewModelTests"` passed with 11/11 tests green.
  - Full `dotnet test --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false` still reports 8 unrelated existing failures in schema-driven editor dialog coverage, image-effect preset serialization, Linux portal capture policy, and region-capture UI smoke tests.
- Follow-up:
  - The next hotkeys pass should inspect duplicate-binding collision UX and whether failed native unregisters need a user-visible retry/recovery path.

### 2026-04-21 22:09 GMT+8
- Area: Imgur uploader plugin
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/plugins/Imgur.Plugin/ViewModels/ImgurConfigViewModel.cs`
  - `src/desktop/plugins/Imgur.Plugin/ImgurConfigModel.cs`
  - `src/desktop/plugins/Imgur.Plugin/ImgurUploader.cs`
  - `tests/XerahS.Tests/Uploaders/ImgurConfigViewModelTests.cs`
- Findings:
  - `LoadFromJson` copied deserialized `AccountType` and `ThumbnailType` enum values directly into combo-box selection indexes.
  - Legacy or hand-edited config JSON can contain out-of-range enum values, which leaves the view-model holding invalid selection indexes instead of falling back to a sane default.
  - That makes the config screen state inconsistent and can persist invalid enum values back into saved config even when the underlying fix is just "use the default option".
- Outcome:
  - Landed a bounded fix so Imgur config load, save, and uploader rebuild paths normalize invalid `AccountType` values back to `Anonymous` and invalid `ThumbnailType` values back to `Medium_Thumbnail`.
  - Added a focused regression test proving malformed JSON enum values are coerced to those safe defaults before reserialization.
- Verification / blockers:
  - Parent repo upstream remains current this run: 0 upstream `develop` commits pending and no merge conflicts occurred.
  - `ShareX.ImageEditor` remotes were re-verified (`origin` = KovaForge, `upstream` = ShareX); the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, not detached, and no submodule commit or parent pointer update was required.
  - `dotnet build --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings and 0 errors.
  - Targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false --filter "FullyQualifiedName~ImgurConfigViewModelTests"` passed with 5/5 tests green.
  - Full `dotnet test --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false` still reports 8 unrelated existing failures in editor overlay/context-menu/schema-driven dialog coverage, image-effect preset serialization, Linux portal capture policy, and region-capture UI smoke tests.
- Follow-up:
  - The next Imgur-area pass should stay off enum normalization and instead inspect album-loading/login UX or upload error-surface handling if the plugin comes up again.

### 2026-04-21 21:09 GMT+8
- Area: Plugin loading/runtime
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/ProviderCatalog.cs`
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoader.cs`
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginDiscovery.cs`
  - `src/desktop/core/XerahS.Uploaders/CustomUploader/CustomUploaderRepository.cs`
  - `tests/XerahS.Tests/CustomUploader/CustomUploaderRepositoryTests.cs`
  - `tests/XerahS.Tests/CustomUploader/CustomUploaderDefinitionBindingServiceTests.cs`
  - `tests/XerahS.Tests/Helpers/PluginConfigurationVerifierTests.cs`
- Findings:
  - `CustomUploaderRepository.DiscoverUploaders` only added valid files to `_loadedUploaders`; it never evicted cached entries when a previously loaded definition file was deleted from disk.
  - The same stale-cache behavior also let files that had become invalid stay resident if a later scan could no longer load them cleanly.
  - That left plugin/runtime state inconsistent with the filesystem and could keep deleted custom uploaders selectable until a separate explicit removal or process restart.
- Outcome:
  - Landed a bounded fix that normalizes scanned file paths, removes invalid scan results from `_loadedUploaders` immediately, and prunes stale cached entries that fall inside the current discovery scope but were not found on disk.
  - Added repository test isolation via `SetUp`/`TearDown` cache clears so discovery tests no longer inherit stale global state.
  - Added a focused regression proving a deleted `.sxcu` file disappears from the repository cache after a fresh discovery scan.
- Verification / blockers:
  - Parent repo upstream remains current this run: 0 upstream `develop` commits pending and no merge conflicts occurred.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX); the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, not detached, and no submodule commit or parent pointer update was required.
  - `dotnet build --configuration Release --no-restore -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings and 0 errors.
  - Targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false --filter "FullyQualifiedName~CustomUploaderRepositoryTests|FullyQualifiedName~CustomUploaderDefinitionBindingServiceTests"` passed with 24/24 tests green.
  - Full `dotnet test --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false` still reports 8 unrelated existing failures in editor overlay/context-menu/schema-driven dialog coverage, image-effect preset serialization, Linux portal capture policy, and region-capture UI smoke tests.
- Follow-up:
  - Next plugin-loading/runtime pass should stay off stale custom-uploader cache cleanup and instead inspect provider-collision handling or reload ordering between built-in and custom providers.

### 2026-04-21 20:09 GMT+8
- Area: FTP uploader plugin
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/plugins/Ftp.Plugin/ViewModels/FtpConfigViewModel.cs`
  - `src/desktop/plugins/Ftp.Plugin/FtpConfigModel.cs`
  - `tests/XerahS.Tests/Uploaders/FtpConfigViewModelTests.cs`
- Findings:
  - `LoadFromJson` copied deserialized `Port` values directly into the view-model.
  - Legacy or partial JSON payloads with a missing `Port` field deserialize to `0`, which immediately fails validation and breaks the intended protocol defaults.
  - The bug was worse for implicit FTPS and SFTP because their expected defaults are protocol-specific, so loading old configs could silently downgrade valid settings into an unusable state.
- Outcome:
  - Landed a bounded fix so `LoadFromJson` now treats `Port <= 0` as missing/invalid and restores the protocol default via `GetDefaultPort(config.Protocol, config.FTPSEncryption)`.
  - Kept FTPS encryption assignment ahead of port normalization so implicit FTPS resolves correctly to port `990`.
  - Added focused regression tests covering missing-port normalization for SFTP and implicit FTPS configs.
- Verification / blockers:
  - Parent repo upstream remains current this run: 0 upstream `develop` commits pending and no merge conflicts occurred.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX); the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, not detached, and no submodule commit or parent pointer update was required.
  - `dotnet build src/desktop/plugins/Ftp.Plugin/XerahS.Ftp.Plugin.csproj --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings and 0 errors.
  - Targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false --filter "FullyQualifiedName~FtpConfigViewModelTests"` passed with 7/7 tests green.
  - Full `dotnet test --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false` still reports 8 unrelated existing failures in editor overlay/context-menu/schema-driven dialog coverage, image-effect preset serialization, Linux portal capture policy, and region-capture UI smoke tests.
- Follow-up:
  - The next FTP-area pass should stay off default-port hydration and instead inspect connection-test/save flows or uploader retry behavior if the subsystem comes up again.

### 2026-04-21 18:23 GMT+8
- Area: Capture pipeline
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/app/XerahS.RegionCapture/ScreenRecording/ScreenRecorderService.cs`
  - `tests/XerahS.Tests/RegionCapture/ScreenRecorderServiceTests.cs`
- Findings:
  - `ScreenRecorderService` normalized encoder dimensions with `width & ~1` and `height & ~1`.
  - That is fine for typical sizes, but it collapses a 1px capture region to `0`, which can hand an invalid 0x0 format to the recorder startup path.
  - The existing stop-path test was also asserting an exception that the service intentionally swallows via `HandleFatalError`, so it was no longer protecting the real default-output-path contract.
- Outcome:
  - Landed a bounded fix that clamps encoder dimensions to a minimum even size of 2 while preserving normal even-dimension normalization for larger captures.
  - Added regression coverage proving a 1x1 region now initializes the encoder as 2x2 instead of 0x0.
  - Tightened the existing default-output-path test to assert the resolved file path is created and stop completes cleanly.
- Verification / blockers:
  - Parent repo upstream remains current this run: 0 upstream `develop` commits pending and no merge conflicts occurred.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX); the submodule remains on branch `develop`, not detached, and no submodule commit or parent pointer update was required.
  - `dotnet build --configuration Release` passed with 0 warnings and 0 errors.
  - Targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false --filter "FullyQualifiedName~ScreenRecorderServiceTests"` passed with 2/2 tests green.
  - Full `dotnet test --configuration Release` still fails with 8 unrelated existing failures in editor overlay/context-menu/schema-driven dialog coverage, image-effect preset serialization, Linux portal capture policy, and region-capture UI smoke tests.
- Follow-up:
  - Next capture-pipeline pass should stay on the remaining region-capture UI/platform smoke failures, not this encoder-dimension edge case, because the tiny-region startup regression is now covered.

### 2026-04-21 19:09 GMT+8
- Area: Settings/configuration
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/core/XerahS.Core/Managers/SettingsManager.cs`
  - `tests/XerahS.Tests/Helpers/SettingsManagerSecretsPathTests.cs`
- Findings:
  - `UploadersConfigFilePath` and `WorkflowsConfigFilePath` only used `ExpandFolderVariables` for custom config folders.
  - Relative custom paths therefore stayed relative, which made config file resolution depend on the process working directory instead of the app base directory used elsewhere by `FileHelpers.GetAbsolutePath`.
  - Whitespace-only custom folder values were also treated as real overrides, producing invalid-looking resolved paths instead of cleanly falling back to `SettingsFolder`.
- Outcome:
  - Landed a bounded fix so custom uploaders/workflows config folders now use `FileHelpers.GetAbsolutePath` and ignore whitespace-only overrides.
  - Added regression coverage proving relative custom folders resolve against `AppDomain.CurrentDomain.BaseDirectory` and whitespace-only values fall back to the default settings folder.
- Verification / blockers:
  - Parent repo upstream remains current this run: 0 upstream `develop` commits pending and no merge conflicts occurred.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX); the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, not detached, and no submodule commit or parent pointer update was required.
  - `dotnet build --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings and 0 errors.
  - Targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false --filter "FullyQualifiedName~SettingsManagerSecretsPathTests"` passed with 6/6 tests green.
  - Full `dotnet test --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false` still reports 8 unrelated existing failures in editor overlay/context-menu/schema-driven dialog coverage, image-effect preset serialization, Linux portal capture policy, and region-capture UI smoke tests.
- Follow-up:
  - The next settings/configuration pass should stay off this path-resolution edge case and instead look at other persistence or migration boundaries if the area comes up again.

### 2026-04-21 15:11 GMT+8
- Area: Uploader core
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/InstanceManager.cs`
  - `tests/XerahS.Tests/Uploaders/InstanceManagerTests.cs`
- Findings:
  - `InstanceManager` assumed every `UploaderInstance` had a non-null `FileTypeRouting` with a non-null `FileExtensions` list.
  - That assumption is unsafe for legacy or manually edited config payloads, because deserialization or external edits can leave `FileTypeRouting` null.
  - Once that happens, duplication, routing validation, destination lookup, and conflict inspection can throw null-reference exceptions before the instance is repaired.
- Outcome:
  - Landed a bounded fix that normalizes configuration and uploader instances on load, add, update, duplication, and routing reads.
  - Added regression coverage proving legacy instances with missing `FileTypeRouting` can now be duplicated and validated without crashing.
- Verification / blockers:
  - Parent repo upstream sync completed with 0 upstream `develop` commits pending and no merge conflicts.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX); the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, not detached, and no parent submodule pointer update was required.
  - Targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false --filter "FullyQualifiedName~InstanceManagerTests"` passed with 3/3 tests green.
  - Full `dotnet test --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false` still reports 9 unrelated existing failures in editor overlay/context-menu/schema-driven filter tests, image-effect preset serialization, and region-capture recording setup.
  - Full solution `dotnet build --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` reached late-stage compilation but was killed by SIGKILL/OOM pressure in this environment before completion, so zero-warning full-build verification could not be re-established this run.
- Follow-up:
  - Keep the next uploader-core pass on migration/backward-compatibility edges, especially any other nullable legacy config fields that still bypass normalization.

### 2026-04-21 14:46 GMT+8
- Area: Editor integration
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/app/XerahS.UI/ViewModels/HistoryViewModel.cs`
  - `tests/XerahS.Tests/Editor/HistoryEditorLaunchTests.cs`
- Findings:
  - `HistoryViewModel` always kicked off asynchronous history loading from its constructor, even in isolated editor-launch tests that were trying to verify a single in-memory `HistoryItem` refresh path.
  - That background load could race the test fixture and mutate `HistoryItems` independently of the editor-session assertions, making the focused regression suite flaky and harder to reason about.
  - The same review also found that `CloneHistoryItem` dropped `AnnotationSidecarPath`, which could discard sidecar linkage when the edited item was replaced after refresh.
- Outcome:
  - Landed a bounded fix by adding an optional `autoLoadHistory` constructor flag so focused tests can instantiate `HistoryViewModel` without starting background history IO.
  - Updated the history editor regression tests to opt out of constructor auto-load.
  - Preserved `AnnotationSidecarPath` when cloning refreshed history items so annotation-backed editor sessions keep their sidecar association after replacement.
- Verification / blockers:
  - Parent repo upstream remained current this run: 0 upstream `develop` commits pending and no merge conflicts occurred.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX); the submodule remains on branch `develop`. No submodule commit or parent pointer update was required.
  - Full solution `dotnet build --configuration Release` passed with 0 warnings and 0 errors.
  - Targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false --filter "FullyQualifiedName~HistoryEditorLaunchTests"` passed with 4/4 tests green.
  - Full solution `dotnet test --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false` still fails with 9 unrelated existing failures in editor overlay/context-menu/schema-driven filter tests, image-effect preset serialization, Linux portal capture policy, and region-capture platform/recording setup.
- Follow-up:
  - Next editor-area pass should tackle the remaining schema-driven dialog and overlay smoke-test failures, but the history editor refresh path is now deterministic and covered.

## Review Log

### 2026-04-21 07:13 GMT+8
- Area: Nextcloud uploader plugin
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/plugins/Nextcloud.Plugin/NextcloudUploader.cs`
  - `src/desktop/plugins/Nextcloud.Plugin/NextcloudClient.cs`
  - `src/desktop/plugins/Nextcloud.Plugin/ViewModels/NextcloudConfigViewModel.cs`
  - `tests/XerahS.Tests/Uploaders/NextcloudConfigViewModelTests.cs`
- Findings:
  - The earlier uploader/client fix left a stale UI-state edge case in the same subsystem: clearing stored credentials only wiped the secrets and basic identity fields.
  - Cached server profile metadata and capability flags, including product name, version, and supported-sharing/chunking/search features, could survive after disconnect, so the config screen still looked like it knew the old server profile even though the account was no longer connected.
  - That stale state also risked misleading users about what a future server connection supports before any fresh profile refresh occurs.
- Outcome:
  - Landed a bounded fix so `ClearStoredCredentials()` now resets the cached Nextcloud profile metadata and all capability flags back to their disconnected defaults before recomputing summaries.
  - Added a regression test covering credential clearing, disconnected summary text, and capability reset behavior.
- Verification / blockers:
  - Parent repo upstream remained current this run: 0 upstream `develop` commits pending and no merge conflicts occurred.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX); the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, and no parent submodule pointer update was required.
  - Full solution `dotnet build --configuration Release` and `dotnet test --configuration Release` both hit SIGKILL/OOM pressure in this environment during the plugin auto-build/test pass, so full-suite verification could not complete this run.
  - Targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false --filter "FullyQualifiedName~NextcloudProviderTests|FullyQualifiedName~NextcloudConfigViewModelTests"` passed with 6/6 tests green.
- Follow-up:
  - Re-run full-solution Release build/test on a roomier runner after the current OOM pressure is addressed; the Nextcloud regression itself is now covered locally.

### 2026-04-21 06:40 GMT+8
- Area: OCR
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/app/XerahS.Assistant/Services/AssistantService.cs`
  - `src/desktop/app/XerahS.Assistant/Routing/AssistantCommandRouter.cs`
  - `tests/XerahS.Tests/Assistant/AssistantServiceTests.cs`
- Findings:
  - Assistant clipboard-backed actions (`CopyText`, latest screenshot path copy, batched history-path copy, and OCR copy mode) called `PlatformServices.Clipboard.SetTextAsync` directly.
  - When clipboard services are unavailable, those paths surfaced an exception-driven failure instead of a recoverable assistant response, which is especially rough for OCR copy because the recognized text was lost unless the user reran the action.
  - This was a justified OCR follow-up even though the area was reviewed recently, because the previous OCR-options fix exposed the missing clipboard failure handling in the same assistant action path.
- Outcome:
  - Landed a bounded fix so assistant clipboard-dependent actions now return a friendly error plus a `CopyText` recovery action carrying the generated payload instead of throwing.
  - Added assistant regression coverage for OCR copy requests when the clipboard is unavailable, and updated the existing OCR-options test fixture to use a known-history file so it exercises the real OCR path under the privacy guard.
- Verification / blockers:
  - Parent repo upstream remained current this run: 0 upstream `develop` commits pending and no merge conflicts occurred.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX); the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, and no parent submodule pointer update was required.
  - Full solution `dotnet build --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings and 0 errors.
  - Targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false --filter "FullyQualifiedName~AssistantServiceTests|FullyQualifiedName~AssistantCommandRouterTests"` passed with 21/21 tests green.
  - Full solution `dotnet test --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false` still fails with 10 unrelated existing failures in editor history refresh, schema-driven editor dialogs, image-effect preset serialization, Linux portal capture policy, and region-capture platform/recording setup.
- Follow-up:
  - Keep the next OCR/assistant pass on conversation-state and tool-routing edge cases only if a new follow-up reason appears; the immediate clipboard failure path is now covered.

### 2026-04-21 05:31 GMT+8
- Area: Tests / test discoverability
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `tests/XerahS.Tests/Xip0052/Xip0052TestDoubles.cs`
  - `tests/XerahS.Tests/Hotkeys/WorkflowEditorViewModelTests.cs`
  - `src/desktop/app/XerahS.UI/ViewModels/WorkflowEditorViewModel.cs`
  - `src/desktop/app/XerahS.UI/ViewModels/TaskSettingsViewModel.cs`
- Findings:
  - `FakeUiViewModelFactory.CreateTaskSettingsViewModel` returned an uninitialized `TaskSettingsViewModel` shell via `RuntimeHelpers.GetUninitializedObject`.
  - That left `_settings` null, so any `WorkflowEditorViewModel` test that changed `SelectedJob` crashed inside `TaskSettingsViewModel.Job` before exercising the real workflow-save behavior.
  - The bounded safe fix was to construct a real `TaskSettingsViewModel` with the fake dialog service so the workflow editor tests run against initialized state instead of a broken test double.
- Outcome:
  - Landed a bounded test-fixture fix so `CreateTaskSettingsViewModel` now returns `new TaskSettingsViewModel(settings, ViewDialogService)`.
  - The targeted workflow editor regression suite now passes, removing three false-negative failures from the Release test run.
- Verification / blockers:
  - Parent repo upstream remained current this run: 0 upstream `develop` commits pending and no merge conflicts occurred.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX); the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, and no parent submodule pointer update was required.
  - Targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release --no-restore -m:1 /p:BuildProjectReferences=false /p:UseSharedCompilation=false /nr:false --filter "FullyQualifiedName~WorkflowEditorViewModelTests"` passed with 3/3 tests green.
  - Full `dotnet test --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false` now reports 395 discovered tests with 10 remaining unrelated failures (editor overlay/context menu/schema-driven filters, image-effect preset serialization, Linux portal policy, and region-capture platform/recording setup).
  - Full `dotnet build --configuration Release --no-restore -m:1 /p:UseSharedCompilation=false /nr:false` repeatedly reached SIGKILL/OOM pressure on this host after building most projects, so zero-warning full-build verification is blocked by environment capacity rather than a new compile error from this change.
- Follow-up:
  - Next test-area pass should triage the remaining 10 genuine failures, starting with the schema-driven editor and image-effect preset serialization cluster.


### 2026-04-21 04:31 GMT+8
- Area: Platform-specific services
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/platform/XerahS.Platform.Windows/WindowsWindowService.cs`
  - `src/platform/XerahS.Platform.MacOS/MacOSWindowService.cs`
  - `src/platform/XerahS.Platform.MacOS/MacOSScreenCaptureKitService.cs`
  - `src/platform/XerahS.Platform.Windows/Services/WindowsSystemService.cs`
  - `src/platform/XerahS.Platform.MacOS/Services/MacOSSystemService.cs`
  - `src/platform/XerahS.Platform.Linux/Services/LinuxSystemService.cs`
  - `src/platform/XerahS.Platform.Linux/Services/WaylandPortalSystemService.cs`
  - `tests/XerahS.Tests/Platform/MacOS/MacOSWindowServiceTests.cs`
- Findings:
  - `MacOSWindowService.SearchWindow` checked the front window title/app name correctly, but it still returned `IntPtr.Zero` on a match, so callers could never distinguish a successful match from failure.
  - `GetForegroundWindow` also always returned `IntPtr.Zero`, which made the service inconsistent with its own `GetAllWindows` and effectively broke any consumer expecting a usable handle for the current front window on macOS.
  - The bounded safe fix here is to use a stable non-zero sentinel handle for the single front-window abstraction and cover the matching semantics with focused regression tests.
- Outcome:
  - Landed a bounded fix so macOS front-window discovery now returns a stable non-zero sentinel handle when front-window info is available.
  - Updated `SearchWindow` and `GetAllWindows` to use that same sentinel handle, and extracted the title/app-name matching logic into a helper for direct regression coverage.
  - Added focused macOS window-service tests covering the sentinel-handle contract and search matching behavior.
- Verification / blockers:
  - Parent repo upstream is current this run: 0 upstream `develop` commits pending and no merge conflicts were present.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX); the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, and no parent submodule pointer update was required.
  - Full solution `dotnet build --configuration Release` passed with 0 warnings and 0 errors.
  - Targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MacOSWindowServiceTests" -m:1 /p:UseSharedCompilation=false /nr:false` passed with 5/5 tests green.
  - Full solution `dotnet test --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false` still fails with 14 unrelated existing failures across assistant OCR, editor overlay/context-menu smoke tests, image-effect preset serialization/schema-driven filter dialogs, workflow editor initialization, Linux portal policy, and region-capture recording setup.
- Follow-up:
  - The next platform pass should stay in macOS/Linux service edges, but this run's macOS window-handle/search regression is now covered.

### 2026-04-21 02:21 GMT+8
- Area: CLI / command surface
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/cli/XerahS.CLI/Commands/CaptureCommand.cs`
  - `tests/XerahS.Tests/XerahS.Tests.csproj`
  - `tests/XerahS.Tests/Tools/CaptureCommandRegionParsingTests.cs`
  - `tests/XerahS.Tests/SendTo/SendToIntegrationCoordinatorTests.cs`
  - `src/desktop/app/XerahS.App/SendToIntegrationCoordinator.cs`
  - `src/desktop/app/XerahS.App/XerahS.App.csproj`
- Findings:
  - `capture region` bound `--region` as an optional string and then forced it through `region!`, `Split`, and `int.Parse`, so missing or malformed values crashed with null, format, or width/height edge-case exceptions instead of returning a CLI validation error.
  - The CLI area was stale in the tracker, and a bounded safe fix here is to require the option, validate the region payload centrally, and cover the parsing edge cases with focused regression tests.
  - While wiring the new regression, the existing `SendToIntegrationCoordinatorTests` dependency on `XerahS.App` still required explicitly linking `SendToIntegrationCoordinator.cs` into the test project to keep Linux Release test builds healthy.
- Outcome:
  - Landed a bounded fix so `capture region` now requires `--region`, rejects blank/malformed/non-integer/non-positive dimensions with actionable error messages, and exits cleanly instead of throwing.
  - Added `CaptureCommandRegionParsingTests` covering null, malformed, non-integer, non-positive, and valid region input cases.
  - Restored the CLI test project reference and linked `SendToIntegrationCoordinator.cs` into `XerahS.Tests` so the targeted CLI regression can compile under Release on this host.
- Verification / blockers:
  - Parent repo upstream is already current this run: 0 upstream `develop` commits pending and no conflicts were present.
  - `ShareX.ImageEditor` remotes were corrected and verified (`origin` = KovaForge, `upstream` = ShareX), the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, and no parent submodule pointer update was required.
  - Full solution `dotnet build --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings and 0 errors.
  - Targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~CaptureCommandRegionParsingTests" -m:1 /p:UseSharedCompilation=false /nr:false` passed with 7/7 tests green.
  - Full solution `dotnet test --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` still fails with 14 unrelated existing failures, including assistant OCR, editor UI smoke paths, image-effect preset serialization, workflow editor initialization, Linux portal policy, and region-capture recording setup.
- Follow-up:
  - Keep the next CLI pass focused on command-surface validation and exit-code consistency, but this run's `capture region` failure-path regression is now covered.

### 2026-04-21 01:45 GMT+8
- Area: Notifications/toasts
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/platform/XerahS.Platform.Windows/WindowsNotificationService.cs`
  - `src/platform/XerahS.Platform.Linux/Services/LinuxNotificationService.cs`
  - `src/platform/XerahS.Platform.MacOS/Services/MacOSNotificationService.cs`
  - `src/desktop/app/XerahS.UI/ViewModels/ToastViewModel.cs`
  - `src/desktop/app/XerahS.UI/Views/ToastWindow.axaml.cs`
  - `src/desktop/cli/XerahS.CLI/Services/HeadlessToastService.cs`
  - `src/desktop/tools/XerahS.WatchFolder.Daemon/Services/HeadlessToastService.cs`
  - `tests/XerahS.Tests/Platform/NotificationServiceProcessStartInfoTests.cs`
  - `tests/XerahS.Tests/Services/ToastWindowClickRoutingTests.cs`
- Findings:
  - `ToastWindow.OnPointerPressed` only armed click tracking for left-button presses, but `OnPointerReleased` tried to handle both left and middle releases.
  - That meant configured toast middle-click actions were never fired unless some other path happened to set drag state first, so middle-click behavior was effectively broken in the Avalonia toast UI.
  - Native Linux/macOS/Windows notification start-info generation still looked sane on review, and the bounded safe fix here was to repair the toast pointer-routing logic with regression coverage.
- Outcome:
  - Landed a bounded fix so toast click tracking now starts for both left and middle pointer presses.
  - Extracted the click-routing threshold logic into a small helper and added regression tests covering left click, middle click, and drag-above-threshold behavior.
- Verification / blockers:
  - Parent repo upstream was already synced earlier this run with 1 upstream commit merged into `develop` and no conflicts; current parent HEAD remains `3c5cd5c3` plus this run's unpushed local fix.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX), the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, and no parent submodule pointer update was required.
  - Full solution `dotnet build --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings and 0 errors.
  - Targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ToastWindowClickRoutingTests|FullyQualifiedName~NotificationServiceProcessStartInfoTests" -m:1 /p:UseSharedCompilation=false /nr:false` passed with 6/6 tests green.
  - Full solution `dotnet test --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false` still fails with 14 unrelated existing failures across assistant OCR, editor UI smoke tests, image-effect preset serialization, workflow editor initialization, Linux portal policy, and region-capture setup/recording.
- Follow-up:
  - Keep the next notifications pass focused on toast lifecycle/state issues if needed, but the immediate middle-click action regression is now covered.


### 2026-04-21 01:10 GMT+8
- Area: File/path handling
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/core/XerahS.Core/Helpers/TaskHelpers.cs`
  - `src/desktop/core/XerahS.Common/Helpers/FileHelpers.cs`
  - `src/desktop/app/XerahS.UI/ViewModels/SettingsViewModel.cs`
  - `src/desktop/app/XerahS.UI/Onboarding/OnboardingWizardViewModel.cs`
  - `src/desktop/app/XerahS.UI/Views/ApplicationSettingsView.axaml`
  - `tests/XerahS.Tests/Helpers/TaskHelpersScreenshotsFolderTests.cs`
- Findings:
  - `TaskHelpers.GetScreenshotsParentFolder` returned `settings.CustomScreenshotsPath` verbatim, unlike `GetScreenshotsFolder`, so variable-based custom paths could stay unresolved and produce inconsistent save/output locations.
  - On Linux, `Environment.ExpandEnvironmentVariables` does not expand Windows-style `%TEMP%` tokens, so tests and runtime paths using `%TEMP%` or `%TMP%` remained partially unresolved even after the parent-folder fix.
  - The bounded safe fix is to normalize the custom parent folder through `FileHelpers.GetAbsolutePath` and teach folder expansion to resolve `%NAME%` environment-variable tokens, with explicit TEMP/TMP fallback to `Path.GetTempPath()`.
- Outcome:
  - Landed a bounded fix so screenshots parent-folder resolution now uses `FileHelpers.GetAbsolutePath(settings.CustomScreenshotsPath)` instead of returning the raw config string.
  - Extended `FileHelpers.ExpandFolderVariables` to expand `%ENV_VAR%` tokens cross-platform, including `%TEMP%` and `%TMP%` fallback handling on Linux.
  - Added regression coverage for custom screenshots parent-folder expansion.
- Verification / blockers:
  - Parent repo upstream remained current this run: 0 upstream `develop` commits pending and no merge conflicts occurred.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX), the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, and no parent submodule pointer update was required.
  - Full solution `dotnet build --configuration Release` had already completed successfully earlier in this run with 0 warnings and 0 errors.
  - Targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release --filter "FullyQualifiedName~TaskHelpersScreenshotsFolderTests" -m:1 /p:UseSharedCompilation=false /nr:false` passed with 4/4 tests green.
  - Full solution `dotnet test --configuration Release --no-build -m:1 /p:UseSharedCompilation=false /nr:false` still fails with 14 unrelated existing failures, including image-effect preset serialization, workflow save initialization, and region-capture recording setup.
- Follow-up:
  - Triage the remaining 14 unrelated failing tests separately; this run's screenshots-folder fix is covered by targeted regression tests and does not touch those subsystems.


### 2026-04-20 23:17 GMT+8
- Area: Hotkeys/input
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/core/XerahS.Core/Hotkeys/WorkflowManager.cs`
  - `src/desktop/core/XerahS.Core/Hotkeys/HotkeyInfo.cs`
  - `src/platform/XerahS.Platform.Abstractions/IHotkeyService.cs`
  - `tests/XerahS.Tests/Hotkeys/WorkflowManagerTests.cs`
- Findings:
  - `WorkflowManager.UnregisterHotkeyInternal` removed the workflow from `_hotkeyMap` before checking whether the platform hotkey service actually succeeded.
  - If native unregister failed, XerahS kept the non-zero hotkey ID and failure status on `HotkeyInfo`, but it silently dropped the workflow mapping and could remove the workflow from the managed list, leaving a still-active hotkey orphaned from runtime dispatch.
  - That is a bad edge case: the OS can still deliver the hotkey while XerahS no longer knows which workflow owns it.
- Outcome:
  - Landed a bounded fix so workflow-map removal, runtime metadata clearing, and optional workflow-list removal now happen only after a successful native unregister.
  - Added a regression test covering unregister failure, verifying the workflow remains mapped, metadata stays intact, and trigger dispatch still reaches the owning workflow.
- Verification / blockers:
  - Parent repo upstream remained current this run: 0 upstream `develop` commits pending and no merge conflicts occurred.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX), the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, and no parent submodule pointer update was required.
  - Full solution `dotnet build --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings and 0 errors.
  - Targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release --filter "FullyQualifiedName~WorkflowManagerTests" -m:1 /p:UseSharedCompilation=false /nr:false` passed with 5/5 tests green.
  - Full solution `dotnet test --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` still fails with 16 unrelated existing failures, including image-effect preset serialization, screenshots-folder path expectations, workflow save initialization, and region-capture recording setup.
- Follow-up:
  - Triage the remaining 16 unrelated failing tests separately; this run's hotkey failure-path fix is covered by targeted regression tests and does not touch those subsystems.



### 2026-04-20 22:17 GMT+8
- Area: Imgur uploader plugin
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/plugins/Imgur.Plugin/ViewModels/ImgurConfigViewModel.cs`
  - `src/desktop/plugins/Imgur.Plugin/ImgurConfigModel.cs`
  - `src/desktop/plugins/Imgur.Plugin/ImgurProvider.cs`
  - `src/desktop/plugins/Imgur.Plugin/ImgurUploader.cs`
  - `tests/XerahS.Tests/Uploaders/ImgurConfigViewModelTests.cs`
- Findings:
  - `ImgurUploader.InternalUpload` retries once after refreshing an invalid access token, but it reused the original stream without rewinding it first.
  - For seekable streams such as `MemoryStream` or file-backed streams, that retry can run from EOF and produce an empty or failed second upload even though the token refresh succeeded.
  - Non-seekable streams cannot be retried safely, so the bounded fix is to rewind seekable streams and fail explicitly otherwise.
- Outcome:
  - Landed a bounded fix so Imgur auth-refresh retries now reset seekable upload streams to position 0 before reuploading.
  - Added regression tests covering successful rewind for seekable streams and the explicit non-seekable rejection path.
  - Updated the pre-existing Imgur config auth-state test fixture so its OAuth token has a valid expiry, matching current runtime authorization behavior.
- Verification / blockers:
  - Parent repo upstream remained current this run: 0 upstream `develop` commits pending and no merge conflicts occurred.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX), the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, and no parent submodule pointer update was required.
  - Full solution `dotnet build --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings and 0 errors.
  - Targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ImgurConfigViewModelTests" -m:1 /p:UseSharedCompilation=false /nr:false` passed with 4/4 tests green.
  - Full solution `dotnet test --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` still fails with 16 unrelated existing failures, including image-effect preset serialization, screenshots-folder path expectations, workflow save initialization, and region-capture recording setup.
- Follow-up:
  - Triage the remaining 16 unrelated failing tests separately; this run's Imgur retry fix is covered by targeted regression tests and does not touch those subsystems.


### 2026-04-20 21:43 GMT+8
- Area: FTP uploader plugin
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/plugins/Ftp.Plugin/ViewModels/FtpConfigViewModel.cs`
  - `src/desktop/plugins/Ftp.Plugin/FtpConfigModel.cs`
  - `src/desktop/plugins/Ftp.Plugin/FtpProvider.cs`
  - `src/desktop/plugins/Ftp.Plugin/FtpUploader.cs`
  - `src/desktop/plugins/Ftp.Plugin/Views/FtpConfigView.axaml`
  - `tests/XerahS.Tests/Uploaders/FtpConfigViewModelTests.cs`
- Findings:
  - The FTP config view advertises FTPS implicit mode on port 990, but `FtpConfigViewModel` only recalculated ports by protocol, not by FTPS encryption mode.
  - Switching an account from explicit FTPS to implicit FTPS left the default port stuck at 21 unless the user noticed and changed it manually, which can silently break uploads against implicit-only servers.
  - The safe behavior is the same as the existing protocol default sync: only auto-adjust when the current port still matches the prior default, and preserve custom user-entered ports.
- Outcome:
  - Landed a bounded fix so FTPS encryption-mode changes now update the default port from 21 to 990 for implicit FTPS, while still preserving customized ports.
  - Added regression tests covering both the default-port update path and the custom-port preservation path.
- Verification / blockers:
  - Parent repo upstream remained current this run: 0 upstream `develop` commits pending and no merge conflicts occurred.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX), the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, and no parent submodule pointer update was required.
  - Full solution `dotnet build --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings and 0 errors.
  - Targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~FtpConfigViewModelTests" -m:1 /p:UseSharedCompilation=false /nr:false` passed with 5/5 tests green.
  - Full solution `dotnet test --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` still fails with 17 pre-existing unrelated failures, including region-capture recording setup and Imgur authorization-state coverage.
- Follow-up:
  - Triage the remaining 17 unrelated failing tests separately; this run's FTP config fix is covered by targeted regression tests and does not touch those subsystems.


### 2026-04-20 20:16 GMT+8
- Area: Plugin loading/runtime
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoader.cs`
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginDiscovery.cs`
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/ProviderCatalog.cs`
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginConfigurationVerifier.cs`
  - `src/desktop/app/XerahS.UI/ViewModels/UploaderInstanceViewModel.cs`
  - `tests/XerahS.Tests/Helpers/PluginConfigurationVerifierTests.cs`
  - `tests/XerahS.Tests/CustomUploader/CustomUploaderDefinitionBindingServiceTests.cs`
- Findings:
  - `ProviderCatalog.ReloadCustomUploader` only removed the old provider after successfully parsing the updated custom uploader file.
  - If a custom uploader definition becomes invalid or is effectively deleted, reload returned `false` but left the previously loaded provider and metadata registered, so runtime/config UI paths could still offer a stale uploader that no longer matches disk state.
- Outcome:
  - Landed a bounded fix so custom uploader reload now removes any existing provider/metadata entry for the file before checking the new load result, ensuring broken or removed definitions do not stay selectable.
  - Added a regression test covering the invalid-update path and confirming the stale provider is fully removed.
- Verification / blockers:
  - Parent repo upstream remained current this run: 0 upstream `develop` commits pending and no merge conflicts occurred.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX), the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, and no parent submodule pointer update was required.
  - Full solution `dotnet build --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings and 0 errors.
  - Targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release --filter "ReloadCustomUploader_InvalidUpdatedDefinition_RemovesStaleProvider|PluginConfigurationVerifierTests" -m:1 /p:UseSharedCompilation=false /nr:false` passed with 7/7 tests green.
  - Full solution `dotnet test --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` still fails with 17 pre-existing unrelated failures across schema-driven editor dialogs, image-effect preset serialization, workflow editor initialization, Linux portal policy, region-capture UI/recording setup, and Imgur authorization-state coverage.
- Follow-up:
  - Triage the remaining 17 unrelated failing tests separately; this run's plugin reload fix is covered by targeted regression tests and does not touch those subsystems.


### 2026-04-20 19:15 GMT+8
- Area: Uploader core
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/ProviderCatalog.cs`
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoader.cs`
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginDiscovery.cs`
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/InstanceManager.cs`
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/InstanceConfiguration.cs`
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginConfigurationVerifier.cs`
  - `src/desktop/app/XerahS.UI/ViewModels/UploaderInstanceViewModel.cs`
  - `tests/XerahS.Tests/CustomUploader/CustomUploaderDefinitionBindingServiceTests.cs`
  - `tests/XerahS.Tests/Uploaders/InstanceManagerTests.cs`
- Findings:
  - `InstanceManager.DuplicateInstance` copied provider/config metadata but dropped `FileTypeRouting`, so duplicated uploader instances came back with a fresh empty routing scope.
  - In practice that strips category extension assignments from the duplicate and can leave the copied uploader unable to match any file types until the user manually reconfigures routing.
- Outcome:
  - Landed a bounded fix so duplicated uploader instances deep-copy `FileTypeRouting` including `AllFileTypes` and the explicit extension list.
  - Added a regression test proving duplicated instances preserve routing values and do not share the original mutable routing collection.
- Verification / blockers:
  - Parent repo upstream remained current this run: 0 upstream `develop` commits pending and no merge conflicts occurred.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX), the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, and no parent submodule pointer update was required.
  - Full solution `dotnet build --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings and 0 errors.
  - Targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~InstanceManagerTests" -m:1 /p:UseSharedCompilation=false /nr:false` passed with 1/1 tests green.
  - Full solution `dotnet test --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` still fails with 16 pre-existing unrelated failures, including assistant OCR, editor overlay/context-menu smoke tests, region capture recording setup, and Imgur authorization-state coverage.
- Follow-up:
  - Triage the remaining 16 unrelated failing tests separately; this run's uploader duplication fix is covered by the new targeted regression and does not touch those subsystems.


### 2026-04-20 18:41 GMT+8
- Area: Settings/configuration
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/core/XerahS.Core/Managers/SettingsManager.cs`
  - `src/desktop/core/XerahS.Core/Models/ApplicationConfig.cs`
  - `tests/XerahS.Tests/Helpers/SettingsManagerSecretsPathTests.cs`
- Findings:
  - `SettingsManager.ResetSettings()` captured `ApplicationConfigFilePath` before resetting state, but it reset `Settings` to a new default config before deleting uploader/workflow config files.
  - Because `UploadersConfigFilePath`, `WorkflowsConfigFilePath`, and `SecretsStoreFilePath` are computed from the live `Settings`, reset operations using custom folders or machine-specific filenames could leave the active resolved files in place while only deleting the default-path variants.
  - That means a user-visible "reset" could silently preserve custom machine-scoped configs and secrets even though the method reported success.
- Outcome:
  - Landed a bounded fix so `ResetSettings()` snapshots all resolved config/secrets paths before mutating `Settings`, then backs up and deletes those exact files.
  - Added regression coverage proving reset now removes and backs up machine-specific secrets plus resolved custom uploader/workflow config files.
- Verification / blockers:
  - Parent repo upstream remained current this run: 0 upstream `develop` commits pending and no merge conflicts occurred.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX), the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, and no parent submodule pointer update was required.
  - Full solution `dotnet build --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings and 0 errors.
  - Targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~SettingsManagerSecretsPathTests"` passed with 4/4 tests green.
  - Full solution `dotnet test --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` still fails with 16 pre-existing unrelated failures across schema-driven editor dialogs, image-effect preset serialization, workflow editor initialization, Linux portal policy, region-capture UI/recording setup, screenshot-path expectations, and Imgur auth-state coverage.
- Follow-up:
  - Triage the remaining 16 existing test failures by subsystem in future runs; this run's settings reset fix is covered by targeted tests and does not touch those failing areas.


### 2026-04-20 17:15 GMT+8
- Area: Capture pipeline
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/platform/XerahS.Platform.Windows/Capture/GdiCaptureStrategy.cs`
  - `src/platform/XerahS.Platform.Windows/Capture/WinRTCaptureStrategy.cs`
  - `src/platform/XerahS.Platform.Windows/WindowsScreenCaptureService.cs`
  - `src/desktop/app/XerahS.RegionCapture/RegionCaptureService.cs`
  - `src/platform/XerahS.Platform.Abstractions/Capture/RegionCaptureOptions.cs`
  - `tests/XerahS.Tests/XerahS.Tests.csproj`
  - `tests/XerahS.Tests/Platform/Windows/WinRTCaptureStrategyTests.cs`
- Findings:
  - `WinRTCaptureStrategy.CaptureRegionAsync` still delegates to `GdiCaptureStrategy`, but `GetCapabilities()` was advertising WinRT-only features such as hardware acceleration, cursor capture, and HDR support.
  - That mismatch can mislead diagnostics and backend-selection logic into believing those capabilities are available when this code path currently falls back to GDI.
  - Added a Windows-only regression test for the fallback capability contract, and excluded it from non-Windows test compilation so Linux cron builds stay green while preserving Windows coverage.
- Outcome:
  - Landed a bounded fix so `WinRTCaptureStrategy.GetCapabilities()` now derives from the current GDI fallback capabilities and reports itself as `WinRT Graphics Capture (GDI fallback)` instead of overstating support.
  - Added `WinRTCaptureStrategyTests` to lock the fallback capability contract on Windows builds.
- Verification / blockers:
  - Parent repo upstream remained current this run: 0 upstream `develop` commits pending and no merge conflicts occurred.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX), the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, and no parent submodule pointer update was required.
  - Full solution `dotnet build --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings and 0 errors.
  - Full solution `dotnet test --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` now runs 374 tests on this host but still fails with 16 pre-existing unrelated failures across assistant OCR behavior, editor UI smoke tests, preset serialization, screenshot-path expectations, workflow editor initialization, Linux portal policy, region-capture UI/recording setup, and Imgur auth state.
- Follow-up:
  - Triage the remaining 16 test failures separately; this run's capture fix did not add any new Linux-visible failures, but the Windows-only regression test should be exercised on a Windows CI/host.

### 2026-04-20 16:15 GMT+8
- Area: Tests / test discoverability
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `tests/XerahS.Tests/XerahS.Tests.csproj`
  - `tests/XerahS.Tests/Helpers/ImageEffectPresetSerializerTests.cs`
  - `tests/XerahS.Tests/Helpers/TaskHelpersScreenshotsFolderTests.cs`
  - `tests/XerahS.Tests/Hotkeys/WorkflowEditorViewModelTests.cs`
  - `tests/XerahS.Tests/Platform/Linux/LinuxCaptureOrchestrationTests.cs`
  - `tests/XerahS.Tests/RegionCapture/RegionCaptureUiSmokeTests.cs`
  - `tests/XerahS.Tests/RegionCapture/ScreenRecorderServiceTests.cs`
  - `tests/XerahS.Tests/Uploaders/ImgurConfigViewModelTests.cs`
- Findings:
  - The test project still targeted only `net10.0-windows10.0.26100.0`, which left `dotnet test` on this Linux cron host building a Windows-targeted assembly that previously exposed no discoverable NUnit tests.
  - The test project also inherited app-driven plugin bundling behavior, causing extra nested plugin builds and unnecessary memory pressure during verification.
  - After switching the test target framework to `net10.0` on non-Windows hosts and disabling app-driven plugin bundling in the test project, NUnit discovery now works locally and `dotnet test --list-tests` exposes 374 tests.
  - A full targeted test run now executes instead of reporting zero tests, but 17 existing failures remain across preset serialization, custom screenshot path expectations, workflow editor view-model setup, Linux capture policy assertions, region-capture UI/recording initialization, and Imgur config authorization state.
- Outcome:
  - Landed a bounded infrastructure fix in `tests/XerahS.Tests.csproj` so Linux cron runs use `net10.0`, preserve the Windows target on Windows hosts, and skip app-driven plugin bundling during test builds.
  - `dotnet build tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` now passes with 0 warnings and 0 errors.
  - `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release --no-build --list-tests -m:1 /p:UseSharedCompilation=false /nr:false` now lists discoverable tests instead of reporting none.
- Verification / blockers:
  - Parent repo upstream remained current this run: 0 upstream `develop` commits pending and no merge conflicts occurred.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX), the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, and no parent submodule pointer update was required.
  - Full solution `dotnet build --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings and 0 errors.
  - Full solution `dotnet test --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` still hit SIGKILL in this environment after build/test startup.
  - Targeted `XerahS.Tests` execution no longer has the discoverability blocker, but the suite currently fails with 17 real test failures that need follow-up fixes in their respective subsystems.
- Follow-up:
  - Triage the 17 now-visible test failures by subsystem, starting with the path-expansion, workflow editor null-state, and Linux region-capture cases because they look like likely regression candidates rather than environment-only noise.


### 2026-04-20 15:37 GMT+8
- Area: Editor integration
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/app/XerahS.UI/ViewModels/HistoryViewModel.cs`
  - `tests/XerahS.Tests/Editor/HistoryEditorLaunchTests.cs`
  - `src/platform/XerahS.Platform.Abstractions/IUIService.cs`
  - `src/desktop/app/XerahS.UI/Services/AvaloniaUIService.cs`
- Findings:
  - The earlier timestamp-only refresh logic in `HistoryViewModel.RefreshHistoryItemAfterEditorSessionAsync` still misses real editor saves when the image or `.xann` sidecar contents change without a last-write timestamp change.
  - That leaves history thumbnails and editor-session-backed UI state stale after same-timestamp saves or annotation-only saves that reuse the same sidecar path.
- Outcome:
  - Landed a bounded follow-up fix so editor-session refresh now compares lightweight file snapshots (exists, length, last-write time) for both the image file and the annotation sidecar, in addition to sidecar path changes.
  - Updated the regression test to exercise a same-timestamp save with changed file content length so the refresh path is covered by the intended edge case.
- Verification / blockers:
  - Parent repo upstream remained current this run: 0 upstream `develop` commits pending and no merge conflicts occurred.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX), the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, and no parent submodule pointer update was required.
  - `dotnet build --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings and 0 errors.
  - `dotnet test --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` exited successfully but still reported no discoverable tests in `tests/XerahS.Tests/bin/Release/net10.0-windows10.0.26100.0/XerahS.Tests.dll`.
- Follow-up:
  - Investigate why the Windows-targeted .NET 10 NUnit test assembly still builds but exposes no discoverable tests under `dotnet test`.

### 2026-04-20 14:37 GMT+8
- Area: OCR
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/app/XerahS.Assistant/Services/AssistantService.cs`
  - `src/desktop/app/XerahS.Assistant/Routing/AssistantCommandRouter.cs`
  - `tests/XerahS.Tests/Assistant/AssistantCommandRouterTests.cs`
  - `tests/XerahS.Tests/Assistant/AssistantServiceTests.cs`
  - `src/desktop/core/XerahS.Core/Models/TaskSettingsOptions.cs`
  - `src/platform/XerahS.Platform.Abstractions/IOcrService.cs`
- Findings:
  - `AssistantService.RunOcrAsync` still invoked OCR with hardcoded defaults, forcing English and ignoring configured OCR scale and single-line settings.
  - That made assistant-triggered OCR inconsistent with the already-fixed after-capture OCR path and broke non-English or single-line assistant OCR flows.
- Outcome:
  - Landed a bounded fix so assistant OCR now uses `SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions` with safe fallback normalization for blank language tags and invalid scale factors.
  - Added regression coverage in `AssistantServiceTests` to verify assistant OCR forwards configured language, scale factor, and single-line settings to `IOcrService`.
- Verification / blockers:
  - Parent repo upstream remained current this run: 0 upstream `develop` commits pending and no merge conflicts occurred.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX), the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, and no parent submodule pointer update was required.
  - `dotnet build --configuration Release` passed with 0 warnings and 0 errors after rerunning sequentially to avoid an Avalonia PDB file-lock race caused by overlapping earlier verification commands.
  - `dotnet test --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` exited successfully but still reported no discoverable tests in `tests/XerahS.Tests/bin/Release/net10.0-windows10.0.26100.0/XerahS.Tests.dll`.
- Follow-up:
  - Investigate why the Windows-targeted .NET 10 NUnit test assembly still builds but exposes no discoverable tests under `dotnet test`.

### 2026-04-20 13:12 GMT+8
- Area: Nextcloud uploader plugin
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/plugins/Nextcloud.Plugin/NextcloudUploader.cs`
  - `src/desktop/plugins/Nextcloud.Plugin/NextcloudClient.cs`
  - `src/desktop/plugins/Nextcloud.Plugin/NextcloudConfigModel.cs`
  - `src/desktop/plugins/Nextcloud.Plugin/NextcloudProvider.cs`
  - `tests/XerahS.Tests/Uploaders/NextcloudProviderTests.cs`
- Findings:
  - `NextcloudUploader.Upload` unconditionally accessed `stream.Length` before creating `ProgressManager`.
  - That throws for valid non-seekable streams such as piped stdin, network streams, or wrapper streams that support reads but not length/seek, so the upload fails before the HTTP request even begins.
- Outcome:
  - Landed a bounded fix so progress tracking is only created when the input stream exposes a seekable length, while non-seekable streams still upload normally without preflight exceptions.
  - Added regression coverage with a non-seekable test stream to ensure the uploader no longer fails with `NotSupportedException` during setup.
- Verification / blockers:
  - Parent repo upstream remained current this run: 0 upstream `develop` commits pending.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX), both remotes fetched successfully, and the submodule was left on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`; no parent submodule pointer update was required.
  - `dotnet build --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings and 0 errors.
  - `dotnet test --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` exited successfully but still reported no discoverable tests in `tests/XerahS.Tests/bin/Release/net10.0-windows10.0.26100.0/XerahS.Tests.dll`.
- Follow-up:
  - Investigate why NUnit tests are still non-discoverable under the current Windows-targeted .NET 10 test assembly output.

### 2026-04-19 14:16 GMT+8
- Area: Nextcloud uploader plugin
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/plugins/Nextcloud.Plugin/NextcloudUploader.cs`
  - `src/desktop/plugins/Nextcloud.Plugin/NextcloudClient.cs`
  - `src/desktop/plugins/Nextcloud.Plugin/NextcloudConfigModel.cs`
  - `src/desktop/plugins/Nextcloud.Plugin/NextcloudProvider.cs`
  - `tests/XerahS.Tests/Uploaders/NextcloudProviderTests.cs`
- Findings:
  - No safe bounded fix landed in that run.
- Blockers / Notes:
  - Tests remained non-discoverable in the broader solution context.

### 2026-04-19 19:10 GMT+8
- Area: Plugin loading/runtime
- Reviewer: CEO-reported fix to be folded into tracker baseline
- Files affected / likely touched:
  - desktop plugin `.csproj` files with incorrect `RootNamespace`
  - `Directory.Build.props`
- Findings:
  - Desktop startup/runtime issue traced to plugin project metadata mismatch.
  - Avalonia plugin views use `ShareX.*` namespaces while several plugin projects still declared `RootNamespace` as `XerahS.*`.
  - Duplicate `RootNamespace` declarations existed in several plugin project files.
- Outcome:
  - Fix reportedly aligned plugin `RootNamespace` values to `ShareX.*`, removed duplicate declarations, added warning comments, and bumped version from `0.22.3` to `0.22.4`.
- Follow-up:
  - Future hourly runs should verify all plugin `.csproj` files are consistent and runtime loading remains healthy.

### 2026-04-19 20:09 GMT+8
- Area: Capture pipeline
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/app/XerahS.RegionCapture/RegionCaptureService.cs`
  - `src/platform/XerahS.Platform.Abstractions/Capture/RegionCaptureOptions.cs`
  - `src/platform/XerahS.Platform.Windows/WindowsScreenCaptureService.cs`
  - `src/platform/XerahS.Platform.Windows/Capture/GdiCaptureStrategy.cs`
- Findings:
  - `WindowsScreenCaptureService.CaptureRectAsync` deleted the capture bitmap in `finally` without guaranteeing the original bitmap had first been re-selected into the memory DC.
  - If `Image.FromHbitmap`, PNG serialization, or decode failed after `SelectObject`, cleanup could attempt to delete an HBITMAP that was still selected into the DC, which is unsafe and can cause intermittent GDI cleanup failures or leaked state.
- Outcome:
  - Landed a bounded fix: track the previously selected object, fail fast if `SelectObject` fails, always restore the original selection in `finally`, then delete the bitmap handle.
- Verification / blockers:
  - `dotnet build --configuration Release` passed with 0 warnings and 0 errors after rerunning serially (`-m:1`) to avoid an Avalonia PDB file lock during the first parallel build attempt.
  - `dotnet test --configuration Release` exited successfully but reported: no tests discoverable in `tests/XerahS.Tests/bin/Release/net10.0-windows10.0.26100.0/XerahS.Tests.dll`.
- Follow-up:
  - Review remaining Windows capture paths for similar select/restore GDI handle patterns.
  - Investigate why the test assembly is built but not exposing discoverable tests under the current target/runtime combination.

### 2026-04-19 22:06 GMT+8
- Area: Editor integration
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/app/XerahS.UI/Services/AvaloniaUIService.cs`
  - `src/desktop/app/XerahS.UI/ViewModels/HistoryViewModel.cs`
  - `src/desktop/core/XerahS.Core/Tasks/Processors/CaptureJobProcessor.cs`
  - `src/tools/XerahS.McpServer/Runtime/HeadlessMcpServices.cs`
  - `src/desktop/core/XerahS.History/HistoryItem.cs`
  - `tests/XerahS.Tests/Editor/HistoryEditorLaunchTests.cs`
  - `ShareX.ImageEditor/src/ShareX.ImageEditor/Presentation/Views/EditorView.CoreBridge.cs`
- Findings:
  - `HistoryViewModel.RefreshHistoryItemAfterEditorSessionAsync` only refreshed history entries when the edited file's last-write time increased.
  - That misses real saves on filesystems or save flows that preserve the timestamp exactly, leaving stale `HistoryItem` instances and annotation sidecar metadata in the UI even though the image content changed.
- Outcome:
  - Landed a bounded fix in `HistoryViewModel`: treat any timestamp change, not only strictly newer timestamps, as an edited-file refresh trigger.
  - Added a regression test covering the same-timestamp edit case alongside the existing changed-timestamp test path in `HistoryEditorLaunchTests`.
- Verification / blockers:
  - Parent repo upstream sync remained current: 0 upstream commits pending on `develop`.
  - `ShareX.ImageEditor` remotes were corrected/verified, fetched from `origin` and `upstream`, and left on branch `develop` (not detached). No submodule pointer update was required.
  - Full repo `dotnet build --configuration Release` could not be completed in this environment: one serial run was killed by SIGKILL, and a targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release --no-restore --filter HistoryEditorLaunchTests -m:1` run failed because `ShareX.ImageEditor/src/ShareX.ImageEditor/obj/os-Unix/host-net10.0/project.assets.json` was missing, followed by cascading `MSB4181` errors from plugin auto-build targets.
- Follow-up:
  - Restore or generate the missing `ShareX.ImageEditor` host assets so targeted tests can run without tripping the plugin auto-build chain.
  - Re-run full build/test verification once the restore-assets blocker is cleared.

### 2026-04-19 23:11 GMT+8
- Area: OCR
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/core/XerahS.Core/Tasks/Processors/CaptureJobProcessor.cs`
  - `src/desktop/core/XerahS.Core/Models/TaskSettings.cs`
  - `src/desktop/core/XerahS.Core/Models/TaskSettingsOptions.cs`
  - `src/platform/XerahS.Platform.Abstractions/IOcrService.cs`
  - `src/platform/XerahS.Platform.Windows/WindowsOcrService.cs`
  - `tests/XerahS.Tests/Tools/OcrViewModelTests.cs`
  - `tests/XerahS.Tests/Tasks/CaptureJobProcessorOcrTests.cs`
- Findings:
  - `CaptureJobProcessor.PerformOCRAsync` ignored the workflow's configured OCR settings and always invoked OCR with hardcoded defaults (`en`, `2f`, multiline).
  - That silently breaks non-English OCR workflows and discards task-level single-line / scale overrides even when the user configured them.
- Outcome:
  - Landed a bounded fix so after-capture OCR now uses `TaskSettings.CaptureSettings.OCROptions`, while still falling back to safe defaults for blank language tags or invalid scale factors.
  - Added regression coverage for honoring task OCR options and for default fallback sanitization.
- Verification / blockers:
  - Parent repo synced 2 upstream `develop` commits (`c8f70cd1`, `bd5b4091`) via merge commit with no conflicts.
  - `ShareX.ImageEditor` remotes were verified and the submodule was left on branch `develop` (not detached); no submodule pointer update was needed this run.
  - `dotnet build src/desktop/core/XerahS.Core/XerahS.Core.csproj --configuration Release --no-restore -m:1 -p:BuildProjectReferences=false` passed with 0 warnings and 0 errors.
  - Full `dotnet build --configuration Release` was attempted repeatedly but the process was killed by SIGKILL in this environment before completion.
  - `dotnet test --configuration Release --no-build -m:1` exited successfully but still reported no discoverable tests in `tests/XerahS.Tests/bin/Release/net10.0-windows10.0.26100.0/XerahS.Tests.dll`.
- Follow-up:
  - Investigate the broader solution/test-host memory pressure causing full Release builds to be SIGKILLed in this environment.
  - Fix test discovery so the newly added OCR regression coverage is executable in normal `dotnet test` runs.

### 2026-04-20 00:11 GMT+8
- Area: Settings/configuration
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/core/XerahS.Core/Managers/SettingsManager.cs`
  - `src/desktop/core/XerahS.Core/Security/SecretStore.cs`
  - `tests/XerahS.Tests/Helpers/SettingsManagerSecretsPathTests.cs`
- Findings:
  - `SettingsManager.ResetSettings()` backed up and deleted application, uploader, and workflow config files, but it left `SecretsStore*.json` and `SecretsStore.key` behind.
  - That means a user-visible "reset settings" could preserve stored uploader credentials on disk, which is incorrect and unsafe.
- Outcome:
  - Landed a bounded fix in `SettingsManager.ResetSettings()` so reset now also backs up and deletes the active secrets store file plus the AES fallback key file.
  - Added regression coverage for machine-specific secrets path handling and for reset-time backup/deletion of the secrets store artifacts.
  - Pushed parent repo commit `40f2bab1` to `origin/develop`.
- Verification / blockers:
  - Parent repo upstream sync remained current this run: 0 upstream `develop` commits to merge.
  - `ShareX.ImageEditor` remotes were re-verified and the submodule was left on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`; no submodule pointer update was required.
  - Full `dotnet build --configuration Release -m:1` progressed deep into the solution, then was killed by SIGKILL in this environment before completing.
  - Targeted `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release --filter SettingsManagerSecretsPathTests -m:1` failed because `ShareX.ImageEditor/src/ShareX.ImageEditor/obj/os-Unix/host-net10.0/project.assets.json` is missing, which then cascaded into plugin auto-build `MSB4181` failures.
- Follow-up:
  - Restore or generate the missing `ShareX.ImageEditor` host restore assets so test runs stop failing in the plugin auto-build chain.
  - Re-run full Release build/test verification once the environment SIGKILL pressure and missing restore-assets blocker are cleared.

### 2026-04-20 01:11 GMT+8
- Area: Uploader core
- Reviewer: Mikhail hourly cron
- Files inspected:
  - Tracker only. Repo file inspection did not begin.
- Findings:
  - Chosen as the stalest pending high-priority area for this run.
  - Mandatory repo maintenance could not start because the first local `exec` command block was denied under the current gateway approval policy for cron runs (`security=allowlist`, `ask=on-miss`).
- Outcome:
  - No upstream sync, submodule hygiene, code review, bug fix, build, or test work could be executed in this run.
- Blockers / Notes:
  - This is an automation-policy blocker, not a repo-state conclusion.
  - Cron needs trusted local exec allowlisting or non-interactive approval disablement before this hourly job can satisfy its contract again.

### 2026-04-20 03:06 GMT+8
- Area: Uploader core
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/ProviderCatalog.cs`
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoader.cs`
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginDiscovery.cs`
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/InstanceManager.cs`
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/InstanceConfiguration.cs`
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginConfigurationVerifier.cs`
  - `src/desktop/app/XerahS.UI/ViewModels/UploaderInstanceViewModel.cs`
  - `tests/XerahS.Tests/CustomUploader/CustomUploaderDefinitionBindingServiceTests.cs`
- Findings:
  - `ProviderCatalog.LoadPlugins(..., forceReload: true)` removed stale plugin DLL metadata only by overwriting matching IDs, but it never purged custom uploader providers whose backing files were deleted or whose definitions changed category/name across a reload.
  - That leaves deleted custom uploader definitions still selectable in memory until process restart, and updates can preserve stale provider state from the previous file contents.
- Outcome:
  - Landed a bounded fix in `ProviderCatalog` so force-reload now removes dynamic providers rooted under the reloaded directories before rediscovery, including custom uploader definitions and plugin metadata tracked by assembly path.
  - Added regression tests covering deleted custom uploader cleanup and updated-definition replacement during force reload.
  - Pushed parent repo commit `ccf77e31` to `origin/develop`.
- Verification / blockers:
  - Parent repo had already merged 1 upstream commit earlier in the run via `git merge --no-ff upstream/develop -m "Merge upstream/develop into develop"`; no additional merge conflicts occurred.
  - `ShareX.ImageEditor` remotes were re-fetched separately (`origin`, `upstream`), left on branch `develop`, and remained at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`; parent pointer still matches that commit. No submodule pointer update was required.
  - `dotnet build src/desktop/core/XerahS.Uploaders/XerahS.Uploaders.csproj --configuration Release -m:1 -p:OS=Unix -p:HostTargetFramework=net10.0` passed with 0 warnings and 0 errors.
  - Full-solution `dotnet test --configuration Release` remained blocked in this environment: one run failed in plugin auto-build/host restore paths and a later broader run was killed by SIGKILL before completion.
- Follow-up:
  - Investigate a lighter-weight way to run targeted uploader tests without pulling the full app/plugin auto-build chain.
  - Re-run full solution Release build/test verification once the current plugin auto-build / environment memory-pressure blocker is cleared.

### 2026-04-20 03:17 GMT+8
- Area: Plugin loading/runtime
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginConfigurationVerifier.cs`
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginLoader.cs`
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginDiscovery.cs`
  - `src/desktop/core/XerahS.Uploaders/PluginSystem/ProviderCatalog.cs`
  - `src/desktop/app/XerahS.UI/ViewModels/UploaderInstanceViewModel.cs`
  - `tests/XerahS.Tests/Helpers/PluginConfigurationVerifierTests.cs`
  - `tests/XerahS.Tests/XerahS.Tests.csproj`
- Findings:
  - `UploaderInstanceViewModel` calls `PluginConfigurationVerifier` directly from the config UI path, but the verifier assumed a non-empty `providerId` and would throw on null/blank values before it could surface a user-facing error state.
  - The cleanup command had the same assumption, so an empty provider selection could also trip avoidable exceptions instead of becoming a no-op.
- Outcome:
  - Landed a bounded fix in `PluginConfigurationVerifier` to fail safely when `providerId` is null/blank, returning an explicit verification error and treating duplicate-DLL cleanup as a no-op.
  - Added regression tests for blank/null provider IDs.
- Verification / blockers:
  - Parent repo upstream remained current this run: 0 upstream `develop` commits pending.
  - `ShareX.ImageEditor` remotes were verified (`origin` = KovaForge, `upstream` = ShareX), fetched successfully when split into separate fetches, and the submodule was left on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046` with no parent pointer change required.
  - `git fetch origin upstream` in the submodule is not valid as written because Git interprets `upstream` as a refspec for remote `origin`; separate `git fetch origin` and `git fetch upstream` calls work correctly.
  - `dotnet build src/desktop/core/XerahS.Uploaders/XerahS.Uploaders.csproj --configuration Release --no-restore -m:1` passed with 0 warnings and 0 errors.
  - Full `dotnet build --configuration Release` and `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --configuration Release --no-restore --filter PluginConfigurationVerifierTests -m:1` both progressed deep into the graph, then were killed by SIGKILL in this environment before completion.
- Follow-up:
  - Keep submodule fetches split by remote unless the command is rewritten to fetch `develop` explicitly from both remotes.
  - Re-run full solution Release build/test verification in an environment without the current SIGKILL/resource-pressure failure.

### 2026-04-20 04:39 GMT+8
- Area: File/path handling
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/core/XerahS.Common/Helpers/FileHelpers.cs`
  - `tests/XerahS.Tests/Helpers/FileHelpersTests.cs`
  - `tests/XerahS.Tests/Helpers/TaskHelpersScreenshotsFolderTests.cs`
  - `tests/XerahS.Tests/SendTo/SendToIntegrationCoordinatorTests.cs`
  - `tests/XerahS.Tests/Editor/HistoryEditorLaunchTests.cs`
- Findings:
  - `FileHelpers.GetUniqueFilePath` started numbering collisions from `2` when the original file existed and had no numeric suffix.
  - That skipped the expected first available sibling name like `capture (1).png`, creating inconsistent numbering versus common filesystem/UI expectations.
- Outcome:
  - Landed a bounded fix so unsuffixed collisions now start at `(1)`, while already-numbered files still increment from their current suffix.
  - Added regression tests for both first-collision naming and incrementing an existing numbered filename.
- Verification / blockers:
  - Parent repo upstream remained current this run: 0 upstream `develop` commits pending and no merge conflicts were needed.
  - `ShareX.ImageEditor` remained on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`; parent repo still points at the same commit, so no submodule pointer update was required.
  - `dotnet build --configuration Release -m:1` passed with 0 warnings and 0 errors.
  - `dotnet test --configuration Release -m:1` exited successfully but reported no discoverable tests in `tests/XerahS.Tests/bin/Release/net10.0-windows10.0.26100.0/XerahS.Tests.dll`, so the new regression coverage is present in source but still not executing in this environment.
  - A targeted filtered run for `FileHelpersTests` produced the same underlying discoverability issue.
- Follow-up:
  - Investigate why the Windows-targeted NUnit test assembly builds but exposes no discoverable tests under the current test host/runtime combination.
  - Continue the next hourly review in another stale pending area rather than revisiting file/path handling immediately unless test-discovery work is selected.

### 2026-04-20 05:18 GMT+8
- Area: Tests / test discoverability
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `tests/XerahS.Tests/XerahS.Tests.csproj`
  - `Directory.Packages.props`
  - NUnit docs: `.NET Core | NUnit Docs` (`https://docs.nunit.org/articles/nunit/getting-started/dotnet-core-and-dotnet-standard.html`)
- Findings:
  - The test project targets `.NET 10` (`net10.0-windows10.0.26100.0`), but the repo still pinned an older test toolchain: `Microsoft.NET.Test.Sdk 17.11.1`, `NUnit3TestAdapter 4.6.0`, `NUnit 4.5.1`, `NUnit.Analyzers 4.3.0`, and `coverlet.collector 6.0.2`.
  - NUnit's current .NET 10 guidance uses a newer baseline (`Microsoft.NET.Test.Sdk 18.4.0`, `NUnit3TestAdapter 6.2.0`, `NUnit 4.6.0`, `NUnit.Analyzers 4.12.0`, `coverlet.collector 8.0.1`). The stale pinned versions are a credible root cause for the repeated "no discoverable tests" result.
- Outcome:
  - Updated `Directory.Packages.props` to the current NUnit/.NET 10 test package baseline above so the solution no longer asks a .NET 10 test assembly to run through stale discovery infrastructure.
  - This is a bounded source fix only in this run. I could not validate, commit, or push it because cron exec is still blocked before any git/build/test command can run.
- Verification / blockers:
  - Mandatory upstream sync, `ShareX.ImageEditor` hygiene, build, test, commit, and push were all blocked at the first `exec` call by the gateway's non-interactive approval policy: `security=allowlist`, `ask=on-miss`.
  - Because of that automation-policy blocker, I could not confirm whether the package updates fully restore discovery or whether follow-up test-project changes are still needed.
- Follow-up:
  - Re-run the mandatory git/submodule/build/test flow as soon as the cron exec allowlist is fixed.
  - If discovery still fails after the package refresh, inspect whether the Windows-only target framework should be multi-targeted or split for Linux-hosted CI/test discovery.

### 2026-04-20 06:41 GMT+8
- Area: FTP uploader plugin
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/plugins/Ftp.Plugin/ViewModels/FtpConfigViewModel.cs`
  - `src/desktop/plugins/Ftp.Plugin/FtpConfigModel.cs`
  - `src/desktop/core/XerahS.UploadersLib/FTPHelpers.cs`
  - `tests/XerahS.Tests/Uploaders/FtpConfigViewModelTests.cs`
  - `tests/XerahS.Tests/XerahS.Tests.csproj`
  - `Directory.Packages.props`
- Findings:
  - `FtpConfigViewModel` left the port untouched when switching between FTP/FTPS and SFTP, so untouched defaults silently stayed on the wrong protocol port, for example `21` after switching to SFTP.
  - `Validate()` accepted impossible port values such as `0`, which deferred a basic config error until a later connection attempt.
  - The earlier test-discovery package baseline refresh was validated in this run, but full `dotnet test --configuration Release` still reports no discoverable tests for the Windows-targeted test assembly on this Linux host.
- Outcome:
  - Landed a bounded fix so protocol changes automatically carry the default port only when the user has not customized it, preserving explicit custom ports.
  - Added config validation to reject ports outside `1..65535` with a clear message.
  - Added regression tests covering default-port switching, custom-port preservation, and invalid-port rejection, and referenced the FTP plugin project from the test project.
- Verification / blockers:
  - Parent repo upstream remained current this run: 0 upstream `develop` commits pending and no merge conflicts were needed.
  - `ShareX.ImageEditor` remotes were verified (`origin=KovaForge`, `upstream=ShareX`), the submodule remained on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, and the parent repo already points at the same commit, so no submodule pointer update was required.
  - `dotnet build --configuration Release` passed with 0 warnings and 0 errors.
  - `dotnet test --configuration Release` exited successfully but still reported no discoverable tests in `tests/XerahS.Tests/bin/Release/net10.0-windows10.0.26100.0/XerahS.Tests.dll`.
- Follow-up:
  - Investigate why the updated NUnit/.NET test package baseline still does not make the Windows-targeted test assembly discoverable on this Linux host.
  - Review the next stale pending subsystem instead of revisiting FTP immediately unless the test-host work selects it.

### 2026-04-20 07:41 GMT+8
- Area: Imgur uploader plugin
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/plugins/Imgur.Plugin/ViewModels/ImgurConfigViewModel.cs`
  - `src/desktop/plugins/Imgur.Plugin/ImgurUploader.cs`
  - `src/desktop/plugins/Imgur.Plugin/ImgurConfigModel.cs`
  - `src/desktop/plugins/Imgur.Plugin/ImgurProvider.cs`
  - `tests/XerahS.Tests/XerahS.Tests.csproj`
  - `tests/XerahS.Tests/Uploaders/ImgurConfigViewModelTests.cs`
- Findings:
  - `ImgurConfigViewModel.LoadFromJson()` rebuilt `_uploader` before the persisted `ClientId` was copied back into the public view-model state.
  - That means an already logged-in Imgur user could reopen settings, appear authenticated via the stored token, but still have an uploader instance with a blank OAuth client ID. Refresh, authorization checks, and album-loading flows could then fail against stale or empty in-memory config.
  - `EnsureUploader()` also only copied `ClientId` back into `_config` before rebuilding, which let other current UI selections drift away from the uploader instance until a full save.
- Outcome:
  - Landed a bounded fix so config load now restores the persisted UI state before rebuilding the uploader, and uploader rebuilds now synchronize the current account/link/album settings back into `_config` first.
  - Added regression coverage that checks persisted Imgur login state rebuilds with the saved client ID and that rebuilt uploader config matches current UI selections.
- Verification / blockers:
  - Parent repo upstream sync remained current this run: 0 upstream `develop` commits to merge, no conflicts.
  - `ShareX.ImageEditor` remotes were verified again and the submodule was left on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`; no submodule pointer update was required.
  - Initial `dotnet build --configuration Release` hit an Avalonia file-lock race (`AVLN9999` on `XerahS.RegionCapture.dll`) plus cascading `MSB4181`, but `dotnet build --configuration Release /m:1` then passed clean with 0 warnings and 0 errors.
  - `dotnet test --configuration Release /m:1` completed successfully but still reported no discoverable tests in `tests/XerahS.Tests/bin/Release/net10.0-windows10.0.26100.0/XerahS.Tests.dll`.
- Follow-up:
  - Investigate why the current .NET 10 Windows-targeted test assembly is still non-discoverable under `dotnet test`, even after recent package baseline updates.

### 2026-04-20 09:07 GMT+8
- Area: Hotkeys/input
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/core/XerahS.Core/Hotkeys/WorkflowManager.cs`
  - `src/platform/XerahS.Platform.Abstractions/Models/HotkeyInfo.cs`
  - `src/desktop/app/XerahS.UI/Views/Controls/HotkeySelectionControl.axaml.cs`
  - `tests/XerahS.Tests/Hotkeys/WorkflowManagerTests.cs`
- Findings:
  - `WorkflowManager.UnregisterHotkeyInternal()` removed the binding from `_hotkeyMap`, but it left `HotkeyInfo.Id` and `NativeTriggerDescription` intact on successful unregister.
  - That stale runtime metadata can leak into later state transitions: subsequent logic still sees a non-zero registration ID, and UI/native-trigger display can continue showing a compositor-provided shortcut label even after the hotkey has been cleared or reconfigured.
- Outcome:
  - Landed a bounded fix so successful unregister now captures the old ID for map removal, then clears `HotkeyInfo.Id` and `NativeTriggerDescription` alongside the existing status reset.
  - Added regression coverage for explicit unregister and strengthened the clear-to-none test to verify runtime metadata is fully reset.
- Verification / blockers:
  - Parent repo upstream remained current this run: 0 upstream `develop` commits pending and no merge conflicts were needed.
  - `ShareX.ImageEditor` remotes were verified as `origin=https://github.com/KovaForge/ShareX.ImageEditor.git` and `upstream=https://github.com/ShareX/ShareX.ImageEditor.git`; the submodule remains on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, so no parent pointer update was required.
  - `dotnet build --configuration Release /m:1` passed with 0 warnings and 0 errors.
  - `dotnet test --configuration Release /m:1` exited successfully but still reported no discoverable tests in `tests/XerahS.Tests/bin/Release/net10.0-windows10.0.26100.0/XerahS.Tests.dll`.
- Follow-up:
  - Keep digging on the Windows-targeted NUnit discovery issue so the new hotkey regression coverage can execute in normal `dotnet test` runs.
  - Review another stale pending subsystem next run instead of revisiting hotkeys/input unless it becomes a follow-up area.

### 2026-04-20 10:07 GMT+8
- Area: CLI / command surface
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/desktop/cli/XerahS.CLI/Program.cs`
  - `src/desktop/cli/XerahS.CLI/Commands/CaptureCommand.cs`
  - `src/desktop/cli/XerahS.CLI/Commands/UploadCommand.cs`
  - `tests/XerahS.Tests/XerahS.Tests.csproj`
- Findings:
  - `UploadCommand` wrote `--text`, `--pipe`, and `--name` temp files directly under the shared temp root using the requested filename, so repeated or concurrent uploads could overwrite each other and leave stale files behind.
  - The command also logged the temporary path instead of the user-facing upload name, which is noisy and exposes an implementation detail.
- Outcome:
  - Landed a bounded fix in `UploadCommand` so temp-backed uploads now get a unique per-invocation temp directory, user-supplied names are sanitized to a file name, console output uses the display name, and the temporary directory is cleaned up on exit.
- Verification / blockers:
  - Parent repo upstream sync remained current this run: 0 upstream `develop` commits to merge and no conflicts.
  - `ShareX.ImageEditor` remotes were verified again and the submodule was left on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`; parent pointer did not need updating.
  - `dotnet build --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings and 0 errors. A prior default-parallel build hit an Avalonia file-lock race on `XerahS.RegionCapture.dll`.
  - `dotnet test --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` exited successfully but still reported no discoverable tests in `tests/XerahS.Tests/bin/Release/net10.0-windows10.0.26100.0/XerahS.Tests.dll`.
  - A temporary attempt to add direct CLI coverage into `XerahS.Tests` was backed out because referencing both `XerahS.App` (`XerahS`) and `XerahS.CLI` (`xerahs`) in the same test project causes an assembly-name collision on the Windows-targeted test graph.
- Follow-up:
  - Add dedicated CLI test coverage in an isolated test project or another harness that does not collide with the app assembly name.
  - Review remaining CLI commands for similar temp-path reuse and output-name inconsistencies.

### 2026-04-20 11:35 GMT+8
- Area: Platform-specific services
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/platform/XerahS.Platform.Linux/Services/LinuxClipboardService.cs`
  - `src/platform/XerahS.Platform.MacOS/MacOSClipboardService.cs`
  - `src/platform/XerahS.Platform.Windows/WindowsClipboardService.cs`
  - `tests/XerahS.Tests/Platform/Linux/LinuxClipboardServiceTests.cs`
- Findings:
  - Linux clipboard file drops were serialized as raw `file://{path}` strings and deserialized by stripping the prefix, which breaks paths containing spaces, `#`, `%`, and other URI-reserved characters.
  - That can corrupt pasted file paths on Linux desktop environments that expect RFC-compliant `text/uri-list` entries.
- Outcome:
  - Landed a bounded fix in `LinuxClipboardService` to emit proper escaped file URIs, parse newline-delimited URI lists safely, decode file URIs back to local paths, and ignore blank entries.
  - Added regression tests covering URI escaping, round-trip decoding, and mixed URI/plain-path clipboard payload parsing.
- Verification / blockers:
  - Upstream `develop` was already current: 0 commits pending merge, no conflicts.
  - `ShareX.ImageEditor` remotes were verified and the submodule remained on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`; no submodule pointer update was required.
  - `dotnet build --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings and 0 errors.
  - `dotnet test --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` exited successfully but still reported no discoverable tests in `tests/XerahS.Tests/bin/Release/net10.0-windows10.0.26100.0/XerahS.Tests.dll`.
- Follow-up:
  - Keep digging into NUnit/.NET 10 discovery so the newly added Linux clipboard regression tests actually execute in normal solution test runs.

### 2026-04-20 12:38 GMT+8
- Area: Notifications/toasts
- Reviewer: Mikhail hourly cron
- Files inspected:
  - `src/platform/XerahS.Platform.Linux/Services/LinuxNotificationService.cs`
  - `src/platform/XerahS.Platform.MacOS/Services/MacOSNotificationService.cs`
  - `src/platform/XerahS.Platform.Windows/WindowsNotificationService.cs`
  - `src/desktop/core/XerahS.Services.Abstractions/INotificationService.cs`
  - `tests/XerahS.Tests/Platform/NotificationServiceProcessStartInfoTests.cs`
- Findings:
  - `NotificationType` was effectively ignored by the desktop native notification implementations.
  - Linux always invoked `notify-send` without urgency mapping, macOS emitted the same AppleScript regardless of severity, and Windows debug fallback logs dropped the type entirely.
  - That made warning/error notifications indistinguishable from informational ones and left no typed regression coverage around the process start arguments.
- Outcome:
  - Landed a bounded fix so Linux maps `NotificationType` to `notify-send -u` urgency, macOS adds a subtitle for non-info severities while preserving safe AppleScript escaping, and Windows debug fallback logs now retain the notification type.
  - Updated notification process-start tests to cover Linux urgency mapping, macOS typed subtitle generation, and info-notification behavior without a subtitle.
- Verification / blockers:
  - Upstream `develop` was already current this run: 0 commits pending merge and no conflicts.
  - `ShareX.ImageEditor` remotes remain correct (`origin=KovaForge`, `upstream=ShareX`), the submodule is still on branch `develop` at `f85886a3f5f3d7a3c90249e939f2f42944fe8046`, and the parent repo already points at that commit.
  - `dotnet build --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` passed with 0 warnings and 0 errors.
  - `dotnet test --configuration Release -m:1 /p:UseSharedCompilation=false /nr:false` exited successfully but still reported no discoverable tests in `tests/XerahS.Tests/bin/Release/net10.0-windows10.0.26100.0/XerahS.Tests.dll`.
- Follow-up:
  - Keep digging on Windows-targeted NUnit discovery on the Linux host so the notification regression tests can execute in normal solution runs.
