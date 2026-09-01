# XIP0087 Omarchy Screenshot Upload Plugin

**Status**: Draft (implementation uses independent `omaxerahs`, not `xerahscli`)
**Created**: 2026-09-02
**Updated**: 2026-09-02
**Version**: v0.28.0
**Area**: Linux | CLI | Uploaders | Integration | Omarchy
**Related**: XIP0063, XIP0075, XIP0076, XIP0079, XIP0082, XIP0083
**Implementation spec**: [XIP0087-omaxerahs-design.md](XIP0087-omaxerahs-design.md)
**Goal**: Provide a safe Omarchy plugin that can upload completed screenshots through the user's configured XerahS image destination without duplicating upload providers, credentials, or workflow policy.

---

## Overview

Omarchy users should be able to send screenshots to XerahS without opening a second capture tool or reconfiguring an upload destination in a second application. XerahS already owns provider discovery, authentication, upload workflows, URL handling, history, and secret storage. The integration should reuse those capabilities through the headless `xerahscli` entry point.

This XIP proposes two coordinated deliverables:

1. A supported native Linux distribution of `xerahscli` with deterministic image-upload and machine-readable output contracts.
2. A separately published Omarchy Quickshell plugin that observes or initiates Omarchy screenshots, queues completed image paths, invokes `xerahscli`, and exposes status and controls.

The plugin is intentionally an adapter, not another uploader implementation. It must not contain provider credentials, call provider APIs directly, modify XerahS configuration, replace Omarchy's screenshot implementation, or watch the clipboard. XerahS owns the remote upload and result; the shell adapter owns the user-requested Wayland clipboard copy and Omarchy notification so those desktop side effects happen exactly once.

Automatic upload is opt-in and disabled by default. A user can always choose an explicit capture-and-upload action instead. Existing screenshots remain local regardless of upload success or plugin removal.

## User Experience

### First-run flow

1. The user installs the native XerahS package, launches XerahS, and configures an image destination.
2. The user verifies readiness with the documented `xerahscli doctor` command.
3. The user installs the XerahS Upload plugin from the Omarchy plugin marketplace.
4. The plugin reports one of these states: `Ready`, `XerahS CLI missing`, or `Image destination not ready`.
5. The user may upload one screenshot explicitly or opt in to automatic upload of new Omarchy screenshots.

The plugin must not enable automatic uploading during installation or first launch.

### Explicit capture and upload

The bar widget or plugin panel exposes **Capture and upload**. The action invokes Omarchy's own screenshot command in save mode, reads the successfully emitted path, and submits that path to XerahS. This is the most deterministic integration because the plugin receives the exact file selected by the user.

The initial command contract is:

```bash
omarchy capture screenshot smart save
xerahscli upload --type image --json --quiet --no-randomize --no-clipboard --no-notify -- "$screenshot_path"
```

The exact argument order may follow the final CLI parser, but explicit image routing, clean JSON, preservation of the original filename, suppression of CLI desktop side effects, and safe end-of-options handling are required behaviours.

### Keyboard integration

The singleton service exposes a Quickshell `IpcHandler` with the stable target `xerahs-upload`. Its `capture` method accepts only Omarchy's supported capture modes: `smart`, `region`, `windows`, and `fullscreen`.

```bash
omarchy-shell xerahs-upload capture smart
omarchy-shell xerahs-upload capture region
omarchy-shell xerahs-upload capture windows
omarchy-shell xerahs-upload capture fullscreen
```

The IPC handler returns promptly after accepting the request. The singleton then starts `omarchy capture screenshot <mode> save` asynchronously, waits for a successful saved path, and queues that exact file once. Upload progress and the final URL are delivered through the plugin notification and status UI. The validated URL is copied to the Wayland clipboard when the upload completes.

This supports two keyboard experiences:

1. **Add a dedicated upload shortcut.** The existing `Print` behaviour remains unchanged, while a new user-selected key calls `omarchy-shell xerahs-upload capture smart`.
2. **Explicitly replace an existing screenshot binding.** A user may point `Print` or another existing binding at the same IPC call, making capture, upload, notification, and URL copy one action.

