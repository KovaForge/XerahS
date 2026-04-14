# Changelog
All notable changes to XerahS will be documented in this file.
The format follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html):
- **MAJOR** (x): Breaking changes (0 while unreleased)
- **MINOR** (y): New features and enhancements
- **PATCH** (z): Bug fixes and patches
## v0.22.2

### Features
- **Core**: Group loaded libraries and show versions in About `(ab8a6034, 246a37f7)`
- **Core**: Preserve editable annotation sidecars `(9112999a, aa577153)`
- **Core**: Add assistant BYOK providers, OCR upload workflows, overlay local commands, safety contract, and workflow aliases `(81f25416, 8ad54b57, d5d7c3d2, 65cdf93a, 92f12d9c)`
- **Core**: Add XIP0063 — XerahS CLI OpenClaw compatibility spec and upload command `(196ba194, b29c2dff)`
- **Core**: Add --name flag to CLI upload command for custom filenames `(fc5236c8)`
- **Core**: Add onboarding debug launcher mode `(8181ac98)`
- **Core**: IUIService: add ShowAnalyzerWindowAsync to all headless implementations `(b45fefa7)`
- **Core**: Implement 6 after-capture task execution paths in CaptureJobProcessor `(acc31132)`
- **Core**: HistoryManagerSQLite: Delete now deletes all items (was only last), Edit uses EnsureConnection for consistency `(b28d8235)`
- **Core**: XIP0068: Re-editing saved annotations design review and critique `(fc3e71c1, a2fc92bd, 9fd13117)`

### Fixes
- **Core**: Align ImageEditor Avalonia with XerahS, upgrade Tmds.DBus, async Sample.png load `(e9ceb393)`
- **Core**: Apply name format pattern to CLI file uploads `(007b6cda)`
- **Core**: Atomic debounce + ObjectDisposedException guard inside Dispatcher.Post lambda `(c5e19538)`
- **Core**: Avoid duplicate developer tools attachment `(f1c859e9)`
- **Core**: Bias scrolling capture away from nested panes `(623ba1eb)`
- **Core**: Catch ConfigureShortcuts NotImplemented in KDE portal fallback `(1412b67f)`
- **Core**: Debounce rapid hotkey fires to prevent duplicate capture tasks `(c83ed6b3)`
- **Core**: Enable Avalonia dev tools and dispose sample bitmap `(51f23270)`
- **Core**: Exclude hidden Windows capture surfaces `(4d189427)`
- **Core**: Fix DevTools double-attachment in DEBUG builds `(1cbf5afb)`
- **Core**: Forward onboarding debug args `(6d5e236e)`
- **Core**: Guard DBus calls against ObjectDisposedException in hotkey service `(6f72f244)`
- **Core**: Harden Linux portal request waiting `(42a0df55)`
- **Core**: Harden publish-release tag existence checks `(4e882baf)`
- **Core**: HasEditableAnnotations falls back to default sidecar path convention `(4d5d99c8)`
- **Core**: Keep scrolling capture bottom detection on active scroller `(b680bcad)`
- **Core**: Prefer the main scrolling capture scroller `(a9f0be8b)`
- **Core**: Prevent infinite recursion in UploadJobProcessor.TryUploadWithFallback — Vladislava Kova `(adc18dea)`
- **Core**: Refresh history thumbnail after annotation re-edit `(0b9b0f8f)`
- **Core**: Remove KDE_SESSION_VERSION false-positive in Wayland detection `(64727c45)`
- **Core**: Repair onboarding step state flow `(b87caa39)`
- **Core**: Repair onboarding wizard rendering `(2ef18e36)`
- **Core**: resolve RID-agnostic plugin path in CopyPluginsToOutput target `(ede42e21)`
- **Core**: Respect task XerahSOverlay preference on KDE Plasma Wayland `(cb75a733)`
- **Core**: Restore build and sync pending updates `(5305afdc)`
- **Core**: Restore screenshot uploader resolution `(c32ab11e)`
- **Core**: Sync Linux portal hotkey state `(bab44e5b)`
- **Core**: Use info.FileName for GenericUploader file uploads `(d2d45d32)`
- **Core**: Use upload pipeline from toast `(90592583)`
- **Core**: Fix XIP0072 screen recording regressions `(31cccc1c)`
- **Core**: Fix onboarding actions and destinations `(543ce29c)`
- **Core**: Fix onboarding trigger race and move it to OnWindowOpened `(7921fd4b)`
- **Core**: Fix WaylandPortalHotkeyService to implement IHotkeyService.HotkeysChanged `(f490e35a)`
- **Core**: Fix XerahS.UI build errors in onboarding code `(9b8b79ab)`
- **Core**: Add missing using XerahS.RegionCapture in RegionCaptureAnnotationViewModel — resolves StepTailStyle not found `(c48380c7)`
- **Core**: Auto uploader falls back to Text uploaders for text-based files when File uploaders fail `(6d93bba4)`
- **Core**: Auto-build and copy plugins to CLI output directory `(dac37123)`
- **Core**: Scrolling Capture no longer triggers tab switch on child window scroll `(6490d078)`
- **Core**: Scrolling Capture targets window main scroll bar instead of scroll bar thumb/track `(4712e39d)`
- **Core**: Restore XIP0060: XerahS.UI build — remove duplicate Onboarding folders causing AXN0002 `(5c73b9c0)`
- **Fix AVLN2000**: invalid binding path syntax in OnboardingWizardWindow.axaml `(be6b7a2d)`

### Refactor
- **Core**: Extract magic strings and GUIDs into central AppContracts.cs `(8bf723b9)`
- **Core**: Split OverlayWindow.axaml.cs into logical partial classes `(9bf57ef8)`
- **Core**: Update VideoEditor submodule to decoupled npm build `(8c1d715c)`
- **Core**: Extract assistant core into shared library `(35cfcda7)`

### Build
- **Core**: Upgrade to Avalonia 12; align Avalonia 12 startup, headless text shaping, desktop bindings and accessibility `(513071ca, b55e14f7, 08968745)`
- **Core**: Update port-imageeditor workflow and Vortice packages `(ab19a591)`
- **Core**: Align Avalonia 12 Android bootstrap and forms `(c6ece84c)`
- **Core**: Remove stale csproj.bak and rename UI views folder `(08ee12b5, 854878e6)`
- **Core**: ShareX.ImageEditor submodule sync with port-imageeditor skill and Vortice packages `(5a94405a)`
- **Core**: Update changelog skill and script with mojibake fix `(9aa0c574)`
- **Core**: Harden markdown hygiene workflow triggers and add mojibake/BOM checks `(cd3c5d3f, 3ef156d7)`
- **Core**: Remove deprecated _Onboarding_BACKUP directories `(bee92a84)`
- **Core**: Update run-debug-app.ps1 `(09a66938)`
- **Core**: ShareX.ImageEditor submodule updates: LF re-applied, interaction cache, LF+Avalonia 12, Watermark placeholder, latest `(08f207bc, b8a45537, 765c57c9, c23d87d0, bfd78ace, ade66014, bb50af89)`
- **Core**: ShareX.VideoEditor update `(41bf06fa)`

