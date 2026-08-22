# Distro-repo staging (PPA / COPR / OBS)

Templates for first-party Linux update channels requested in ShareX/XerahS #253.

Stamp:

```bash
.ai/skills/publish-release/scripts/prepare-distro-repo-assets.sh --tag vX.Y.Z --repo owner/name
```

Publish (secrets-gated; see `docs/linux/distro-repos.md`):

```bash
.ai/skills/publish-release/scripts/publish-distro-repos.sh --tag vX.Y.Z --repo owner/name
```

Output lands in `dist/distro-repo/` plus `REPO-PUBLISH.md`.

| Channel | Distro | Input asset | Upload tool |
|---|---|---|---|
| Launchpad PPA | Ubuntu / Debian | `XerahS-*-linux-*.tar.gz` + `debian/` | `debsign` then `dput` after `debuild -S` |
| Fedora COPR | Fedora / EPEL | `xerahs.spec` + GitHub `Source0` | `copr-cli build` |
| openSUSE OBS | Leap / Tumbleweed / SLES | `_service` + `xerahs.obs.spec` | `osc commit` |
| AUR | Arch | existing `build/linux/aur/xerahs-git/` | community `xerahs-git` |

Do not invent a second RPM or DEB format. The GitHub `.deb` / `.rpm` assets remain the one-off installers. These channels exist so `apt` / `dnf` / `zypper` can update after that.