The plugin must provide copyable Hyprland configuration examples for both cases. Installation must not edit bindings automatically. If a user replaces an existing binding, the removal instructions must tell them how to restore Omarchy's normal `omarchy-capture-screenshot` command.

For example, after checking that the proposed key is unbound, a user can add this to `~/.config/hypr/bindings.lua`:

```lua
o.bind("SUPER + SHIFT + PRINT", "Screenshot and upload", "omarchy-shell xerahs-upload capture smart")
```

Or the user can explicitly replace `Print`:

```lua
-- PRINT is normally bound to omarchy-capture-screenshot.
hl.unbind("PRINT")
o.bind("PRINT", "Screenshot and upload", "omarchy-shell xerahs-upload capture smart")
```

Restoring the stock behaviour is equally explicit:

```lua
hl.unbind("PRINT")
o.bind("PRINT", "Screenshot", "omarchy-capture-screenshot")
```

The deterministic IPC-bound capture is the recommended version-one experience. Automatic directory observation is a secondary convenience for users who want the unchanged stock screenshot actions to upload as well.

### Automatic upload

When the user explicitly enables **Automatically upload new Omarchy screenshots**, the plugin observes completed writes in Omarchy's resolved screenshot directory. It only considers newly closed files matching Omarchy's `screenshot-*.png` convention. It does not scan or upload existing files when enabled.

Automatic mode covers Omarchy's normal and save screenshot paths. Clipboard-only capture has no file path and is therefore never inferred or uploaded. A clipboard watcher is explicitly prohibited.

With automatic mode enabled, the unmodified Omarchy `Print` shortcut becomes a capture-and-upload flow: Omarchy first saves the screenshot normally, the singleton observes the completed file, XerahS uploads it, and the plugin replaces the image clipboard content with the resulting URL after success. This path is seamless for the user but remains a documented filename/directory observation heuristic until Omarchy provides a native screenshot-completed event.

The stock command writes the initial capture before offering its optional editor action. Automatic mode therefore uploads that initially saved image; it cannot infer that a later annotation session is complete. The observer claims a canonical path on its first accepted completed write and does not upload later rewrites of that path automatically. Users who want an unambiguous one-shot capture-to-URL flow should use the recommended IPC-bound save-mode shortcut.

### Successful upload

On success:

- XerahS applies the configured image destination and returns the resulting URL.
- The plugin copies the validated `http` or `https` URL with `wl-copy` when URL copying is enabled in plugin settings.
- The plugin displays a notification through Omarchy's notification wrapper.
- The widget records the last successful upload, without storing credentials or provider secrets.
- The source screenshot remains unchanged and is not deleted.

### Failed upload

On failure:

- The local screenshot remains unchanged.
- The existing clipboard content is not replaced by the plugin.
- The plugin exposes a concise error and a retry action.
- Authentication or destination-readiness failures pause automatic processing until the user resolves them.
- Transient failures use bounded retry; no upload loops indefinitely.

## Prerequisites

- Omarchy with Quickshell plugin support and `omarchy plugin validate`.
- A native XerahS Linux installation that provides both `xerahs` and `xerahscli` on `PATH`.
- A configured and ready XerahS image uploader.
- Omarchy's existing `inotify-tools` dependency for automatic mode.
- Network access required by the user's selected upload destination.

Flatpak-only XerahS installations are out of scope for the first version. Omarchy screenshot paths are host files, while the current Flatpak package intentionally does not grant broad host filesystem access. The plugin must detect this case and explain that the native package is required rather than requesting broader Flatpak permissions silently.

## Current Architecture

### Omarchy screenshot flow

Omarchy binds `Print` and its capture menu to `omarchy-capture-screenshot`. The script:

1. Resolves the output directory from `OMARCHY_SCREENSHOT_DIR`, the XDG Pictures directory, or `~/Pictures`.
2. Captures a region with `slurp` and `grim`.
3. Names the result `screenshot-*.png`.
4. Depending on mode, saves the file, copies it to the clipboard, notifies the user, or opens an editor.

The current script prints a saved path for normal and save modes, but Omarchy exposes no screenshot-completed plugin signal, D-Bus event, or post-capture hook. Marketplace installation also does not authorize a plugin to rewrite the user's keybindings or packaged Omarchy scripts.

