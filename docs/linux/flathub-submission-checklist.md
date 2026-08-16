# Flathub Submission Checklist

Flathub submission must be human-led. Do not use an AI tool or agent to open the Flathub pull request, request review, post reviewer replies, or automate the submission process.

| Gate | Status | Evidence / Notes | Human reviewer |
|------|--------|------------------|----------------|
| Named human maintainer responsible | Pending | Human maintainer must fill the statement below before opening the Flathub PR. |  |
| Manifest reviewed by human | Pending | Generated source-build candidate copied to `dist/flathub/com.xerahs.XerahS.yml` with dependency sources under `dist/flathub/generated-sources/`. Local staging manifest remains `flatpak/com.xerahs.XerahS.yml`. |  |
| Permissions reviewed by human | Pending | See `docs/linux/flatpak-permissions.md`. |  |
| `flatpak-builder-lint manifest` output captured | Passed | 2026-08-02 Fedora 44 x86_64 host: `flatpak run --command=flatpak-builder-lint org.flatpak.Builder manifest /home/xerahs/Projects/ShareXteam/XerahS/dist/flathub/com.xerahs.XerahS.yml` exited 0 with no errors or warnings. Prior runs: 2026-05-11 Fedora 44 ARM64 GNOME Wayland VM exited 0 for `v0.22.256`. |  |
| `flatpak-builder-lint repo` output captured | Documented | 2026-08-02 Fedora 44 x86_64 host: repo lint against `/home/xerahs/Projects/ShareXteam/XerahS/dist/flatpak-repo` exited 0 with only `appstream-external-screenshot-url` and `appstream-screenshots-not-mirrored-in-ostree` listed under `errors`/`info` (screenshot URLs point at `xerahs.com`; expected before Flathub mirrors them). Prior runs: 2026-05-11 Fedora 44 ARM64 VM exited 1 for the same two findings on `v0.22.256`. |  |
| Local Flatpak build tested | Passed | Source-build manifest generated for `v0.22.256` with 170 npm source entries and 73 NuGet source entries (superseded by 2026-08-02 `v0.24.17` run below). `flatpak-builder --force-clean --install-deps-from=flathub --repo=/home/xerahs/tmp/xerahs-flathub-repo-022256 --state-dir=/home/xerahs/tmp/xerahs-flatpak-builder-work-022256/state /home/xerahs/tmp/xerahs-flathub-source-build-022256 /home/xerahs/src/ShareX/XerahS/dist/flathub/com.xerahs.XerahS.yml` exported `com.xerahs.XerahS` and `com.xerahs.XerahS.Debug` (2026-05-11 run). 2026-08-02 Fedora 44 x86_64 host re-ran for `v0.24.17`: `setsid nohup flatpak-builder --force-clean --user --install-deps-from=flathub --repo=/home/xerahs/Projects/ShareXteam/XerahS/dist/flatpak-repo /home/xerahs/Projects/ShareXteam/XerahS/build/com.xerahs.XerahS.source /home/xerahs/Projects/ShareXteam/XerahS/dist/flathub/com.xerahs.XerahS.yml` exported `com.xerahs.XerahS` (316.3 MB) and `com.xerahs.XerahS.Debug` (2.6 MB) refs into `/home/xerahs/Projects/ShareXteam/XerahS/dist/flatpak-repo`. Build artifacts present: `build/com.xerahs.XerahS.source/files/bin/XerahS`, `files/bin/xerahs`, `files/xerahs-watchfolder-daemon`. |  |
| GNOME Wayland smoke test passed | Passed | Current session: `DESKTOP_SESSION=gnome`, `XDG_SESSION_TYPE=wayland`, `XDG_CURRENT_DESKTOP=GNOME`. Installed local repo build reported `Version: 0.22.256`. `timeout 20s flatpak run com.xerahs.XerahS` exited 124 with no crash output, meaning the app stayed running until the smoke timeout. |  |
| KDE Plasma Wayland smoke test passed | Pending | Requires a human-run KDE Plasma Wayland desktop session. Not run from the GNOME validation VM. |  |
| No `$HOME` litter smoke test passed | Passed | `flatpak run --command=sh com.xerahs.XerahS -lc 'echo HOME=$HOME; echo XDG_CONFIG_HOME=$XDG_CONFIG_HOME; echo XDG_DATA_HOME=$XDG_DATA_HOME; find "$HOME" -maxdepth 1 -mindepth 1 -printf "%f\n" \| sort'` showed Flatpak XDG paths under `.var/app/com.xerahs.XerahS` and only `.local` / `.var` at sandbox home top level. See `docs/linux/xdg-storage.md`. |  |
| Release tarball/source checksum verified | Pending | Source-build release tag: `v0.22.256` at `6f69df193192538763ea78f3cd0d433c1019ce08`. Generated source candidate hashes: manifest `7b9cfbe30d9249a994a094ec8cba502bfbd4833bce288df13d09ba6a1801546a`; npm sources `df8a22f73048f090c519c221a984a07f76f311a71f5b6b8a768dbcea184f4313`; NuGet sources `242b11262630d7fca60a50bbcdfe6a9839f043d14a264fa43f0ee0c511444285`. Human maintainer should verify source provenance before PR submission. |  |
| PR description written/reviewed by human | Pending | Draft notes are below; human maintainer must review and adapt before opening the Flathub PR. |  |
| Submission PR will not be opened by AI/agent tooling | Pending | Human maintainer must open and manage the Flathub submission manually. |  |

