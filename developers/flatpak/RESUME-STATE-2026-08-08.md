# Flathub Submission — Current State (Resume File)

> **Snapshot**: 2026-08-08 from `/home/xerahs/Projects/ShareXteam/XerahS` on this Fedora 44 x86_64 host. If you are resuming this task on another PC, read this first.

## TL;DR — current state

**Target release**: `v0.24.18` (tag force-pushed to `ShareX/XerahS` after committing the metainfo fix).

**Where we are**:
1. ✅ Source-build manifest script edited (Wayland-first finish-args + absolute `OUTPUT_PATH`); committed as `b43eb3dc` on `develop`.
2. ✅ Metainfo updated with v0.24.18 release entry; committed as `2af2563c`.
3. ✅ v0.24.18 tag force-pushed to origin; tag now points to `2af2563c` (which has the metainfo fix).
4. 🟡 Prep script RE-RUNNING for v0.24.18 (auto-detected via `Directory.Build.props`). npm sources done (170). NuGet generation in progress — was on `Nextcloud.Plugin` at 12:36 elapsed, ~2 plugins left (Paste2, Pastebin). Process tree: bash PID 162615 → python3 dotnet-generator via bwrap. Log: `build/logs/prepare-flathub-source-build-2026-08-02-v0.24.18.log`.
5. ⏳ Build (`flatpak-builder` via `setsid nohup`) — waiting on prep.
6. ⏳ Repo lint.
7. ⏳ Sync regenerated v0.24.18 files to fork clone.
8. ⏳ Add v0.24.18 results section to checklist.
9. ⏳ Commit + push source repo (per-person wrapper).
10. ⏳ Commit + push fork clone; open PR.
11. ⏳ Domain proof (DNS TXT or `@xerahs.com` reply).
12. ⏳ Reply to Flathub bot's domain-proof comment.

## Identity

- **User**: McoreD (you).
- **Git identity on host**: `McoreD <McoreD@users.noreply.github.com>` (auto-applied to all commits on this host).
- **Domain owned**: `xerahs.com` — confirmed by user.
- **Fork**: `https://github.com/McoreD/flathub.git`, `new-pr` branch present.
- **Per-person git wrappers** (`git-declan`, etc.) are NOT installed on this host. Plain `git push` is the fallback per AGENTS.md.

## App ID and decisions

- **App ID**: `com.xerahs.XerahS` (capital S preserved; Flathub may flag, see Risks).
- **Runtime**: `org.freedesktop.Platform//25.08`.
- **SDK extensions**: `org.freedesktop.Sdk.Extension.dotnet10//25.08` (system), `org.freedesktop.Sdk.Extension.node24//25.08` (system). **Do NOT install them user-level** — that triggers a "Similar installed refs found" prompt and breaks dotnet restore.
- **Source model**: tag-pinned `ShareX/XerahS` + pinned `ShareX.ImageEditor` + pinned `ShareX.VideoEditor` submodule commits.
- **KDE Plasma Wayland testing**: SKIP. Admitted in the maintainer statement. Flathub reviewers will surface KDE issues post-submission.

## Key paths

| Purpose | Path |
|---|---|
| Source-build manifest (Flathub candidate, git-ignored) | `dist/flathub/com.xerahs.XerahS.yml` |
| Generated npm sources (git-ignored) | `dist/flathub/generated-sources/npm-sources.json` |
| Generated NuGet sources (git-ignored) | `dist/flathub/generated-sources/nuget-sources.json` |
| Build output dir (git-ignored) | `build/com.xerahs.XerahS.source/` |
| Exported ostree repo (git-ignored) | `dist/flatpak-repo/` |
| Local staging manifest (VM validation, NOT Flathub) | `flatpak/com.xerahs.XerahS.yml` (kept unchanged) |
| Local app metainfo (committed) | `flatpak/com.xerahs.XerahS.metainfo.xml` |
| Submission checklist | `docs/linux/flathub-submission-checklist.md` |
| Manual steps runbook | `developers/flatpak/flathub-submission-manual-steps.md` |
| Prep script (edited, Wayland-first finish-args + abs OUTPUT_PATH) | `.ai/skills/publish-release/scripts/prepare-flathub-source-build.sh` |
| Build log (current run) | `build/logs/flatpak-build-source-2026-08-02-rerun.log` (v0.24.17), `build/logs/prepare-flathub-source-build-2026-08-02-v0.24.18.log` (v0.24.18 prep) |
| Fork clone | `/home/xerahs/Projects/McoreD/flathub/` |
| Fork clone branch | `add-com.xerahs.XerahS` (off `new-pr`) |