The relevant Omarchy source at `b71dcad96e9d0b2962b7d225828a5cb6000ad720` is:

- `default/hypr/bindings/utilities.lua`
- `default/omarchy/omarchy-menu.jsonc`
- `bin/omarchy-capture-screenshot`
- `bin/omarchy-plugin-add`
- `default/quickshell/omarchy/services/PluginService.qml`

### Omarchy plugin model

Omarchy supports singleton `service` plugins and per-screen `bar-widget` plugins. A screenshot observer must live in a singleton service; placing the watcher in a bar widget would create duplicate watchers and upload attempts on multi-monitor systems. A thin bar widget may obtain and control the singleton through Omarchy's plugin service registry.

The proposed manifest therefore declares both kinds:

```json
{
  "schemaVersion": 1,
  "id": "io.github.sharex.xerahs-upload",
  "name": "XerahS Upload",
  "version": "0.1.0",
  "author": "ShareX Team",
  "description": "Upload Omarchy screenshots with your configured XerahS image destination.",
  "kinds": ["service", "bar-widget"],
  "entryPoints": {
    "service": "Service.qml",
    "barWidget": "BarWidget.qml"
  },
  "barWidget": {
    "displayName": "XerahS Upload",
    "defaultSection": "right",
    "allowMultiple": false
  }
}
```

The final manifest must be validated against the Omarchy version targeted at publication.

### XerahS upload flow

XerahS already has a headless CLI project at `src/desktop/cli/XerahS.CLI`. Its `upload` command uses the established upload pipeline rather than a separate provider implementation:

```text
UploadCommand
  -> CliUploaderBootstrapper
  -> ProviderCatalog / InstanceManager
  -> UploadJobProcessor
  -> configured uploader instance
  -> AfterUpload workflow actions
```

This is the correct seam for the Omarchy plugin. Provider secrets remain in XerahS's existing secret store, using libsecret on supported Linux desktops and the existing protected fallback where necessary.

At XerahS commit `672b258c013e3fa49d669f3f718a888d1c0374a3`, two blockers prevent treating the CLI as a stable Omarchy dependency:

1. Native Linux packaging publishes and installs the desktop application but does not install `xerahscli` as a supported executable.
2. The CLI's default path identifies an uploaded pathname as generic file data. A configured file uploader may therefore receive a PNG before the selected image uploader is considered. Screenshot integration requires deterministic image-category routing.

Machine-readable output must also remain isolated from human diagnostics. In JSON mode, stdout must contain exactly one versioned JSON result object; notifications, warnings, and progress belong on stderr or must be suppressed by quiet mode. The current headless toast implementation writes notification text to stdout and can therefore precede a JSON error.

The CLI also currently forces `CopyURLToClipboard` and randomizes the input filename through a temporary copy by default. Those behaviours are unsuitable for a shell integration unless they are explicitly controllable. The plugin must be able to preserve the original screenshot name and own clipboard/notification side effects without duplicating the CLI.

Finally, independent `xerahscli` processes share uploader settings guarded only by an in-process lock. The plugin can serialize its own queue, but XerahS needs a cross-process settings lock, or a future persistent request/response service, before concurrent external callers can be considered fully safe.

## Proposed Architecture

```text
                         explicit action
Omarchy bar/panel ------------------------------+
                                                 |
Omarchy screenshot directory -- inotify adapter -+--> singleton queue
                                                        |
                                                        v
                                              upload adapter script
                                                        |
                                                        v
                                  xerahscli upload --type image --json
                                                        |
                          +-----------------------------+--------------------+
                          |                             |                    |
                          v                             v                    v
                XerahS provider catalog       XerahS secret store   XerahS workflow/history
                          |
                          v
                    uploaded URL
                          |
                          v
               validated URL -> wl-copy
                          |
                          v
            Omarchy notification + widget status
```

### Repository boundary

The marketplace expects `manifest.json`, `README.md`, and `LICENSE` at the root of a public GitHub repository. A plugin nested inside the XerahS monorepo would not satisfy that install contract without additional marketplace capabilities.

