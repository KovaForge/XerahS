# Distro-repo staging (PPA / COPR / OBS)

Templates for first-party Linux update channels requested in ShareX/XerahS #253.

These files do **not** publish. They are stamped by:

```bash
.ai/skills/publish-release/scripts/prepare-distro-repo-assets.sh --tag vX.Y.Z --repo owner/name
```

Output lands in `dist/distro-repo/` plus `REPO-PUBLISH.md` (operator checklist).

| Channel | Distro | Input asset | Operator tool | Live publish |
|---|---|---|---|---|
| Launchpad PPA | Ubuntu / Debian | `XerahS-*-linux-*.tar.gz` + `debian/` | `debsign` then `dput` after `debuild -S` | no (account + GPG required) |
| Fedora COPR | Fedora / EPEL | `xerahs.spec` + GitHub `Source0` | `copr-cli build` | no (project + token required) |
| openSUSE OBS | Leap / Tumbleweed / SLES | `_service` + `xerahs.spec` | `osc commit` | no (OBS project required) |
| AUR | Arch | existing `build/linux/aur/xerahs-git/` | community `xerahs-git` | already documented |

Do not invent a second RPM or DEB format. The GitHub `.deb` / `.rpm` assets remain the one-off installers. These channels exist so `apt` / `dnf` / `zypper` can update after that.
