#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: prepare-distro-repo-assets.sh [options]

Stamp first-party Linux repo templates (Launchpad PPA, Fedora COPR,
openSUSE OBS) for a XerahS release tag. Writes candidates under
dist/distro-repo/ plus an operator checklist.

This script only stamps. Live upload is
.ai/skills/publish-release/scripts/publish-distro-repos.sh
(secrets-gated; skips a backend when credentials are missing).

Options:
  --tag <vX.Y.Z>          Release tag (default: v<Directory.Build.props Version>)
  --repo <owner/name>     GitHub repository (default: resolved from origin)
  --output-dir <path>     Output directory (default: dist/distro-repo)
  --ubuntu-series <name>  debian/changelog series (default: noble)
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
  local ubuntu_series="$9"

  mkdir -p "$(dirname "$dest")"
  sed \
    -e "s|@VERSION@|${version}|g" \
    -e "s|@REPO@|${repo}|g" \
    -e "s|@TARBALL_ARCH@|${tarball_arch}|g" \
    -e "s|@RPM_ARCH@|${rpm_arch}|g" \
    -e "s|@CHANGELOG_DATE@|${changelog_date}|g" \
    -e "s|@RFC2822_DATE@|${rfc2822_date}|g" \
    -e "s|@UBUNTU_SERIES@|${ubuntu_series}|g" \
    "$src" > "$dest"
  local base
  base="$(basename "$dest")"
  if [[ "$base" == "rules" || "$base" == "postinst" || "$base" == "postrm" ]]; then
    chmod 755 "$dest"
  fi
}

TAG_NAME=""
GH_TARGET_REPO=""
OUTPUT_DIR=""
DOWNLOAD_TARBALLS=0
UBUNTU_SERIES="noble"

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
    --ubuntu-series)
      [[ $# -ge 2 ]] || { echo "Error: --ubuntu-series requires a value." >&2; exit 1; }
      UBUNTU_SERIES="$2"
      shift 2
      ;;
    --download-tarballs)
      DOWNLOAD_TARBALLS=1
      shift
      ;;
    --push|--upload|--dput|--copr|--osc|--publish)
      echo "Error: $1 is refused here. Use publish-distro-repos.sh for live upload." >&2
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
PACKAGING_ROOT="$repo_root/build/linux/packaging"

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

stamp() {
  stamp_file "$1" "$2" "$VERSION" "$GH_TARGET_REPO" "$3" "$4" \
    "$CHANGELOG_DATE" "$RFC2822_DATE" "$UBUNTU_SERIES"
}

stamp "$TEMPLATE_ROOT/xerahs.spec" \
  "$OUTPUT_DIR/copr/xerahs-linux-x64.spec" "x64" "x86_64"
stamp "$TEMPLATE_ROOT/xerahs.spec" \
  "$OUTPUT_DIR/copr/xerahs-linux-arm64.spec" "arm64" "aarch64"
stamp "$TEMPLATE_ROOT/xerahs.obs.spec" \
  "$OUTPUT_DIR/obs/xerahs.spec" "x64" "x86_64"
stamp "$TEMPLATE_ROOT/_service" \
  "$OUTPUT_DIR/obs/_service" "linux-x64" "x86_64"

stamp "$TEMPLATE_ROOT/debian/control" \
  "$OUTPUT_DIR/ppa/debian/control" "linux-x64" "x86_64"
stamp "$TEMPLATE_ROOT/debian/rules" \
  "$OUTPUT_DIR/ppa/debian/rules" "linux-x64" "x86_64"
stamp "$TEMPLATE_ROOT/debian/changelog.in" \
  "$OUTPUT_DIR/ppa/debian/changelog" "linux-x64" "x86_64"
stamp "$TEMPLATE_ROOT/debian/copyright" \
  "$OUTPUT_DIR/ppa/debian/copyright" "linux-x64" "x86_64"
stamp "$TEMPLATE_ROOT/debian/watch" \
  "$OUTPUT_DIR/ppa/debian/watch" "linux-x64" "x86_64"