The Omarchy adapter should therefore be developed and released from a dedicated public repository. Proposed repository name: `ShareX/omarchy-xerahs`. The final name is a release decision; the manifest ID should remain stable once published.

The XerahS repository owns CLI contracts, Linux packaging, and integration tests. The plugin repository owns Quickshell UI, directory observation, queue orchestration, Omarchy-facing documentation, and marketplace assets.

### Plugin layout

```text
manifest.json
Service.qml
BarWidget.qml
scripts/
  watch-screenshots.sh
  upload-screenshot.sh
tests/
README.md
LICENSE
preview.png
```

Shell scripts must use quoted argv, avoid `eval`, reject unsupported modes, and treat paths beginning with `-` safely. No file in the plugin repository may be a symlink.

### Singleton service

`Service.qml` owns all mutable integration state:

- `Disabled`: automatic upload has not been enabled.
- `NotReady`: `xerahscli` or a ready image uploader is unavailable.
- `Idle`: ready with an empty queue.
- `Capturing`: explicit Omarchy capture is active.
- `Queued`: one or more paths await processing.
- `Uploading`: one path is being submitted to XerahS.
- `Succeeded`: the most recent upload completed.
- `Failed`: the most recent upload needs attention.
- `Paused`: automatic processing is suspended.

Only one CLI process may upload at a time. Queue length is bounded, duplicate paths are collapsed, and process shutdown terminates the directory observer cleanly.

The service also owns the single `IpcHandler` target `xerahs-upload`. At minimum it exposes:

- `capture(mode)`: start an Omarchy save-mode capture and enqueue its exact resulting path;
- `status()`: return versioned JSON describing readiness, queue length, active state, and the last result;
- `pause()` and `resume()`: control automatic observation without changing the user's keybindings;
- `retry()`: retry the most recent unambiguously failed local path.

IPC methods must validate all arguments and must not expose an arbitrary shell-command execution surface.

### Screenshot observation

Automatic mode launches one `inotifywait` process for the resolved Omarchy screenshot directory and listens for `close_write`. The adapter accepts only regular PNG files whose basename matches Omarchy's screenshot naming convention.

Deduplication claims each canonical path on its first accepted completed write, with stable file metadata and a content digest available when event identity is ambiguous. Later rewrites of an already claimed path do not trigger another automatic upload. State is stored under an XDG state path dedicated to the plugin. The plugin must not persist upload credentials or full provider configuration.

Directory observation is a compatibility adapter, not a perfect semantic event. An unrelated process can create a matching filename. This limitation must be documented in the plugin UI and README.

### Future Omarchy screenshot event

The preferred long-term integration is a small upstream Omarchy contract that emits the absolute path after `grim` successfully closes the output file. It should emit for normal and save modes, never for cancellation, capture failure, or clipboard-only mode.

If Omarchy adds that contract, the plugin should prefer it and retain directory observation only for compatible older releases. Upstreaming the event is not required to build the first plugin release and must not be simulated by patching packaged Omarchy files.

### XerahS CLI contract

The plugin depends on the following stable native CLI surface:

```bash
xerahscli doctor uploaders --category image --json --quiet
xerahscli upload --type image --json --quiet --no-randomize --no-clipboard --no-notify -- /absolute/path/screenshot.png
```

Required behaviours:

- `--type auto|image|file|text` selects upload semantics explicitly; `image` selects `EDataType.Image`.
- `auto` recognizes supported image content or extensions, while `file` remains an explicit override compatible with the existing `--as-file` behaviour.
- Image selection must clear or replace any destination instance ID inherited from a File-category workflow.
- Readiness checks are category-specific and verify a configured image uploader.
- Exit code `0` means the operation succeeded; non-zero means it did not.
- JSON success includes a schema version and at least `url`, `filename`, `size`, `type`, uploader identity, and instance identity.
- JSON failure includes the same schema version, a stable error code, and a user-actionable message.
- In `--json --quiet` mode, stdout contains one JSON object and no toast, progress, banner, or diagnostic text.
- Human diagnostics and headless toast output go to stderr.
- `--no-randomize` preserves the source filename rather than uploading a randomized temporary copy.
- `--clipboard/--no-clipboard` and `--notify/--no-notify` make desktop side effects explicit.
- Provider authentication and workflow selection come from the user's existing XerahS configuration.
- XerahS owns upload history and provider-specific error interpretation; in integration mode the plugin owns the final Wayland clipboard copy and Omarchy notification.

