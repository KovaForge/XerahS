# First-party Linux update channels (PPA / COPR / OBS)

ShareX/XerahS [#253](https://github.com/ShareX/XerahS/issues/253) asked for
auto-updating distro repositories, not more GitHub one-off artifacts.

| Channel | Distro | Status | Notes |
|---|---|---|---|
| GitHub `.deb` / `.rpm` / `.tar.gz` / AppImage | all | Have | One-off install. No `apt` / `dnf` / `zypper` update. |
| Flatpak bundle | all | Have | Distro-agnostic. Flathub PR is still a separate ops step. |
| Community AUR `xerahs-git` | Arch | Have | Documented in the README. |
| Launchpad PPA | Ubuntu / Debian | **Prep only** | Templates + stamp script. No live PPA. |
| Fedora COPR | Fedora / EPEL | **Prep only** | Templates + stamp script. No live COPR project. |
| openSUSE OBS | Leap / Tumbleweed / SLES | **Prep only** | Templates + stamp script. No live OBS project. |

## What landed

Templates live in `build/linux/repo-staging/`. After a successful GitHub
release, stamp candidates with:

```bash
.ai/skills/publish-release/scripts/prepare-distro-repo-assets.sh --tag vX.Y.Z --repo KovaForge/XerahS
```

Or as optional Step 9 of `publish-release`:

```bash
./.ai/skills/publish-release/scripts/run-release-sequence.sh --assume-changelog-done --monitor --prepare-distro-repo-source --bump z --yes
```

Output: `dist/distro-repo/` (gitignored) plus `REPO-PUBLISH.md`.

The stamp script **does not publish**. It refuses `--push`, `--upload`,
`--dput`, `--copr`, `--osc`, and `--publish`. A human must own the Launchpad
PPA, Fedora COPR project, and OBS project before any live upload.

## How each channel works

### Launchpad PPA

Wraps the existing GitHub `XerahS-*-linux-*.tar.gz` in a Debian source
package (`debian/control`, `rules`, `changelog`, `copyright`). Operator
runs `debuild -S` then `dput` to a Launchpad PPA they own. Launchpad
rejects unsigned uploads: `debsign` the `.changes` with the same GPG
key attached to the Launchpad account before `dput`.

Build amd64 and arm64 as **separate** `debuild` runs. Each run needs the
matching tarball next to `debian/`. One tree, two tarballs, two source
uploads if you want both arches.

### Fedora COPR

Uses a first-party `xerahs.spec` whose `Source0` is the GitHub release
tarball (same payload as the AUR PKGBUILD, not the internal
`XerahS.Packaging` staging tarball). Two specs: `x86_64` and `aarch64`.
Operator runs `copr-cli build` against a COPR project they own.

`desktop-file-utils` is a `BuildRequires` only. Local `rpmbuild` hosts
need `rpm-build` and `desktop-file-utils` installed.

COPR and OBS fetch `Source0` over HTTPS from GitHub Releases. If the
target repo later requires auth for release assets, those builds 404
until the URL or token is updated. The community AUR `xerahs-git`
PKGBUILD still uses a local `file://` source on purpose; do not "fix"
that to match COPR/OBS.

### openSUSE OBS

Same spec as COPR, minus `ExclusiveArch` so Leap / Tumbleweed multibuild
can cover both arches. `_service` downloads the **linux-x64** GitHub
tarball. An operator who wants a first-class arm64 OBS package must add
a second package or a second `_service` entry. OBS can also emit Ubuntu
and Fedora repos from that one project, which is the "cover almost
everything at once" path from #253.

## Do not stamp unpublished tags

`Source0` and `_service` point at
`https://github.com/<repo>/releases/download/vX.Y.Z/XerahS-X.Y.Z-linux-*.tar.gz`.
A draft or missing GitHub release 404s the build. Stamp only after the
tag workflow has attached the linux tarballs.

## What this is not

- Not a second `.deb` / `.rpm` format. GitHub assets stay the one-off
  installers.
- Not a live `add-apt-repository` / `dnf copr enable` / `zypper ar`
  channel until a human creates those projects.
- Not a replacement for AppImage, Flatpak, or AUR.

See also `build/linux/repo-staging/README.md` and
`.ai/skills/publish-release/SKILL.md` Optional Step 9.
