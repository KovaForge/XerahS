## v0.23.105

### Features
- **Core**: add after-capture OCR clipboard task (2ac82fab)

### Fixes
- **CLI**: skip redundant named-copy for --text/--pipe when --name provided (3c55fcb6)
- **Core**: CLI/OpenClaw wrapper manifest-vs-runtime parity (requireUploaderReport, --json) (6b08fc97)
- **Core**: Make OpenClaw manifest DTOs public. (30f12f37)
- **Core**: Surface history backup failures as user-visible toast in HistoryViewModel (4ccf08b8)
- **History**: surface user-visible backup failure diagnostic via LastBackupFailureReason (e0f91f93)
- **Linux Deb Packaging**: add Recommends wl-clipboard, xclip so clipboard CLI fallback works out-of-box on stock Ubuntu (LINUX-IMPROVEMENT-PLAN P3) (6eac1277)
- **MCP**: harden IsHistorySearchResourceUri against prefix and malformed query attacks (de6e3939)
- **SettingsBase**: surface SettingsBackupFailed event with phase tag for backup create/prune/pruneFolder failures (f756d368)

### Build
- **Core**: Add macOS Info.plist template and hardened-runtime entitlements (MACOS-IMPROVEMENT-PLAN P1/P2; plutil-lint clean, not yet wired into packaging) (9cebcf0a)
- **Core**: Pin SQLite bundle packages for restore audit. (b58489b7)
- **Docs**: sync CHANGELOG from KovaForge work after upstream merge (dc0940a6)

### Documentation
- **Add Linux Improvement Plan**: evidence-based state assessment, prioritized P1-P8 backlog, top-5 implementation outlines with verification and rollback (2737d319)
- **Add MacOS Improvement Plan**: evidence-based state assessment, prioritized P1-P10 backlog, top-5 implementation outlines with verification and rollback (27e23488)
- **Add XIP0080**: Linux global hotkeys via direct evdev listener (99a195b7)
- **Core**: Add 2026-06-06 OCR onboarding persistence blog draft. (c6038164)
- **Core**: Add 2026-06-06 no-activity blog draft. (7f4e321c)
- **Core**: Add 2026-06-07 no-activity blog draft. (36a5d295, f1057f09)
- **Core**: Add 2026-06-08 blog draft. (22a55a9c)
- **Core**: Add 2026-06-09 no-activity blog draft. (e9c4b79f)
- **Core**: Add 2026-06-10 blog draft. (8d490d9b)
- **Core**: Add 2026-06-11 blog draft. (dbf26515)
- **Core**: Add 2026-06-11 no-activity blog draft. (5d205ed9)
- **Core**: Add 2026-06-12 blog draft. (8cfad0f6)
- **Core**: Add 2026-06-12 daily blog draft. (79925979)
- **Core**: Add 2026-06-12 no-activity blog draft. (f20c1a94)
- **Core**: Add 2026-06-13 daily blog draft. (5e89df90)
- **Core**: Add 2026-06-13 no-activity blog draft. (b30c490a)
- **Core**: Add 2026-06-14 no-activity blog draft. (3ed56654)
- **Core**: Add 2026-06-15 no-activity blog draft. (f3029f77)
- **Core**: Add 2026-06-16 no-activity blog draft. (f934c738)
- **Core**: Add 2026-06-17 blog draft. (4d0d746b)
- **Core**: Add 2026-06-18 blog draft. (da1fa370)
- **Core**: Add 2026-06-19 blog draft. (53170428)
- **Core**: Expand XIP0080 with scope, branching strategy, and success criteria (e3189a4f)
- **Core**: Hourly sweep tracker/state update (clean review + upstream merge) (31eb2236)
- **Core**: KNOWN_ISSUES: document macOS distribution, permission, and window-capture issues; link improvement plan (dd519426)
- **Core**: Move improvement plans into XIP proposals (XIP0077-XIP0079). (f5d969ca)
- **Core**: RELIABILITY-PLAN — failure simulations, sign-off list, drift findings, sequencing (sections 4-7) (a8a10c70)
- **Core**: RELIABILITY-PLAN — observed-state snapshot + failure-mode table (sections 1-2) (a44df371)
- **Core**: RELIABILITY-PLAN — prioritized upgrades U1-U10 with steps/criteria/owners/rollbacks (section 3) (9bc0dcd1)
- **Core**: Record HistoryViewModel backup-toast fix in tracker and state (be4fdcba)
- **Core**: Record MCP IsHistorySearchResourceUri hardening in state and tracker (0392f2e2)
- **Core**: Record history backup user-visible diagnostic in state and tracker (96e62d2b)
- **Core**: Refresh 2026-06-05 blog draft. (3744bfee)
- **Core**: Refresh 2026-06-06 blog draft. (387df544)
- **Core**: Refresh 2026-06-07 blog draft. (95377e08)
- **Core**: Refresh 2026-06-08 blog draft. (36d5acff)
- **Core**: Refresh 2026-06-09 blog draft. (eb95c4c3)
- **Core**: Refresh 2026-06-10 blog draft. (ee2114d4)
- **Core**: Refresh 2026-06-13 blog draft. (df0e51e0)
- **Core**: Refresh 2026-06-14 blog draft. (330febfa)
- **Core**: Refresh 2026-06-15 blog draft. (7d2817cb)
- **Core**: Refresh 2026-06-16 blog draft. (038997ff)
- **Core**: Refresh 2026-06-17 blog draft. (b3392d6c)
- **Core**: Refresh 2026-06-18 blog draft. (2e473086)
- **Core**: Refresh 2026-06-19 blog draft. (0d29b9be)
- **Core**: add AGENTS wrapper policy (e0e75d9c, eb3e0fe7)
- **Core**: add KFIP0010 for X/Twitter OCR clipboard accessibility drafts (64047239)
- **Core**: review and narrow KFIP0010 implementation scope (8fc3398b)

### Changed
- **Core**: [KFIP] Add KFIP0010 for X/Twitter compression-resilient capture and format optimization (fa236405)
## v0.23.98

### Features
- **Core**: Add --randomize flag (default true) to CLI upload command, appending random alphanumeric suffix matching UI's %ra{10} behavior to avoid CDN caching (6315a90c)