### Readiness and compatibility

The plugin performs checks in this order:

1. Confirm `xerahscli` resolves to an executable.
2. Confirm the CLI exposes a compatible machine-readable version or capability contract.
3. Run the image-uploader readiness command.
4. Resolve and validate the Omarchy screenshot directory only if automatic mode is enabled.

The UI must distinguish a missing CLI, incompatible CLI, missing image destination, locked/unavailable secret service, and upload failure. It must never respond by installing packages with privilege escalation or changing XerahS settings automatically.

## Implementation Plan

### Phase 1 Native Linux CLI packaging

- Extend the Linux publish pipeline to build `XerahS.CLI` for supported native targets.
- Install the CLI payload in the XerahS package and expose `/usr/bin/xerahscli` through the package manager.
- Ensure the CLI can locate the same bundled provider plugins and resources as the desktop application.
- Update the Arch/AUR packaging to include the CLI executable.
- Verify the desktop application and CLI share the intended XDG configuration and secret-store locations.
- Add release-asset checks that fail if a native Linux package omits `xerahscli`.

### Phase 2 Image upload CLI contract

- Add `--type auto|image|file|text` while preserving the existing `--as-file` and text behaviours as compatible aliases where appropriate.
- Route recognized image paths to `EDataType.Image` by default unless the caller explicitly selects file treatment, and prevent a File-category destination ID from leaking into the image request.
- Add category-specific uploader readiness to `doctor`.
- Make JSON/quiet output deterministic and prevent headless toast output from contaminating stdout.
- Add explicit clipboard and notification switches so an external shell can own those desktop side effects.
- Preserve the original filename when `--no-randomize` is selected.
- Add a cross-process lock around shared settings mutation, or define a persistent request/response upload service.
- Define stable JSON error identifiers for not-ready, authentication, invalid-path, unsupported-version, cancelled, provider, and network failures.
- Publish the supported CLI capability/version contract in user documentation.

### Phase 3 XerahS automated tests

- Verify a PNG uses the configured image uploader even when a separate file uploader is ready.
- Verify `--as-file` continues to select the file uploader.
- Verify `--type image` rejects or reports an invalid non-image path consistently.
- Verify JSON stdout is exactly one parseable object on success and failure.
- Verify clipboard and notification switches suppress side effects and the integration copies the URL at most once.
- Verify CLI exit codes and category-specific doctor results.
- Verify native packaging contains a runnable `xerahscli` and provider payload.
- Verify concurrent CLI processes cannot race while reading, repairing, or saving uploader-instance settings.

### Phase 4 Omarchy plugin repository

- Create the dedicated public plugin repository with the root marketplace files.
- Implement the singleton service and a thin, multi-monitor-safe bar widget.
- Implement explicit capture-and-upload using Omarchy's own capture command.
- Expose the singleton `xerahs-upload` IPC target for stable keyboard bindings.
- Implement opt-in `close_write` observation, filtering, deduplication, queue bounds, and clean shutdown.
- Parse versioned CLI JSON rather than scraping human-readable output; accept only a successful exit and an `http` or `https` result URL.
- Copy the result URL with `wl-copy` and send the completion notification from the plugin when those options are enabled.
- Expose readiness, pause/resume, retry, last-result, and explicit capture controls.
- Route notifications through `omarchy-notification-send`.
- Ensure disable, re-enable, Quickshell restart, and plugin removal leave no watcher or upload process behind.

### Phase 5 Documentation and publication

- Document native XerahS installation and image-destination setup.
- Document what explicit and automatic modes upload, including the filename-watch limitation.
- Document all filesystem, process, network, and credential boundaries.
- Document dedicated and replacement keybinding examples as explicit, reversible user actions; do not overwrite `Print` automatically.
- Document disable, removal, troubleshooting, and retained local state.
- Validate with `omarchy plugin validate` and `qmllint -I "$OMARCHY_PATH/shell"`.
- Test installation from the exact public repository URL on a clean Omarchy user profile.
- Publish a licensed preview image owned by or permitted for the project.
- Submit the marketplace issue with the `Productivity` category and an appropriate existing tag; propose an `Uploads` tag only if the marketplace maintainers want a more specific taxonomy.