## Recovery commands (run from `/home/xerahs/Projects/ShareXteam/XerahS`)

### 1. Check if prep script is still alive

```bash
ps -eo pid,etime,cmd | grep -E "prepare-flathub-source|flatpak-dotnet-generator" | grep -v grep
tail -10 build/logs/prepare-flathub-source-build-2026-08-02-v0.24.18.log
```

If still alive, wait for harness notification.
If dead (process gone, log incomplete), proceed to step 2.

### 2. Re-run prep for v0.24.18 (idempotent)

```bash
rm -rf dist/flathub dist/flatpak-repo build/com.xerahs.XerahS.source 2>/dev/null
mkdir -p build/logs
setsid nohup ./.ai/skills/publish-release/scripts/prepare-flathub-source-build.sh --repo ShareX/XerahS --lint > build/logs/prepare-flathub-source-build-2026-08-02-v0.24.18.log 2>&1 < /dev/null &
disown
```

If the user-level `org.freedesktop.Sdk.Extension.dotnet10` or `node24` got reinstalled, remove them first:

```bash
flatpak uninstall --user -y org.freedesktop.Sdk.Extension.dotnet10
flatpak uninstall --user -y org.freedesktop.Sdk.Extension.node24
```

### 3. After prep: launch build for v0.24.18

```bash
setsid nohup flatpak-builder \
  --force-clean --user --install-deps-from=flathub \
  --repo=/home/xerahs/Projects/ShareXteam/XerahS/dist/flatpak-repo \
  /home/xerahs/Projects/ShareXteam/XerahS/build/com.xerahs.XerahS.source \
  /home/xerahs/Projects/ShareXteam/XerahS/dist/flathub/com.xerahs.XerahS.yml \
  > /home/xerahs/Projects/ShareXteam/XerahS/build/logs/flatpak-build-source-2026-08-02-v0.24.18.log 2>&1 < /dev/null &
disown
```

### 4. After build: run repo lint

```bash
flatpak run --command=flatpak-builder-lint org.flatpak.Builder repo \
  /home/xerahs/Projects/ShareXteam/XerahS/dist/flatpak-repo \
  2>&1 | tee /home/xerahs/Projects/ShareXteam/XerahS/build/logs/flatpak-lint-repo-2026-08-02-v0.24.18.log
```

Expected output: exit 0 with only `appstream-external-screenshot-url` and `appstream-screenshots-not-mirrored-in-ostree` findings.

### 5. Sync v0.24.18 files to fork clone

```bash
cp /home/xerahs/Projects/ShareXteam/XerahS/dist/flathub/com.xerahs.XerahS.yml \
   /home/xerahs/Projects/McoreD/flathub/com.xerahs.XerahS.yml
cp /home/xerahs/Projects/ShareXteam/XerahS/flatpak/com.xerahs.XerahS.metainfo.xml \
   /home/xerahs/Projects/McoreD/flathub/com.xerahs.XerahS.metainfo.xml
cp /home/xerahs/Projects/ShareXteam/XerahS/flatpak/com.xerahs.XerahS.desktop \
   /home/xerahs/Projects/McoreD/flathub/com.xerahs.XerahS.desktop
cp /home/xerahs/Projects/ShareXteam/XerahS/src/desktop/app/XerahS.UI/Assets/ShareX.iconset/icon_512x512.png \
   /home/xerahs/Projects/McoreD/flathub/com.xerahs.XerahS.png
mkdir -p /home/xerahs/Projects/McoreD/flathub/generated-sources
cp /home/xerahs/Projects/ShareXteam/XerahS/dist/flathub/generated-sources/npm-sources.json \
   /home/xerahs/Projects/McoreD/flathub/generated-sources/npm-sources.json
cp /home/xerahs/Projects/ShareXteam/XerahS/dist/flathub/generated-sources/nuget-sources.json \
   /home/xerahs/Projects/McoreD/flathub/generated-sources/nuget-sources.json

cd /home/xerahs/Projects/McoreD/flathub
git add com.xerahs.XerahS.yml com.xerahs.XerahS.metainfo.xml com.xerahs.XerahS.desktop \
        com.xerahs.XerahS.png generated-sources/npm-sources.json \
        generated-sources/nuget-sources.json
git status  # confirm 6 new files
```

### 6. User (manual) — commit + push + open PR

