---
name: update-changelog
description: Rules and workflows for updating docs/CHANGELOG.md with user-facing, consolidated release notes (not commit logs). Includes version grouping, noise filtering, platform/XIP-aware bullets, and GitHub tag-linked headings.
---

## Goal

`docs/CHANGELOG.md` is a **release notes** document for humans, not a git log.

Target style (canonical examples: `v0.22.236`, `v0.23.128`, `v0.23.129`):

- One **user-facing bullet** per component + intent cluster (often 3–8 bullets per category, not 50+).
- Plain language: what changed and why it matters.
- Platform prefixes when helpful: `**macOS — Hotkeys (XIP0078 P4)**:` or `**Linux — Clipboard (XIP0079 P3)**:`.
- Optional one-line release summary for large ranges (e.g. "Broad reliability release aggregating v0.23.27 onward").
- `---` horizontal rule between version sections.
- File opens with a `# Changelog` preamble (intro + semver legend), then newest versions.
- **No commit hashes** in bullets (version heading links to the tag when it exists).

The helper script produces a **draft**. An agent **must** rewrite that draft into release-note prose before publishing.

---

## Automation Script (Recommended)

Scripts:

- PowerShell (primary): `.ai/skills/update-changelog/scripts/update-changelog.ps1`
- Bash wrapper (macOS/Linux): `.ai/skills/update-changelog/scripts/update-changelog.sh`

Preview draft:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .ai/skills/update-changelog/scripts/update-changelog.ps1
```

```bash
./.ai/skills/update-changelog/scripts/update-changelog.sh
```

Apply to `docs/CHANGELOG.md`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .ai/skills/update-changelog/scripts/update-changelog.ps1 -FromTag v0.23.128 -Version 0.23.129 -Apply
```

```bash
./.ai/skills/update-changelog/scripts/update-changelog.sh --from-tag v0.23.128 --version 0.23.129 --apply
```

Flags:

| Flag | Effect |
|------|--------|
| `-FromTag` / `--from-tag` | Lower bound (default: `git describe --tags --abbrev=0`) |
| `-Version` / `--version` | Target version (default: root `Directory.Build.props`) |
| `-Apply` / `--apply` | Upsert section into `docs/CHANGELOG.md` |
| `-OutputPath` / `--output-path` | Write draft to file |
| `-NoConsolidation` | Per-commit lines (debug only; do not publish) |
| `-IncludeHashes` | Append hashes (audit only; do not publish) |

Script behavior (default):

1. **Skips noise commits** (hourly-review tracker/state, clawpatch ingest, bare version bumps, CI release-only commits).
2. **Buckets repetitive docs** (blog-draft series, XIP/IEIP proposal churn, hourly-review records).
3. **Buckets platform waves** (pipe-drain deadlocks, XIP P1–P5 platform work).
4. **Merges same category + component** into one bullet (semicolon-separated themes).
5. **Omits hashes** unless `-IncludeHashes`.
6. **Links version heading** only when `vX.Y.Z` tag exists locally or on `origin`.
7. **Appends `---`** after each generated version block.

---

## Mandatory Agent Rewrite Pass

After running the script (or when editing by hand), **always** compress further:

### 1. Collapse commit-log categories

If a `### Fixes` section still has 10+ `**Core**:` lines, merge into themed bullets:

```markdown
### Fixes
- **MCP server**: History search parsing, blob resource hardening, task identity race, stale-path diagnostics.
- **Linux**: Pipe-drain deadlocks across CLI tools, theme service, clipboard, input, and capture helpers; Oem102 hotkey mapping; Wayland active-window routing.
- **macOS**: Clipboard path whitespace, dock hide for tray startup, upload picker fallback, update prompts.
```

### 2. Roll up sparse patch versions

When several consecutive versions are release-only or trivial, combine:

```markdown
## v0.23.121 / v0.23.120

### Changed
- Release version bumps only; no additional user-facing changes in these ranges.
```

Or fold patch-only work into the next meaningful minor heading with a summary line.

### 3. Use platform + XIP labels for improvement-plan work

