# Flathub Submission Checklist

Flathub submission must be human-led. Do not use an AI tool or agent to open the Flathub pull request, request review, post reviewer replies, or automate the submission process.

| Gate | Status | Evidence / Notes | Human reviewer |
|------|--------|------------------|----------------|
| Named human maintainer responsible | Pending |  |  |
| Manifest reviewed by human | Pending | Local staging manifest: `flatpak/com.xerahs.XerahS.yml`; verified generated source-build candidate: `/home/xerahs/tmp/xerahs-flathub-verify-022253/com.xerahs.XerahS.yml`. Copy to `dist/flathub/com.xerahs.XerahS.yml` after `dist/` ownership cleanup. |  |
| Permissions reviewed by human | Pending | See `docs/linux/flatpak-permissions.md` |  |
| `flatpak-builder-lint manifest` output captured | Passed | 2026-05-10 Fedora 44 ARM64 VM: `flatpak run --filesystem=/home/xerahs/src/ShareX/XerahS --filesystem=/home/xerahs/tmp/xerahs-flathub-verify-022253 --command=flatpak-builder-lint org.flatpak.Builder manifest /home/xerahs/tmp/xerahs-flathub-verify-022253/com.xerahs.XerahS.yml` exited 0 |  |
| `flatpak-builder-lint repo` output captured | Documented | 2026-05-10 Fedora 44 ARM64 VM: repo lint against `/home/xerahs/tmp/xerahs-flathub-repo-022253` exited 1 only for `appstream-external-screenshot-url` and `appstream-screenshots-not-mirrored-in-ostree`. This is expected for local exports before Flathub mirrors screenshots. |  |
| Local Flatpak build tested | Passed | Source-build manifest generated for `v0.22.253` with 170 npm source entries and 73 NuGet source entries. `flatpak-builder --force-clean --install-deps-from=flathub --repo=/home/xerahs/tmp/xerahs-flathub-repo-022253 --state-dir=/home/xerahs/tmp/xerahs-flatpak-builder-work-022253/state /home/xerahs/tmp/xerahs-flathub-source-build-022253 /home/xerahs/tmp/xerahs-flathub-verify-022253/com.xerahs.XerahS.yml` exported `com.xerahs.XerahS` and `com.xerahs.XerahS.Debug`. |  |
| GNOME Wayland smoke test passed | Pending |  |  |
| KDE Plasma Wayland smoke test passed | Pending |  |  |
| No `$HOME` litter smoke test passed | Pending | See `docs/linux/xdg-storage.md` |  |
| Release tarball/source checksum verified | Pending | Release tag used for source build: `v0.22.253` at `e7ff13ec1bb8f7bc4567c7b7eff4646ae203d524`. Human maintainer should verify source provenance before PR submission. |  |
| PR description written/reviewed by human | Pending |  |  |
| Submission PR will not be opened by AI/agent tooling | Pending |  |  |

## 2026-05-10 Source-Build Dependency Results

- Fedora 44 Workstation ARM64 VM, Freedesktop SDK 25.08.
- Generated source-build candidate from release tag `v0.22.253`.
- npm offline source generation passed using `flatpak-node-generator` against `ShareX.VideoEditor/frontend/package-lock.json`: 170 source/cache entries.
- NuGet offline source generation passed using `flatpak-dotnet-generator.py` against Linux publish projects: 73 source entries.
- Manifest lint passed for the generated source-build candidate.
- `flatpak-builder --force-clean --install-deps-from=flathub ...` completed and exported the app/debug refs to `/home/xerahs/tmp/xerahs-flathub-repo-022253`.
- During Flatpak build commands, log scan found no `registry.npmjs.org` or `api.nuget.org` access after `Building XerahS version 0.22.253`; only .NET welcome/help links were present.
- `npm ci` completed from generated cache, and .NET restore/publish used generated `NuGet.config` sources: `/run/build/xerahs/nuget-sources` and `/usr/lib/sdk/dotnet10/nuget/packages`.
- Repo lint is documented rather than clean because local AppStream screenshots are not mirrored to Flathub's OSTree media location before submission/review.
- Local paths `dist/`, `dist/flathub/`, and some prior Flatpak build outputs contain root-owned artifacts from earlier runs. Clean or `chown` them before copying `/home/xerahs/tmp/xerahs-flathub-verify-022253/com.xerahs.XerahS.yml` into `dist/flathub/`.

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