## Non-Negotiable Rules

1. **Uploads are never enabled implicitly.** Installation and first launch leave automatic mode off.
2. **No duplicate provider pipeline.** The plugin invokes `xerahscli`; it does not implement provider APIs, authentication, remote retries, or upload history independently. Its only post-upload side effects are the requested Wayland clipboard copy and Omarchy notification.
3. **No credential access.** The plugin never reads, writes, logs, exports, or stores XerahS provider secrets.
4. **No clipboard inference.** The plugin does not watch the clipboard or treat clipboard content as evidence of a screenshot.
5. **No destructive screenshot handling.** A source screenshot is never modified or deleted, including after success.
6. **No silent configuration changes.** The plugin does not alter Omarchy keybindings, shell files, environment files, XerahS settings, or uploader selection.
7. **No privilege escalation.** Installation and runtime do not invoke `sudo`, a package manager, or a privileged service.
8. **One observer and one upload worker.** Multi-monitor bar instances must not duplicate watchers, queue consumers, or upload attempts.
9. **Machine-readable process boundary.** Integration uses structured JSON and stable exit codes, not localized UI text.
10. **Safe removal.** Removal stops plugin-owned processes and leaves XerahS configuration, credentials, history, and local screenshots intact.
11. **No second Quickshell process.** The plugin runs inside Omarchy's existing shell plugin lifecycle.
12. **No hidden Flatpak permission expansion.** Native installation is required until a narrower supported portal or filesystem contract exists.

## Privacy and Security

Uploading a screenshot can disclose sensitive visual information. The design therefore requires an explicit action before each upload unless the user separately opts in to automatic mode. The automatic-mode control must state that every new matching Omarchy screenshot in the configured directory may be sent to the selected remote provider.

The plugin runs unsandboxed in the user's Quickshell session, as Omarchy plugins do generally. Marketplace validation is a listing and schema check, not a security review. The repository must document that the plugin can:

- observe filenames and read selected screenshot files;
- launch Omarchy and XerahS CLI processes;
- make uploads indirectly through XerahS and the configured provider;
- retain bounded non-secret operational state;
- display notifications and status in the shell.

Logs must not contain image bytes, provider credentials, authorization headers, or full CLI JSON responses that may contain private URLs. Debug logging of paths must be opt-in and clearly disclosed. If a URL is sensitive, the plugin should display only the host and filename in persistent UI while allowing the existing XerahS workflow to manage the full clipboard value.

The upload adapter must pass paths as argv, not construct an executable shell expression. It must reject non-regular files and revalidate the canonical path immediately before upload to reduce symlink and replacement races.

## Deliverables

### XerahS repository

- Native Linux `xerahscli` package payload and PATH entry.
- Explicit image-routing CLI option and image-aware default routing.
- Image-category readiness diagnostics.
- Deterministic JSON/quiet output and stable exit codes.
- Explicit clipboard/notification controls and safe original-filename handling.
- Cross-process protection for shared uploader settings.
- Unit, integration, and package-content tests.
- Linux installation and headless upload documentation.

### Omarchy plugin repository

- Valid root `manifest.json`.
- Singleton `Service.qml` and thin `BarWidget.qml`.
- Safe watcher and upload adapter scripts.
- Automated script/queue tests where practical.
- `README.md` covering install, configure, use, privacy, troubleshooting, disable, and removal.
- An OSI-approved license, with upstream notices preserved for any copied code.
- Optional owned or licensed `preview.png`.
- Completed Omarchy marketplace submission.

## Affected Components

### XerahS