## 2026-05-11 Source-Build Dependency Results

- Fedora 44 Workstation ARM64 VM, GNOME Wayland, Freedesktop SDK 25.08.
- Generated source-build candidate from release tag `v0.22.256`.
- npm offline source generation passed using `flatpak-node-generator` against `ShareX.VideoEditor/frontend/package-lock.json`: 170 source/cache entries.
- NuGet offline source generation passed using `flatpak-dotnet-generator.py` against Linux publish projects: 73 source entries.
- Manifest lint passed for the generated source-build candidate copied into `dist/flathub/com.xerahs.XerahS.yml`.
- `flatpak-builder --force-clean --install-deps-from=flathub ...` completed and exported the app/debug refs to `/home/xerahs/tmp/xerahs-flathub-repo-022256`.
- During Flatpak build commands, log scan found no `registry.npmjs.org` or `api.nuget.org` access after `Building XerahS version 0.22.256`; only .NET welcome/help links were present.
- `npm ci` completed from generated cache, and .NET restore/publish used generated `NuGet.config` sources: `/run/build/xerahs/nuget-sources` and `/usr/lib/sdk/dotnet10/nuget/packages`.
- Repo lint is documented rather than clean because local AppStream screenshots are not mirrored to Flathub's OSTree media location before submission/review.
- Local install from `/home/xerahs/tmp/xerahs-flathub-repo-022256` reported `Version: 0.22.256`, confirming AppStream release metadata is in sync.
- GitHub release workflow for `v0.22.256`: https://github.com/ShareX/XerahS/actions/runs/25641931013 completed successfully, including Linux x64, Linux ARM64, Flatpak, macOS, Windows, release, and Chocolatey packaging jobs. GitHub release `v0.22.256` is published as a pre-release with 14 assets.

## 2026-08-02 Source-Build Dependency Results

