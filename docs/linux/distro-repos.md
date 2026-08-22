# First-party Linux update channels (PPA / COPR / OBS)

ShareX/XerahS [#253](https://github.com/ShareX/XerahS/issues/253) asked for
auto-updating distro repositories, not more GitHub one-off artifacts.

| Channel | Distro | Status | Notes |
|---|---|---|---|
| GitHub `.deb` / `.rpm` / `.tar.gz` / AppImage | all | Have | One-off install. No `apt` / `dnf` / `zypper` update. |
| Flatpak bundle | all | Have | Distro-agnostic. Flathub PR is still a separate ops step. |
| Community AUR `xerahs-git` | Arch | Have | Documented in the README. |
| Launchpad PPA | Ubuntu / Debian | Publish path | `publish-distro-repos.sh` + `LAUNCHPAD_*` secrets. |
| Fedora COPR | Fedora / EPEL | Publish path | `publish-distro-repos.sh` + `COPR_CONFIG`. |
| openSUSE OBS | Leap / Tumbleweed / SLES | Publish path | `publish-distro-repos.sh` + `OSC_*` secrets. |

Packages are **binary repacks** of the GitHub release tarball
(`XerahS-<version>-linux-<arch>.tar.gz`). Distro builders do not compile .NET.

## User install (after the project exists)

```bash
# Ubuntu / Debian
sudo add-apt-repository ppa:sharex/xerahs
sudo apt update
sudo apt install xerahs

# Fedora
sudo dnf copr enable sharex/xerahs
sudo dnf install xerahs

# openSUSE Tumbleweed (URL follows the OBS project path)
sudo zypper ar -f https://download.opensuse.org/repositories/home:/ShareX:/XerahS/openSUSE_Tumbleweed/ xerahs
sudo zypper refresh
sudo zypper in xerahs
```

A maintainer must create the empty Launchpad PPA, COPR project, and OBS
project once. Until then the commands above 404.

## One-time maintainer setup

### Launchpad PPA

1. Create PPA `xerahs` under the ShareX Launchpad team (or your team).
2. Upload a GPG key to Launchpad and to `keyserver.ubuntu.com`.
3. GitHub secrets:
   - `LAUNCHPAD_PPA` — `ppa:sharex/xerahs`
   - `LAUNCHPAD_GPG_PRIVATE_KEY` — armored private key
   - `LAUNCHPAD_GPG_PASSPHRASE` — optional
   - `LAUNCHPAD_GPG_KEY_ID` — optional fingerprint
4. Default Ubuntu series: `noble jammy` (`XERAHS_PPA_SERIES`).

### Fedora COPR

1. Create project `sharex/xerahs` at <https://copr.fedorainfracloud.org>.
2. Enable chroots `fedora-latest-x86_64` and `fedora-latest-aarch64`.
3. Copy API config from <https://copr.fedorainfracloud.org/api/>.
4. GitHub secrets:
   - `COPR_CONFIG` — full `~/.config/copr` file contents
   - `COPR_PROJECT` — `sharex/xerahs` (optional; that is the default)

### Open Build Service

1. Create project `home:ShareX:XerahS` (or a team project) at
   <https://build.opensuse.org>.
2. Add repositories for openSUSE Tumbleweed/Leap (and Fedora/Debian if wanted).
3. GitHub secrets:
   - `OSC_USERNAME`
   - `OSC_PASSWORD` (application password / token is fine)
   - `OBS_PROJECT` — `home:ShareX:XerahS` (optional default)

## Publish commands

Stamp only:

```bash
.ai/skills/publish-release/scripts/prepare-distro-repo-assets.sh --tag vX.Y.Z --repo ShareX/XerahS
```

Stamp and upload (skips a backend when its secrets or tools are missing):

```bash
.ai/skills/publish-release/scripts/publish-distro-repos.sh --tag vX.Y.Z --repo ShareX/XerahS
```

From `publish-release` after the GitHub tag workflow has attached linux tarballs:

```bash
./.ai/skills/publish-release/scripts/run-release-sequence.sh --assume-changelog-done --monitor --publish-distro-repos --bump z --yes
```

The tag workflow job `publish-linux-repos` runs the same script. Forks
without secrets stay green.

## Do not stamp unpublished tags

`Source0` and `_service` point at
`https://github.com/<repo>/releases/download/vX.Y.Z/XerahS-X.Y.Z-linux-*.tar.gz`.
A draft or missing GitHub release 404s the build. Publish only after the
tag workflow has attached the linux tarballs.

## What this is not

- Not a second `.deb` / `.rpm` format. GitHub assets stay the one-off
  installers.
- Not a replacement for AppImage, Flatpak, or AUR.

See also `build/linux/repo-staging/README.md` and
`.ai/skills/publish-release/SKILL.md` Optional Step 9.
