# XerahS Hourly Review Current Tracker

Purpose: compact human-readable companion to `docs/reports/hourly_review_state.json` for the recurring XerahS review.

Use `hourly_review_state.json` as the hot machine-readable source. The full historical ledger was preserved at `docs/reports/archive/hourly_review_tracker_2026-04-30.md`.

## Rules

- Read `docs/reports/hourly_review_state.json` first; use this file only for quick human context.
- Prefer the stalest high-priority area that is not blocked by a larger prerequisite.
- Record raw/noisy evidence as log paths only; do not paste full command output here.
- After each run, update the JSON area row, keep only a compact current summary here, and archive any long detail outside the hot files.

## Next Candidates

- Plugin loading/runtime
- Media subsystem
- Hotkeys/input
- MCP server
- CLI / command surface

## Current Coverage

| Area | Last Reviewed | Priority | Last Outcome | Follow-up |
|---|---|---|---|---|
| Capture pipeline | 2026-04-30 05:41 AWST | High | Fixed DXGI rectangle capture crop conversion so fractional coordinates are preserved outward and non-finite/huge finite values are rejected/clamped before integer casts. | Continue capture pipeline review around DXGI multi-monitor rotation/scaling edge cases and GDI fallback parity. |
| OCR | 2026-04-30 06:41 AWST | High | Fixed onboarding OCR language refresh so removed language options are unsubscribed before replacement, preventing stale options from mutating selected languages after refresh. | Continue OCR review around onboarding selected-language collection replacement/unsubscription and platform OCR language refresh edge cases. |
| Settings/configuration | 2026-04-30 07:41 AWST | High | Fixed uploader config saves so SettingsChanged observers are notified for destination/provider changes; added regression coverage. | Continue settings review around async save completion semantics and custom config backup paths. |
| Assistant local memory/privacy/history | 2026-04-30 00:34 AWST | High | Fixed OCR cache history path normalization so whitespace-padded caller paths hit canonical history rows. | Continue assistant review around history DB path casing/symlink equivalence and OCR cache invalidation when capture files are moved or deleted. |
| Tests / test discoverability | 2026-04-30 08:41 AWST | High | Marked main NUnit test discovery packages as private test/build assets so Microsoft.NET.Test.Sdk and NUnit3TestAdapter do not flow as normal transitive assets; added guardrail coverage. | Continue tests review around discovery package asset metadata for Avalonia.Headless.NUnit/coverage collectors and cross-target test host behavior. |
| Editor integration | 2026-04-30 09:43 AWST | High | Fixed editor window direct-close handling so ShowEditorSessionAsync completes with null instead of leaving callers awaiting forever; added regression coverage. | Continue editor integration review around Save/Save As result propagation and multi-image send-to sequencing. |
| Uploader core / plugin routing | 2026-04-30 01:20 AWST | High | Fixed file-extension input normalization for uploader destination lookup and add-file-type conflict checks. | Continue uploader routing review around stale default-instance IDs and case-insensitive instance/category lookups. |
| Plugin loading/runtime | 2026-04-29 04:11 AWST | High | Fixed plugin package installation so a missing plugins root is created before installing a .xsdp package instead of failing with DirectoryNotFoundException. |  |
| FTP uploader plugin | 2026-04-30 02:18 AWST | Medium | Fixed FTP/SFTP missing-directory retry parent-path handling for bare, nested, absolute, and root-file remote paths. | Next FTP pass can inspect URL generation for HttpHomePathAutoAddSubFolderPath with absolute SFTP subfolders and name-parser tokens. |
| Hotkeys/input | 2026-04-29 06:20 AWST | Medium | Fixed workflow hotkey retry after a failed registration leaves a stale assigned ID. |  |
| Imgur uploader plugin | 2026-04-29 07:11 AWST | Medium | Fixed Imgur Client ID normalization before config save, login URL generation, uploader creation, and explorer auth setup. |  |
| Media subsystem | 2026-04-29 08:31 AWST | High | Fixed VideoThumbnailer combined-grid generation so invalid/non-positive column counts are clamped instead of crashing with divide-by-zero. | Continue media review around TakeThumbnails hardcoded 30s FFmpeg timeout and GetRandomTimeSlice shared RandomFast state. |
| MCP server | 2026-04-29 11:15 AWST | Medium | Fixed annotation renderer parameter parsing so malformed/scalar MCP annotation inputs are coerced or ignored safely. | Continue MCP review around RunTaskAsync upload result distinction and broader annotation schema validation. |
| CLI / command surface | 2026-04-29 14:05 AWST | Medium | Fixed CLI upload to use the direct upload processor/bootstrap readiness path and added uploader doctor/bootstrap commands. | Continue CLI review around uploader bootstrap provider choices and JSON doctor output contract. |
| Notifications/toasts | 2026-04-30 03:15 AWST | Medium | Fixed zero-duration auto-hide toasts so they start fade immediately instead of remaining visible indefinitely. | Toast opacity/fade behavior now has explicit zero-duration coverage; rotate through older tracker items next. |
| File/path handling | 2026-04-30 04:36 AWST | High | Fixed SettingsBase weekly backup scheduling so weekly-only configurations create backup archives. | Continue rotating through older settings/file persistence edge cases; consider checking backup retention pruning interactions next. |
| Indexer subsystem | 2026-04-29 18:17 AWST | Medium | Fixed negative MaxDepthLevel handling so non-positive depth is treated as unlimited instead of suppressing root files/folders. | Continue indexer review around unauthorized/path-too-long enumeration parity and output file collision handling. |
| Platform-specific services | 2026-04-29 19:24 AWST | High | Fixed macOS file-drop clipboard path handling so relative/whitespace-padded paths are normalized before AppleScript POSIX file specifiers. | Continue platform-specific review around macOS clipboard helper process timeouts and AppleScript argument robustness. |
| Region capture / window enumeration | 2026-04-29 20:17 AWST | High | Fixed Sway Wayland rectangle parsing so malformed rect objects and overflowing right/bottom edges are rejected safely. | Continue region/window enumeration review around GNOME eval rect validation and X11 frame extent overflow/invalid metadata handling. |