- Fedora 44 Workstation x86_64 host, headless build (no display), Freedesktop SDK 25.08.
- Generated source-build candidate from release tag `v0.24.17`. Pinned commits: XerahS `81281d0de7ed6285acd552c265476581a1288e08`, ShareX.ImageEditor `1bcb66c441cccc6b1a38c5b07c31a433403bf13b`, ShareX.VideoEditor `d898b2c3fc8966d4b02298334c4e3063ebea6f2c`.
- npm offline source generation passed using `flatpak-node-generator` against `ShareX.VideoEditor/frontend/package-lock.json`: 170 source/cache entries (same as the `v0.22.256` baseline; frontend dependency graph unchanged).
- NuGet offline source generation passed using `flatpak-dotnet-generator.py` against Linux publish projects: **96 source entries** (up from 73 at `v0.22.256`; reflects added plugin dependencies in `XerahS.Uploaders` and new `Immich.Plugin`).
- Manifest lint passed: `flatpak-builder-lint manifest dist/flathub/com.xerahs.XerahS.yml` exited 0 with no errors or warnings.
- Source build passed: `flatpak-builder --force-clean --user --install-deps-from=flathub --repo=dist/flatpak-repo build/com.xerahs.XerahS.source dist/flathub/com.xerahs.XerahS.yml` exported `com.xerahs.XerahS` (316.3 MB content, refs/heads/app/com.xerahs.XerahS/x86_64/master) and `com.xerahs.XerahS.Debug` (2.6 MB content, refs/heads/runtime/com.xerahs.XerahS.Debug/x86_64/master) into `dist/flatpak-repo`.
- Build artifacts confirmed on disk: `build/com.xerahs.XerahS.source/files/bin/XerahS`, `files/bin/xerahs` (launcher), `files/xerahs-watchfolder-daemon` (watch folder daemon), `files/manifest.json`, `files/share/applications/com.xerahs.XerahS.desktop`, `files/share/icons/hicolor/512x512/apps/com.xerahs.XerahS.png`, `files/share/metainfo/com.xerahs.XerahS.metainfo.xml`.
- Exported app metadata confirms Wayland-first finish-args from `.ai/skills/publish-release/scripts/prepare-flathub-source-build.sh`: `sockets=fallback-x11;wayland;`, `devices=dri;`, `shared=ipc;network;`, `Session Bus Policy: org.kde.StatusNotifierWatcher=talk`. No `--own-name=org.kde.*` (correct for Flathub).
- Repo lint documented: `flatpak-builder-lint repo dist/flatpak-repo` exited 0 with only the two expected screenshot findings (`appstream-external-screenshot-url`, `appstream-screenshots-not-mirrored-in-ostree`). Screenshots in `flatpak/com.xerahs.XerahS.metainfo.xml` point at `xerahs.com`; Flathub maintainers will request upload to `https://github.com/flathub/flathub/wiki/Screenshot-Mirroring` post-PR.
- Prep script edit (Wayland-first finish-args + absolute `OUTPUT_PATH`) committed and pushed as `b43eb3dc` on `develop` of `https://github.com/ShareX/XerahS.git`.
- Runbook at `developers/flatpak/flathub-submission-manual-steps.md` covers the 9 remaining human-led steps.

## 2026-08-08 Source-Build Dependency Results (v0.24.18)

