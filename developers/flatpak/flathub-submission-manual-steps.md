# Flathub Submission — Manual Steps for the Human Maintainer

> **You must perform these steps yourself.** Per [docs/linux/flathub-submission-checklist.md](../../docs/linux/flathub-submission-checklist.md) line 3, Flathub submissions are human-led: AI/agent tooling must not open the PR, request review, post reviewer replies, or automate the submission. This document is your runbook.
>
> Source: [Flathub submission guide](https://docs.flathub.org/docs/for-app-authors/submission) and [Flathub requirements](https://docs.flathub.org/docs/for-app-authors/requirements).

The agent workflow stops at "produced a lint-clean, buildable source-build candidate locally". Everything from this point forward is yours.

---

## Pre-flight — what to verify before you start

Before opening the Flathub PR, confirm each row is `OK`:

| # | Check | Where to look |
|---|-------|---------------|
| 1 | The Flathub-targeted source-build manifest `dist/flathub/com.xerahs.XerahS.yml` is lint-clean locally. | `build/logs/flatpak-lint-source-2026-08-02.log` should show `{"errors": [], "warnings": []}` from the post-prep lint. |
| 2 | A full source build was run from the manifest and produced a working `dist/flatpak-repo/`. | `build/com.xerahs.XerahS.source/files/XerahS` exists; `build/logs/flatpak-build-source-2026-08-02.log` ends with the Flatpak assemble step. |
| 3 | `flatpak-builder-lint repo` on the export passes (or only documents the two known `appstream-screenshots-not-mirrored-in-ostree` / `appstream-external-screenshot-url` findings). | `build/logs/flatpak-lint-repo-2026-08-02.log` |
| 4 | You own `xerahs.com` (or are prepared to switch the app ID to `io.github.ShareX.XerahS`). | DNS records, registrar account. |
| 5 | The Wayland-first finish-args edit in `.ai/skills/publish-release/scripts/prepare-flathub-source-build.sh` is committed and pushed to `develop`. | `git log --oneline -- .ai/skills/publish-release/scripts/prepare-flathub-source-build.sh` shows your commit. |
| 6 | `docs/linux/flathub-submission-checklist.md` "Source-build dependency results" and the local lint/build results rows are filled in. | Open the file. |
| 7 | The maintainer statement at [docs/linux/flathub-submission-checklist.md:45-55](../../docs/linux/flathub-submission-checklist.md#L45-L55) is signed with your name + date. | Open the file. |

If any row is not OK, **do not proceed**. Go back to the agent run and resolve.

---

## Step 1 — Commit and push the prep-script edit (if not already done)

The agent leaves the Wayland-first finish-args change in `.ai/skills/publish-release/scripts/prepare-flathub-source-build.sh` as an unstaged diff so you can review it.

```bash
cd /home/xerahs/Projects/ShareXteam/XerahS
git diff .ai/skills/publish-release/scripts/prepare-flathub-source-build.sh
```

If the diff looks right (replace `--socket=x11` with `--socket=wayland --socket=fallback-x11 --share=ipc`):

```bash
# Use your per-person wrapper per AGENTS.md "Git identity and wrappers (mandatory)".
# Example for Declan:
git-declan add .ai/skills/publish-release/scripts/prepare-flathub-source-build.sh
git-declan commit -m "[v<next-version>] [Flatpak] Source-build manifest: switch to Wayland-first finish-args"
git-declan push
```

> **Version prefix**: per AGENTS.md §4, the prefix must be the next unreleased XerahS app version. Read `Directory.Build.props` `<Version>` and compare it with `git tag --sort=-v:refname | Select-Object -First 1` (or `git tag --sort=-v:refname | head -1` on Linux). If the root version is not ahead of the latest tag, bump it first.

---

## Step 2 — Prepare the files to add to flathub/flathub

The Flathub new-app PR lives in `flathub/flathub`, not in this repo. The files you copy across are:

| Source (in this repo, after the agent run) | Destination filename in flathub/flathub |
|---|---|
| `dist/flathub/com.xerahs.XerahS.yml` | `com.xerahs.XerahS.yml` (top-level) |
| `flatpak/com.xerahs.XerahS.metainfo.xml` | `com.xerahs.XerahS.metainfo.xml` |
| `flatpak/com.xerahs.XerahS.desktop` | `com.xerahs.XerahS.desktop` |
| `src/desktop/app/XerahS.UI/Assets/ShareX.iconset/icon_512x512.png` | `com.xerahs.XerahS.png` |
| `dist/flathub/generated-sources/npm-sources.json` | `generated-sources/npm-sources.json` |
| `dist/flathub/generated-sources/nuget-sources.json` | `generated-sources/nuget-sources.json` |

The `dist/flathub/` paths are git-ignored and produced by the prep script. If you re-run the prep script later, the contents are regenerated — use the latest run.

The 512×512 icon is the canonical one referenced by `flatpak/com.xerahs.XerahS.yml` line 65 and `docs/linux/flatpak-vm-validation.md`; per the lessons learnt at [developers/lessons-learnt/general.md](../../developers/lessons-learnt/general.md) line 299 the build installs the 512×512 PNG, not a 256×256.

**Important checks on the icon file**:
- The Flathub requirements prefer SVG, or 256×256 PNG as a fallback. 512×512 is accepted.
- Verify the icon actually opens and is a valid PNG. The local VM manifest installs it as `com.xerahs.XerahS.png` under `/app/share/icons/hicolor/512x512/apps/`.

---

## Step 3 — Domain ownership proof (Flathub requirement)

Flathub requires proof you control the domain used in the app ID. For `com.xerahs.XerahS`, that's `xerahs.com`.

**Two accepted forms**:

1. **Reply-to email on the domain** — when Flathub's bot opens a discussion thread on your PR, reply from an `@xerahs.com` address. The maintainers will then verify via the email domain.
2. **DNS TXT record** — add a TXT record to `xerahs.com` containing a Flathub-issued token (the bot will tell you what to put in the record when you ask).

**If you do not own `xerahs.com`**, you have two options:

- (Recommended) Acquire `xerahs.com` (the team is already referencing it in the metainfo screenshots URL, so it presumably already is yours).
- Rename the app ID before first publish to `io.github.ShareX.XerahS` (Flathub's GitHub namespace prefix). This is **irreversible** after first publish and requires regenerating the manifest + dist flatpaks + every reference in the repo.

Do this step **before** opening the PR; Flathub will reject the submission if the proof is not in place.

---

## Step 4 — Fork flathub/flathub

In a browser:

1. Go to <https://github.com/flathub/flathub>.
2. Click **Fork** (top right).
3. In the fork dialog, **uncheck** "Copy the `master` branch only". Flathub uses `new-pr` as the base branch for new submissions, and you need it in your fork. The default fork option (master only) will drop `new-pr`.
4. Fork to your personal account (`YOUR_USERNAME`).

---

## Step 5 — Clone and branch

```bash
git clone --branch=new-pr git@github.com:YOUR_USERNAME/flathub.git
cd flathub

# Create a submission branch off new-pr
git checkout -b add-com.xerahs.XerahS new-pr
```

The `--branch=new-pr` clone avoids the lengthy history of `master` and pre-checks out the right base branch.

---

## Step 6 — Copy the files into your submission branch

From the XerahS repo working tree (assuming it is at `/home/xerahs/Projects/ShareXteam/XerahS`):

```bash
# Run these inside the flathub fork clone (cd flathub first).
cp /home/xerahs/Projects/ShareXteam/XerahS/dist/flathub/com.xerahs.XerahS.yml com.xerahs.XerahS.yml
cp /home/xerahs/Projects/ShareXteam/XerahS/flatpak/com.xerahs.XerahS.metainfo.xml com.xerahs.XerahS.metainfo.xml
cp /home/xerahs/Projects/ShareXteam/XerahS/flatpak/com.xerahs.XerahS.desktop com.xerahs.XerahS.desktop
cp /home/xerahs/Projects/ShareXteam/XerahS/src/desktop/app/XerahS.UI/Assets/ShareX.iconset/icon_512x512.png com.xerahs.XerahS.png

mkdir -p generated-sources
cp /home/xerahs/Projects/ShareXteam/XerahS/dist/flathub/generated-sources/npm-sources.json generated-sources/npm-sources.json
cp /home/xerahs/Projects/ShareXteam/XerahS/dist/flathub/generated-sources/nuget-sources.json generated-sources/nuget-sources.json
```

**Tree shape after copy**:

```
flathub/
├── com.xerahs.XerahS.desktop
├── com.xerahs.XerahS.metainfo.xml
├── com.xerahs.XerahS.png
├── com.xerahs.XerahS.yml
└── generated-sources/
    ├── npm-sources.json
    └── nuget-sources.json
```

No other files. No source code. No build artifacts. (Flathub builds from source via its own CI; the manifest + generated-sources are the only inputs.)

---

## Step 7 — Commit, push, open the PR

In the flathub fork clone:

```bash
git add com.xerahs.XerahS.yml \
        com.xerahs.XerahS.metainfo.xml \
        com.xerahs.XerahS.desktop \
        com.xerahs.XerahS.png \
        generated-sources/npm-sources.json \
        generated-sources/nuget-sources.json

git commit -m "Add com.xerahs.XerahS"

git push -u origin add-com.xerahs.XerahS
```

Then in a browser:

1. Go to <https://github.com/flathub/flathub/compare/new-pr...YOUR_USERNAME:add-com.xerahs.XerahS>.
2. Click **Create pull request**.
3. **Base branch**: `new-pr` (not `master`).
4. **Compare branch**: `add-com.xerahs.XerahS`.
5. **Title**: `Add com.xerahs.XerahS`.
6. **Description** (adapt the draft below):

   ```text
   ## Summary
   XerahS is a modern, cross-platform screen capture and file-sharing
   application. It supports full-screen, window, and region screenshots,
   screen recordings, animated GIFs, and integrates with dozens of upload
   destinations.

   - App ID: com.xerahs.XerahS
   - Source: https://github.com/ShareX/XerahS (tag v0.24.17)
   - License: GPL-3.0-or-later
   - Runtime: org.freedesktop.Platform//25.08
   - Manifest build: source-build with offline-generated npm + NuGet
     dependency sources (committed under generated-sources/)

   ## Verification
   - Manifest lints clean with `flatpak run --command=flatpak-builder-lint
     org.flatpak.Builder manifest com.xerahs.XerahS.yml`.
   - Full source build succeeded locally; see
     https://github.com/ShareX/XerahS/blob/develop/docs/linux/flathub-submission-checklist.md
     for the local run record.
   - Permissions reviewed per
     https://github.com/ShareX/XerahS/blob/develop/docs/linux/flatpak-permissions.md.

   bot, build
   ```
7. Submit.

The `bot, build` line at the end of the description comments `bot, build` to trigger the first test build. If you forget it, you can also trigger the build by commenting `bot, build` on the PR after it opens.

---

## Step 8 — Reviewer interaction

- Reply to reviewer comments from the `@xerahs.com` address (or whichever domain matches the app ID) so the bot can verify domain ownership.
- Address any review comments by editing the files in the flathub fork branch and pushing. Do **not** modify the manifest in the upstream XerahS repo unless the same change should land there too.
- If a reviewer asks for a permissions change (likely candidate: the `org.kde.StatusNotifierWatcher` `talk-name`, or the GPU `device=dri`), update `com.xerahs.XerahS.yml` in the flathub fork, commit, push. The source-build manifest regenerator in this repo will need to mirror the same change for future runs.
- Common Flathub review notes to expect:
  - `oars-1.1` content rating is already declared; reviewers may ask for explicit `<content_attribute>` tags if the app is judged to handle user content.
  - Screenshots are hosted at `xerahs.com`; reviewers may ask for them to be hosted on Flathub's media CDN. If so, upload to the Flathub screenshots repo and update the `<screenshot><image>` URL.
  - If the capital `S` in `XerahS` is flagged, the only fix is renaming (irreversible).

---

## Step 9 — After merge

1. Update the [docs/linux/flathub-submission-checklist.md](../../docs/linux/flathub-submission-checklist.md) rows that were `Pending` (Named human maintainer responsible, Manifest reviewed by human, KDE Plasma Wayland smoke test, Release tarball/source checksum verified, PR description written/reviewed by human) to `Done` with the merge PR URL.
2. Tag the next XerahS release as `latest` on GitHub (the team's `run-release-sequence.sh` handles this; you do not need to do it manually unless the auto-detection picks the wrong tag).
3. Drop the `develop`-only `org.kde.*` own-name reference from [docs/linux/flatpak-permissions.md](../../docs/linux/flatpak-permissions.md) (since the source-build manifest no longer carries it).

---

## What to do if a step fails

- **Prep script fails on npm sources**: see the fix at the top of `.ai/skills/publish-release/scripts/prepare-flathub-source-build.sh` for the absolute-path patch. Re-run after fixing.
- **Prep script fails on NuGet sources with "not installed"**: install the missing `org.freedesktop.Sdk.Extension.dotnet10//25.08` and `org.freedesktop.Sdk.Extension.node24//25.08` via `flatpak install flathub ...`.
- **`flatpak-builder` build stalls**: per AGENTS.md §2, stop, clear locks, prefer `-m:1`, kill stale `dotnet` processes, retry.
- **Linter flags `--own-name=org.kde.*`**: that is the local VM manifest, not the source-build candidate. The source-build manifest already drops it. Make sure you are linting the right file.
- **Linter flags `appstream-screenshots-not-mirrored-in-ostree` locally**: expected until Flathub mirrors the screenshots. Ignore for local runs; Flathub CI will pass this once the screenshots are uploaded.
- **App ID rejected for casing**: rename to `io.github.ShareX.XerahS` (irreversible). Update the prep script, regenerate the manifest, update `flatpak/com.xerahs.XerahS.metainfo.xml` `<id>`, and update the local `flatpak/com.xerahs.XerahS.desktop` `Icon=...` and `Exec=...` lines. Then redo this whole manual flow.
