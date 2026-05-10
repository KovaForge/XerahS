# Flathub Submission Checklist

Flathub submission must be human-led. Do not use an AI tool or agent to open the Flathub pull request, request review, post reviewer replies, or automate the submission process.

| Gate | Status | Evidence / Notes | Human reviewer |
|------|--------|------------------|----------------|
| Named human maintainer responsible | Pending |  |  |
| Manifest reviewed by human | Pending | Local staging manifest: `flatpak/com.getsharex.XerahS.yml`; generated source-build candidate: `dist/flathub/com.getsharex.XerahS.yml` after ownership cleanup and regeneration |  |
| Permissions reviewed by human | Pending | See `docs/linux/flatpak-permissions.md` |  |
| `flatpak-builder-lint manifest` output captured | Passed | 2026-05-10 Fedora 44 ARM64 VM: `flatpak run --filesystem=/home/xerahs/tmp/xerahs-flathub-verify --command=flatpak-builder-lint org.flatpak.Builder manifest /home/xerahs/tmp/xerahs-flathub-verify/com.getsharex.XerahS.yml` exited 0 |  |
| `flatpak-builder-lint repo` output captured | Pending | Repo export not available yet because source build has not completed |  |
| Local Flatpak build tested | Blocked | Source-build manifest generated for `v0.22.236` with 170 npm source entries and 73 NuGet source entries. Build proceeds offline through `npm ci` and .NET restore, then fails because `v0.22.236` does not contain the watch-folder daemon RID narrowing fix now added in source. Exact failure: restore asks for macOS/Windows runtime packs from offline-only sources during `XerahS.WatchFolder.Daemon` publish. |  |
| GNOME Wayland smoke test passed | Pending |  |  |
| KDE Plasma Wayland smoke test passed | Pending |  |  |
| No `$HOME` litter smoke test passed | Pending | See `docs/linux/xdg-storage.md` |  |
| Release tarball/source checksum verified | Pending |  |  |
| PR description written/reviewed by human | Pending |  |  |
| Submission PR will not be opened by AI/agent tooling | Pending |  |  |

## 2026-05-10 Source-Build Dependency Results

- Fedora 44 Workstation ARM64 VM, Freedesktop SDK 25.08.
- Generated source-build candidate from release tag `v0.22.236`.
- npm offline source generation passed using `flatpak-node-generator` against `ShareX.VideoEditor/frontend/package-lock.json`: 170 source/cache entries.
- NuGet offline source generation passed using `flatpak-dotnet-generator.py` against Linux publish projects: 73 source entries.
- Manifest lint passed for the generated source-build candidate.
- `flatpak-builder --force-clean --install-deps-from=flathub ...` no longer fails at `npm ci`; frontend install/build runs from generated cache.
- `dotnet restore` no longer reaches `https://api.nuget.org/v3/index.json` during build commands after generated `NuGet.config` limits package sources to `/run/build/xerahs/nuget-sources` and `/usr/lib/sdk/dotnet10/nuget/packages`.
- Remaining source-build blocker for `v0.22.236`: the tag itself still restores non-Linux runtime packs for `XerahS.WatchFolder.Daemon`. Current source has been updated to narrow nested daemon publish `RuntimeIdentifiers` to the active Linux RID; validate again from a new pre-release tag containing that fix.
- Local paths `/tmp/com.getsharex.XerahS.yml`, `/tmp/xerahs-flathub-source-build`, `dist/`, `dist/flathub/`, and `.flatpak-builder/` contain root-owned artifacts from prior runs. Clean or `chown` them before running the exact runbook commands.

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