```markdown
### Features
- **macOS — App bundle (XIP0078 P1)**: Render `Info.plist` from template with stable bundle identity (`com.xerahs.app`).
- **Linux — Notifications (XIP0079 P2)**: After-upload toasts support action buttons via portal and `notify-send --action` fallback.
```

### 4. Drop or one-line internal-only work

Omit (or fold into Documentation as one line):

- Hourly review tracker / state JSON updates
- Individual blog-draft add/refresh commits
- Clawpatch report ingestion
- "Record X in tracker" commits

### 5. Target density

| Range | Target bullets per category |
|-------|----------------------------|
| Small patch (1–5 user commits) | 1–3 per category |
| Medium release (6–30 commits) | 3–8 per category |
| Large wave (30+ commits) | 5–12 per category; use sub-themes |

Aim for **50–90% fewer lines** than raw `git log` output.

---

## Draft → Final (agent rewrite)

The script draft for `v0.23.121..HEAD` at `0.23.129` might look like:

```markdown
### Features
- **Linux — Notifications (XIP0079 P2)**: notification action buttons via portal and notify-send
- **macOS — Packaging (XIP0078 P2)**: env-gated codesign/notarize/DMG pipeline, ad-hoc signing default in package-mac.sh
...
### Documentation
- **Blog**: Blog drafts (2026 series, add/update)
```

**Publish** after rewriting to release-note prose:

```markdown
## v0.23.129

### Features
- **Linux — Hotkeys (XIP0079 P1)**: Surface global-hotkey delivery state in Settings → Hotkeys with a warning banner when portal bind is degraded.
- **Linux — Notifications (XIP0079 P2)**: After-upload toasts support real action buttons; async `notify-send` fallback; no UI-thread blocking.
- **Linux — Clipboard (XIP0079 P3)**: Probe `wl-copy` / `xclip`; settings warnings; `.rpm` recommends clipboard tools; **Persist clipboard after exit** for Wayland.

### Fixes
- **Linux — Mixed-DPI (XIP0079 P4)**: Cumulative monitor layout for vertically stacked mixed-DPI Wayland displays.

### Documentation
- **Linux (XIP0079 P5)**: Rewrite `developers/linux/INSTALL.md` for Ubuntu/Fedora/Arch; update `KNOWN_ISSUES.md`.

---
```

Actions on every draft:

1. Turn commit subjects into **what changed for users**.
2. Fold internal build fixes into one line (e.g. Linux-only UI partials off macOS builds).
3. Collapse blog/XIP doc churn into one Documentation bullet or omit.
4. Split macOS (XIP0078) and Linux (XIP0079) into separate version sections when they shipped as different tags.

---

### Good — platform improvement release (`v0.23.129`)

```markdown
## v0.23.129

### Features
- **Linux — Hotkeys (XIP0079 P1)**: Surface global-hotkey delivery state in Settings → Hotkeys with a warning banner when portal bind is degraded.
- **Linux — Notifications (XIP0079 P2)**: After-upload toasts support real action buttons; async `notify-send` fallback; no UI-thread blocking.
- **Linux — Clipboard (XIP0079 P3)**: Probe `wl-copy` / `xclip`; settings warnings; `.rpm` recommends clipboard tools; **Persist clipboard after exit** for Wayland.

### Documentation
- **Linux (XIP0079 P5)**: Rewrite `developers/linux/INSTALL.md` for Ubuntu/Fedora/Arch; update `KNOWN_ISSUES.md`.

---
```

### Bad — commit log (never publish)

```markdown
### Fixes
- **Core**: LinuxCliToolRunner pipe-drain deadlock (b23cb6ba)
- **Core**: LinuxThemeService gsettings pipe-fill + timeout-stretching deadlock (74652cf4)
- **Core**: Update hourly review tracker for LinuxInputService xdotool fix (4c307ea0)
- **Core**: Add 2026-07-01 blog draft. (1489a3ae)
```

---

## Version Grouping Strategy

### Current unreleased work

- Lower bound: latest released tag (`git describe --tags --abbrev=0`).
- Upper bound: `HEAD`.
- Heading: root `Directory.Build.props` `<Version>`.
- One heading per target version unless user requests historical reconstruction.

