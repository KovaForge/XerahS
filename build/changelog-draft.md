## [v0.23.117](https://github.com/ShareX/XerahS/releases/tag/v0.23.117)

### Features
- **Core**: add after-capture OCR clipboard task
- **Core**: Add capture command palette
- **Core**: add markdown directory index output
- **Core**: Add --randomize flag (default true) to CLI upload command, appending random alphanumeric suffix matching UI's %ra{10} behavior to avoid CDN caching
- **Core**: Implement Send-to post-v1 policies

### Fixes
- **CLI**: skip redundant named-copy for --text/--pipe when --name provided
- **Core**: Clean up temporary zip backups after replacement failure
- **Core**: Command palette UX: keyboard selection wrap, blank-escape close, search whitespace normalization
- **Core**: Correct IsFileLocked to return false for missing files and null/empty paths instead of misreporting them as locked
- **Core**: Editor save and integration: sidecar/image failure handling, dirty-state preservation, overwrite truncation, bitmap disposal, annotation persist-after-continue
- **Core**: Expose OCR index schema ensure for history deletes
- **Core**: FFmpeg and media pipeline: path escaping, cancellation, process-tree kill, CombineScreenshots guards, probe argument quoting, workflow override wiring
- **Core**: FileDownloader reliability: cancellation tokens, chunked encoding, early-EOF hang on Content-Length mismatch
- **Core**: Handle invalid backup copy paths
- **Core**: harden gdi cursor replacement cleanup
- **Core**: HSB equality includes Alpha to satisfy hash contract
- **Core**: Ignore stale deleted-file OCR when searching assistant history
- **Core**: Log stale uploader default cleanup
- **Core**: macOS: tray Dock icon hidden (#252), upload file picker fallback, front-window parsing, update prompts with manual action, clipboard path whitespace
- **Core**: match regional ocr defaults
- **Core**: Mobile S3 configuration: file-scoped config and imports
- **Core**: OCR onboarding language lifecycle: regional defaults, refresh and persistence, fallback when enumeration fails, null guards, failure message normalization
- **Core**: OpenClaw/CLI upload pipeline: text upload, JSON validation and diagnostics, path normalization, bootstrap uploader JSON, manifest parity, plugin bundling, macOS plugin discovery, S3 keychain credentials
- **Core**: Persist annotation edits after continue
- **Core**: Prevent user props from overriding release build guardrails
- **Core**: Remove display language selection from onboarding welcome step
- **Core**: Report invalid SFTP key files
- **Core**: Resolve CLI plugin discovery failure on macOS by prioritizing canonical Documents path and bundle S3 plugin during publish
- **Core**: Run silent Windows updater
- **Core**: Scrolling capture: ReferenceEquals guard when closing old capture window
- **Core**: share amazon s3 keychain credentials
- **Core**: StringCollectionToStringTypeConverter silent type erasure
- **Core**: Surface history backup failures
- **Core**: Toast notifications: fade opacity, multi-monitor bounds, context-menu close resume
- **Core**: Update ImageEditor effect browser spacing
- **Core**: Uploader default-instance lifecycle: non-mutating checks, stale cleanup, category validation, routing conflicts, auto fallback within category, drag-drop normalization
- **Core**: Wire WelcomeStepViewModel into onboarding wizard
- **History**: surface user-visible backup failure diagnostic via LastBackupFailureReason
- **ImageEditor**: normalize resource paths to forward slashes for cross-platform consistency (minimal bug fix); bump to 0.23.109
- **Indexer**: History and indexer: OCR index cleanup on delete, enumeration resilience for long paths and I/O errors
- **Linux**: Linux platform: Wayland/X11 capture routing, hotkey mapping (Oem102), deb packaging clipboard recommends (wl-clipboard, xclip), active-window fallbacks
- **MCP**: MCP history search and resources: query parsing, URI matching, thumbnail/blob paths, stale and oversized diagnostics, task identity race, error-shape alignment
- **SettingsBase**: Settings and backup reliability: async saves, atomic zip replacement, weekly backup TOCTOU handling, restore from backups, empty-destination guards, user-visible failure toasts
- **WaylandCliCapture Grim/slurp/grimblast**: Linux/macOS CLI subprocess reliability: stderr drain and bounded waits to prevent pipe-fill and timeout-stretching deadlocks (clipboard, theme, capture, input, audio, Wayland tools)

### Build
- **Core**: Dependency updates: Avalonia 12.0.5, SkiaSharp 3.119.4, SQLite bundle pins
- **Core**: macOS Info.plist template and hardened-runtime entitlements (not yet wired into packaging)
- **Docs**: sync CHANGELOG from KovaForge work after upstream merge

### Documentation
- **Core**: Append TypeConverter fix to hourly tracker
- **Core**: Blog drafts (2026 series)
- **Core**: Contributor workflow docs (AGENTS wrapper policy, CONTRIBUTING.md)
- **Core**: Correct FFmpeg Linux and override guidance
- **Core**: Linux and macOS improvement plans (XIP0077-XIP0079) and KNOWN_ISSUES updates
- **Core**: Normalize XIP proposal statuses
- **Core**: record editor save overwrite sweep
- **Core**: record editor sidecar save review
- **Core**: Record history backup user-visible diagnostic in state and tracker
- **Core**: Record MCP history blob sweep
- **Core**: Record MCP IsHistorySearchResourceUri hardening in state and tracker
- **Core**: record MCP thumbnail resource sweep
- **Core**: Record MCP URI sweep
- **Core**: Record OCR sweep results
- **Core**: Record uploader diagnostics sweep
- **Core**: Record WaylandCliCapture active-window fix in tracker
- **Core**: Reliability upgrade plan (observed state, failure modes, U1-U10 upgrades, simulations, sign-off)
- **Core**: Require XIP batch push and issue closure
- **Kfip**: XIP, IEIP, and KFIP proposals and related documentation

### Testing
- **Core**: Guardrail and test-coverage improvements (Headless.NUnit, McpServer.Tests, FFmpeg regression tests)

### Changed
- **Core**: [Fix] ToastWindow: adjust position to screen bounds on multi-monitor setups
- **Core**: [KFIP] Add KFIP0009 for X/Twitter screen capture workflow enhancements
- **Core**: [KFIP] Add KFIP0010 for X/Twitter compression-resilient capture and format optimization
- **Core**: [v0.23.47] Normalize platform OCR language tags and display names in tool UI language loader
- **Core**: Fix EmojiCatalogEntry.GetSearchScore case-insensitive SearchIndex lookup
- **Core**: Fix macOS crash when selecting folder in onboarding
- **Core**: Fix toast context menu close not resuming fade when duration has not elapsed
- **Core**: guard slurpOutput against null before Trim() in CaptureWithGrimSlurpAsync
- **Core**: Proposal documents (create/update)
- **Core**: Release and CI maintenance: prerelease defaults, v0.22.256 workflow docs, Flathub verification, Fedora updater script, package pins
- **Core**: ShareX.ImageEditor submodule updates
- **Fix Editor Save**: report image vs sidecar failures distinctly, keep dirty on image failure