### Fixes
- **Core**: Add FFmpeg concat file escape regression tests for EscapeConcatFilePath (ca499b00)
- **Core**: Add macOS upload file picker fallback (98c32d24)
- **Core**: Add non-mutating IsDefaultInstance to prevent GetDefaultInstance destructive side-effect during read-only is_default checks (57007555)
- **Core**: Align plugin assembly version with root app version (79eaffb0)
- **Core**: Apply onboarding OCR language to DefaultTaskSettings (97bfbc8a)
- **Core**: Bundle CLI plugins for agent hosts (3aeca5a4)
- **Core**: Clean up HistoryOcrIndex rows when history items are deleted (8d04e49b)
- **Core**: Clean up temporary zip backups after replacement failure (a1206525)
- **Core**: CopyFile exception handling and BackupFileZip atomic replacement (a54d2bea)
- **Core**: Decode plus signs in MCP history searches (cac58f31)
- **Core**: Emit zero opacity before toast fade close (af8aa04a)
- **Core**: Escape FFmpeg concat list paths (939c9e8d)
- **Core**: Escape FFmpeg video probe paths (b7a2facf)
- **Core**: Escape special URI chars in MCP CreateFileUrl output (eeccf40f)
- **Core**: Expose OCR index schema ensure for history deletes (d653646d)
- **Core**: FFmpegDownloader cancellation token propagation (b777ea5d)
- **Core**: FileDownloader cancellation token propagation and InvalidOperationException handling (15f5e784)
- **Core**: FileDownloader chunked/streaming-encoding support (15a5773a)
- **Core**: FileDownloader early EOF hang on Content-Length mismatch (a0472ad5)
- **Core**: Guard ScrollingCaptureToolService.CurrentCapture clear with ReferenceEquals so closing old window does not lose active capture from newer window (ff788638)
- **Core**: Guard null OCR onboarding language selections (d012848d)
- **Core**: HSB equality includes Alpha to satisfy hash contract (dc08334a)
- **Core**: Handle IOException in BackupFileWeekly TOCTOU race (b3becc1f)
- **Core**: Handle invalid backup copy paths (993bbf90)
- **Core**: Handle weekly backup destination failures (f609d6e9)
- **Core**: Ignore malformed MCP history query parameters (16d0cae0)
- **Core**: Ignore stale deleted-file OCR when searching assistant history (0fb00f89)
- **Core**: Ignore unavailable uploader instances in file routing conflicts (248b4160)
- **Core**: Keep auto uploader fallback within category (cd8135a8)
- **Core**: Kill FFmpeg process tree on forced close (fa103593)
- **Core**: Linux hotkey Oem102 key mapping to backslash for Wayland portal and X11 fallback (eb6b878a)
- **Core**: LinuxCliToolRunner pipe-drain deadlock (b23cb6ba)
- **Core**: LinuxThemeService gsettings pipe-fill + timeout-stretching deadlock (74652cf4)
- **Core**: Log stale default uploader cleanup on RemoveInstance for parity with GetDefaultInstance and category-change UpdateInstance paths (6705b0e9)
- **Core**: Log stale uploader default cleanup (cb2edffd)
- **Core**: Make RegionCaptureAnnotationOptionsStore.Persist awaitable to prevent fire-and-forget config save data loss (91eaa4b3)
- **Core**: Normalize OCR failure status messages (dcadd89d)
- **Core**: Normalize upload drag-drop file collection handling (04fe3eb2)
- **Core**: Persist annotation edits after continue (85e3eac0)
- **Core**: Preserve MCP local path whitespace (a3e40a3a)
- **Core**: Preserve macOS clipboard file path whitespace (8d74190b)
- **Core**: Prevent user props from overriding release build guardrails (0bbaa3b8)
- **Core**: Prune empty plugin quarantine folders (b453a38d)
- **Core**: Prune old settings backup month folders after successful save (c2200f7e)
- **Core**: Recreate embedded editor save destinations (903138a9)
- **Core**: Report invalid SFTP key files (d3d94864)
- **Core**: Resolve CLI plugin discovery failure on macOS by prioritizing canonical Documents path and bundle S3 plugin during publish (3366f0f5)
- **Core**: Return actionable MCP oversized blob resources (bc403d6f)
- **Core**: Return actionable missing MCP history blobs (dd99a07e)
- **Core**: Run silent Windows updater (4705b063)
- **Core**: StringCollectionToStringTypeConverter silent type erasure (7ac362a5)
- **Core**: Surface OCR language refresh errors (01251cc8)
- **Core**: Surface history backup failures (0200f3e1)
- **Core**: Tighten MCP history search URI matching (660cbf1a)
- **Core**: Unblock macOS update prompts and add manual update action (84c05a67)
- **Core**: Update ImageEditor effect browser spacing (6f7700b7)
- **Core**: WaylandCliCapture active-window fallback for SWAY (688a78ba)
- **Core**: Wire OcrStepViewModel into onboarding wizard and add XAML template (92773c76)
- **Core**: Wire WelcomeStepViewModel into onboarding wizard (7e282755)
- **Core**: Wire workflow FFmpeg override into recording backends (96d254f8)
- **Core**: add MaxCombinedWidth/Height guards to CombineScreenshots dimension overflow (4fba4e73)
- **Core**: bootstrap uploaders exit code reflects blocking issues (b5163200)
- **Core**: re-throw UnauthorizedAccessException from annotation sidecar save (c80b23c5)
- **FileHelpers**: guard BackupFileZip/BackupFileWeekly against empty/whitespace destination folders (c5236d5a)
- **Indexer**: guard CountIndexedContents DirectoryInfo ctor inside try-catch (bd63b227)
- **LinuxInputService TryGetWithXdotool**: drain stderr and bound stdout wait to prevent pipe-fill and timeout-stretching deadlocks (20bf0f95)
- **LinuxScreenService Xrandr Capture**: drain stderr and bound stdout wait to prevent pipe-fill and timeout-stretching deadlocks (2a8c7d4a)
- **MCP CreateHistoryDetailsAsync**: surface file_exists/file_missing_path stale-path diagnostic (27f8216c)
- **MCP ResolveHistoryBlobPath**: distinguish thumbnail-vs-source missing file in exception message (e8c9fb14)
- **MacOSInputService GetCursorPosition**: drain stderr and bound stdout wait to prevent pipe-fill and timeout-stretching deadlocks (aee46cb7)
- **OCR**: persist full onboarding language selection to OCROptions.PreferredLanguages (3fc75695)
- **PulseAudioHelper RunPactl**: drain stderr and bound stdout wait to prevent pipe-fill and timeout-stretching deadlocks (b4e23a9f)
- **WaylandCliCapture Grim/slurp/grimblast**: drain stderr and bound stdout wait to prevent pipe-fill and timeout-stretching deadlocks (1a0962d7)

### Documentation
- **Core**: Add 2026-05-16 indexer and release blog draft. (f846381d)
- **Core**: Add 2026-05-16 no-activity blog draft. (12638c09)
- **Core**: Add 2026-05-17 OCR and updater blog draft. (a9d6cdd4)
- **Core**: Add 2026-05-17 reliability fixes blog draft. (ee25e967)
- **Core**: Add 2026-05-17 silent-updater blog draft. (6ae289a3)
- **Core**: Add 2026-05-18 no-activity blog draft. (3f3d4376)
- **Core**: Add 2026-05-18 reliability fixes blog draft. (f7e08763)
- **Core**: Add 2026-05-19 stability work blog draft. (bc384ba1)
- **Core**: Add 2026-05-20 uploader routing blog draft. (12c6f649)
- **Core**: Add 2026-05-22 daily blog draft. (b1fcb513)
- **Core**: Add 2026-05-23 daily blog draft. (5698e4b1)
- **Core**: Add 2026-05-25 no-activity blog draft. (ff592f33)
- **Core**: Add 2026-05-26 no-activity blog draft. (1f244917)
- **Core**: Add 2026-05-27 no-activity blog draft. (574eaa48, a672d8e6)
- **Core**: Add 2026-05-28 coding activity blog draft. (b0bcceea)
- **Core**: Add 2026-05-28 documentation-only blog draft. (b90c0e7b)
- **Core**: Add 2026-05-28 no-activity blog draft. (2cae5f21)
- **Core**: Add 2026-05-29 HSB equality fix blog draft. (bd71441b)
- **Core**: Add 2026-05-29 coding activity blog draft. (8aa87dc7)
- **Core**: Add 2026-05-29 fix roundup blog draft. (645e5034)
- **Core**: Add 2026-05-29 no-activity blog draft. (3fa5e15a)
- **Core**: Add 2026-05-30 no-activity blog draft. (0ef79e11)
- **Core**: Add 2026-05-30 no-activity status blog draft. (aa8b4649)
- **Core**: Add 2026-05-30 plugin version alignment blog draft. (b2415ab3)
- **Core**: Add 2026-05-31 CLI bootstrap follow-up blog draft. (2e80d0d4)
- **Core**: Add 2026-05-31 FFmpeg blog draft. (775b9906)
- **Core**: Add 2026-05-31 no-activity blog draft. (da37b1be)
- **Core**: Add 2026-06-01 blog draft. (a9543149)
- **Core**: Add 2026-06-01 docs-only activity blog draft. (ea5b585b)
- **Core**: Add 2026-06-01 no-activity blog draft. (3c3f027c)
- **Core**: Add 2026-06-02 blog draft. (8719ebb3)
- **Core**: Add 2026-06-02 docs-maintenance blog draft. (2d112157)
- **Core**: Add 2026-06-02 no-activity blog draft. (057acb2f)
- **Core**: Add 2026-06-03 blog draft. (269b5926)
- **Core**: Add 2026-06-03 no-activity blog draft. (63763964)
- **Core**: Add 2026-06-03 uploader cleanup and OCR parity blog draft. (e22460cc)
- **Core**: Add 2026-06-04 blog draft. (1cd748b4)
- **Core**: Add 2026-06-04 no-activity blog draft. (0e4bba5d)
- **Core**: Add 2026-06-05 blog draft. (a8c7ef0c)
- **Core**: Add 2026-06-05 no-activity blog draft. (39b74797)
- **Core**: Add 2026-06-06 no-activity blog draft. (15a1bc0e)
- **Core**: Append FileDownloader cancellation token review tracker entry (63b8a1de)
- **Core**: Append TypeConverter fix to hourly tracker (f0568d15)
- **Core**: Correct FFmpeg Linux and override guidance (093f3d3a)
- **Core**: Record CreateHistoryDetailsAsync stale-path fix in tracker and state (90a7704c)
- **Core**: Record Declan auth sweep blocker (1a740850, 95677987)
- **Core**: Record FFmpegDownloader cancellation fix in tracker and state (61434773)
- **Core**: Record FileHelpers empty/whitespace destination fix in tracker and state (0f471c52)
- **Core**: Record LinuxCliToolRunner pipe-drain fix in tracker and state (7e20351a)
- **Core**: Record LinuxInputService xdotool fix in tracker and state (4c307ea0)
- **Core**: Record LinuxScreenService xrandr capture fix in tracker and state (0c40b591)
- **Core**: Record LinuxThemeService gsettings fix in tracker and state (f32ba590)
- **Core**: Record MCP ResolveHistoryBlobPath fix in tracker and state (f80df7b1)
- **Core**: Record MCP URI sweep (25382a73)
- **Core**: Record MCP history blob sweep (7f48dcbd)
- **Core**: Record MacOSInputService osascript fix in tracker and state (e452e06f)
- **Core**: Record OCR sweep results (4f5f9b0a)
- **Core**: Record OCROptions.PreferredLanguages multi-language persistence fix in tracker and state (0079a1ff)
- **Core**: Record OcrViewModel onboarding language-parity fix in tracker and state (a82ec142)
- **Core**: Record PulseAudioHelper RunPactl fix in tracker and state (b7ada146)
- **Core**: Record RemoveInstance stale-default cleanup logging fix in tracker and state (f509f7eb)
- **Core**: Record WaylandCliCapture active-window fix in tracker (baa44de9)
- **Core**: Record WaylandCliCapture grim/slurp/grimblast stderr-drain fix in tracker and state (3ba026f0)
- **Core**: Record blog audit submodule lesson. (2bfa4724)
- **Core**: Record uploader diagnostics sweep (89046f9d)
- **Core**: Refresh 2026-05-17 silent-updater blog draft. (4e60d4b6)
- **Core**: Refresh 2026-05-29 audited blog draft. (d36b263b)
- **Core**: Refresh 2026-05-29 fix roundup blog draft. (f1e30745)
- **Core**: Refresh 2026-05-30 audit note blog draft. (0e1bf085)
- **Core**: Refresh 2026-05-30 blog draft. (cce60eaf)
- **Core**: Refresh 2026-05-31 blog draft. (27d4fa36)
- **Core**: Refresh 2026-06-01 blog draft. (df926fe8)
- **Core**: Refresh 2026-06-02 blog draft. (cf94ed2a)
- **Core**: Refresh 2026-06-03 blog draft. (86ba4a07)
- **Core**: Refresh 2026-06-04 blog draft. (3c9d1823)
- **Core**: Update hourly review state JSON (f6714a3e)
- **Core**: Update hourly review tracker (61060ee7)
- **Core**: Update hourly review tracker after BackupFileWeekly fix (7386b4fc)
- **Core**: Update hourly review tracker and state JSON (642a161a, 64c763ca, 839ec142, de402053, ef95e94b, f5af1fa6)
- **Core**: Update hourly review tracker for MCP path fix (59f7919d)
- **Core**: Update hourly review tracker for OCR null guard (856694b4)
- **Core**: Update hourly review tracker for Settings/configuration backup retention fix (2cc07195)
- **Core**: Update hourly_review_state.json with Wayland active-window fix (0c8cbdf5)
- **Core**: add CONTRIBUTING.md with git wrapper identity rules (b57231e3)
- **Core**: log 2026-05-18 04:33 AWST editor integration sidecar save fix (b5a55f91)
- **Core**: log 2026-05-18 04:49 AWST media subsystem CombineScreenshots overflow guards (5a92156d)
- **Core**: record CLI OpenClaw plugin export review sweep (174181ce)
- **Core**: record CLI ReClip/bootstrap continued review sweep (9da3e9d2)
- **Core**: record OCR language refresh review sweep (fd1211fe)
- **Core**: sync tracker from prior Declan run (042d9c86)
- **Core**: update hourly review state for CLI continued review sweep (2aa770cb)
- **Core**: update hourly review tracker and state after toast OnMenuClosed fix (04f6f6a9)
- **Core**: update hourly review tracker and state for File/path handling fixes (a54d2bea) (d1c3eed9)
- **Core**: update hourly review tracker for indexer ctor guard fix (bd63b227) (4ea07e70)
- **Core**: update hourly_review_state.json after editor integration fix (56d26fe5)
- **Core**: update hourly_review_state.json after media subsystem CombineScreenshots fix (9b0b87ba)