### Documentation
- **Core**: Blog drafts (2026 series, add/update) `(0087f904, 2340b1b5, 2d628d7b, 33f5c573, 6e1e7a40, 88d35930, a976d281, b4c42e32, b4f537e2, bda406d3, d25e1379, dcb60a58, bf44e8b0, 43098032)`
- **Core**: Repair markdown encoding across .ai skills, blog, status, and developer docs `(a2563962, 0f54498a, b3273120)`
- **Core**: Restore UTF-8 diagrams in build README; add health badge and normalize README encoding `(8d79f80e, 501a6e8e, 69590aa9)`
- **Core**: Align changelog skill categories `(e180fd8b)`
- **Core**: Align commit version policy with tags `(d1aaa3bd)`
- **Core**: Align release changelog path `(ff523e30)`
- **Core**: Align XIPs with and update Avalonia 12 XIP backlog `(b4c43b5e, 700561db)`
- **Core**: Clarify spotlight assistant settings placement `(e167810a)`
- **Core**: Consolidate build skill guardrails `(aa8a9540)`
- **Core**: Consolidate release workflow skills `(a9edddd7)`
- **Core**: Consolidate XIP skill guidance `(e9a1959d)`
- **Core**: Correct uploader UI skill references `(2d73018d)`
- **Core**: Draft spotlight assistant XIP `(54ef3380)`
- **Core**: Expand spotlight assistant provider plan `(b7964cb0)`
- **Core**: Normalize XIP reference format (XIP-NNNN -> XIPNNNN) `(7c730871)`
- **Core**: Refine editable annotation sidecar design `(1b53648d)`
- **Core**: Repair maintenance sync guardrails `(39ef0ba1)`
- **Core**: Update XIP sync skill `(1fc53d26)`
- **Core**: XIP/IEIP proposals — XIP0065, XIP0066, XIP0067 Avalonia 12 upgrade docs, XIP0069, XIP0070, user research, design/review docs `(5ef03e41, b574e02d, 32f0edbc, f9f6d370, cd8ca16b)`
- **Core**: XIP0060: State of the Art Onboarding Wizard `(4910906a)`
- **Core**: XIP0061: KDE Plasma / Nobara — portal fixes, version deps, remaining open items `(460d1c65)`
- **Core**: KFIP0001 KovaForge Improvement Proposals series; KFIP0071 Part 2 privacy rules, tool specs, provider defaults, Sofia design specs `(de955b6e, b7372bb8, a0972d89)`

### Changed
- **Core**: Smart Region Capture Profiles — TweetCaptureDetector + CaptureProfileService implementation, wired into after-capture workflow `(ca8949c2, 9f433081, 0740efd2, 84b5d863, ff74a0e0)`
- **Core**: Social Media Screenshot Automation — Tweet & Thread Capture with styled export `(bd6174b1, 4cb8a5d0, a9d7a62b, 4fc601a1, 4f56ded8)`
- **Core**: Smart Region Capture Profiles rename XIP0073→KFIP0002; consolidate duplicate XIP0074 `(88628121, 15a0e3e6, 4ee81449)`
- **Core**: Add "Ignore empty folders" option to Index Folder `(53b86e32)`
- **Core**: XIP0060: Onboarding state machine, ViewModels, complete UI build, design system, step views, reusable controls, converters, trigger wiring, style rewrite, wizard complete `(5d032a17, 4939680c, 16ef57dc, 42497263, bee2b008, ccaf6ce9, 68dbf543, 6bf203a8)`
- **Core**: [MCP] Add README, usage guide, initial stdio transport skeleton, integration tests, HTTP transport + Cloudflare Worker + manifest, prompt templates — typed records `(e2764bce, de12d8f2, bfa6f82b, 133881d9, 10aa1d58, c1845a77)`
- **Core**: [MCP] Align MCP contract and prompts `(3f416b01)`
- **Core**: [MCP] Implement XerahS MCP runtime; expose MCP settings in desktop UI; refresh usage guide `(ba6da988, f450dde8, 94a09599)`
- **Core**: Update model IDs to MiniMax-M2.7, gemini-2.5-flash, claude-haiku-4-5 `(02729ca6)`
- **Core**: ShareX.ImageEditor submodule sync through ShareX@c6e3c5260 `(f361802d)`
- **Core**: ShareX.VideoEditor update `(41bf06fa)`

### Testing
- **Core**: Add AfterCaptureTaskFlagsTests `(0e6fc37d)`
- **Core**: XIP0073 build verification `(a5bfad98)`

