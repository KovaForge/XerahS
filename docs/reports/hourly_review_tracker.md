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
| Capture pipeline | 2026-04-19 20:09 GMT+8 | Reviewed | Fixed GDI bitmap DC restoration to avoid deleting selected HBITMAPs | High | Reviewed Windows capture path; tests still non-discoverable in current solution run |
| OCR | 2026-04-19 23:11 GMT+8 | Reviewed | Fixed after-capture OCR to honor configured task OCR options instead of forcing English/2x/multiline defaults | High | Reviewed CaptureJobProcessor OCR path; full solution build still SIGKILLed in this environment and tests remain non-discoverable |
| Editor integration | 2026-04-19 22:06 GMT+8 | Reviewed | Fixed history refresh detection when editor saves without advancing file timestamp; broader test/build blocked by missing ShareX.ImageEditor restore assets | High | Reviewed Avalonia editor session flow, history refresh, and headless UI service paths |
| Uploader core | 2026-04-20 03:06 GMT+8 | Reviewed | Fixed custom uploader force-reload cleanup so deleted/updated definitions do not leave stale providers in memory | High | Reviewed plugin-system reload paths; full-solution Release verification still hit plugin auto-build / SIGKILL pressure in this environment |
| Nextcloud uploader plugin | 2026-04-19 14:16 GMT+8 | Reviewed | Inspected, no safe bounded fix landed | Medium | Review summary reported in hourly cron output |
| FTP uploader plugin | - | Pending | - | Medium | Path handling, credential flow, error surfacing |
| Imgur uploader plugin | - | Pending | - | Medium | Upload response validation, failures |
| Settings/configuration | 2026-04-20 00:11 GMT+8 | Reviewed | Fixed ResetSettings so it also backs up and deletes SecretsStore artifacts instead of leaving credentials behind | High | Reviewed settings persistence/reset paths; full Release verification still blocked by missing ShareX.ImageEditor host restore assets and SIGKILL pressure |
| Hotkeys/input | - | Pending | - | Medium | Recorder, edge cases, platform-specific key mapping |
| Notifications/toasts | - | Pending | - | Low | UX correctness, fallback text, timing |
| Plugin loading/runtime | - | Pending | - | High | Assembly loading, Avalonia compiled XAML, metadata mismatches |
| CLI / command surface | - | Pending | - | Medium | Argument handling, exit codes, error messages |
| Platform-specific services | - | Pending | - | Medium | Windows/Linux/macOS abstractions and guards |
| File/path handling | - | Pending | - | High | Long paths, invalid chars, normalization, temp files |
| Tests / test discoverability | - | Pending | - | High | Missing execution, solution integration, runtime targeting |

## Review Log

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