### Changed
- **Core**: Bump version to 0.23.31 (9dfb3481)
- **Core**: Bump version to 0.23.37 (b2cb0bd7)
- **Core**: Finalize hourly review tracker commit refs (f1cdd069)
- **Core**: Fix toast context menu close not resuming fade when duration has not elapsed (c92c3224)
- **Core**: Trim next_candidates to be more specific about remaining toasts work (8d331ad0)
- **Core**: Update Directory.Packages.props (c153fdc2)
- **Core**: Update ImageEditor to ShareX@abff8a8f8 (53c6f8d6)
- **Core**: Update ShareX.ImageEditor (cbf1587d)
- **Core**: Update hourly review state JSON (030d8161)
- **Core**: Update hourly review state after BackupFileWeekly fix (716bcf50)
- **Core**: Update hourly review state after Editor integration fix (0.23.37) (722896d1)
- **Core**: Update hourly review tracker after OCR cleanup (74a28ca1)
- **Core**: Update hourly review tracker after platform fix (9a829c8c)
- **Core**: Update hourly review tracker for FFmpeg close fix (4125df06)
- **Core**: Update hourly review tracker for FFmpeg concat fix (415c7525)
- **Core**: Update hourly review tracker for FFmpeg path quoting (4b98f152)
- **Core**: Update hourly review tracker for FTP fix (b6fc431d)
- **Core**: Update hourly review tracker for MCP blob fix (1257e9fa)
- **Core**: Update hourly review tracker for MCP query fix (1230c07c, 47141057)
- **Core**: Update hourly review tracker for OCR language refresh fix (99f760ec)
- **Core**: Update hourly review tracker for ToastWindow multi-monitor fix (8cd83e5f)
- **Core**: Update hourly review tracker for assistant OCR search fix (0aa8cfcc)
- **Core**: Update hourly review tracker for auto uploader fallback fix (688a53be)
- **Core**: Update hourly review tracker for backup cleanup fix (044daa4d)
- **Core**: Update hourly review tracker for build props guardrail fix (8782e622)
- **Core**: Update hourly review tracker for file path fix (a7e74c24)
- **Core**: Update hourly review tracker for history backup fix (2888e536)
- **Core**: Update hourly review tracker for plugin cleanup fix (196040ee)
- **Core**: Update hourly review tracker for toast fade fix (f4ef4215)
- **Core**: Update hourly review tracker for upload drag-drop fix (9cf7ad97)
- **Core**: Update hourly review tracker for uploader routing fix (b7517a19)
- **Core**: Update hourly review tracker for weekly backup fix (15c692f8)
- **Core**: [Docs] Record sync-only sweep at v0.23.86 (9852a1f5)
- **Core**: [Docs] Update hourly review tracker for OCR tool UI language loader normalization (7040c70f)
- **Core**: [Fix] ToastWindow: adjust position to screen bounds on multi-monitor setups (d20dc8b5)
- **Core**: [KFIP] Add KFIP0009 for X/Twitter screen capture workflow enhancements (04b8049b)
- **Core**: [Meta] Update hourly review tracker for editor annotation PersistAsync fix (5cd7e27f)
- **Core**: [Meta] Update hourly review tracker for scrolling capture fix v0.23.51 (ff7b2589)
- **Core**: [Meta] Update review tracker with commit hash 57007555 (b9d8fe72)
- **Core**: [v0.23.47] Normalize platform OCR language tags and display names in tool UI language loader (be5df01f)
- **Core**: [v0.23.71] docs: append hourly review tracker entry for Oem102 hotkey fix (013112ec)
- **Core**: [v0.23.71] docs: record sync-only sweep 2026-05-25 (cae2e247)
- **Core**: [v0.23.71] docs: update hourly review state for Oem102 hotkey fix (71e78428)
- **Core**: queue 5 clawpatch findings into hourly sweep tracker (19c61c46)
- **Fix Editor Save**: report image vs sidecar failures distinctly, keep dirty on image failure (2c8c4a36)

## v0.23.27

### Features
- **Core**: Add capture command palette (4f22032d)
- **Core**: Implement Send-to post-v1 policies (7c4b11dd)
- **Core**: add markdown directory index output (040f6e5f)

