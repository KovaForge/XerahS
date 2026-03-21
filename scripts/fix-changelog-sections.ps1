$file = "c:\Users\liveu\source\repos\ShareX Team\XerahS\docs\CHANGELOG.md"
$content = [System.IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8)

# === Consolidated v0.19.9 section ===
$new199 = @'
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
- **ImageEditor**: Submodule updates and macOS build; add ShareX.ImageEditor at develop; Zoom to Fit in zoom picker; §7a easy wins (Random.Shared, Category overrides, Gamma LUT cache) `(03833f97, aa407405, ae2a7ac6, 3179068a, 18c11a48, aeba3c67, 81d9cfee, 16e6f52d, 36dfd283, 6ab5833c, 6c220749, 8a8a493d, c65cb432, a13faf83, e3e01c2f)`
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

'@

# === Consolidated v0.18.11 section ===
$new1811 = @'
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

'@

# Replace v0.19.x block (v0.19.9 thin + v0.19.8 + v0.19.5 + v0.19.0) with consolidated v0.19.9
$content = [System.Text.RegularExpressions.Regex]::Replace(
    $content,
    '(?s)## v0\.19\.9.*?(?=## v0\.18\.11)',
    $new199
)

# Replace v0.18.x block (empty v0.18.11 + v0.18.9) with consolidated v0.18.11
$content = [System.Text.RegularExpressions.Regex]::Replace(
    $content,
    '(?s)## v0\.18\.11.*?(?=## v0\.17\.4)',
    $new1811
)

[System.IO.File]::WriteAllText($file, $content, [System.Text.Encoding]::UTF8)
Write-Host "Done. Sections replaced successfully."