## Recent Runs

### 2026-04-30 09:43 AWST - Editor integration
- Topic: editor window direct-close completion
- Outcome: Direct window closes now complete editor sessions with null, preserving task-mode cancel/no-save handling; added regression coverage; bumped version to 0.22.152.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260430-093615.log`; test `/tmp/xerahs-hourly-sweep/test-20260430-093931.log`

### 2026-04-30 08:41 AWST - Tests / test discoverability
- Topic: NUnit test discovery package asset metadata
- Outcome: Added PrivateAssets/all and explicit IncludeAssets for Microsoft.NET.Test.Sdk and NUnit3TestAdapter in XerahS.Tests; added regression guard; bumped version to 0.22.151.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260430-083931.log`; test `/tmp/xerahs-hourly-sweep/test-20260430-084035.log`

### 2026-04-30 07:41 AWST - Settings/configuration
- Topic: uploader config save notifications
- Outcome: Raised SettingsChanged after uploader config saves so destination/provider changes notify settings observers; added regression coverage; bumped version to 0.22.150.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260430-073743.log`; test `/tmp/xerahs-hourly-sweep/test-20260430-074103.log`

### 2026-04-30 06:41 AWST - OCR
- Topic: Onboarding OCR language refresh handlers
- Outcome: Unsubscribed removed onboarding OCR language options during platform language refresh and added regression coverage.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260430-063524.log`; test `/tmp/xerahs-hourly-sweep/test-20260430-063846.log`

### 2026-04-30 05:41 AWST - Capture pipeline
- Topic: DXGI crop rectangle coordinate conversion
- Outcome: Preserved fractional capture bounds outward and avoided invalid/overflow-prone casts before clamping.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260430-054007.log`; test `/tmp/xerahs-hourly-sweep/test-20260430-054110.log`

### 2026-04-30 04:36 AWST - File/path handling
- Topic: SettingsBase backup scheduling flags
- Outcome: Allowed weekly-only backup zip creation and added focused coverage.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260430-043410.log`; test `/tmp/xerahs-hourly-sweep/test-20260430-043553.log`

### 2026-04-30 03:15 AWST - Notifications/toasts
- Topic: zero-duration auto-hide fade startup
- Outcome: Started fade immediately for valid zero-duration auto-hide toasts and added focused coverage.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260430-031132.log`; test `/tmp/xerahs-hourly-sweep/test-20260430-031439.log`

### 2026-04-30 02:18 AWST - FTP uploader plugin
- Topic: remote upload directory retry path handling
- Outcome: Created missing retry directories from the parent remote path instead of bare filenames.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260430-021245.log`; test `/tmp/xerahs-hourly-sweep/test-20260430-021556.log`

### 2026-04-30 01:20 AWST - Uploader core / plugin routing
- Topic: file-extension input normalization
- Outcome: Normalized caller-provided extensions for destination lookup and duplicate route checks.
- Evidence: build `/tmp/xerahs-hourly-sweep/build-20260430-011652.log`; test `/tmp/xerahs-hourly-sweep/test-20260430-012004.log`

### 2026-04-30 10:42 AWST - Plugin loading/runtime
- Area: Plugin loading/runtime (.xsdp package install/extraction)
- Files: `src/desktop/core/XerahS.Uploaders/PluginSystem/PluginPackager.cs`, `tests/XerahS.Tests/Helpers/PluginManifestSecurityTests.cs`, `Directory.Build.props`
- Finding: duplicate zip entries in a plugin package could be extracted with overwrite semantics after the manifest was preview-validated, allowing a later duplicate `plugin.json` or assembly entry to replace earlier content in the temp install tree.
- Fix: reject duplicate normalized archive entry paths during safe extraction and cover duplicate `plugin.json` packages with a regression test.
- Version: bumped `0.22.152` -> `0.22.153`.
- Build: pass, 0 warnings/0 errors (`/tmp/xerahs-hourly-sweep/build-20260430-103803.log`).
- Tests: pass, 724 total / 0 failed (`/tmp/xerahs-hourly-sweep/test-20260430-104119.log`).
- Follow-up: continue plugin runtime review around package entry canonicalization, duplicate directory/file collisions, and load-context unload diagnostics.
