#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: prepare-distro-repo-assets.sh [options]

Stamp first-party Linux repo templates (Launchpad PPA, Fedora COPR,
openSUSE OBS) for a XerahS release tag. Writes candidates under
dist/distro-repo/ plus an operator checklist.

This script does NOT publish. It refuses --push / --upload / --dput /
--copr / --osc flags. An operator must create the Launchpad/COPR/OBS
project and run the commands in REPO-PUBLISH.md.

Options:
  --tag <vX.Y.Z>          Release tag (default: v<Directory.Build.props Version>)
  --repo <owner/name>     GitHub repository (default: resolved from origin)
  --output-dir <path>     Output directory (default: dist/distro-repo)
  --download-tarballs     Fetch linux-x64 and linux-arm64 release tarballs
  -h, --help              Show this help

Related: ShareX/XerahS#253. Complements, does not replace, the GitHub
.deb / .rpm / AppImage / Flatpak assets.
USAGE
}

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Error: required command not found: $1" >&2
    exit 1
  fi
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
# shellcheck source=resolve-github-repo.sh
source "$SCRIPT_DIR/resolve-github-repo.sh"

resolve_version_from_props() {
  local version_file="$1"
  awk '
    {
      if ($0 ~ /<Version>[[:space:]]*[0-9]+\.[0-9]+\.[0-9]+[[:space:]]*<\/Version>/) {
        value = $0
        sub(/^.*<Version>[[:space:]]*/, "", value)
        sub(/[[:space:]]*<\/Version>.*$/, "", value)
        print value
        exit
      }
    }
  ' "$version_file" | tr -d '[:space:]'
}

stamp_file() {
  local src="$1"
  local dest="$2"
  local version="$3"
  local repo="$4"
  local tarball_arch="$5"
  local rpm_arch="$6"
  local changelog_date="$7"
  local rfc2822_date="$8"

  mkdir -p "$(dirname "$dest")"
  local content
  content="$(cat "$src")"
  content="${content//@VERSION@/${version}}"
  content="${content//@REPO@/${repo}}"
  content="${content//@TARBALL_ARCH@/${tarball_arch}}"
  content="${content//@RPM_ARCH@/${rpm_arch}}"
  content="${content//@CHANGELOG_DATE@/${changelog_date}}"
  content="${content//@RFC2822_DATE@/${rfc2822_date}}"
  printf '%s' "$content" > "$dest"
  if [[ "$(basename "$dest")" == "rules" ]]; then
    chmod 755 "$dest"
  fi
}