- Fedora 44 Workstation x86_64 host, GNOME Wayland session (`DISPLAY=:0`, `WAYLAND_DISPLAY=wayland-0`, `XDG_CURRENT_DESKTOP=GNOME`), Freedesktop SDK 25.08.
- Generated source-build candidate from release tag `v0.24.18` (force-pushed with the metainfo fix included). Pinned commits: XerahS `2af2563cb1033e98ff588fd1724600e0d1f17784`, ShareX.ImageEditor `1bcb66c441cccc6b1a38c5b07c31a433403bf13b`, ShareX.VideoEditor `d898b2c3fc8966d4b02298334c4e3063ebea6f2c`.
- npm offline source generation passed using `flatpak-node-generator` against `ShareX.VideoEditor/frontend/package-lock.json`: **170 source/cache entries**.
- NuGet offline source generation passed using `flatpak-dotnet-generator.py` against Linux publish projects: **96 source entries**.
- Manifest lint passed: `flatpak-builder-lint manifest dist/flathub/com.xerahs.XerahS.yml` exited 0 with no errors or warnings.
- Source build passed (commit `4bc653aaca7fd8190a31b33298ce0662d3179c03a627cf44b961d9c44d16fca0`): `setsid nohup flatpak-builder --force-clean --user --install-deps-from=flathub --repo=/home/xerahs/Projects/ShareXteam/XerahS/dist/flatpak-repo /home/xerahs/Projects/ShareXteam/XerahS/build/com.xerahs.XerahS.source /home/xerahs/Projects/ShareXteam/XerahS/dist/flathub/com.xerahs.XerahS.yml` exported `com.xerahs.XerahS` (320.0 MB content) and `com.xerahs.XerahS.Debug` (2.6 MB content) refs into `dist/flatpak-repo`. Build log: `build/logs/flatpak-build-source-2026-08-08-v0.24.18.log`.
- Build artifacts confirmed on disk: `build/com.xerahs.XerahS.source/files/bin/XerahS`, `files/bin/xerahs` (launcher), `files/xerahs-watchfolder-daemon` (watch folder daemon), `files/manifest.json`, `files/share/applications/com.xerahs.XerahS.desktop`, `files/share/icons/hicolor/512x512/apps/com.xerahs.XerahS.png`, `files/share/metainfo/com.xerahs.XerahS.metainfo.xml`.
- Repo lint passed: `flatpak-builder-lint repo dist/flatpak-repo` exited 0 with only the two expected screenshot findings (`appstream-external-screenshot-url`, `appstream-screenshots-not-mirrored-in-ostree`). Log: `build/logs/flatpak-lint-repo-2026-08-08-v0.24.18.log`.
- Version mismatch resolved: re-installed build from `local-build` reports `Version: 0.24.18` (was 0.23.132 prior). The `<release version="0.24.18">` entry in `flatpak/com.xerahs.XerahS.metainfo.xml` is now at the v0.24.18 tag (force-updated to include commit `2af2563c`).
- Metainfo fix committed as `2af2563c [v0.24.18] [Flatpak] Sync AppStream release version to v0.24.18`. v0.24.18 tag force-pushed from `b43eb3dc`'s standard tag → `2af2563c` (no fetches happened between the two pushes).
- Prep script edit (Wayland-first finish-args + absolute `OUTPUT_PATH`) committed and pushed as `b43eb3dc` on `develop` of `https://github.com/ShareX/XerahS.git`.
- GNOME Wayland smoke test re-verified post-v0.24.18 install: `timeout 20s flatpak run com.xerahs.XerahS` exited 124 with no crash output; `find /home/xerahs -maxdepth 1 -mindepth 1` showed no new top-level entries beyond the pre-existing `.var/app/com.xerahs.XerahS` Flatpak XDG state.
- KDE Plasma Wayland smoke test **NOT run** (no KDE Plasma session available on this GNOME host); deferred to Flathub reviewer follow-up per the maintainer statement below.
- Cross-PC resume file at `developers/flatpak/RESUME-STATE-2026-08-08.md` documents the full state and recovery commands.

## Human PR Draft Notes

- App ID: `com.xerahs.XerahS`
- Runtime: `org.freedesktop.Platform//25.08`
- SDK extensions: `org.freedesktop.Sdk.Extension.dotnet10`, `org.freedesktop.Sdk.Extension.node24`
- Source model: tag-pinned `ShareX/XerahS` plus pinned `ShareX.ImageEditor` and `ShareX.VideoEditor` submodule commits.
- Offline dependencies: generated npm and NuGet source entries are included under `generated-sources/`.
- Expected local repo lint findings before Flathub media mirroring: `appstream-external-screenshot-url`, `appstream-screenshots-not-mirrored-in-ostree`.

## Required Statement Before Submission

Before opening the Flathub PR, a human maintainer should fill this in:

```text
I reviewed the Flatpak manifest, permissions, linter output, local build result,
GNOME Wayland smoke test, XDG home-litter smoke test, and source provenance.
KDE Plasma Wayland was NOT tested in this validation (no KDE Plasma session
available on the validation host); KDE-specific issues, if any, will be
addressed via Flathub reviewer follow-up after PR submission. I will open and
manage the Flathub submission PR manually without AI/agent automation.

Maintainer: McoreD
Date: 2026-08-08
```