### Fixes
- **Core**: Add Avalonia.Headless.NUnit PrivateAssets parity and extend guardrail test coverage (a9e30545)
- **Core**: Add coverlet.collector and PrivateAssets to McpServer.Tests with guardrail test (6ed7fe95)
- **Core**: Clamp negative Padding/Spacing in VideoThumbnailer.CombineScreenshots to prevent negative bitmap dimensions (ecf35aae)
- **Core**: Clean stale default instance mappings on category change and validate category in GetDefaultInstance (6fa5e6fe)
- **Core**: Correct IsFileLocked to return false for missing files and null/empty paths instead of misreporting them as locked (d27c1f97)
- **Core**: Dispose editor copy SKBitmap to prevent resource leak in HandleCopyRequested (69306f25)
- **Core**: Drain stderr in macOS clipboard pbpaste/pbcopy helpers and remove unreachable stderr redirect from Linux clipboard/monitor process starts to prevent pipe-buffer deadlocks (7ba6823a)
- **Core**: Handle PathTooLongException, DirectoryNotFoundException, and IOException in indexer enumeration so long paths, deleted directories, and I/O errors don't crash the entire index or fallback count; add InternalsVisibleTo for Indexer tests (9c631766)
- **Core**: Hide macOS Dock icon for tray startup (#252) (7bff961e)
- **Core**: MCP history/search query parsing handles ampersand delimiters, supports limit/from/to params, and resource error shape includes UserCancelled/ArgumentOutOfRangeException (1d03d95e, cd9f4369)
- **Core**: MCP server RunTaskAsync task identity race condition (b8af4ba6)
- **Core**: Omit MCP history thumbnail_resource URI when no local file exists (67e134a3)
- **Core**: Remove malformed trailing quotes from FFmpeg GetVideoInfo probe argument (3c60ca2d)
- **Core**: add openclaw cli text upload (11c06aca)
- **Core**: align HandlePromptsGetAsync error shape with other MCP handlers (b318078b)
- **Core**: await async settings saves (8fbdaed6)
- **Core**: bound openclaw cli diagnostic keys (9d6b48b3)
- **Core**: bound openclaw cli json shape diagnostics (77e87220)
- **Core**: close command palette on blank escape (9cca74ce)
- **Core**: describe openclaw cli json validation failures (59746bf3)
- **Core**: dispose send-to editor bitmaps (654c871d)
- **Core**: emit bootstrap uploader cli json (44e8e2b5)
- **Core**: expose MCP history thumbnail resource URI (3e021e04)
- **Core**: harden gdi cursor replacement cleanup (826799db)
- **Core**: harden macos front-window parsing (8246aca1)
- **Core**: harden mcp history blob resources (e7491e40)
- **Core**: harden onboarding ocr language lifecycle (03adcc4c)
- **Core**: honor cli upload as-file readiness (7617b218)
- **Core**: ignore unavailable default uploaders (1e2ba71e)
- **Core**: keep mobile s3 config file-scoped (dbcda512)
- **Core**: keep mobile s3 imports file-scoped (b007df41)
- **Core**: match refreshed ocr regional languages (225a49cf)
- **Core**: match regional ocr defaults (7c9b9c37)
- **Core**: normalize command palette search whitespace (9d5c08de)
- **Core**: normalize generated openclaw upload paths (352fe2f0)
- **Core**: normalize ocr selected language mutations (14ec204d)
- **Core**: normalize onboarding ocr language refresh (6069a2d9)
- **Core**: order refreshed ocr selections (b565c8a9)
- **Core**: pipe generated openclaw cli text uploads (da942aa1)
- **Core**: preserve editor dirty state on sidecar save failure (03a05ca5)
- **Core**: preserve ocr fallback languages (e3681c68)
- **Core**: preserve ocr languages when enumeration fails (04b9ef76)
- **Core**: preserve openclaw cli json results (78a056f4)
- **Core**: preserve openclaw invalid json diagnostics (a9570c51)
- **Core**: print generated openclaw cli errors (c9eed7f0)
- **Core**: quote openclaw cli diagnostic keys (ac45a9ce)
- **Core**: restore settings from backup zips (3468d613)
- **Core**: share amazon s3 keychain credentials (e93f2854)
- **Core**: trim generated openclaw upload names (dab6b1a7)
- **Core**: trim refreshed ocr language selections (356353cb)
- **Core**: truncate editor save overwrites (e5fc4607)
- **Core**: validate openclaw cli json output (3948284a)
- **Core**: validate openclaw upload urls (92acf76c)
- **Core**: validate openclaw uploader reports (14d79a85)
- **Core**: wrap command palette keyboard selection (e8c5b84d)

### Documentation
- **Core**: Add 2026-05-11 Flathub metadata blog draft. (92aaa9bc)
- **Core**: Add 2026-05-11 release-wrap blog draft. (f770ed76)
- **Core**: Add 2026-05-11 uploader reliability and release prep blog draft. (6c917115)
- **Core**: Add 2026-05-12 Send-to policies blog draft. (c1042739)
- **Core**: Add 2026-05-12 send-to policy blog draft. (cf0caa7a)
- **Core**: Add 2026-05-13 capture command palette blog draft. (1c878df9)
- **Core**: Add 2026-05-13 command palette blog draft. (289b97fc)
- **Core**: Add 2026-05-14 no-activity blog draft. (399c75f7)
- **Core**: Add 2026-05-15 no-activity blog draft. (5ec54af3, 70928457)
- **Core**: Add 2026-05-16 docs sync blog draft. (fce8910b)
- **Core**: Add hourly review tracker entry for MCP server history search query parsing fix (36a3c1f5)
- **Core**: Complete XIP0057 implementation notes (5650c60e)
- **Core**: Normalize XIP proposal statuses (ef0af0bd)
- **Core**: Refresh 2026-05-10 OCR, OpenClaw, and Flatpak blog draft. (3b6cc545)
- **Core**: Refresh 2026-05-11 uploader reliability blog draft. (995e8a2a)
- **Core**: Refresh 2026-05-12 Send-to policies blog draft. (699652e6)
- **Core**: Refresh 2026-05-12 send-to policies blog draft. (7c5c3693)
- **Core**: Refresh 2026-05-13 command palette blog draft. (a3505a13)
- **Core**: Refresh 2026-05-14 docs-maintenance blog draft. (33a17937)
- **Core**: Refresh 2026-05-15 mobile S3 fixes blog draft. (fbfcfc9c)
- **Core**: Require XIP batch push and issue closure (be83d51f)
- **Core**: Update hourly review tracker and state for McpServer.Tests coverage fix (94b93875)
- **Core**: Update hourly review tracker for MCP server fix (ea2d4d29)
- **Core**: Update hourly review tracker for editor integration copy SKBitmap fix (0b5f80f1)
- **Core**: Update hourly review tracker for file/path handling IsFileLocked fix (70d210ae)
- **Core**: Update hourly review tracker for platform-specific services clipboard stderr drain sweep (a7f04783)
- **Core**: Update hourly review tracker for uploader core default-instance fixes (1df94fbc)
- **Core**: add KFIP0008 capture privacy redaction proposal (c93427af)
- **Core**: correct xerahs sweep test totals (29bab024)
- **Core**: record MCP thumbnail resource sweep (a1ccf7e9)
- **Core**: record editor save overwrite sweep (794b1301)
- **Core**: record editor sidecar save review (3e7e402e)

### Changed
- **Core**: Add Fedora VS Code updater script (0b39ac17)
- **Core**: Bump app version for blog drafts. (b6aa7d55)
- **Core**: Create derive-goal-from-session.md (e5ca8e24)
- **Core**: Start minor release for command palette (bfbe0a60)
- **Core**: Update XIP0039-imageeditor-refactor-priorities.md (991e26a2)
- **Core**: Update derive-goal-from-session.md (1238ca1a)
- **Core**: Update hourly review tracker for indexer enumeration exception fix (974d6b58)
- **Core**: Update hourly review tracker for media FFmpeg argument fix (438a6829)
- **Core**: [CI] Keep automated releases prerelease by default (d47e82e1)
- **Core**: [Docs] Mark v0.22.256 release workflow complete (c702b613)
- **Core**: [Docs] Record v0.22.256 Flathub verification (e92498b4)

## v0.22.239

### Fixes
- **Core**: Resolve startup log issues (83b367ab)
- **Core**: parse raw openclaw plugin json output (4ce71989)
- **Core**: redact openclaw plugin stdout diagnostics (fb0ee5ff)
- **Core**: use core openclaw plugin sdk import (5868981b)

### Build
- **Core**: Attach ImageEditor during release prep (d84cc957)

### Documentation
- **Core**: Link changelog only for existing tags (314700ee)
- **Core**: Link changelog tags and omit hashes (437b49b6)
- **Core**: Update changelog for release prep (0ea08f80)

# Changelog
All notable changes to XerahS will be documented in this file.
The format follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html):
- **MAJOR** (x): Breaking changes (0 while unreleased)
- **MINOR** (y): New features and enhancements
- **PATCH** (z): Bug fixes and patches

## v0.22.237

### Fixes
- **Core**: Resolve startup log issues

### Build
- **Core**: Attach ImageEditor during release prep

### Documentation
- **Core**: Link changelog tags and omit hashes
- **Core**: Update changelog for release prep

## [v0.22.236](https://github.com/ShareX/XerahS/releases/tag/v0.22.236)

### Features
- **Mobile**: Expand Android and iOS parity with native shells, About screens, hosted/custom uploader imports, upload history, privacy/store metadata, and mobile configuration flows
- **Assistant**: Add assistant provider configuration, OCR upload workflows, overlay commands, safety contracts, aliases, and current model IDs
- **CLI**: Expand CLI automation with OpenClaw compatibility, upload naming, file-forced uploads, directory indexing, and ReClip commands
- **Plugins**: Add community destination-plugin registry, installer UX, Pixelfox packaging, and KFIP0004 registry validation coverage
- **MCP**: Introduce the XerahS MCP runtime, transports, desktop settings, prompts, usage guide, and integration coverage
- **Onboarding**: Build the onboarding wizard state machine, UI, converters, trigger flow, debug launcher, and style integration
- **Capture Workflows**: Add smart-region and social-media capture workflows with after-capture task execution, OCR wiring, copy-path actions, and profile services
- **About**: Add About tab/library grouping and loaded library version display
- **Annotations**: Preserve editable annotation sidecars and standardize saved-annotation re-editing support
- **Image Effects**: Complete the SXIEF/schema-driven filter migration and harden image-effect preset handling
- **Indexer**: Improve Index Folder and watch-folder options, including ignore-empty-folder handling

### Fixes
- **Capture**: Improve capture coordinate mapping, DXGI/GDI/WinRT fallback behavior, cursor composition, region/scroll targeting, recording bounds, and monitor scaling
- **Uploaders**: Normalize uploader routing and provider behavior across FTP/SFTP, Nextcloud, Imgur, Dropbox, S3, cookies, custom uploaders, fallback paths, URLs, and result history
- **Plugins**: Harden plugin dependency resolution, package extraction, manifest validation, load/unload cleanup, diagnostics, provider IDs, and fallback assembly checks
- **Mobile**: Harden Android and iOS upload, import, share, secrets, package identity, diagnostics, and store-release flows
- **Linux**: Harden Linux Wayland/X11 capture, portal hotkeys, clipboard URIs, Flatpak/XDG state, sandbox IDs, desktop entries, and geometry parsing
- **macOS**: Stabilize macOS overlay mapping, region selection, clipboard file drops, service helpers, capture scaling, hotkeys, and release assets
- **CLI**: Stabilize CLI capture/upload/record validation, JSON output, temp-file cleanup, naming, pipe/text uploads, and task completion matching
- **Assistant**: Stabilize assistant aliases, OCR history/cache/options, copy-path privacy, clipboard handling, overlay output, and local-file lookup
- **History and Editor**: Fix history lookup, editor sessions, sidecar fallback, thumbnail refresh, bitmap lifetimes, image presets, annotations, and Pin to Screen resource cleanup
- **Media**: Fix FFmpeg, recording output, mixed thumbnail grids, random seek slots, video thumbnail leaks, and media timing behavior
- **Notifications**: Fix toast actions, timeout/process cleanup, markdown image links, severity propagation, timing validation, and menu behavior
- **Settings**: Harden settings save/reload, backups, reset cleanup, secret paths, upgrade detection, recent-task state, and config repair
- **OCR**: Normalize OCR language/options handling, reruns, whitespace results, cache reuse, and history persistence
- **Onboarding**: Repair onboarding rendering, step state, actions, destinations, hotkey parsing, trigger timing, and build errors
- **MCP**: Harden MCP parameter validation, notification responses, headless contracts, and annotation parsing
- **Indexer**: Fix Indexer traversal, extension filters, folder statistics, async output paths, and total-folder counting
- **Workflows**: Repair workflow hotkeys, duplicate task identity, stale IDs, timeout handlers, task settings, and destination category mapping
- **Paths**: Harden path normalization, filename mutation, unique suffix handling, directory collisions, URL encoding, and home-path behavior
- **Build Stability**: Resolve build, binding, DevTools, XAML, release/debug, and test isolation regressions

