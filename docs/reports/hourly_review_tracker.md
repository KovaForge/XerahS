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
| OCR | - | Pending | - | High | OCR integration, settings, fallback/error handling |
| Editor integration | - | Pending | - | High | ShareX.ImageEditor integration and submodule touchpoints |
| Uploader core | - | Pending | - | High | Generic uploader orchestration, retries, cancellation |
| Nextcloud uploader plugin | 2026-04-19 14:16 GMT+8 | Reviewed | Inspected, no safe bounded fix landed | Medium | Review summary reported in hourly cron output |
| FTP uploader plugin | - | Pending | - | Medium | Path handling, credential flow, error surfacing |
| Imgur uploader plugin | - | Pending | - | Medium | Upload response validation, failures |
| Settings/configuration | - | Pending | - | High | Config persistence, migration, invalid values |
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