- `src/desktop/cli/XerahS.CLI/Commands/UploadCommand.cs`
- `src/desktop/cli/XerahS.CLI/Commands/DoctorCommand.cs`
- `src/desktop/cli/XerahS.CLI/Services/CliUploaderBootstrapper.cs`
- `src/desktop/cli/XerahS.CLI/Services/HeadlessToastService.cs`
- `src/desktop/cli/XerahS.CLI/Program.cs`
- `src/desktop/core/XerahS.Core/Tasks/Processors/UploadJobProcessor.cs`
- Linux publish scripts and Arch/AUR packaging
- CLI and packaging tests
- Linux and CLI documentation

### New Omarchy plugin repository

- plugin manifest and lifecycle service
- bar widget and optional panel controls
- screenshot observer and upload adapter
- plugin tests, documentation, license, and marketplace assets

### Omarchy upstream, optional follow-up

- a stable screenshot-completed event or post-capture hook that supplies a successfully written absolute path

## Acceptance Criteria

### XerahS contract

- A supported native Arch/Omarchy installation places both `xerahs` and `xerahscli` on `PATH`.
- A configured image uploader can upload a PNG through `xerahscli` without a running XerahS window.
- PNG routing selects the image uploader deterministically even when a file uploader is also configured.
- `--as-file` preserves explicit generic-file behaviour.
- Image readiness can be checked without uploading.
- JSON/quiet stdout is parseable as exactly one result object on every terminal path.
- Integration mode can suppress CLI clipboard and notification side effects.
- Parallel external CLI calls cannot corrupt or race shared uploader-instance settings.
- Credentials remain in the existing XerahS secret store.

### Plugin behaviour

- Installing the plugin does not upload, edit keybindings, modify application settings, or request privilege escalation.
- Explicit capture-and-upload submits exactly the file produced by the corresponding successful Omarchy command.
- `omarchy-shell xerahs-upload capture <mode>` accepts every supported capture mode, rejects invalid modes, and queues the exact successful output path once.
- A dedicated shortcut can coexist with the stock `Print` binding, and a user-replaced binding provides a single capture-to-URL action.
- Automatic mode uploads only new, completed, matching screenshots after the user enables it.
- Clipboard-only capture is never uploaded.
- Multi-monitor configurations run exactly one observer and one queue worker.
- Duplicate filesystem events result in at most one upload for the same completed file version.
- Disabling, re-enabling, restarting Quickshell, and removing the plugin do not leave orphan processes.
- Failed uploads preserve the screenshot and expose a retry without an infinite loop.
- Plugin removal preserves screenshots, XerahS settings, provider secrets, and history.

### Publication

- `omarchy plugin validate` succeeds from the repository root.
- QML linting completes without errors against the supported Omarchy shell import path.
- The public repository root contains the manifest, README, and license.
- README documents dependencies, install, first-run setup, explicit and automatic modes, privacy, limitations, disable, and removal.
- The marketplace checklist truthfully confirms that configuration is not overwritten without explicit consent and that listing does not constitute a security review.

## Validation Plan

### Automated

- XerahS CLI routing and JSON-contract tests.
- XerahS side-effect-switch, filename-preservation, and process-concurrency tests.
- XerahS package-content smoke test for `xerahscli` and provider discovery.
- Plugin shell-script tests for quoting, special-character paths, filtering, deduplication, queue bounds, and malformed CLI JSON.
- Plugin service tests for state transitions and single-worker behaviour.
- Manifest validation and QML linting in CI.

### Manual on Omarchy

1. Install native XerahS and configure separate image and file destinations.
2. Confirm doctor reports the image destination ready.
3. Install the plugin from its public GitHub URL.
4. Confirm no screenshot is uploaded before an explicit action or automatic opt-in.
5. Use explicit capture-and-upload; confirm the image destination receives the PNG exactly once.
6. Bind a new key to `omarchy-shell xerahs-upload capture smart`; confirm capture, one upload, URL notification, and URL clipboard content.
7. Explicitly replace and then restore the `Print` binding using the documented commands.
8. Enable automatic mode and exercise normal, save-only, and clipboard-only Omarchy modes with the stock binding.
9. Confirm normal and save-only files upload once, while clipboard-only does not upload.
10. Cancel region selection and confirm no upload is attempted.
11. Test filenames and directories containing spaces and shell metacharacters.
12. Test offline, provider rejection, expired credentials, and missing CLI conditions.
13. Test two monitors and verify a single watcher and upload process.
14. Disable, re-enable, restart Quickshell, and remove the plugin while idle and during an upload.
15. Confirm existing screenshots, XerahS settings, credentials, and history remain intact.