### Build
- **Avalonia**: Move the app and Android bootstrap through Avalonia 12, headless text shaping, view-folder, and Vortice adjustments
- **ImageEditor**: Sync ShareX.ImageEditor and supporting port tooling across Avalonia and framework updates
- **VideoEditor**: Update ShareX.VideoEditor integration, submodule revisions, and WebUI build behavior
- **Plugins**: Harden plugin copy, architecture separation, runtime restore, and solution-build behavior
- **Build**: Stabilize release, CI, deterministic dotnet, isolated output, and repository build graph behavior
- **Tooling**: Add changelog, markdown hygiene, mojibake, and BOM safeguards
- **Build**: Apply build-system maintenance and dependency updates

### Documentation
- **Blog**: Consolidate the 2026 engineering, release-readiness, stabilization, plugin, Android, Flatpak, and maintenance blog draft series
- **Proposals**: Consolidate XIP, IEIP, and KFIP proposal drafts, reviews, renames, migrations, and design/research updates
- **Mobile Release**: Document Android Play, iOS App Store, privacy, release-build, and mobile parity readiness
- **Plugins**: Refresh destination-plugin, registry, Pixelfox, and OpenClaw setup documentation
- **Workflow**: Tighten changelog, release, build, XIP sync, maintenance, and commit-policy guidance
- **ImageEditor**: Record ShareX.ImageEditor port planning, comparison, manifest, and sync guidance
- **Repository Docs**: Repair markdown encoding and normalize repository, developer, build, and README documentation
- **Maintenance**: Fold hourly review trackers, verification notes, and status snapshots into the release maintenance record
- **Documentation**: Update supporting documentation and release notes

### Testing
- **Platform**: Add platform verification coverage for macOS native crosshair capture
- **Coverage**: Restore and expand coverage for filters, editor history, after-capture workflows, proposals, and regression paths

### Changed
- **Editors**: Sync editor integrations with ShareX.ImageEditor and ShareX.VideoEditor updates
- **Branding**: Refresh logos, icons, feature graphics, and release artwork
- **Versioning**: Align version, prerelease, and origin-release synchronization metadata
- **Privacy and Signing**: Tighten privacy, signing, export-compliance, and local-signing behavior
- **General**: Apply miscellaneous release maintenance, UI polish, and compatibility updates

