# XerahS Hourly Review Current Tracker

Purpose: compact human-readable companion to `docs/reports/hourly_review_state.json` for the recurring XerahS review.

Use `hourly_review_state.json` as the hot machine-readable source. The full historical ledger was preserved at `docs/reports/archive/hourly_review_tracker_2026-04-30.md`.

## Rules

- Read `docs/reports/hourly_review_state.json` first; use this file only for quick human context.
- Prefer the stalest high-priority area that is not blocked by a larger prerequisite.
- Record raw/noisy evidence as log paths only; do not paste full command output here.
- After each run, update the JSON area row, keep only a compact current summary here, and archive any long detail outside the hot files.

## Next Candidates

- Settings/configuration
- Tests / test discoverability
- Editor integration
- Plugin loading/runtime
- Media subsystem

## Current Coverage

| Area | Last Reviewed | Priority | Last Outcome | Follow-up |
|---|---|---|---|---|
| Capture pipeline | 2026-04-30 05:41 AWST | High | Fixed DXGI rectangle capture crop conversion so fractional coordinates are preserved outward and non-finite/huge finite values are rejected/clamped before integer casts. | Continue capture pipeline review around DXGI multi-monitor rotation/scaling edge cases and GDI fallback parity. |
| OCR | 2026-04-30 06:41 AWST | High | Fixed onboarding OCR language refresh so removed language options are unsubscribed before replacement, preventing stale options from mutating selected languages after refresh. | Continue OCR review around onboarding selected-language collection replacement/unsubscription and platform OCR language refresh edge cases. |
| Settings/configuration | 2026-04-28 22:21 AWST | High | Fixed settings upgrade detection/version stamping so saved settings persist the current app version, loads mark real upgrades, and IsUpgradeFrom uses numeric version comparison instead of lexicographic string ordering. |  |
| Assistant local memory/privacy/history | 2026-04-30 00:34 AWST | High | Fixed OCR cache history path normalization so whitespace-padded caller paths hit canonical history rows. | Continue assistant review around history DB path casing/symlink equivalence and OCR cache invalidation when capture files are moved or deleted. |
| Tests / test discoverability | 2026-04-29 00:10 AWST | High | Hardened XerahS.McpServer.Tests test adapter metadata so xunit.runner.visualstudio stays a private/build-only test asset instead of flowing as a normal transitive package asset. |  |
| Editor integration | 2026-04-29 01:10 AWST | High | Fixed CLI/watch-folder headless editor fallback so unavailable editors return null instead of falsely returning the original bitmap as an edited result. |  |
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