## Alternatives Considered

### Add an uploader directly to Omarchy's screenshot script

Rejected. It would modify packaged Omarchy behaviour, bypass the marketplace plugin lifecycle, and couple Omarchy to XerahS-specific policy.

### Reimplement providers in the plugin

Rejected. It would duplicate authentication, secret storage, retry logic, destination settings, history, and provider maintenance already owned by XerahS.

### Watch the clipboard

Rejected. Clipboard content does not reliably identify a screenshot and could cause unrelated or sensitive user data to be uploaded.

### Use `OMARCHY_SCREENSHOT_EDITOR` as the upload hook

Rejected as the primary design. It replaces the editor action, does not represent every screenshot mode, and can produce misleading editor-related notifications. It may be documented as an expert workaround only if its limitations are explicit.

### Automatically replace the Print binding

Rejected. Marketplace plugins must not overwrite user configuration without explicit consent. An optional, reversible binding example can be documented for users who want one-step explicit capture-and-upload.

### Package the plugin inside the XerahS monorepo

Rejected for marketplace publication because the plugin installer expects the manifest and listing files at repository root. The repositories may share CI fixtures or documentation links, but their release boundaries remain separate.

### Grant the XerahS Flatpak broad screenshot-directory access

Deferred. Broad host filesystem permissions would weaken the existing sandbox boundary. A future design should use a narrow portal or explicit file grant before enabling a Flatpak path.

## Risks and Mitigations

| Risk | Mitigation |
|---|---|
| A matching non-Omarchy PNG is uploaded in automatic mode | Keep automatic mode off by default, document the filename heuristic, filter completed new files only, and prefer a future Omarchy event contract. |
| Multi-monitor widgets create duplicate uploads | Own observation and queue processing in one singleton service; widgets are controls only. |
| File and image uploaders are confused | Add explicit `--type image`, image-aware default routing, and category-specific tests. |
| Human CLI output breaks plugin parsing | Require one-object JSON stdout and stable exit codes in quiet mode. |
| Plugin gains access to provider credentials | Keep all provider configuration and secret access behind the XerahS CLI boundary. |
| Automatic retries cause duplicate remote objects | Serialize uploads, deduplicate filesystem events, bound retries, and avoid retrying ambiguous provider success automatically. |
| Screenshot is replaced between observation and upload | Resolve and validate a regular canonical file immediately before invocation; pass it safely as argv. |
| Plugin removal leaves background processes | Tie all processes to the singleton lifecycle and test disable/remove during capture and upload. |
| Native CLI and GUI use different resources or config | Package them together, share XDG paths, and test provider discovery from the installed artifact. |
| Flatpak cannot read host screenshots safely | Report the unsupported configuration and require the native package for version one. |

## Open Questions

1. Should the first plugin release include automatic observation, or ship explicit capture-and-upload first and add opt-in observation after field testing?
2. What stable CLI capability/version response should the plugin use instead of inferring support from the application version?
3. Should ambiguous provider outcomes remain failed with manual retry, or can individual uploader contracts expose idempotency safely?
4. Should a future Omarchy screenshot event be proposed upstream before marketplace publication or as a compatibility improvement afterward?
5. What final public repository name and maintainership model should be used for the plugin?

## Research References

- [Publishing Your Plugin](https://plugins.omarchy.org/publish.html)
- [Developing Plugins](https://plugins.omarchy.org/develop.html)
- [Omarchy repository](https://github.com/omacom/omarchy), reviewed at `b71dcad96e9d0b2962b7d225828a5cb6000ad720`
- XerahS repository, reviewed at `672b258c013e3fa49d669f3f718a888d1c0374a3`

## Definition of Done

XIP0087 is complete when a clean Omarchy installation can install a public, validated plugin; explicitly upload an Omarchy screenshot through the configured XerahS image destination; optionally enable clearly disclosed automatic upload; receive deterministic status without duplicate uploads; and remove the plugin without changing or losing screenshots, XerahS configuration, secrets, or history.