## [v0.21.0](https://github.com/ShareX/XerahS/releases/tag/v0.21.0)
### Features
- **Custom uploaders & Send-to**: Catalog multi-add, save-back flow, and Send-to behavior prompt
### Fixes
- **ShareX.ImageEditor**: Submodule updates for effect browser parity, categories/borders, host shortcut rows, auto-crop dialog, empty-state actions, crop dedupe, and latest-effects compatibility
- **Overlay & capture parity**: Align Linux overlay capture with Windows; fix region selector preference on hotkey-triggered captures
- **Modals & catalog**: Centralize modal opening; dispatch opens on UI thread for Add from Catalog on Linux
- **Recording & video editor**: Gate unsupported pause on Wayland; harden editor launch
- **Core**: Upload fallback File→Image; suppress AfterCapture toast on cancel; repair uploader mojibake labels
- **Hotkeys / Imgur**: X11 fallback when portal bind cancelled; cross-platform OAuth URL helpers
- **Linux (Wayland / GNOME / KDE)**: Portal retry, transparent overlay and mixed-DPI, `UseTransparentOverlay` plumbing, DBus crash guard, selector defaults, GNOME crop workflow
- **Paths / UI / upload**: User-writable plugins folder; effect browser aligned with unified editor API; auto-heal stale destination instance IDs
### Refactor
- **Core**: Remove upload destination auto-persist and simplify resolution
### Build
- **AUR & Windows**: PKGBUILD and script updates; reusable AUR packaging; permissions; MSI via WiX
- **ShareX.ImageEditor**: Submodule tracking (IEIP0004 branch, develop, parity/revert/schema fixes)
- **Tooling & quality**: Upload fallback logging/comments; default publish-release to prerelease; LF enforcement; CS8604/DBus
### Documentation
- **Blog drafts (Mar 2026)**: Annotation/IEIP/Linux/XIP/multipart/Wayland series — add and revise
- **XIPs & proposals**: XIP0054–0056 (multipart, Send-to, history); Send-to post-v1; systems-thinking prompt; workflow destination tooltip; commit prefix; proposal consolidation; IEIP0004 finalize; capture/upload XML and fallback docs
- **IEIP0004 / Linux**: Lessons from catalog browser integration; INSTALL.md; GNOME Wayland portal/overlay notes; interactive fallback explanation
- **Developers**: Move `PLUGIN_SDK.md` to `developers/guidelines/`
### Changed
- **Multipart upload**: Abstractions, coverage, and S3 multipart support
- **ShareX.ImageEditor / IEIP**: Schema-driven effects overhaul and IEIP0005 doc; effect apply, schema dialog binding/slider; ongoing submodule sync
- **Imgur**: OAuth UX, token flow, and client ID defaults
- **Custom uploaders**: Hide legacy import after first run; Save to Plugins label and XIP0056 auto-instance metadata
- **Meta**: `Directory.Build.props` and feature-systems-thinking prompt updates
## [v0.20.12](https://github.com/ShareX/XerahS/releases/tag/v0.20.12)
### Fixes
- **RegionCapture Toolbar**: Revert `RegionCaptureAnnotationViewModel` to the pre-ToolInfo adapter behavior to restore stable annotation toolbar interactions.
- **RegionCapture Icons**: Load `ImageEditorStyles.axaml` in the overlay window so toolbar buttons render distinct Lucide icons instead of fallback glyphs.
## [v0.20.11](https://github.com/ShareX/XerahS/releases/tag/v0.20.11)
### Features
- **Clipboard Monitor**: Add cross-platform clipboard monitoring with toggle in Application Settings > Integration tab; register on Windows, Linux, and macOS; suppress origin loops and harden async reads; default to disabled
- **Tool Info Panel (IEIP0002)**: Implement ToolInfo adapter in RegionCapture; update dimensions during shape resize via handles; tune visual prominence
- **Creative ImageEditor Filters**: Integrate creative image effects and filters into the ImageEditor
### Fixes
- **Menus**: Fix startup command binding regression across platforms, clipboard monitor focus-stealing, tool windows hidden behind main window, and menu dismissal on Linux
- **Annotation Toolbar**: Restore fixed-width, square, centered-split, right-side layout and tool options; share annotation toolbar with ImageEditor
- **Recording**: Apply CLI duration across recorder jobs, wire stop signal to active sessions, route start to last region, configure custom region recording fallback
- **Send To**: Wire Windows pipeline, harden Linux entry generation, make macOS fallback explicit, use native Windows shortcut
- **Theme**: Normalize effect property controls to XerahS theme; align task image effect editing with ImageEditor UX; add ShareX resource compatibility and correct surface tokens
- **ImageEditor**: Restore Task Settings Add Effect enumeration; keep effect browser dialogs visible on Linux; prevent startup crash in native theme resources
- **Scrolling Capture**: Correct stitching
- **Cross-Assembly Views**: Resolve registration and update IEIP0003
- **Linux Upload Content**: Prevent clipboard hang
- **macOS**: Skip native dylib rebuild when sources unchanged
- **History/Explorer**: Replace emoji glyphs with Lucide font icons
- **Plugins**: Clean plugin folders safely across app and user roots
- **Tools Navigation**: Improve tools navigation and upload window activation
- **Release Scripts**: Fix tag name collision and redirect `find_tag_run_id` status to stderr
### Refactor
- **Fluent Theming**: Migrate XerahS UI to native Fluent theming; adopt OS-aware accent across desktop UI and RegionCapture; align app and RegionCapture theming; defer editor accent to ImageEditor; apply ImageEditor system theme support
- **Compiled Bindings (XIP0053)**: Enable compiled bindings defaults, harden ViewLocator with explicit mappings, complete guardrails
- **DI/Host (XIP0052)**: Inject task and recording managers through host services, extract overlay capture sessions, harden MVVM workflow boundaries, finalize host startup wiring, consolidate desktop composition
- **Mobile Theming**: Add adaptive theme tokens and switch Mobile.Ava and Mobile.Maui views to shared theme resources
- **UI Polish**: Move host icon surface into XerahS UI, remove inline workflow type dropdown, center button content, standardize color swatch tile width and names, preserve previous color on selection changes
- **Annotation Toolbar**: Refactor toolbar styles in ShareX.ImageEditor
### Build
- **VideoEditor**: Update submodule for Tailwind 4.2.2 and playback/WebUI fixes
- Exclude Windows clipboard tests on non-Windows platforms
### Testing
- Add XIP0052 composition boundary and injected manager coverage; stabilize manager tests
## [v0.20.5](https://github.com/ShareX/XerahS/releases/tag/v0.20.5)
### Features
- **VideoEditor**: Integrate ShareX.VideoEditor with desktop host wiring, `open-video-editor` CLI support, diagnostics, FFmpeg/ffprobe-backed UI and headless trim, and packaged WebUI assets
- **Uploaders**: Add Nextcloud and native Immich uploader plugins with scaffolding and design notes
- **History**: Add image combine actions and multi-selection groundwork
- **Theme**: Track OS system accent colour app-wide via `SystemAccentColor`
### Fixes
- **VideoEditor**: Harden startup, dependency resolution, packaged WebUI/bootstrap, FFmpeg path propagation, playback sync, and reopen lifecycle
- **Custom Uploaders**: Inline editor in settings while preserving names, hiding duplicate labels, and making inline names read-only
- **Linux Wallpaper**: Detect wallpaper providers across desktop environments, preload and normalize sources, and restore ImageEditor wallpaper backgrounds through platform abstractions
- **UI/Theme Surfaces**: Normalize all tool window, hotkey control, card, and index folder surfaces; restore scrollbars; apply accent buttons across color picker, image splitter/combiner/thumbnailer, video converter/thumbnailer, upload content, and hash check window
- **ImageEditor**: Region capture toolbar icons, overlay alignment, pin export, pinned-window drag, preview bitmap cloning, screenshotspath picker, remembered window size, and submodule updates
- **Linux Region Capture**: Restore X11 fallbacks, enable Wayland overlay selector with portal capture, harden selector preference handling, and drain portal hotkey rebinds before dispose
- **Shell Integration**: Wire startup and shell integration entries for Windows, Linux, and macOS
- **Workflow/Editor UI**: Stage workflow editor changes until save; disable File Save/Save As when no image; sort View Zoom alphabetically; wire annotate editor task actions and hide task buttons in correct host contexts
- **Settings**: Fix ScrollViewer not scrolling to bottom; fix Destination Settings provider panel flicker; fix About view Social groupBox width
- **Linux**: Avoid Avalonia dispatcher sync-context capture in portal watchers
- **Build Targets**: Fix Windows-to-macOS packaging cross-compilation and Linux desktop build targeting
### Refactor
- **Theme (XIP0050)**: Remove FluentAvalonia package; introduce shared surface window and page base controls; centralize desktop theme styles; make accent the default button style
- **DI/MVVM (XIP0052)**: Migrate to Microsoft.Extensions.DependencyInjection; inject task and recording managers; extract pipeline from WorkerTask; consolidate desktop composition
- **Linux Capture**: Replace UseModernCapture semantics with per-selector preference plumbing and settings UI
- **Core/UI**: Share history and toast context menus; align app typography
### Build
- **Release Automation**: Normalize editor projects to Any CPU, automate and harden Chocolatey release sync, fix CRLF pack output paths, and add fresh-clone bootstrap helpers
- **VideoEditor**: Update hybrid web/native toolchain requirements for the WebUI build
### Documentation
- **Developer Workflow**: Document fresh-clone setup, shared agent workflow, shared-library commit conventions, explicit GitHub issue handling, and FFmpeg guidance
- **Architecture**: Add VEIP0001 hybrid VideoEditor direction, Immich plugin XIP, XIP0050 (FluentAvalonia removal), XIP0051 (Linux selector preferences), XIP0052 (agentic DI refactoring)
### Testing
- **Region Capture**: Add UI smoke tests for region capture flows
## [v0.19.9](https://github.com/ShareX/XerahS/releases/tag/v0.19.9)
### Features
- **Video Editor**: Integrate ShareX.VideoEditor submodule; add `WorkflowType.VideoEditor`, Tools menu and sidebar nav, `AnnotateMedia` (renamed from `AnnotateImage`) with toast dispatch to VideoEditor; open editor after recording when AnnotateMedia is set; headless stubs and IUIService wiring
- **Uploaders**: Add URL shortener foundation and Bitly URL shortener plugin support
### Fixes
- **Linux Region Capture**: Improve cropping for physical-resolution desktops, including KDE Plasma portal bitmaps and X11 overlay positioning; add diagnostics, detect XWayland vs native Wayland, and restore fast overlay region capture
- **Linux**: UseModernCapture option (XDG Portal vs overlay), Wayland region capture and mixed-DPI bounds, GNOME portal recording output, double region-selection prompt fix; KDE Spectacle and GNOME fallbacks (XIP0046-C); system tray SNI (GNOME/Wayland); systemd user unit path via UserProfile
- **Linux Recording**: Harden GStreamer pipeline by correcting region crop, removing conflicting `video/x-raw` caps before `glupload`, adding GL-to-CPU fallback, making fatal errors selectable in RecordingView, and cleaning up portal session on fatal errors
- **Core**: Validate URL before OpenURL Process.Start; SaveRequested/SaveAsRequested for embedded and standalone editor; fall back to File-category instances when no Image uploader; default white tray icon on Linux/macOS; Tools_* nav items and VideoEditor dispatch; AnnotateImage JSON deserialization; Linux portal handle format and RPM packaging; fix tray stop button behavior and hotkey recording stop flow
- **Core**: Correct DXGI capture ModeRotation mapping for DMDO_90/DMDO_270 rotations
- **ImageEditor**: Submodule updates and macOS build; add ShareX.ImageEditor at develop; Zoom to Fit in zoom picker; —7a easy wins (Random.Shared, Category overrides, Gamma LUT cache)
- **VideoEditor submodule**: Button theme isolation and ReactiveUI main thread scheduler fixes
- **Watch Folder**: Support legacy watchfolder.service
- **Core**: Hide Video Editor from Tools menu in release builds
- **PluginLoadContext**: Fix stale shared dependency name/order checks
- **Updates/Logging**: Fix reflection-disabled GitHub update JSON handling and normalize error log naming to `yyyyMMdd`
### Refactor
- **ImageEditor (EIP0001)**: Advance Phase 1 commits; migrate to new namespaces; rename submodule and sync references
- **Core (PathsManager)**: Centralize plugins path selection; centralize log and app path handling and expand path audit coverage for plugins/screenshots/tools/troubleshooting paths
- **Indexer**: Share tree helpers and settings types, collapse async adapters, and externalize HTML styles
### Build
- **ImageEditor**: Replace the redundant legacy submodule layout and update embedded ShareX.ImageEditor integration; update submodule references
- **Release Automation**: Run maintenance chores during release bump-tag flow; enforce standard release notes block
- **Developer Tooling**: Add `run-debug-app.sh` helper script
### Documentation
- **Architecture**: Move image editor refactor proposal to IEIP; move proposals into docs/proposals; Backend Porting checklist (March 2026); EIP0001 phases A/B/C; OS-specific known issues and Linux hotkey workaround; XIP0046 summary (Issues C, D, E fixed); FFMPEG.md; XIP0042/XIP0044/XIP0046 task docs; run-debug-app.ps1; VEIP0001 and XIP0046 proposal
- **XIP0047**: Summarize Linux region capture DPI and performance investigation, including X11 overlay shift and KDE physical-bitmap crop fixes
- **XIP0042**: Update the SkiaSharp hardware acceleration task document; XIP sync workflow and backups; XIP0043 complete; XIP0038/XIP0040/XIP0042 doc audits
### Performance
- **Linux**: Faster overlay and smoother crosshair on Linux (region capture)
## [v0.18.11](https://github.com/ShareX/XerahS/releases/tag/v0.18.11)
### Features
- **Mobile**: Android and iOS MVP with Share Extension and MAUI; adaptive theming, upload queue/picker/history, active destination selector, desktop-compatible upload filename pattern, broad share-intent support; Amazon S3 and Custom Uploader config UI; Swift/Kotlin native shells and share extension
- **Media Explorer**: Provider file browsing with S3 and Imgur, navigation, search, filtering, and CDN thumbnail optimization
- **Watch Folder**: Daemon with lifecycle hooks, runtime policy, settings controls, and tests
- **Indexer**: Async streaming with progress and cancellation; open in own window; file extension filters; dark theme with light-mode toggle
- **ImageEditor**: Integrate submodule; File Open choice dialog; annotation options persistence; app/editor theme sync
- **Workflows**: UploadContentWindow; AutoCapture, Pin to Screen, Ruler, MonitorTest, HashCheck; 6 media tools (ImageCombiner, ImageSplitter, ImageThumbnailer, VideoConverter, VideoThumbnailer, AnalyzeImage); OCR and ScrollingCapture end-to-end
- **Upload**: Auto destination uploader; cross-platform secrets store with diagnostics; proxy config UI
- **Amazon S3**: AWS SSO auth, region selection, CNAME, public bucket policy; redesign config to mimic Custom Uploaders
- **Plugins**: Dropbox, Paste2, GitHub Gist, FTP/FTPS/SFTP, Pastebin; XIP0040 plugin architecture; DestinationsPluginSdk
- **UI**: Copy Errors to HistoryView, AfterUploadWindow, Toast
- **Linux Capture**: DBus fallbacks, KDE permissions, decision trace orchestration, portal waterfall
- **Packaging**: Scoop, WinGet, Chocolatey support; generate-winget.ps1 enhancements
- **Misc**: Imgur album selection and GIFV; Dropbox OAuth overhaul
### Fixes
- **ImageEditor**: XAML startup crash, highlight/crop/submodule fixes, context menu, DPI and crop handles
- **Scrolling Capture**: Auto-scroll, workflow settings, hotkeys, scroll position detection
- **Media Explorer**: Harden listing, normalize URLs, error handling, copyable footer
- **Mobile**: iOS App Group for S3 config in Share Extension; unify share payload and TimeZoneInfo
- **Upload**: MainViewModel parameterless copy/upload; multi-uploader fallback, clipboard routing
- **Capture/Region**: Annotation layer rendering, crop offset, AfterCapture refresh, workflow integration
- **Workflows**: Allow OCR and scrolling workflows from tray
- **Linux**: Portal timeout, Wayland/slurp/portal fixes, GStreamer clamp, D-Bus and plugins path resolution
- **After Capture**: ShowAfterCaptureWindow persistence
- **Misc**: FAQ XerahS/ShareX Linux ref; update checker pre-releases; backup machine-specific; S3 setup reorder; macOS icon in Windows build; File Open dialog crash
- **Core**: Correct flipped monitor orientation in DXGI capture; fail fast for Linux publish and validate package payload; harden daemon bundling across desktop RIDs; marshal Avalonia clipboard access to UI thread; remove WinForms dependency from Windows platform
- **Core**: Avoid SIGPIPE in archive validation checks
- **Update Changelog Script**: Ensure entries array has Count for single-category
### Refactor
- **Core**: Split large ViewModels, WatchFolder daemon base service, ScreenRecordingManager startup; WindowState naming; GeneralHelpers split
- **Upload**: Polymorphic uploader config pilot
- **Workflows**: App workflow orchestration services
- **Linux Capture**: Modular providers, parallel lanes, coordinator, contracts
### Build
- **CI/Release**: All-platform release workflow, Linux by arch, release title, bump/tag automation
- **Android**: Mobile build infrastructure
- **Linux**: Plugin packaging, RPM strip, display diagnostics, desktop-file-utils
- **ImageEditor**: Submodule checkout, recovery hook, pre-push
- **Core**: Add changelog update automation script; validate release assets and RID metadata
- **Misc**: Version/changelog bumps, central package management, plugin DLL deduplication, cross-compilation macOS, GPL headers Swift/Kotlin
### Documentation
- **Consolidate**: Developer docs to developers/; plugins to developers/plugins and .xsdp; changelog consolidation; mobile README simplification
- **Planning**: Roadmap, XIP0033 complete, task docs
- **Misc**: Feasibility report JS/CSS; sync-submodules; build/Linux/mobile docs; XIP0040/0039; update-changelog skill in run-maintenance
- **Core**: Create XIP0043-Remove-WinForms-and-Harden-CrossRID-Daemon-Bundling.md
### Testing
- **Linux Capture**: Waterfall and lane matrix tests
### Performance
- **RegionCapture**: Reduce annotation rebuild pressure
- **Core**: Skip app-driven plugin build in solution builds; update ImageEditor submodule for TFM simplification
## [v0.17.4](https://github.com/ShareX/XerahS/releases/tag/v0.17.4)
### Features
- **Indexer**: Modernize HTML output flow and default to dark theme with light-mode toggle
### Build
- **CI**: Split Linux release builds by runner architecture and set release title metadata
- **Automation**: Add release bump/tag workflow skill for standardized release prep
## [v0.16.3](https://github.com/ShareX/XerahS/releases/tag/v0.16.3)
### Features
- **Mobile**: Add active upload destination selector and in-app destination label on Android and iOS
- **Mobile**: Use desktop-compatible upload filename pattern on Android and iOS
- **Mobile**: Add broad share-intent support for arbitrary file types on Android and iOS
- **Media Explorer**: Implement provider file browsing with S3 and Imgur support, including navigation, search, filtering, and CDN thumbnail optimization
- **Watch Folder**: Add watch-folder daemon with lifecycle hooks, runtime policy controls, and tests
- **Mobile**: Add adaptive theming infrastructure with native styling polish
- **Mobile**: Add upload queue, picker, and history screens
- **UI**: Add Copy Errors to UI (HistoryView, AfterUploadWindow, Toast)
- **ImageEditor**: Add app/editor theme synchronization with platform-aware styling
### Fixes
- **iOS**: Use App Group settings so Share Extension can read Amazon S3 configuration
- **ImageEditor**: Fix precompiled Avalonia XAML startup crash (`XamlLoadException`) in editor app initialization
- **ImageEditor**: Improve highlight rendering/fill behavior, Smart Eraser, text defaults, and canvas zoom performance
- **ImageEditor**: Restore crop UX and precision with full-image/L-shape fixes, visible handles, and DPI-aware hit zones
- **Scrolling Capture**: Improve auto-scroll behavior and workflow settings integration
- **Workflows**: Allow OCR and scrolling workflows from tray
- **Media Explorer**: Harden listing, normalize URLs, and improve error handling
- **Mobile**: Unify iOS share payload handling and TimeZoneInfo serialization
- **Upload**: Align MainViewModel helper with parameterless copy/upload events
- **ImageEditor**: Update submodule with context menu fixes
- **Capture**: Optimize annotation layer rendering and resource management
- **Documentation**: Update FAQ to correctly reference XerahS instead of ShareX in Linux screen capture section
- **Infrastructure**: Integrate update-changelog skill into run-maintenance workflow
### Refactor
- **Core**: Split large ViewModels, extract WatchFolder daemon base service, and consolidate ScreenRecordingManager startup flow
- **Core**: Remove WindowState naming collisions
- **Core**: Split GeneralHelpers into utility classes
- **Upload**: Add polymorphic uploader config pilot
- **Workflows**: Extract app workflow orchestration services
### Build
- **Infrastructure**: Add all-platform release workflow and repository sync helper script
- **Android**: Add Android mobile build infrastructure
- **Linux**: Harden plugin packaging, RPM strip behavior, and display diagnostics
- **Hooks**: Add cross-platform ImageEditor recovery and auto-push on pre-push
### Documentation
- **Maintenance**: Simplify mobile README and move refactor/hardening notes into documentation archives
- **Planning**: Update task planning docs and move completed XIP0033
- **Plugins**: Consolidate plugin documentation into 'developers/plugins' and standardize on .xsdp extension
- **Developer**: Consolidate developer documentation into 'developers' root folder
- **Architecture**: Add feasibility report for JS/CSS migration
- **Submodules**: Add sync-submodules workflow and update ImageEditor to latest develop
- **Tasks**: Add refactoring audit skill and native UI theming task
## [v0.15.5](https://github.com/ShareX/XerahS/releases/tag/v0.15.5)
### Features
- **Linux Capture**: Add DBus fallbacks, KDE desktop permissions, and decision trace orchestration
### Fixes
- **Linux Capture**: Enforce portal-only sandbox policy, unify waterfall, and improve logging
- **Builds**: Fix cross-platform build configuration and add linux-arm64 support
### Refactor
- **Linux Capture**: Modularize providers with parallel lanes, coordinator, and contracts
### Testing
- **Linux Capture**: Add Linux capture waterfall and lane matrix tests
### Documentation
- **Build System**: Rename developer README and add Linux guide
- **Roadmap**: Finalize Linux phase roadmap and release gate
## v0.15.0
### Features
- **Mobile**: Add Android and iOS MVP with Share Extension support, .NET MAUI project
- **Mobile**: Add Custom Uploader and Amazon S3 configuration UI (#124, #125, @Hexeption)
- **Indexer**: Implement async streaming indexer with progress and cancellation
### Fixes
- **Image Editor**: Share annotation preview visuals with ImageEditor to ensure consistency
### Fixes
- **Annotations**: Optimize rendering, remove draw-start dot artifact, and improve responsiveness
- **Workflow**: Complete WorkflowType end-to-end wiring
- **UX**: Hide SilentRun window on first open instead of minimizing
- **Updates**: Gracefully handle repositories with only pre-releases
- **After Capture**: Persist "Show after capture window" behavior across repeated runs
- **Upload**: Add multi-uploader auto destination fallback and wire mobile Amazon S3 and plugin integration to InstanceManager
- **Watch Folder**: Convert MOV captures to MP4
- **Settings**: Make backup and secrets filenames machine-specific
- **Amazon S3**: Reorder and renumber setup steps
- **iOS**: Improve local signing setup and share extension flow
### Build
- **Plugins**: Centralize plugin copy target and pass host TFM
- **Dependencies**: Bump Avalonia packages to 11.3.12
- **ImageEditor**: Update submodule for theme-aware view, net9 compatibility, and track develop branch
### Documentation
- **Audits**: Organize audit files and update UI control inventory snapshots
- **Tasks**: Mark XIP0030 complete and move to completed tasks
## v0.14.0
### Features
- **Monitor Test**: Implement MonitorTest workflow with diagnostic and pattern testing modes
- **Tools**: Add Ruler workflow with full RegionCapture integration
- **Indexer**: Make Index Folder open in its own window
- **Editor**: Integrate upstream ShareX.ImageEditor submodule with File Open choice dialog
- **Region Capture**: Add annotation options persistence
### Fixes
- **Logging**: Fix duplicate date in log filename on date rotation
- **Region Capture**: Improve annotation toolbar integration and reduce rebuild pressure
- **Indexer**: Enable Open in Browser button and remove WebView in favor of system browser
- **Navigation**: Enable menu navigation and update editor data transfer APIs
- **Editor**: Sync ImageEditor fixes, persist annotation options, refactor platform abstractions, enable Zoom to Fit
- **ImageEditor**: Update submodule with unified undo-redo, smart padding crop sync, clipboard fixes, z-order fixes, and dispose bug fixes
- **Packaging**: Restore macOS icon in Windows package build
- **Upload**: Delay upload progress title update until actual upload starts
- **macOS**: Harden mac packaging and cross-platform editor wiring
- **Dialogs**: Prevent File Open dialog crash and add global exception logging
### Build
- **Cross-Compilation**: Add macOS from Windows support and build system documentation
- **Infrastructure**: Fix version parsing in Windows package script
## v0.13.0
### Fixes
- **Menu Bar**: Fix hash checker routing and dynamic workflows menu
- **Upload**: Improve Upload Content workflow handling, window UX, and text upload routing
## v0.12.0
### Fixes
- **Tools**: Add media tools to navigation bar and fix DataTemplate issues
- **Proxy**: Fix custom uploader loading and add configuration UI (#77, @Hexeption)
- **Linux**: Add dark mode support, theme settings, and Wayland Hyprland screenshot support (#62, @unicxrn; #61, @unicxrn)
- **macOS**: Add native application menu (#60, @Hexeption)
- **Custom Uploaders**: Fix compatibility improvements and version compatibility (#74, @Hexeption; #71, @emmsixx)
- **Security**: Fix DPAPI platform warning (#73, @Hexeption)
### Refactor
- **Editor**: Rename namespace from ShareX.Editor to XerahS.Editor and update all references
### Build
- **Plugins**: Improve plugin copy target to only include plugin assemblies
- **Configuration**: Update build files, packaging configuration, issue templates, and .gitignore
## v0.11.0
### Features
- **Upload**: Implement UploadContentWindow and remove superseded upload WorkflowTypes
## v0.10.0
### Features
- **Workflows**: Implement AutoCapture workflows
## v0.9.0
### Features
- **Workflows**: Implement Pin to Screen workflows
- **Amazon S3**: Enhance SSO with region selection
### Fixes
- **Upload**: Improve upload error surfacing and history actions
- **Workflows**: Preserve workflow order and exclude None
- **Custom Uploaders**: Fix compatibility check for XerahS versions
### Build
- **Plugins**: Restore plugin DLL deduplication with retry logic
### Core
- **Rendering**: Remove RectangleLight; modern Skia rendering deprecated it
## v0.8.0
### Features
- **Security**: Add cross-platform secrets store with diagnostics
- **Upload**: Add auto destination uploader
- **Custom Uploaders**: Implement full support including editor UI and integration
- **Task Settings**: Redesign Task Settings UX with dedicated Image/Video tabs
- **Tray Icon**: Add recording-aware tray icon with pause/abort controls
- **Image Formats**: Add AVIF and WebP image format support
- **Linux/Wayland**: Fix screen capture on Wayland by integrating XDG Portal API
### Fixes
- **Capture**: Allow clipboard payloads in capture phase
- **Upload**: Add clipboard upload auto routing
- **Region Capture**: Correct crop offset, refresh AfterCapture UI, and fix coordinate mapping for Windows (#29)
- **Linux**: Fix active window capture hierarchy, coordinates, hotkey initialization, and Region Capture
- **UX**: Hide main window when capture triggered from tray/navbar
- **UI**: Fix update dialog layout
### Refactor
- **Editor**: Update XerahS.Editor.csproj references and docs
## [v0.7.0](https://github.com/ShareX/XerahS/releases/tag/v0.7.0) - Annotation Overlays & Packaging
### Features & Improvements
- **Annotations**: Enable Annotation Toolbar in Region Capture Overlay and refactor (#53)
- **Region Capture**: Add support for transparent background capture (RectangleTransparent)
- **macOS**: Native single-file app bundle packaging (`.app`)
- **Packaging**: Automated multi-arch Windows release builds
- **Plugins**: Support for user-installed plugins and packaging
- **Window Capture**: Add support via monitor cropping fallback
- **Media Library**: Basic implementation (#49)
### Bug Fixes
- **Annotation Layer**: Fix coordinate system for multi-monitor/high DPI and compositing
- **Exceptions**: Global exception handling implementation
- **Screen**: Fix frozen screen issue (#51)
- **Cursor**: Fix system cursor issues (#46)
## [v0.6.0](https://github.com/ShareX/XerahS/releases/tag/v0.6.0) - UI Redesign & Auto-Update
### Features & Improvements
- **UI Redesign**: Comprehensive visual overhaul of all views using Grid layout and consistent styling
- **Auto-Update**: Implement auto-update system with Avalonia UI
- **After Upload**: Add "After Upload" results window
- **Property Grid**: Add ApplicationConfig property grid
- **CLI**: Add `verify-recording` command for automated screen recording validation
- **Editor**: Unify editor undo history across different toolsets
- **Architecture**: Move Windows-specific P/Invoke types to dedicated Platform.Windows project
- **FFmpeg**: Improve FFmpeg download/config UX with progress hooks and better path resolution
- **Documentation**: Replace ShareX.Avalonia references with XerahS (#44)
- **Workflow**: Update cursor handling (#43)
### Bug Fixes
- **Recording**: Improve GIF recording quality, add clipboard support, pause, and stroke-based abort
- **After Upload**: Fix window theming and errors
- **Rendering**: Fix speech balloon tail geometry rendering
- **Region Capture**: Fix system cursor appearing in screenshots and hotkey issues (#38, #39)
## v0.5.0 - Core Capture & Editor Improvements
### Features & Improvements
- **Capture**: Add single instance enforcement for the application
- **Region Capture**: Enhance crosshair visibility, add magnifier pixel sampling, and hide system cursor when ghost cursor active
- **Editor**: Wire ImageEffectsViewModel to unified undo/redo stack
- **UX**: Set default file picker location to Desktop for easier access
### Bug Fixes
- Fix 11+ HIGH/MEDIUM priority issues including null safety and resource management
- Set RegionCaptureControl cursor to None to prevent double cursor visibility
## v0.4.0 - Image Effects & Tools
### Features & Improvements
- **Image Effects**: Refactor preset management and improve effects UI
- **Tools**: Add QR code generator/decoder and Color Picker tools with standard color name mapping
- **Watch Folders**: Implement Watch Folder system with per-folder workflow assignments
- **Indexer**: Add Index Folder preview and modernize HTML output using WebView
- **macOS**: Add native ScreenCaptureKit video recording support
### Bug Fixes
- **Capture**: Fix cursor tracking and visibility during GDI capture
- **Capture**: Fix NullReferenceException in DXGI capture by preventing premature disposal of D3D11 device context
## v0.3.0 - Modern Capture Architecture
### Features & Improvements
- **Modern Capture**: Implement DXGI-based high-performance screen capture for Windows
- **Screen Recording**: Unified recording pipeline with Windows Media Foundation and FFmpeg support
- **Workflow System**: Major overhaul of hotkeys into full Workflow system with GUID persistence
- **Toast Notifications**: New custom Avalonia-based notification system with advanced settings
- **Linux**: Initial support for Wayland via XDG Desktop Portal and native X11 capture
- **Settings**: Add weekly backup system for application settings
- **UX**: Add tray icon support with customizable click actions
### Bug Fixes
- **Modern Capture**: Fix multi-monitor blank capture issues
- **Region Capture**: Fix DPI handling, coordinate mapping, and offsets/scaling on multi-monitor setups
- **Code Quality**: Massive code audit fixing 500+ license headers and 160+ nullability issues
- **Windows**: Standardize Windows TFM and fix CsWinRT interop issues
## v0.2.0 - macOS Support & Plugin System
### Features & Improvements
- **macOS**: Initial platform support including ScreenCaptureKit, SharpHook hotkeys, and app bundling
- **Plugins**: Implement dynamic plugin system with packaging (`.sxap`), CLI tools, and `.sxadp` file association
- **History**: Switch history storage from XML to SQLite with automatic backups
- **Editor**: Integrate ShareX.Editor as core component with SkiaSharp rendering
## v0.1.0 - Initial Feature Set
### Core Features
- **UI**: Reimagined interface with two-toolbar system and modern dark theme
- **Capture**: Region, Fullscreen, and Window capture modes
- **Annotations**: Object-based editor with Rectangle, Ellipse, Arrow, Line, Text, Number, Crop tools, and full Undo/Redo support
- **Hotkeys**: Global hotkey system with Win32 registration
- **Image Effects**: Initial implementation of 40+ effects including Resize, Shadows, and Gradients
- **History**: Basic task history tracking
---
*This changelog follows Semantic Versioning while the project remains in pre-release (0.x.x).*