cp "$TEMPLATE_ROOT/debian/xerahs.desktop" "$OUTPUT_DIR/ppa/debian/xerahs.desktop"
cp "$TEMPLATE_ROOT/debian/source/format" "$OUTPUT_DIR/ppa/debian/source/format"
cp "$TEMPLATE_ROOT/debian/compat" "$OUTPUT_DIR/ppa/debian/compat"
cp "$TEMPLATE_ROOT/debian/postinst" "$OUTPUT_DIR/ppa/debian/postinst"
cp "$TEMPLATE_ROOT/debian/postrm" "$OUTPUT_DIR/ppa/debian/postrm"
chmod 755 "$OUTPUT_DIR/ppa/debian/postinst" "$OUTPUT_DIR/ppa/debian/postrm"
cp "$PACKAGING_ROOT/99-xerahs-input.rules" "$OUTPUT_DIR/ppa/debian/99-xerahs-input.rules"
cp "$PACKAGING_ROOT/com.xerahs.input.policy" "$OUTPUT_DIR/ppa/debian/com.xerahs.input.policy"
cp "$PACKAGING_ROOT/99-xerahs-input.rules" "$OUTPUT_DIR/obs/99-xerahs-input.rules"
cp "$PACKAGING_ROOT/com.xerahs.input.policy" "$OUTPUT_DIR/obs/com.xerahs.input.policy"

if [[ $DOWNLOAD_TARBALLS -eq 1 ]]; then
  require_cmd curl
  for rid in linux-x64 linux-arm64; do
    name="XerahS-${VERSION}-${rid}.tar.gz"
    url="https://github.com/${GH_TARGET_REPO}/releases/download/${TAG_NAME}/${name}"
    dest="$OUTPUT_DIR/tarballs/${name}"
    echo "Downloading $url"
    if curl -fsSL -o "$dest" "$url"; then
      cp -f "$dest" "$OUTPUT_DIR/ppa/${name}"
    else
      echo "Warning: failed to download $name from $GH_TARGET_REPO $TAG_NAME." >&2
      echo "Copy the GitHub release tarball next to debian/ before debuild." >&2
      rm -f "$dest"
    fi
  done
fi

cat > "$OUTPUT_DIR/REPO-PUBLISH.md" <<EOF
# Distro repo publish checklist (${TAG_NAME} / ${GH_TARGET_REPO})

Generated by \`.ai/skills/publish-release/scripts/prepare-distro-repo-assets.sh\`.

Live upload (secrets-gated; skips a backend when credentials are missing):

\`\`\`bash
.ai/skills/publish-release/scripts/publish-distro-repos.sh --tag ${TAG_NAME} --repo ${GH_TARGET_REPO}
\`\`\`

Operator setup and secret names: \`docs/linux/distro-repos.md\`.

## Ubuntu / Debian — Launchpad PPA

Staged tree: \`ppa/\`. Users after the PPA exists:

\`\`\`bash
sudo add-apt-repository ppa:sharex/xerahs
sudo apt update
sudo apt install xerahs
\`\`\`

## Fedora — COPR

Staged specs: \`copr/xerahs-linux-x64.spec\`, \`copr/xerahs-linux-arm64.spec\`.
Users after the project exists:

\`\`\`bash
sudo dnf copr enable sharex/xerahs
sudo dnf install xerahs
\`\`\`

## openSUSE / SLES — OBS

Staged: \`obs/xerahs.spec\`, \`obs/_service\`, udev/polkit files.
Users after the project exists (URL follows the OBS project path):

\`\`\`bash
sudo zypper ar -f https://download.opensuse.org/repositories/home:/ShareX:/XerahS/openSUSE_Tumbleweed/ xerahs
sudo zypper refresh
sudo zypper in xerahs
\`\`\`

## Related

- Issue: https://github.com/ShareX/XerahS/issues/253
- Templates: \`build/linux/repo-staging/\`
- Existing one-off packager: \`build/linux/XerahS.Packaging/\`
- AUR: \`build/linux/aur/xerahs-git/\`
EOF

echo "Stamped distro-repo candidates for ${TAG_NAME} (${GH_TARGET_REPO}) in ${OUTPUT_DIR}"
echo "Operator checklist: ${OUTPUT_DIR}/REPO-PUBLISH.md"