TAG_NAME=""
GH_TARGET_REPO=""
OUTPUT_DIR=""
DOWNLOAD_TARBALLS=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tag)
      [[ $# -ge 2 ]] || { echo "Error: --tag requires a value." >&2; exit 1; }
      TAG_NAME="$2"
      shift 2
      ;;
    --repo)
      [[ $# -ge 2 ]] || { echo "Error: --repo requires owner/name." >&2; exit 1; }
      GH_TARGET_REPO="$2"
      shift 2
      ;;
    --output-dir)
      [[ $# -ge 2 ]] || { echo "Error: --output-dir requires a path." >&2; exit 1; }
      OUTPUT_DIR="$2"
      shift 2
      ;;
    --download-tarballs)
      DOWNLOAD_TARBALLS=1
      shift
      ;;
    --push|--upload|--dput|--copr|--osc|--publish)
      echo "Error: $1 is refused. This script only stamps candidates; it does not publish." >&2
      echo "See dist/distro-repo/REPO-PUBLISH.md after a successful run." >&2
      exit 1
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Error: unknown option: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

repo_root="$(git rev-parse --show-toplevel 2>/dev/null || true)"
if [[ -z "$repo_root" ]]; then
  echo "Error: not inside a git repository." >&2
  exit 1
fi
cd "$repo_root"
repo_root="$(pwd -P)"

if ! GH_TARGET_REPO="$(resolve_github_repo_prefer_origin "$GH_TARGET_REPO")"; then
  echo "Error: could not resolve GitHub repo. Pass --repo owner/name." >&2
  exit 1
fi

version_file="$repo_root/Directory.Build.props"
if [[ -z "$TAG_NAME" ]]; then
  local_version="$(resolve_version_from_props "$version_file")"
  if [[ -z "$local_version" ]]; then
    echo "Error: could not read <Version> from $version_file." >&2
    exit 1
  fi
  TAG_NAME="v${local_version}"
fi

if [[ ! "$TAG_NAME" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Error: tag must look like vX.Y.Z (got '$TAG_NAME')." >&2
  exit 1
fi

VERSION="${TAG_NAME#v}"
OUTPUT_DIR="${OUTPUT_DIR:-$repo_root/dist/distro-repo}"
TEMPLATE_ROOT="$repo_root/build/linux/repo-staging"

if [[ ! -d "$TEMPLATE_ROOT" ]]; then
  echo "Error: template root missing: $TEMPLATE_ROOT" >&2
  exit 1
fi

CHANGELOG_DATE="$(date -u '+%a %b %d %Y')"
if date -u -R >/dev/null 2>&1; then
  RFC2822_DATE="$(date -u -R)"
else
  RFC2822_DATE="$(date -u '+%a, %d %b %Y %H:%M:%S +0000')"
fi

rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR/ppa/debian/source" \
         "$OUTPUT_DIR/copr" \
         "$OUTPUT_DIR/obs" \
         "$OUTPUT_DIR/tarballs"

stamp_file "$TEMPLATE_ROOT/xerahs.spec" \
  "$OUTPUT_DIR/copr/xerahs-linux-x64.spec" \
  "$VERSION" "$GH_TARGET_REPO" "x64" "x86_64" "$CHANGELOG_DATE" "$RFC2822_DATE"
stamp_file "$TEMPLATE_ROOT/xerahs.spec" \
  "$OUTPUT_DIR/copr/xerahs-linux-arm64.spec" \
  "$VERSION" "$GH_TARGET_REPO" "arm64" "aarch64" "$CHANGELOG_DATE" "$RFC2822_DATE"

# OBS uses the same payload spec without ExclusiveArch so Leap/Tumbleweed
# multibuild can cover x86_64 and aarch64 from one package.
cp "$OUTPUT_DIR/copr/xerahs-linux-x64.spec" "$OUTPUT_DIR/obs/xerahs.spec"
if grep -q '^ExclusiveArch:' "$OUTPUT_DIR/obs/xerahs.spec"; then
  sed -i.bak '/^ExclusiveArch:/d' "$OUTPUT_DIR/obs/xerahs.spec"
  rm -f "$OUTPUT_DIR/obs/xerahs.spec.bak"
fi
stamp_file "$TEMPLATE_ROOT/_service" \
  "$OUTPUT_DIR/obs/_service" \
  "$VERSION" "$GH_TARGET_REPO" "linux-x64" "x86_64" "$CHANGELOG_DATE" "$RFC2822_DATE"

stamp_file "$TEMPLATE_ROOT/debian/control" \
  "$OUTPUT_DIR/ppa/debian/control" \
  "$VERSION" "$GH_TARGET_REPO" "linux-x64" "x86_64" "$CHANGELOG_DATE" "$RFC2822_DATE"
stamp_file "$TEMPLATE_ROOT/debian/rules" \
  "$OUTPUT_DIR/ppa/debian/rules" \
  "$VERSION" "$GH_TARGET_REPO" "linux-x64" "x86_64" "$CHANGELOG_DATE" "$RFC2822_DATE"
stamp_file "$TEMPLATE_ROOT/debian/changelog.in" \
  "$OUTPUT_DIR/ppa/debian/changelog" \
  "$VERSION" "$GH_TARGET_REPO" "linux-x64" "x86_64" "$CHANGELOG_DATE" "$RFC2822_DATE"
stamp_file "$TEMPLATE_ROOT/debian/copyright" \
  "$OUTPUT_DIR/ppa/debian/copyright" \
  "$VERSION" "$GH_TARGET_REPO" "linux-x64" "x86_64" "$CHANGELOG_DATE" "$RFC2822_DATE"
stamp_file "$TEMPLATE_ROOT/debian/watch" \
  "$OUTPUT_DIR/ppa/debian/watch" \
  "$VERSION" "$GH_TARGET_REPO" "linux-x64" "x86_64" "$CHANGELOG_DATE" "$RFC2822_DATE"
cp "$TEMPLATE_ROOT/debian/xerahs.desktop" "$OUTPUT_DIR/ppa/debian/xerahs.desktop"
cp "$TEMPLATE_ROOT/debian/source/format" "$OUTPUT_DIR/ppa/debian/source/format"
cp "$TEMPLATE_ROOT/debian/compat" "$OUTPUT_DIR/ppa/debian/compat"

if [[ $DOWNLOAD_TARBALLS -eq 1 ]]; then
  require_cmd curl
  for rid in linux-x64 linux-arm64; do
    name="XerahS-${VERSION}-${rid}.tar.gz"
    url="https://github.com/${GH_TARGET_REPO}/releases/download/${TAG_NAME}/${name}"
    dest="$OUTPUT_DIR/tarballs/${name}"
    echo "Downloading $url"
    if ! curl -fsSL -o "$dest" "$url"; then
      echo "Warning: failed to download $name from $GH_TARGET_REPO $TAG_NAME." >&2
      echo "Copy the GitHub release tarball next to debian/ before debuild." >&2
      rm -f "$dest"
    fi
  done
fi

cat > "$OUTPUT_DIR/REPO-PUBLISH.md" <<EOF
# Distro repo publish checklist (${TAG_NAME} / ${GH_TARGET_REPO})

Generated by \`.ai/skills/publish-release/scripts/prepare-distro-repo-assets.sh\`.
This file is an operator runbook. The prep script never publishes.

## What already ships (do not reinvent)

- GitHub one-off installers: \`.deb\`, \`.rpm\`, \`.tar.gz\`, \`.AppImage\`
- Flatpak bundle + optional Flathub source-build candidate
- Community AUR: \`xerahs-git\`

These channels exist so \`apt\` / \`dnf\` / \`zypper\` can update after that.

## Ubuntu / Debian — Launchpad PPA

Prerequisites: Launchpad account, GPG key, a PPA (suggested name: \`xerahs\`).

1. Copy \`XerahS-${VERSION}-linux-x64.tar.gz\` (and arm64 if building both) next to \`ppa/debian/\`.
2. On Ubuntu:
   \`\`\`bash
   sudo apt install devscripts debhelper dput
   cd ${OUTPUT_DIR}/ppa
   debuild -S -sa
   dput ppa:<launchpad-user>/xerahs ../xerahs_${VERSION}-1_source.changes
   \`\`\`
3. Wait for Launchpad to build amd64 + arm64.
4. Users:
   \`\`\`bash
   sudo add-apt-repository ppa:<launchpad-user>/xerahs
   sudo apt update
   sudo apt install xerahs
   \`\`\`

Do not dput from CI until a human owns the PPA.

## Fedora — COPR

Prerequisites: Fedora account, \`copr-cli\`, a project (suggested name: \`xerahs\`).

1. Specs are already stamped:
   - \`copr/xerahs-linux-x64.spec\` (\`ExclusiveArch: x86_64\`, Source0 = GitHub tarball)
   - \`copr/xerahs-linux-arm64.spec\` (\`ExclusiveArch: aarch64\`)
2. Create the project once:
   \`\`\`bash
   copr-cli create xerahs --chroot fedora-latest-x86_64 --chroot fedora-latest-aarch64
   \`\`\`
3. Build:
   \`\`\`bash
   copr-cli build xerahs ${OUTPUT_DIR}/copr/xerahs-linux-x64.spec
   copr-cli build xerahs ${OUTPUT_DIR}/copr/xerahs-linux-arm64.spec
   \`\`\`
4. Users:
   \`\`\`bash
   sudo dnf copr enable <fedora-user>/xerahs
   sudo dnf install xerahs
   \`\`\`

Do not \`copr-cli build\` from CI until a human owns the project.

## openSUSE / SLES — OBS

Prerequisites: openSUSE account, \`osc\`, a home or org project
(suggested: \`home:<user>:xerahs\` or \`XerahS:release\`).

1. Files:
   - \`obs/xerahs.spec\`
   - \`obs/_service\` (downloads the GitHub linux-x64 tarball)
2. First checkout + commit:
   \`\`\`bash
   osc checkout home:<user>:xerahs
   cp ${OUTPUT_DIR}/obs/xerahs.spec ${OUTPUT_DIR}/obs/_service home:<user>:xerahs/xerahs/
   cd home:<user>:xerahs/xerahs
   osc add xerahs.spec _service
   osc commit -m "XerahS ${VERSION} from ${GH_TARGET_REPO} ${TAG_NAME}"
   \`\`\`
3. Enable Leap 15.6 + Tumbleweed (+ SLE_15_SP6 if you want enterprise).
4. Users:
   \`\`\`bash
   sudo zypper ar -f https://download.opensuse.org/repositories/home:/<user>:/xerahs/openSUSE_Tumbleweed/ xerahs
   sudo zypper refresh
   sudo zypper in xerahs
   \`\`\`

OBS can also emit Ubuntu/Debian/Fedora repos from the same spec. That is
the "cover almost everything at once" path from #253. Do not \`osc commit\`
from CI until a human owns the project.

## What this prep does not do

- Create Launchpad / COPR / OBS accounts or tokens
- Sign packages
- Run \`dput\`, \`copr-cli build\`, or \`osc commit\`
- Replace AppImage / Flatpak / AUR
- Invent a second RPM or DEB format

## Related

- Issue: https://github.com/ShareX/XerahS/issues/253
- Templates: \`build/linux/repo-staging/\`
- Existing one-off packager: \`build/linux/XerahS.Packaging/\`
- AUR: \`build/linux/aur/xerahs-git/\`
EOF

echo "Stamped distro-repo candidates for ${TAG_NAME} (${GH_TARGET_REPO}) in ${OUTPUT_DIR}"
echo "Operator checklist: ${OUTPUT_DIR}/REPO-PUBLISH.md"
echo "No packages were uploaded."
