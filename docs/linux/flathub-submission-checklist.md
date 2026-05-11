# Flathub Submission Checklist

Flathub submission must be human-led. Do not use an AI tool or agent to open the Flathub pull request, request review, post reviewer replies, or automate the submission process.

| Gate | Status | Evidence / Notes | Human reviewer |
|------|--------|------------------|----------------|
| Named human maintainer responsible | Pending | Human maintainer must fill the statement below before opening the Flathub PR. |  |
| Manifest reviewed by human | Pending | Generated source-build candidate copied to `dist/flathub/com.xerahs.XerahS.yml` with dependency sources under `dist/flathub/generated-sources/`. Local staging manifest remains `flatpak/com.xerahs.XerahS.yml`. |  |
| Permissions reviewed by human | Pending | See `docs/linux/flatpak-permissions.md`. |  |
| `flatpak-builder-lint manifest` output captured | Passed | 2026-05-11 Fedora 44 ARM64 GNOME Wayland VM: `flatpak run --filesystem=/home/xerahs/src/ShareX/XerahS --command=flatpak-builder-lint org.flatpak.Builder manifest /home/xerahs/src/ShareX/XerahS/dist/flathub/com.xerahs.XerahS.yml` exited 0. |  |
| `flatpak-builder-lint repo` output captured | Documented | 2026-05-11 Fedora 44 ARM64 VM: repo lint against `/home/xerahs/tmp/xerahs-flathub-repo-022256` exited 1 only for `appstream-external-screenshot-url` and `appstream-screenshots-not-mirrored-in-ostree`. This is expected for local exports before Flathub mirrors screenshots. |  |
| Local Flatpak build tested | Passed | Source-build manifest generated for `v0.22.256` with 170 npm source entries and 73 NuGet source entries. `flatpak-builder --force-clean --install-deps-from=flathub --repo=/home/xerahs/tmp/xerahs-flathub-repo-022256 --state-dir=/home/xerahs/tmp/xerahs-flatpak-builder-work-022256/state /home/xerahs/tmp/xerahs-flathub-source-build-022256 /home/xerahs/src/ShareX/XerahS/dist/flathub/com.xerahs.XerahS.yml` exported `com.xerahs.XerahS` and `com.xerahs.XerahS.Debug`. |  |
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
GNOME Wayland smoke test, KDE Plasma Wayland smoke test, XDG home-litter smoke
test, and source provenance. I will open and manage the Flathub submission PR
manually without AI/agent automation.

Maintainer:
Date:
```