## v0.21.0
### Features
- **Custom uploaders & Send-to**: Catalog multi-add, save-back flow, and Send-to behavior prompt `(07fee6a1, 11badd97, 6e552f83)`
### Fixes
- **ShareX.ImageEditor**: Submodule updates for effect browser parity, categories/borders, host shortcut rows, auto-crop dialog, empty-state actions, crop dedupe, and latest-effects compatibility `(9767ecc2, 9c74ef23, 521eb39d, 7354a04f, 334822f7, e59fc0c2, cdb03b49)`
- **Overlay & capture parity**: Align Linux overlay capture with Windows; fix region selector preference on hotkey-triggered captures `(4688c133, ee254f7b)`
- **Modals & catalog**: Centralize modal opening; dispatch opens on UI thread for Add from Catalog on Linux `(be448902, 326d1049)`
- **Recording & video editor**: Gate unsupported pause on Wayland; harden editor launch `(2a6a0b50, 06659ec3)`
- **Core**: Upload fallback File→Image; suppress AfterCapture toast on cancel; repair uploader mojibake labels `(3aceb524, 51dc50ae, 486c8320)`
- **Hotkeys / Imgur**: X11 fallback when portal bind cancelled; cross-platform OAuth URL helpers `(0939672f, 656e2975)`
- **Linux (Wayland / GNOME / KDE)**: Portal retry, transparent overlay and mixed-DPI, `UseTransparentOverlay` plumbing, DBus crash guard, selector defaults, GNOME crop workflow `(377425be, a3685245, b65e9848, e65bcc56, 08e4ed7e, c0c04c84, a72a6e6c)`
- **Paths / UI / upload**: User-writable plugins folder; effect browser aligned with unified editor API; auto-heal stale destination instance IDs `(fb1c443b, 0ad4bfb4, f1e87b3a)`
### Refactor
- **Core**: Remove upload destination auto-persist and simplify resolution `(33beb845)`
### Build
- **AUR & Windows**: PKGBUILD and script updates; reusable AUR packaging; permissions; MSI via WiX `(ec08bbb0, 9811d0a5, 06fff733, a948921e)`
- **ShareX.ImageEditor**: Submodule tracking (IEIP0004 branch, develop, parity/revert/schema fixes) `(6d4a3939, 4377f217, e5acc011, 337bc8aa, f49f8cb2, 31348f9f, e06a47ed)`
- **Tooling & quality**: Upload fallback logging/comments; default publish-release to prerelease; LF enforcement; CS8604/DBus `(9b2e33db, 4ab11c88, bd3e9c53, 90fa44b6, f5e6d2d0)`
### Documentation
- **Blog drafts (Mar 2026)**: Annotation/IEIP/Linux/XIP/multipart/Wayland series — add and revise `(edd2b92f, a6d87b36, dfeaed38, c6a2df06, cfe5d8c6, 5b3ae91a, 0c4cf944, 621ba13a, 0bdf100d, d21c61a8, 760aa548)`
- **XIPs & proposals**: XIP0054–0056 (multipart, Send-to, history); Send-to post-v1; systems-thinking prompt; workflow destination tooltip; commit prefix; proposal consolidation; IEIP0004 finalize; capture/upload XML and fallback docs `(1aa8ed28, de12cba2, 4495a4bb, cbe8f323, 8bdf1cc0, 230bdf02, 9777ead6, dc73a7b6, 62ff53d3, eb3dcc96, 126a07f1, 7bc160ea, e39283b6, 83767f4c, 89f6d0a5, 96e99345, eb7e5d8d, 612d547c, 73ad1148, ac9e42e0)`
- **IEIP0004 / Linux**: Lessons from catalog browser integration; INSTALL.md; GNOME Wayland portal/overlay notes; interactive fallback explanation `(c920beee, fb6ec404, 5397a7f3, 6fa792a6)`
- **Developers**: Move `PLUGIN_SDK.md` to `developers/guidelines/` `(e4e407ea)`
### Changed
- **Multipart upload**: Abstractions, coverage, and S3 multipart support `(1033f4d4, 66383717, af844088)`
- **ShareX.ImageEditor / IEIP**: Schema-driven effects overhaul and IEIP0005 doc; effect apply, schema dialog binding/slider; ongoing submodule sync `(999f6dcf, 39c9757c, a3f690e2, dd19a5b0, ae8a1765, 05b24ba1, 0feae29e, b61ea536, e7f10fd2, 6b6ab9a4)`
- **Imgur**: OAuth UX, token flow, and client ID defaults `(6cba98fd, 630749d0)`
- **Custom uploaders**: Hide legacy import after first run; Save to Plugins label and XIP0056 auto-instance metadata `(c014052b, 53f6eea0)`
- **Meta**: `Directory.Build.props` and feature-systems-thinking prompt updates `(c7a74ee2, d91ceb35)`
## v0.20.12
### Fixes
- **RegionCapture Toolbar**: Revert `RegionCaptureAnnotationViewModel` to the pre-ToolInfo adapter behavior to restore stable annotation toolbar interactions.
- **RegionCapture Icons**: Load `ImageEditorStyles.axaml` in the overlay window so toolbar buttons render distinct Lucide icons instead of fallback glyphs.
## v0.20.11
### Features
- **Clipboard Monitor**: Add cross-platform clipboard monitoring with toggle in Application Settings > Integration tab; register on Windows, Linux, and macOS; suppress origin loops and harden async reads; default to disabled `(baaea3d7, 4ebdb46c, d6e947ba, f0215255, 14cc7e9b, 7c0e8054, 9f00e0b5)`
- **Tool Info Panel (IEIP0002)**: Implement ToolInfo adapter in RegionCapture; update dimensions during shape resize via handles; tune visual prominence `(76200251, 1f1e4878, 2878a023, cf28f368)`
- **Creative ImageEditor Filters**: Integrate creative image effects and filters into the ImageEditor `(80eca12f, aa62a121)`
### Fixes
- **Menus**: Fix startup command binding regression across platforms, clipboard monitor focus-stealing, tool windows hidden behind main window, and menu dismissal on Linux `(a3ca8fc8, 5fc0eae3, fe97e969, fb8ad6c4, dee38279)`
- **Annotation Toolbar**: Restore fixed-width, square, centered-split, right-side layout and tool options; share annotation toolbar with ImageEditor `(2a6e64f3, 0ca7ad9d, b6b0e72f, a5a22032, 613ad215, 151cec4c, 7e0ac98b)`
- **Recording**: Apply CLI duration across recorder jobs, wire stop signal to active sessions, route start to last region, configure custom region recording fallback `(7d0e43e6, 60ae1aa6, b6518f96, 19497c23)`
- **Send To**: Wire Windows pipeline, harden Linux entry generation, make macOS fallback explicit, use native Windows shortcut `(e805d850, 042319e3, d508dc5c, 56a769a6)`
- **Theme**: Normalize effect property controls to XerahS theme; align task image effect editing with ImageEditor UX; add ShareX resource compatibility and correct surface tokens `(93f9b1b9, 30805eff, 75821964)`
- **ImageEditor**: Restore Task Settings Add Effect enumeration; keep effect browser dialogs visible on Linux; prevent startup crash in native theme resources `(021c308b, 9ca1ea1c, d2c238fb)`
- **Scrolling Capture**: Correct stitching `(4c4b7c73)`
- **Cross-Assembly Views**: Resolve registration and update IEIP0003 `(3cc83c40)`
- **Linux Upload Content**: Prevent clipboard hang `(c84e2b03)`
- **macOS**: Skip native dylib rebuild when sources unchanged `(ae83aaa7)`
- **History/Explorer**: Replace emoji glyphs with Lucide font icons `(a19afd49)`
- **Plugins**: Clean plugin folders safely across app and user roots `(541a7a9d)`
- **Tools Navigation**: Improve tools navigation and upload window activation `(f693b0f9)`
- **Release Scripts**: Fix tag name collision and redirect `find_tag_run_id` status to stderr `(148bc37d, 1e686af2)`
### Refactor
- **Fluent Theming**: Migrate XerahS UI to native Fluent theming; adopt OS-aware accent across desktop UI and RegionCapture; align app and RegionCapture theming; defer editor accent to ImageEditor; apply ImageEditor system theme support `(59501d03, 9a929846, 3ce2cca8, ce84ab7d, ac4f2555, dd9d2f05, 6bd443cc)`
- **Compiled Bindings (XIP0053)**: Enable compiled bindings defaults, harden ViewLocator with explicit mappings, complete guardrails `(5eb3a8fc, 962bacd3, e854aed4)`
- **DI/Host (XIP0052)**: Inject task and recording managers through host services, extract overlay capture sessions, harden MVVM workflow boundaries, finalize host startup wiring, consolidate desktop composition `(4a98b5f3, f98c2b03, 80e6ac43, f4cf60de, 8f0beac7, c0a80d6e)`
- **Mobile Theming**: Add adaptive theme tokens and switch Mobile.Ava and Mobile.Maui views to shared theme resources `(73138429, 0a59a9a6, 0a7a3b52, cf0851c9)`
- **UI Polish**: Move host icon surface into XerahS UI, remove inline workflow type dropdown, center button content, standardize color swatch tile width and names, preserve previous color on selection changes `(46b3273f, 401839c4, af34e206, 4d7d8d0f, 3297c862, 2d5ed933, 2fe7deef)`
- **Annotation Toolbar**: Refactor toolbar styles in ShareX.ImageEditor `(11f0dc9e)`
### Build
- **VideoEditor**: Update submodule for Tailwind 4.2.2 and playback/WebUI fixes `(fb11f425, fa7c0b58, 8d212da7, ca253d20, 4c0a7413)`
- Exclude Windows clipboard tests on non-Windows platforms `(70811abf)`
### Testing
- Add XIP0052 composition boundary and injected manager coverage; stabilize manager tests `(0c9e5caa, 3c600ee6, f7c0e4cc)`
## v0.20.5
### Features
- **VideoEditor**: Integrate ShareX.VideoEditor with desktop host wiring, `open-video-editor` CLI support, diagnostics, FFmpeg/ffprobe-backed UI and headless trim, and packaged WebUI assets `(267351e8, 1e345954, e81f1671, 9e2b917a, f0d954bc, f645d0f8, cbfc28d6, 2f684a0e, 9637f7fe, 45f16227)`
- **Uploaders**: Add Nextcloud and native Immich uploader plugins with scaffolding and design notes `(bea34b98, b8bbfc15, a531c6a0, 5b112930, 4164b5c5)`
- **History**: Add image combine actions and multi-selection groundwork `(e52d4311, 9da0b6b2, a662c8ac)`
- **Theme**: Track OS system accent colour app-wide via `SystemAccentColor` `(60837e44)`
### Fixes
- **VideoEditor**: Harden startup, dependency resolution, packaged WebUI/bootstrap, FFmpeg path propagation, playback sync, and reopen lifecycle `(7a814699, adcbec9a, d4e1a449, 6be3f5b1, aaeee1de, bff513c2, f181f624, ec098878, 450ed938, 294b338b, 935a1ea3, c02c7bd0, 75f34059, 1fa78e6a, 39a0e65d, 451699c2, 5da69b45, 9ec27e11)`
- **Custom Uploaders**: Inline editor in settings while preserving names, hiding duplicate labels, and making inline names read-only `(75701a4c, d3a67428, af4635a3, 95325504)`
- **Linux Wallpaper**: Detect wallpaper providers across desktop environments, preload and normalize sources, and restore ImageEditor wallpaper backgrounds through platform abstractions `(3401968f, 4b12b6df, f59aa050, 551b1967, 4e6528d3, ee3c3981, 85fe2871, 5a131ce1, 43907c07, bc12e966)`
- **UI/Theme Surfaces**: Normalize all tool window, hotkey control, card, and index folder surfaces; restore scrollbars; apply accent buttons across color picker, image splitter/combiner/thumbnailer, video converter/thumbnailer, upload content, and hash check window `(7db2f3ed, 637899a7, 57f55361, 05d73905, 17ffcb04, dee46e8d, 4268d657, ec599b8b, 19b4424f, ec782d3d, 7a314245, 8c025fb4, 4d411ce0, 87946d13, 550ab5f8, e59362e7, 5a7ea131, 4b88f19c, dc6bb5ba, d77b4a4f, cca26574, 7cbeef73, 2160e463, d2e82af6, b2bed6c1, 2b661408, 77eaede2, 553bc915, c20f703e)`
- **ImageEditor**: Region capture toolbar icons, overlay alignment, pin export, pinned-window drag, preview bitmap cloning, screenshotspath picker, remembered window size, and submodule updates `(e1606785, 4d71abc5, 60629966, 794dcaee, eb7f99fd, a2b49176, fa6fda17, 79b62291, 6e8441c0, 3ce020e4, 8f9d8be4, 93fe5f0e, d2d95606, 9f691ab6, 735f08b7, bfb3f5bc, c98458dc, e133763c, 43bbccbe, 857b192d, 24093641, 119d6324)`
- **Linux Region Capture**: Restore X11 fallbacks, enable Wayland overlay selector with portal capture, harden selector preference handling, and drain portal hotkey rebinds before dispose `(a510da3f, 6a90f696, 6f96bbac, 968c236c, b69fe286, e55618f0)`
- **Shell Integration**: Wire startup and shell integration entries for Windows, Linux, and macOS `(ffd1b400, fc366b80, 2b724b9c, 498ce04d)`
- **Workflow/Editor UI**: Stage workflow editor changes until save; disable File Save/Save As when no image; sort View Zoom alphabetically; wire annotate editor task actions and hide task buttons in correct host contexts `(181f8230, e8689dc8, 1cca79ec, b10a5338, 5560ea1b, 5809651a, a5be14e6, 0bffed33, 9d4e09be, 43b7c8f9)`
- **Settings**: Fix ScrollViewer not scrolling to bottom; fix Destination Settings provider panel flicker; fix About view Social groupBox width `(f5d91f46, 8846173d, 9b62687f)`
- **Linux**: Avoid Avalonia dispatcher sync-context capture in portal watchers `(809bd1aa)`
- **Build Targets**: Fix Windows-to-macOS packaging cross-compilation and Linux desktop build targeting `(501ebaaa, f466d80a)`
### Refactor
- **Theme (XIP0050)**: Remove FluentAvalonia package; introduce shared surface window and page base controls; centralize desktop theme styles; make accent the default button style `(6818ba10, 5b3fb8c6, cdc17b83, 4795bfc5, 8254e13c, 6895b18e, 0fbada88, b2e17ebd, 68ff55e9, dc79432a)`
- **DI/MVVM (XIP0052)**: Migrate to Microsoft.Extensions.DependencyInjection; inject task and recording managers; extract pipeline from WorkerTask; consolidate desktop composition `(a1c37be1, ef40ed9a, 7b0e9930, 4a98b5f3, f98c2b03, 8f0beac7, 80e6ac43, f4cf60de, c0a80d6e)`
- **Linux Capture**: Replace UseModernCapture semantics with per-selector preference plumbing and settings UI `(3852768d, 298ce627, 48036c10, 94e4d020)`
- **Core/UI**: Share history and toast context menus; align app typography `(20739eea, 6f4a0e69, ae27f5eb)`
### Build
- **Release Automation**: Normalize editor projects to Any CPU, automate and harden Chocolatey release sync, fix CRLF pack output paths, and add fresh-clone bootstrap helpers `(c4c0ed5d, 7c6cb235, 07282313, bf240e76, 41834128, 16af5a18, 075be629)`
- **VideoEditor**: Update hybrid web/native toolchain requirements for the WebUI build `(1383ccfa)`
### Documentation
- **Developer Workflow**: Document fresh-clone setup, shared agent workflow, shared-library commit conventions, explicit GitHub issue handling, and FFmpeg guidance `(e577b3fe, 8ba6112a, 975ac87d, 00f5c095, 5d10aabe, ffac673b)`
- **Architecture**: Add VEIP0001 hybrid VideoEditor direction, Immich plugin XIP, XIP0050 (FluentAvalonia removal), XIP0051 (Linux selector preferences), XIP0052 (agentic DI refactoring) `(bbed6737, 4164b5c5, 82c2274a, c5d50d61, 6021c3ab, 1cf8aa43, 503e4438)`
### Testing
- **Region Capture**: Add UI smoke tests for region capture flows `(d293e5b2)`
## v0.19.9
### Features
- **Video Editor**: Integrate ShareX.VideoEditor submodule; add `WorkflowType.VideoEditor`, Tools menu and sidebar nav, `AnnotateMedia` (renamed from `AnnotateImage`) with toast dispatch to VideoEditor; open editor after recording when AnnotateMedia is set; headless stubs and IUIService wiring `(5a969637, 66dd517c, 5a3f3d20, d3edd7c2, 3e8b9203, b0cf726c, f4e081df, 65a8a0f2)`
- **Uploaders**: Add URL shortener foundation and Bitly URL shortener plugin support `(af65a13b)`
### Fixes
- **Linux Region Capture**: Improve cropping for physical-resolution desktops, including KDE Plasma portal bitmaps and X11 overlay positioning; add diagnostics, detect XWayland vs native Wayland, and restore fast overlay region capture `(2c538a18, 5f60ca94, fd15830a, bb8548cd, f20211f4, ac69ff73)`
- **Linux**: UseModernCapture option (XDG Portal vs overlay), Wayland region capture and mixed-DPI bounds, GNOME portal recording output, double region-selection prompt fix; KDE Spectacle and GNOME fallbacks (XIP0046-C); system tray SNI (GNOME/Wayland); systemd user unit path via UserProfile `(8e2f372b, 8b686d9c, 792f9f5c, 5aafdad2, ad0f48d5, 58283cb1, 6426a6c6, 17a52cdc, 74dd1532, ffa8f982)`
- **Linux Recording**: Harden GStreamer pipeline by correcting region crop, removing conflicting `video/x-raw` caps before `glupload`, adding GL-to-CPU fallback, making fatal errors selectable in RecordingView, and cleaning up portal session on fatal errors `(01527ef5, ef55b9e7, 78523202, eba1e9d0, ba13d971, d69bd5a1)`
- **Core**: Validate URL before OpenURL Process.Start; SaveRequested/SaveAsRequested for embedded and standalone editor; fall back to File-category instances when no Image uploader; default white tray icon on Linux/macOS; Tools_* nav items and VideoEditor dispatch; AnnotateImage JSON deserialization; Linux portal handle format and RPM packaging; fix tray stop button behavior and hotkey recording stop flow `(d16c0179, 6595731d, 7ec997c0, 63f81ce6, ddf64eb5, e9f8594b, b4b47f53, 1172b9a5, c6e9dd21, 36410a85)`
- **Core**: Correct DXGI capture ModeRotation mapping for DMDO_90/DMDO_270 rotations `(b484d197)`
- **ImageEditor**: Submodule updates and macOS build; add ShareX.ImageEditor at develop; Zoom to Fit in zoom picker; —7a easy wins (Random.Shared, Category overrides, Gamma LUT cache) `(03833f97, aa407405, ae2a7ac6, 3179068a, 18c11a48, aeba3c67, 81d9cfee, 16e6f52d, 36dfd283, 6ab5833c, 6c220749, 8a8a493d, c65cb432, a13faf83, e3e01c2f)`
- **VideoEditor submodule**: Button theme isolation and ReactiveUI main thread scheduler fixes `(ac7a1eec, 672a1e09)`
- **Watch Folder**: Support legacy watchfolder.service `(9d291a15)`
- **Core**: Hide Video Editor from Tools menu in release builds `(692cb5a0)`
- **PluginLoadContext**: Fix stale shared dependency name/order checks `(fff53962)`
- **Updates/Logging**: Fix reflection-disabled GitHub update JSON handling and normalize error log naming to `yyyyMMdd` `(f2ed43cf)`
### Refactor
- **ImageEditor (EIP0001)**: Advance Phase 1 commits; migrate to new namespaces; rename submodule and sync references `(512e4216, eebd11e3, 6d58166f)`
- **Core (PathsManager)**: Centralize plugins path selection; centralize log and app path handling and expand path audit coverage for plugins/screenshots/tools/troubleshooting paths `(1ec799ac, ad12770f, bcb0423e)`
- **Indexer**: Share tree helpers and settings types, collapse async adapters, and externalize HTML styles `(5b3b5ad6, b9b6913e, b7a1580d, 6a7608bd)`
### Build
- **ImageEditor**: Replace the redundant legacy submodule layout and update embedded ShareX.ImageEditor integration; update submodule references `(99c79b0f, 259307a3, 10b04276, 009d2201, 12c0380f, 600a1fdd, 8236ce9c, 9c2f85c4)`
- **Release Automation**: Run maintenance chores during release bump-tag flow; enforce standard release notes block `(df7976f4, 88287c36, 7b601c62)`
- **Developer Tooling**: Add `run-debug-app.sh` helper script `(7d4fe9ec)`
### Documentation
- **Architecture**: Move image editor refactor proposal to IEIP; move proposals into docs/proposals; Backend Porting checklist (March 2026); EIP0001 phases A/B/C; OS-specific known issues and Linux hotkey workaround; XIP0046 summary (Issues C, D, E fixed); FFMPEG.md; XIP0042/XIP0044/XIP0046 task docs; run-debug-app.ps1; VEIP0001 and XIP0046 proposal `(cc325496, 73d661ce, d21b4a9a, 26c25e9b, bd700307, fb3c0400, daf7c1f9, 18b424ae, 7594c988, 87fa948d, efe3a4c7, c17a71d9, b733f172, 41e1d9c0, f315361d)`
- **XIP0047**: Summarize Linux region capture DPI and performance investigation, including X11 overlay shift and KDE physical-bitmap crop fixes `(abdba2b1, af43a177)`
- **XIP0042**: Update the SkiaSharp hardware acceleration task document; XIP sync workflow and backups; XIP0043 complete; XIP0038/XIP0040/XIP0042 doc audits `(3605dfa7, 5994bb13, 2b9a95ed, 8ebe0ae8, 7c70e94a, b9da24b8, 4c06d5cf, 939f92c5, 28c39130, 5b418f5d)`
### Performance
- **Linux**: Faster overlay and smoother crosshair on Linux (region capture) `(a6e93903)`
## v0.18.11
### Features
- **Mobile**: Android and iOS MVP with Share Extension and MAUI; adaptive theming, upload queue/picker/history, active destination selector, desktop-compatible upload filename pattern, broad share-intent support; Amazon S3 and Custom Uploader config UI; Swift/Kotlin native shells and share extension `(8746372, 03698c6, 493d147, 4b79ddb, a7cfb22, 1e5f9eb, 30bbe98, 68d97d9, 52d6ad2, 0b42d73, ccfa4ea, 357188f, c0af5d6, dbb6633, 7292102, 78a488e, 08604ee5, 21c40429, 5876b44b, 1e61b8bf)`
- **Media Explorer**: Provider file browsing with S3 and Imgur, navigation, search, filtering, and CDN thumbnail optimization `(9deedf9, e374160)`
- **Watch Folder**: Daemon with lifecycle hooks, runtime policy, settings controls, and tests `(79c1292, 2b94600, 4265528, 992c41b)`
- **Indexer**: Async streaming with progress and cancellation; open in own window; file extension filters; dark theme with light-mode toggle `(8b2fe88, 8b20b3b, e3445f5b, cc58316, d24cdcf)`
- **ImageEditor**: Integrate submodule; File Open choice dialog; annotation options persistence; app/editor theme sync `(0db2c71, 1a41df5, 7e82df3, 0d42719, 71fa3e1)`
- **Workflows**: UploadContentWindow; AutoCapture, Pin to Screen, Ruler, MonitorTest, HashCheck; 6 media tools (ImageCombiner, ImageSplitter, ImageThumbnailer, VideoConverter, VideoThumbnailer, AnalyzeImage); OCR and ScrollingCapture end-to-end `(298457a, a45d02f, 1e0d3f2, 5647b4d, 8ea941e, 56a1ea3, 8e3164ac, 3a779ef1, ed56345c, 1eff3202)`
- **Upload**: Auto destination uploader; cross-platform secrets store with diagnostics; proxy config UI `(f3abe81, c2b8105, f626f09, 473cbb88)`
- **Amazon S3**: AWS SSO auth, region selection, CNAME, public bucket policy; redesign config to mimic Custom Uploaders `(9e2623be, 6880866, 6bacd05e)`
- **Plugins**: Dropbox, Paste2, GitHub Gist, FTP/FTPS/SFTP, Pastebin; XIP0040 plugin architecture; DestinationsPluginSdk `(e04a8953, 3ec377db, 83669aec, 848d3064, c5c49513, 1c92e2c2)`
- **UI**: Copy Errors to HistoryView, AfterUploadWindow, Toast `(5c08812)`
- **Linux Capture**: DBus fallbacks, KDE permissions, decision trace orchestration, portal waterfall `(290b3e0, dc02dbd, c744059)`
- **Packaging**: Scoop, WinGet, Chocolatey support; generate-winget.ps1 enhancements `(1ce955e0, aaa833f6, 552ef730, 124095e7)`
- **Misc**: Imgur album selection and GIFV; Dropbox OAuth overhaul `(70a34373, d4993fd0)`
### Fixes
- **ImageEditor**: XAML startup crash, highlight/crop/submodule fixes, context menu, DPI and crop handles `(258bb09, f987eaa, 73dff63, 0eca71e, fcddf02, d9ab54a, db3bcaa, 584de4e, bd44498, 80eb42f, a1ac173, 592a2f1, 2cbc692, f85c57f, bb862c4, c5618de)`
- **Scrolling Capture**: Auto-scroll, workflow settings, hotkeys, scroll position detection `(1fa45f2, 971219c, 8ac2c8b)`
- **Media Explorer**: Harden listing, normalize URLs, error handling, copyable footer `(9bab13e, e1a5d59, 6b2b8d6, f4e796b)`
- **Mobile**: iOS App Group for S3 config in Share Extension; unify share payload and TimeZoneInfo `(42a1033, 0aad5c1, a835153)`
- **Upload**: MainViewModel parameterless copy/upload; multi-uploader fallback, clipboard routing `(06a2232, 72079e6, c06f17f, 6527590)`
- **Capture/Region**: Annotation layer rendering, crop offset, AfterCapture refresh, workflow integration `(f3e3908, b3034be, af35c74, 4048f00, c5efeab, 4500b8a)`
- **Workflows**: Allow OCR and scrolling workflows from tray `(4e07852)`
- **Linux**: Portal timeout, Wayland/slurp/portal fixes, GStreamer clamp, D-Bus and plugins path resolution `(501af7bb, 4de4a5b1, 4735dcb1, 89a61dd4, d2590b9d, 5e12cbed)`
- **After Capture**: ShowAfterCaptureWindow persistence `(9a04c9d, a3a581d, a8262d4)`
- **Misc**: FAQ XerahS/ShareX Linux ref; update checker pre-releases; backup machine-specific; S3 setup reorder; macOS icon in Windows build; File Open dialog crash `(699634f, ed68066, c618542, 3196b02, ba40fbb, 5cbf5dd)`
- **Core**: Correct flipped monitor orientation in DXGI capture; fail fast for Linux publish and validate package payload; harden daemon bundling across desktop RIDs; marshal Avalonia clipboard access to UI thread; remove WinForms dependency from Windows platform `(106a497d, 78f93344, d3052258, 6d24889e, 0ced3438)`
- **Core**: Avoid SIGPIPE in archive validation checks `(93287f30)`
- **Update Changelog Script**: Ensure entries array has Count for single-category `(22b5cbb3)`
### Refactor
- **Core**: Split large ViewModels, WatchFolder daemon base service, ScreenRecordingManager startup; WindowState naming; GeneralHelpers split `(86286af, 315549a, 1160519, 506072e, 78214dd)`
- **Upload**: Polymorphic uploader config pilot `(7f2815d)`
- **Workflows**: App workflow orchestration services `(4ee8ab9)`
- **Linux Capture**: Modular providers, parallel lanes, coordinator, contracts `(733a49d, 5dd9931, 0a81693, 3569c0a)`
### Build
- **CI/Release**: All-platform release workflow, Linux by arch, release title, bump/tag automation `(2fbe5ee, bd8d0d3, aeccb68, 55f25d3)`
- **Android**: Mobile build infrastructure `(3952287)`
- **Linux**: Plugin packaging, RPM strip, display diagnostics, desktop-file-utils `(817d83a, 0723b45, 1c79a94, 2f6e3112)`
- **ImageEditor**: Submodule checkout, recovery hook, pre-push `(3098824, 899e8f1)`
- **Core**: Add changelog update automation script; validate release assets and RID metadata `(18d58b73, 571e383c)`
- **Misc**: Version/changelog bumps, central package management, plugin DLL deduplication, cross-compilation macOS, GPL headers Swift/Kotlin `(81db32e, a2bf5a61, 19b3a84c, 519423d9, 55f25d30, cbcd5bb3)`
### Documentation
- **Consolidate**: Developer docs to developers/; plugins to developers/plugins and .xsdp; changelog consolidation; mobile README simplification `(1f17491, b78882f, 41702bd, 21927b4, ad719c9, c9ebe39, 72f2e55, c043844)`
- **Planning**: Roadmap, XIP0033 complete, task docs `(caeaae1, e3f37e3, 04cf9cf, 168b2ea)`
- **Misc**: Feasibility report JS/CSS; sync-submodules; build/Linux/mobile docs; XIP0040/0039; update-changelog skill in run-maintenance `(8fc7446, 47d833c, ce35146, e9ed21a, 8e97f89, ccff1c4, a05200f, 14be1df, 717be27, 76df673, 5ade43b)`
- **Core**: Create XIP0043-Remove-WinForms-and-Harden-CrossRID-Daemon-Bundling.md `(63895920)`
### Testing
- **Linux Capture**: Waterfall and lane matrix tests `(7f49769)`
### Performance
- **RegionCapture**: Reduce annotation rebuild pressure `(3bf82243)`
- **Core**: Skip app-driven plugin build in solution builds; update ImageEditor submodule for TFM simplification `(57fb31f6, 619dddda)`
## v0.17.4
### Features
- **Indexer**: Modernize HTML output flow and default to dark theme with light-mode toggle `(cc58316, d24cdcf)`
### Build
- **CI**: Split Linux release builds by runner architecture and set release title metadata `(aeccb68)`
- **Automation**: Add release bump/tag workflow skill for standardized release prep `(55f25d3)`
## v0.16.3
### Features
- **Mobile**: Add active upload destination selector and in-app destination label on Android and iOS `(0b42d73, ccfa4ea)`
- **Mobile**: Use desktop-compatible upload filename pattern on Android and iOS `(357188f, c0af5d6)`
- **Mobile**: Add broad share-intent support for arbitrary file types on Android and iOS `(dbb6633, 7292102)`
- **Media Explorer**: Implement provider file browsing with S3 and Imgur support, including navigation, search, filtering, and CDN thumbnail optimization `(9deedf9, e374160)`
- **Watch Folder**: Add watch-folder daemon with lifecycle hooks, runtime policy controls, and tests `(79c1292, 2b94600, 4265528, 992c41b)`
- **Mobile**: Add adaptive theming infrastructure with native styling polish `(4b79ddb, a7cfb22, 1e5f9eb, 30bbe98)`
- **Mobile**: Add upload queue, picker, and history screens `(68d97d9, 52d6ad2)`
- **UI**: Add Copy Errors to UI (HistoryView, AfterUploadWindow, Toast) `(5c08812)`
- **ImageEditor**: Add app/editor theme synchronization with platform-aware styling `(0d42719, 71fa3e1)`
### Fixes
- **iOS**: Use App Group settings so Share Extension can read Amazon S3 configuration `(42a1033)`
- **ImageEditor**: Fix precompiled Avalonia XAML startup crash (`XamlLoadException`) in editor app initialization `(258bb09, f987eaa)`
- **ImageEditor**: Improve highlight rendering/fill behavior, Smart Eraser, text defaults, and canvas zoom performance `(73dff63, 0eca71e, fcddf02, d9ab54a, db3bcaa, 584de4e, bd44498)`
- **ImageEditor**: Restore crop UX and precision with full-image/L-shape fixes, visible handles, and DPI-aware hit zones `(80eb42f, a1ac173, 592a2f1, 2cbc692, f85c57f)`
- **Scrolling Capture**: Improve auto-scroll behavior and workflow settings integration `(1fa45f2, 971219c, 8ac2c8b)`
- **Workflows**: Allow OCR and scrolling workflows from tray `(4e07852)`
- **Media Explorer**: Harden listing, normalize URLs, and improve error handling `(9bab13e, e1a5d59, 6b2b8d6, f4e796b)`
- **Mobile**: Unify iOS share payload handling and TimeZoneInfo serialization `(0aad5c1, a835153)`
- **Upload**: Align MainViewModel helper with parameterless copy/upload events `(06a2232)`
- **ImageEditor**: Update submodule with context menu fixes `(bb862c4, c5618de)`
- **Capture**: Optimize annotation layer rendering and resource management `(f3e3908, b3034be, af35c74, 4048f00)`
- **Documentation**: Update FAQ to correctly reference XerahS instead of ShareX in Linux screen capture section `(699634f)`
- **Infrastructure**: Integrate update-changelog skill into run-maintenance workflow `(5ade43b)`
### Refactor
- **Core**: Split large ViewModels, extract WatchFolder daemon base service, and consolidate ScreenRecordingManager startup flow `(86286af, 315549a, 1160519)`
- **Core**: Remove WindowState naming collisions `(506072e)`
- **Core**: Split GeneralHelpers into utility classes `(78214dd)`
- **Upload**: Add polymorphic uploader config pilot `(7f2815d)`
- **Workflows**: Extract app workflow orchestration services `(4ee8ab9)`
### Build
- **Infrastructure**: Add all-platform release workflow and repository sync helper script `(2fbe5ee, bd8d0d3)`
- **Android**: Add Android mobile build infrastructure `(3952287)`
- **Linux**: Harden plugin packaging, RPM strip behavior, and display diagnostics `(817d83a, 0723b45, 1c79a94)`
- **Hooks**: Add cross-platform ImageEditor recovery and auto-push on pre-push `(3098824, 899e8f1)`
### Documentation
- **Maintenance**: Simplify mobile README and move refactor/hardening notes into documentation archives `(ad719c9, c9ebe39, 72f2e55, c043844)`
- **Planning**: Update task planning docs and move completed XIP0033 `(caeaae1, e3f37e3, 04cf9cf, 168b2ea)`
- **Plugins**: Consolidate plugin documentation into 'developers/plugins' and standardize on .xsdp extension `(b78882f, 41702bd, 21927b4)`
- **Developer**: Consolidate developer documentation into 'developers' root folder `(1f17491)`
- **Architecture**: Add feasibility report for JS/CSS migration `(8fc7446, 47d833c, ce35146, e9ed21a, 8e97f89, ccff1c4)`
- **Submodules**: Add sync-submodules workflow and update ImageEditor to latest develop `(a05200f, a0e3054, 14be1df)`
- **Tasks**: Add refactoring audit skill and native UI theming task `(ff8ea0e)`
## v0.15.5
### Features
- **Linux Capture**: Add DBus fallbacks, KDE desktop permissions, and decision trace orchestration `(290b3e0, dc02dbd)`
### Fixes
- **Linux Capture**: Enforce portal-only sandbox policy, unify waterfall, and improve logging `(2de4ac6, c744059, a381faa)`
- **Builds**: Fix cross-platform build configuration and add linux-arm64 support `(ad8611c, 519423d)`
### Refactor
- **Linux Capture**: Modularize providers with parallel lanes, coordinator, and contracts `(733a49d, 5dd9931, 0a81693, 3569c0a)`
### Testing
- **Linux Capture**: Add Linux capture waterfall and lane matrix tests `(7f49769)`
### Documentation
- **Build System**: Rename developer README and add Linux guide `(717be27)`
- **Roadmap**: Finalize Linux phase roadmap and release gate `(76df673)`
## v0.15.0
### Features
- **Mobile**: Add Android and iOS MVP with Share Extension support, .NET MAUI project `(8746372, 03698c6, 493d147)`
- **Mobile**: Add Custom Uploader and Amazon S3 configuration UI `(#124, #125, @Hexeption; 78a488e)`
- **Indexer**: Implement async streaming indexer with progress and cancellation `(8b2fe88)`
### Fixes
- **Image Editor**: Share annotation preview visuals with ImageEditor to ensure consistency `(cc074ad)`
### Fixes
- **Annotations**: Optimize rendering, remove draw-start dot artifact, and improve responsiveness `(d1afa2f, faa84e7, 891eed0)`
- **Workflow**: Complete WorkflowType end-to-end wiring `(47ead0b)`
- **UX**: Hide SilentRun window on first open instead of minimizing `(7567223)`
- **Updates**: Gracefully handle repositories with only pre-releases `(ed68066)`
- **After Capture**: Persist "Show after capture window" behavior across repeated runs `(9a04c9d, a3a581d, a8262d4)`
- **Upload**: Add multi-uploader auto destination fallback and wire mobile Amazon S3 and plugin integration to InstanceManager `(72079e6, c06f17f, a576e78, 44c316b, 02087fb)`
- **Watch Folder**: Convert MOV captures to MP4 `(27f6fec)`
- **Settings**: Make backup and secrets filenames machine-specific `(c618542, 55a32d0)`
- **Amazon S3**: Reorder and renumber setup steps `(3196b02)`
- **iOS**: Improve local signing setup and share extension flow `(30f6822)`
### Build
- **Plugins**: Centralize plugin copy target and pass host TFM `(6bfa2e1)`
- **Dependencies**: Bump Avalonia packages to 11.3.12 `(27ce502)`
- **ImageEditor**: Update submodule for theme-aware view, net9 compatibility, and track develop branch `(5e8eee0, e03ec12, 71601ee, a17d91e, 493d147)`
### Documentation
- **Audits**: Organize audit files and update UI control inventory snapshots `(e3d2a9c, aadfea4)`
- **Tasks**: Mark XIP0030 complete and move to completed tasks `(25a83a1)`
## v0.14.0
### Features
- **Monitor Test**: Implement MonitorTest workflow with diagnostic and pattern testing modes `(56a1ea3, 1dc10f8)`
- **Tools**: Add Ruler workflow with full RegionCapture integration `(5647b4d, 8ea9419)`
- **Indexer**: Make Index Folder open in its own window `(8b20b3b)`
- **Editor**: Integrate upstream ShareX.ImageEditor submodule with File Open choice dialog `(0db2c71, 1a41df5)`
- **Region Capture**: Add annotation options persistence `(7e82df3)`
### Fixes
- **Logging**: Fix duplicate date in log filename on date rotation `(69cb3c2)`
- **Region Capture**: Improve annotation toolbar integration and reduce rebuild pressure `(4500b8a, 3bf8224)`
- **Indexer**: Enable Open in Browser button and remove WebView in favor of system browser `(4582529, 16945a0)`
- **Navigation**: Enable menu navigation and update editor data transfer APIs `(49772bf)`
- **Editor**: Sync ImageEditor fixes, persist annotation options, refactor platform abstractions, enable Zoom to Fit `(3ee199a, 2cc8fa7, 554099c, 79eb2be, e5ffef7)`
- **ImageEditor**: Update submodule with unified undo-redo, smart padding crop sync, clipboard fixes, z-order fixes, and dispose bug fixes `(240649d, b3125b8, 0ee0ad7, 4eb30bf, 0c2b53e, 1131223, 751eb7c)`
- **Packaging**: Restore macOS icon in Windows package build `(ba40fbb)`
- **Upload**: Delay upload progress title update until actual upload starts `(9d4894b)`
- **macOS**: Harden mac packaging and cross-platform editor wiring `(6e1d569)`
- **Dialogs**: Prevent File Open dialog crash and add global exception logging `(5cbf5dd)`
### Build
- **Cross-Compilation**: Add macOS from Windows support and build system documentation `(a2bf5a6, 19b3a84)`
- **Infrastructure**: Fix version parsing in Windows package script `(5069a01)`
## v0.13.0
### Fixes
- **Menu Bar**: Fix hash checker routing and dynamic workflows menu `(8068e6f)`
- **Upload**: Improve Upload Content workflow handling, window UX, and text upload routing `(62a1cda, 4fd8182)`
## v0.12.0
### Fixes
- **Tools**: Add media tools to navigation bar and fix DataTemplate issues `(485a438)`
- **Proxy**: Fix custom uploader loading and add configuration UI `(#77, @Hexeption)`
- **Linux**: Add dark mode support, theme settings, and Wayland Hyprland screenshot support `(#62, @unicxrn; #61, @unicxrn)`
- **macOS**: Add native application menu `(#60, @Hexeption)`
- **Custom Uploaders**: Fix compatibility improvements and version compatibility `(#74, @Hexeption; #71, @emmsixx)`
- **Security**: Fix DPAPI platform warning `(#73, @Hexeption)`
### Refactor
- **Editor**: Rename namespace from ShareX.Editor to XerahS.Editor and update all references `(25135d0, d0d1266, 1dfeb3b)`
### Build
- **Plugins**: Improve plugin copy target to only include plugin assemblies `(a9b5c63)`
- **Configuration**: Update build files, packaging configuration, issue templates, and .gitignore `(09222cc, 5c03c33, b107da9, 789ec93)`
## v0.11.0
### Features
- **Upload**: Implement UploadContentWindow and remove superseded upload WorkflowTypes `(298457a)`
## v0.10.0
### Features
- **Workflows**: Implement AutoCapture workflows `(a45d02f)`
## v0.9.0
### Features
- **Workflows**: Implement Pin to Screen workflows `(1e0d3f2)`
- **Amazon S3**: Enhance SSO with region selection `(6880866)`
### Fixes
- **Upload**: Improve upload error surfacing and history actions `(760a6ef)`
- **Workflows**: Preserve workflow order and exclude None `(6c08b22)`
- **Custom Uploaders**: Fix compatibility check for XerahS versions `(422710a)`
### Build
- **Plugins**: Restore plugin DLL deduplication with retry logic `(81db32e)`
### Core
- **Rendering**: Remove RectangleLight; modern Skia rendering deprecated it `(12d3ae5)`
## v0.8.0
### Features
- **Security**: Add cross-platform secrets store with diagnostics `(c2b8105, f626f09)`
- **Upload**: Add auto destination uploader `(f3abe81)`
- **Custom Uploaders**: Implement full support including editor UI and integration `(5962870, 8020d73)`
- **Task Settings**: Redesign Task Settings UX with dedicated Image/Video tabs `(43436af)`
- **Tray Icon**: Add recording-aware tray icon with pause/abort controls `(7d22818)`
- **Image Formats**: Add AVIF and WebP image format support `(3b89381)`
- **Linux/Wayland**: Fix screen capture on Wayland by integrating XDG Portal API `(4cc5a9f)`
### Fixes
- **Capture**: Allow clipboard payloads in capture phase `(a2e336f)`
- **Upload**: Add clipboard upload auto routing `(6527590)`
- **Region Capture**: Correct crop offset, refresh AfterCapture UI, and fix coordinate mapping for Windows `(c5efeab, #29)`
- **Linux**: Fix active window capture hierarchy, coordinates, hotkey initialization, and Region Capture `(2957c89, 007f261, 73dd95d, e8a9cc8)`
- **UX**: Hide main window when capture triggered from tray/navbar `(45264fb)`
- **UI**: Fix update dialog layout `(7868256)`
### Refactor
- **Editor**: Update XerahS.Editor.csproj references and docs `(1dfeb3b, 90b9fe0)`
## v0.7.0 - Annotation Overlays & Packaging
### Features & Improvements
- **Annotations**: Enable Annotation Toolbar in Region Capture Overlay and refactor `(05dcaf3, #53)`
- **Region Capture**: Add support for transparent background capture (RectangleTransparent) `(9ee7277)`
- **macOS**: Native single-file app bundle packaging (`.app`) `(c2b882c)`
- **Packaging**: Automated multi-arch Windows release builds `(49a7ec6)`
- **Plugins**: Support for user-installed plugins and packaging `(e787536)`
- **Window Capture**: Add support via monitor cropping fallback `(d73daf5)`
- **Media Library**: Basic implementation `(#49)`
### Bug Fixes
- **Annotation Layer**: Fix coordinate system for multi-monitor/high DPI and compositing `(5d69425, 61bd0c9, 3875298)`
- **Exceptions**: Global exception handling implementation `(ad6d443)`
- **Screen**: Fix frozen screen issue `(#51)`
- **Cursor**: Fix system cursor issues `(#46)`
## v0.6.0 - UI Redesign & Auto-Update
### Features & Improvements
- **UI Redesign**: Comprehensive visual overhaul of all views using Grid layout and consistent styling `(34f4cbf, d390fa7)`
- **Auto-Update**: Implement auto-update system with Avalonia UI `(54b9546)`
- **After Upload**: Add "After Upload" results window `(18a3ab7)`
- **Property Grid**: Add ApplicationConfig property grid `(c4d20bf)`
- **CLI**: Add `verify-recording` command for automated screen recording validation `(732e173)`
- **Editor**: Unify editor undo history across different toolsets `(24ad021)`
- **Architecture**: Move Windows-specific P/Invoke types to dedicated Platform.Windows project `(90da89a)`
- **FFmpeg**: Improve FFmpeg download/config UX with progress hooks and better path resolution `(1646cbb, 7677ceb, b4fdcbf)`
- **Documentation**: Replace ShareX.Avalonia references with XerahS `(#44)`
- **Workflow**: Update cursor handling `(#43)`
### Bug Fixes
- **Recording**: Improve GIF recording quality, add clipboard support, pause, and stroke-based abort `(1baecc0, 4148e49, c3d04a7)`
- **After Upload**: Fix window theming and errors `(9b752c0, 6dfe81e)`
- **Rendering**: Fix speech balloon tail geometry rendering `(784594e)`
- **Region Capture**: Fix system cursor appearing in screenshots and hotkey issues `(85a4e2f, #38, #39)`
## v0.5.0 - Core Capture & Editor Improvements
### Features & Improvements
- **Capture**: Add single instance enforcement for the application `(aacb23b)`
- **Region Capture**: Enhance crosshair visibility, add magnifier pixel sampling, and hide system cursor when ghost cursor active `(a838ae1, 56aa4de, d338b32)`
- **Editor**: Wire ImageEffectsViewModel to unified undo/redo stack `(81a3815)`
- **UX**: Set default file picker location to Desktop for easier access `(f5083e3)`
### Bug Fixes
- Fix 11+ HIGH/MEDIUM priority issues including null safety and resource management `(9188a22, 1f9a74f)`
- Set RegionCaptureControl cursor to None to prevent double cursor visibility `(fe35424)`
## v0.4.0 - Image Effects & Tools
### Features & Improvements
- **Image Effects**: Refactor preset management and improve effects UI `(154a6c9, 5d9dbd7, ee47e3d)`
- **Tools**: Add QR code generator/decoder and Color Picker tools with standard color name mapping `(66bd61b, bdb22f8, 0b50328)`
- **Watch Folders**: Implement Watch Folder system with per-folder workflow assignments `(49e838d, 63124f6, 951e034)`
- **Indexer**: Add Index Folder preview and modernize HTML output using WebView `(63ca369, 3f3a751, e57932e)`
- **macOS**: Add native ScreenCaptureKit video recording support `(fd75640)`
### Bug Fixes
- **Capture**: Fix cursor tracking and visibility during GDI capture `(f6973f6, e0a056b, 265a96a)`
- **Capture**: Fix NullReferenceException in DXGI capture by preventing premature disposal of D3D11 device context `(df9bd33)`
## v0.3.0 - Modern Capture Architecture
### Features & Improvements
- **Modern Capture**: Implement DXGI-based high-performance screen capture for Windows `(1440efc, 25f544d)`
- **Screen Recording**: Unified recording pipeline with Windows Media Foundation and FFmpeg support `(9224b62, 7a6e47b, 8fc451c)`
- **Workflow System**: Major overhaul of hotkeys into full Workflow system with GUID persistence `(faebe87, 09f1e35)`
- **Toast Notifications**: New custom Avalonia-based notification system with advanced settings `(6229154, f1d9b88)`
- **Linux**: Initial support for Wayland via XDG Desktop Portal and native X11 capture `(3573ad1, f7a103c, b92fb89, 7ccd5d9)`
- **Settings**: Add weekly backup system for application settings `(0a8e15f)`
- **UX**: Add tray icon support with customizable click actions `(035e8b4, 4ddfb59)`
### Bug Fixes
- **Modern Capture**: Fix multi-monitor blank capture issues `(52ae45e)`
- **Region Capture**: Fix DPI handling, coordinate mapping, and offsets/scaling on multi-monitor setups `(e4817b1, 954dee3, e47e81b)`
- **Code Quality**: Massive code audit fixing 500+ license headers and 160+ nullability issues `(dca9217, dd90761)`
- **Windows**: Standardize Windows TFM and fix CsWinRT interop issues `(2f44742, 4e88d23)`
## v0.2.0 - macOS Support & Plugin System
### Features & Improvements
- **macOS**: Initial platform support including ScreenCaptureKit, SharpHook hotkeys, and app bundling `(acba9d5, ca05d4b, 6fbf63e)`
- **Plugins**: Implement dynamic plugin system with packaging (`.sxap`), CLI tools, and `.sxadp` file association `(f81c656, a2adbf3, e787536, df9bbd1)`
- **History**: Switch history storage from XML to SQLite with automatic backups `(22b6cf5, 0f20d76)`
- **Editor**: Integrate ShareX.Editor as core component with SkiaSharp rendering `(57bfe32, 90b5871)`
## v0.1.0 - Initial Feature Set
### Core Features
- **UI**: Reimagined interface with two-toolbar system and modern dark theme `(c0bad1e, 231e4df)`
- **Capture**: Region, Fullscreen, and Window capture modes `(4839944)`
- **Annotations**: Object-based editor with Rectangle, Ellipse, Arrow, Line, Text, Number, Crop tools, and full Undo/Redo support `(bd1153c, 9b6cfe0, 9ecd720, cb7b54a)`
- **Hotkeys**: Global hotkey system with Win32 registration `(80cd222)`
- **Image Effects**: Initial implementation of 40+ effects including Resize, Shadows, and Gradients `(0840cef, 6777d86)`
- **History**: Basic task history tracking `(9c1c2f8)`
---
*This changelog follows Semantic Versioning while the project remains in pre-release (0.x.x).*