### Historical / cleanup passes

When rebuilding noisy sections (as in v0.23.117 → v0.23.27 rollup):

1. Read commits across the whole range.
2. Group by **user-facing theme**, not by patch version.
3. Keep version headers for releases that matter; merge trivial patches.
4. Preserve `v0.22.236`+ summarized style for older stable releases.

### Version headings

- Untagged: `## v0.23.129`
- Tagged: `## [v0.22.236](https://github.com/ShareX/XerahS/releases/tag/v0.22.236)`

---

## Categories

Use [Keep a Changelog](https://keepachangelog.com/) sections:

- **Features** — new user-visible capability
- **Fixes** — bug fixes and reliability hardening
- **Refactor** — internal-only (omit unless user-facing)
- **Build** — packaging, dependencies, CI
- **Documentation** — user/dev docs, XIP status (not blog-draft spam)
- **Testing** — test infrastructure (usually omit unless major)
- **Performance** — measurable user-visible gains
- **Changed** — fallback; prefer a real category when possible

Map commit prefixes:

| Commit prefix | Category |
|---------------|----------|
| `[Feature]` / `feat` | Features |
| `[Fix]` / `fix` | Fixes |
| `[Docs]` / `docs` | Documentation |
| `[CI]` / `build` / `chore(infra)` | Build |
| `[Refactor]` | Refactor |

---

## Attribution

- External contributors: `(#PR, @username)` on the relevant bullet.
- Do not attribute maintainer merge commits or internal agent sweeps.

---

## File Layout

```markdown
# Changelog

All notable changes to XerahS will be documented in this file.

The format follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html):

- **MAJOR** (x): Breaking changes (0 while unreleased)
- **MINOR** (y): New features and enhancements
- **PATCH** (z): Bug fixes and patches

---

## v0.23.129

### Features
- **Component**: User-facing description.

---

## v0.23.128
...
```

When applying updates, ensure the preamble exists at the top. Insert new version sections **after** the preamble `---` and **before** older versions.

---

## Workflow

1. Resolve range: `-FromTag` + `Directory.Build.props` version.
2. Run script → draft (preview, do not `-Apply` until reviewed).
3. **Rewrite draft** using mandatory agent pass (above).
4. Remove duplicate/orphan verbose blocks if consolidating history.
5. Ensure `---` between versions and preamble at top.
6. Run mojibake normalization (see below).
7. Verify: no hashes in bullets, linked headings only for existing tags, readable density.

### Mojibake cleanup (after any write)

```powershell
$c = [System.IO.File]::ReadAllText('docs/CHANGELOG.md', [System.Text.Encoding]::UTF8)
$c = $c -replace [char]0x00C2 + [char]0x00A7, [char]0x2014
$c = $c -replace [char]0x00C2 + [char]0x00A7, [char]0x00A7
$c = $c -replace "\r?\n", "`n"
$c = $c -replace "`n{3,}", "`n`n"
$c = $c -replace "`n", "`r`n"
[System.IO.File]::WriteAllText('docs/CHANGELOG.md', $c, [System.Text.Encoding]::UTF8)
```

---

## Consolidation Buckets (extend in script)

The script's `Get-ConsolidationBucket` merges repetitive patterns. Extend when new churn appears:

| Pattern | Merged summary |
|---------|----------------|
| `ShareX.ImageEditor` commits | ShareX.ImageEditor submodule updates |
| `2026-.. blog` add/refresh | Blog drafts (2026 series) |
| `XIP/IEIP` proposal docs | XIP/IEIP proposals and related documentation |
| `hourly review` / `tracker` / `clawpatch` | *(skipped — not user-facing)* |
| `pipe-drain` / `stderr` + Linux/macOS service | Platform pipe-drain deadlock hardening |
| `XIP0078` / `XIP0079` + `P\d` | Platform improvement-plan item (keep P number) |
| `multipart` / `S3 multipart` | Multipart upload support |
| `[CI] Release v` only | *(skipped)* |

---

## Related

- Release sequence: `.ai/skills/publish-release/SKILL.md` (step 2: update changelog)
- Maintenance: `.ai/skills/run-maintenance/SKILL.md`