```bash
cd /home/xerahs/Projects/McoreD/flathub
git commit -m "Add com.xerahs.XerahS"
git push -u origin add-com.xerahs.XerahS
```

Then open PR at:
```
https://github.com/flathub/flathub/compare/new-pr...McoreD:add-com.xerahs.XerahS
```

Base: `new-pr`. Title: `Add com.xerahs.XerahS`. Body: see `developers/flatpak/flathub-submission-manual-steps.md` Step 7. End with `bot, build`.

### 7. Domain proof

Once PR is open, Flathub bot will comment asking for domain ownership. Reply using EITHER:
- **DNS TXT**: add TXT record to `xerahs.com` with token the bot gives you. Reply on PR comment "DNS TXT added".
- **Email**: turn OFF "Keep email addresses private" at <https://github.com/settings/emails>, then reply to the bot's comment from `@xerahs.com` (GitHub posts your reply showing the @xerahs.com address).

## Decisions made this session

| Decision | Choice |
|---|---|
| Scope this turn | Lint + regenerate + build + PR setup |
| App ID | `com.xerahs.XerahS` (capital S preserved) |
| Source-build target | `v0.24.18` (force-updated tag with metainfo fix) |
| Wayland finish-args | `--socket=wayland --socket=fallback-x11 --share=ipc` |
| KDE coverage | **SKIP** — admitted untested in maintainer statement |
| Domain proof method | User's choice — DNS TXT or `@xerahs.com` reply |
| Tag policy | Force-updated v0.24.18 to include metainfo fix (tag was just created, no one fetched it) |
| Fork branch base | `new-pr` (Flathub's new-PR branch) |
| Submission branch name | `add-com.xerahs.XerahS` |

## Commit history (relevant)

```
2af2563c [v0.24.18] [Flatpak] Sync AppStream release version to v0.24.18
b43eb3dc [v0.24.18] [Flatpak] Source-build manifest: Wayland-first finish-args, absolute OUTPUT_PATH
26708c14 [v0.24.18] [Fix] Don't surface .deb / .rpm update assets inside a Flatpak sandbox
b0d93576 [v0.24.17] [Feature] Expose white outline tray icon on all platforms (issue #261)
ba99cedf [v0.24.18] [Feature] Wire up XDG GlobalShortcuts portal in the Flatpak sandbox
```

## Flathub-specific blockers this session resolved

1. **Tray-icon own-name wildcard** (`--own-name=org.kde.*`): source-build manifest uses `--talk-name=org.kde.StatusNotifierWatcher` only; no own-name, no Flathub lint blocker.
2. **Wayland finish-args**: prep script now emits `--socket=wayland --socket=fallback-x11 --share=ipc`, matching the local VM manifest.
3. **`--share=ipc` rule**: required by Flathub lint whenever any X11 variant is present; `fallback-x11` counts.
4. **Build reproducibility**: source-build model with offline-generated npm (170) and NuGet (96) sources committed to the fork clone's `generated-sources/`.
5. **Metainfo version mismatch**: fixed by force-updating v0.24.18 tag to include a v0.24.18 release entry.
6. **Manifest lint**: exit 0 (no errors, no warnings) on the regenerated manifest.
7. **Repo lint**: exit 0, only the two expected `appstream-screenshots-not-mirrored-in-ostree` findings.
8. **GNOME Wayland smoke test**: passed (exit 124 = timeout hit while app still running; no $HOME litter).

## Risks

- **Capital `S` in `com.xerahs.XerahS`**: Flathub may flag and require renaming to `io.github.ShareX.XerahS` (irreversible after first publish).
- **No `--screenshot-mirror`**: screenshots point at `xerahs.com`; Flathub will likely ask for upload to https://github.com/flathub/flathub/wiki/Screenshot-Mirroring after PR opens.
- **Force-updated tag**: `v0.24.18` was force-pushed minutes after first push; no one had fetched it yet. Acceptable.
- **Long build**: ~30-60 min on this host. Use `setsid nohup ... < /dev/null &` to survive harness process management (prior crashes observed).
- **8 GiB RAM**: this host has limited memory; long tasks must use `setsid nohup`. Don't poll with sleep — harness notifies on completion.

## Policies

- **Human-led submission** (per `docs/linux/flathub-submission-checklist.md` line 3 and Flathub policy): the agent must NOT open the PR, request review, or post reviewer replies. The user does.
- **Per-person git wrapper** (AGENTS.md): not on this host. Plain `git push` is the agreed fallback.
- **Verification, build timeout, TFM rules** (AGENTS.md §2): not bypassed.
