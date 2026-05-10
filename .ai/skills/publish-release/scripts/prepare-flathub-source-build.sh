#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: prepare-flathub-source-build.sh [options]

Generate a Flathub source-build manifest candidate from a XerahS release tag
and check the source/dependency prerequisites needed before manual Flathub
submission.

Options:
  --tag <vX.Y.Z>          Release tag to use (default: v<Directory.Build.props Version>)
  --repo <owner/name>     GitHub repository (default: resolved from origin)
  --output <path>         Output manifest path (default: dist/flathub/com.getsharex.XerahS.yml)
  --lint                  Run flatpak-builder-lint manifest on the generated manifest
  -h, --help              Show this help

This script does not open or automate a Flathub pull request.
USAGE
}

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Error: required command not found: $1" >&2
    exit 1
  fi
}

resolve_github_repo_from_origin() {
  local remote_url
  remote_url="$(git remote get-url origin 2>/dev/null || true)"
  if [[ -z "$remote_url" ]]; then
    return 1
  fi

  case "$remote_url" in
    https://github.com/*)
      remote_url="${remote_url#https://github.com/}"
      ;;
    http://github.com/*)
      remote_url="${remote_url#http://github.com/}"
      ;;
    git@github.com:*)
      remote_url="${remote_url#git@github.com:}"
      ;;
    ssh://git@github.com/*)
      remote_url="${remote_url#ssh://git@github.com/}"
      ;;
    *)
      return 1
      ;;
  esac

  remote_url="${remote_url%.git}"
  if [[ "$remote_url" =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ ]]; then
    echo "$remote_url"
    return 0
  fi

  return 1
}

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

ensure_local_tag_object() {
  local tag_name="$1"

  if git rev-parse -q --verify "refs/tags/${tag_name}^{commit}" >/dev/null; then
    return 0
  fi

  echo "Fetching release tag metadata for $tag_name..."
  git fetch --depth=1 origin "refs/tags/${tag_name}:refs/tags/${tag_name}"
}

resolve_tree_commit() {
  local tag_name="$1"
  local path="$2"
  local commit

  commit="$(git ls-tree "$tag_name" "$path" | awk '{ print $3 }' | tr -d '[:space:]')"
  if [[ -z "$commit" ]]; then
    echo "Error: failed to resolve submodule commit for $path at $tag_name" >&2
    exit 1
  fi

  echo "$commit"
}

resolve_submodule_url() {
  local name="$1"
  local url

  url="$(git config -f .gitmodules --get "submodule.${name}.url" || true)"
  if [[ -z "$url" ]]; then
    echo "Error: failed to resolve submodule URL for $name from .gitmodules" >&2
    exit 1
  fi

  case "$url" in
    git@github.com:*)
      url="https://github.com/${url#git@github.com:}"
      ;;
  esac
  url="${url%.git}.git"
  echo "$url"
}

escape_sed_replacement() {
  printf '%s' "$1" | sed 's/[&/\]/\\&/g'
}

TAG_NAME=""
GH_TARGET_REPO=""
OUTPUT_PATH="dist/flathub/com.getsharex.XerahS.yml"
RUN_LINT=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tag)
      TAG_NAME="$2"
      shift 2
      ;;
    --repo)
      GH_TARGET_REPO="$2"
      shift 2
      ;;
    --output)
      OUTPUT_PATH="$2"
      shift 2
      ;;
    --lint)
      RUN_LINT=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Error: unknown option '$1'" >&2
      usage >&2
      exit 1
      ;;
  esac
done

require_cmd git
require_cmd awk
require_cmd sed
require_cmd mkdir

repo_root="$(git rev-parse --show-toplevel 2>/dev/null || true)"
if [[ -z "$repo_root" ]]; then
  echo "Error: not inside a git repository." >&2
  exit 1
fi
cd "$repo_root"
repo_root="$(pwd -P)"

if [[ -z "$TAG_NAME" ]]; then
  version="$(resolve_version_from_props Directory.Build.props)"
  if [[ -z "$version" ]]; then
    echo "Error: failed to resolve current version from Directory.Build.props" >&2
    exit 1
  fi
  TAG_NAME="v${version}"
fi

if [[ ! "$TAG_NAME" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Error: --tag must look like vX.Y.Z (received '$TAG_NAME')" >&2
  exit 1
fi

if [[ -z "$GH_TARGET_REPO" ]]; then
  GH_TARGET_REPO="$(resolve_github_repo_from_origin || true)"
fi
if [[ -z "$GH_TARGET_REPO" ]]; then
  GH_TARGET_REPO="${GH_REPO:-}"
fi
if [[ -z "$GH_TARGET_REPO" ]]; then
  echo "Error: could not resolve GitHub repo from origin. Pass --repo owner/name." >&2
  exit 1
fi

ensure_local_tag_object "$TAG_NAME"

main_commit="$(git rev-parse "${TAG_NAME}^{commit}")"
image_editor_commit="$(resolve_tree_commit "$TAG_NAME" ShareX.ImageEditor)"
video_editor_commit="$(resolve_tree_commit "$TAG_NAME" ShareX.VideoEditor)"
image_editor_url="$(resolve_submodule_url ShareX.ImageEditor)"
video_editor_url="$(resolve_submodule_url ShareX.VideoEditor)"
main_url="https://github.com/${GH_TARGET_REPO}.git"

mkdir -p "$(dirname "$OUTPUT_PATH")"

cat > "$OUTPUT_PATH" <<EOF
app-id: com.getsharex.XerahS
runtime: org.freedesktop.Platform
runtime-version: '25.08'
sdk: org.freedesktop.Sdk
command: xerahs
sdk-extensions:
  - org.freedesktop.Sdk.Extension.dotnet10
  - org.freedesktop.Sdk.Extension.node24

finish-args:
  - --socket=x11
  - --share=ipc
  - --device=dri
  - --share=network
  - --talk-name=org.kde.StatusNotifierWatcher

# Generated by .ai/skills/publish-release/scripts/prepare-flathub-source-build.sh.
# Source release: ${TAG_NAME}
# This is a source-build candidate for human review, not an automated Flathub PR.
# Before submission, add generated offline dependency sources for:
#   - NuGet/.NET restore packages
#   - ShareX.VideoEditor/frontend npm packages
modules:
  - name: xerahs
    buildsystem: simple
    build-options:
      env:
        DOTNET_CLI_HOME: /run/build/xerahs/.dotnet
        NPM_CONFIG_CACHE: /run/build/xerahs/.npm
        NPM_CONFIG_AUDIT: 'false'
        NPM_CONFIG_FUND: 'false'
        PATH: /usr/lib/sdk/dotnet10/bin:/usr/lib/sdk/node24/bin:/app/bin:/usr/bin
        XERAHS_PLUGIN_JOBS: '2'
    build-commands:
      - |
        case "\$(uname -m)" in
          x86_64) export XERAHS_ARCHITECTURES=linux-x64 ;;
          aarch64) export XERAHS_ARCHITECTURES=linux-arm64 ;;
          *) echo "Unsupported Flatpak build architecture: \$(uname -m)" >&2; exit 1 ;;
        esac
        ./build/linux/package-linux.sh
        publish_dir="src/desktop/app/XerahS.App/bin/Release/net10.0/\${XERAHS_ARCHITECTURES}/publish"
        test -f "\${publish_dir}/XerahS"
        test -f "\${publish_dir}/xerahs-watchfolder-daemon"
        test -d "\${publish_dir}/frontend/dist"
        cp -rT "\${publish_dir}" /app
      - mkdir -p /app/bin
      - ln -s ../XerahS /app/bin/XerahS
      - ln -s ../XerahS /app/bin/xerahs
      - install -Dm644 src/desktop/app/XerahS.UI/Assets/ShareX.iconset/icon_512x512.png /app/share/icons/hicolor/512x512/apps/com.getsharex.XerahS.png
      - install -Dm644 flatpak/com.getsharex.XerahS.desktop /app/share/applications/com.getsharex.XerahS.desktop
      - install -Dm644 flatpak/com.getsharex.XerahS.metainfo.xml /app/share/metainfo/com.getsharex.XerahS.metainfo.xml
      - chmod 755 /app/XerahS
      - test -f /app/xerahs-watchfolder-daemon && chmod 755 /app/xerahs-watchfolder-daemon || true
    sources:
      - type: git
        url: ${main_url}
        tag: ${TAG_NAME}
        commit: ${main_commit}
        disable-submodules: true
      - type: git
        url: ${image_editor_url}
        commit: ${image_editor_commit}
        dest: ShareX.ImageEditor
      - type: git
        url: ${video_editor_url}
        commit: ${video_editor_commit}
        dest: ShareX.VideoEditor
EOF

echo "Generated Flathub source-build manifest candidate:"
echo "  $OUTPUT_PATH"
echo ""
echo "Resolved source commits:"
echo "  XerahS             $main_commit ($TAG_NAME)"
echo "  ShareX.ImageEditor $image_editor_commit"
echo "  ShareX.VideoEditor $video_editor_commit"

if find . -path './.git' -prune -o -name packages.lock.json -print | grep -q .; then
  echo "NuGet lock files: found"
else
  echo "NuGet lock files: missing"
fi

if [[ -f ShareX.VideoEditor/frontend/package-lock.json ]]; then
  echo "npm lock file: found at ShareX.VideoEditor/frontend/package-lock.json"
else
  echo "npm lock file: missing"
fi

if [[ $RUN_LINT -eq 1 ]]; then
  require_cmd flatpak
  flatpak run --filesystem="$repo_root" --command=flatpak-builder-lint org.flatpak.Builder manifest "$OUTPUT_PATH"
fi

echo ""
echo "Next required work before Flathub submission:"
echo "  1. Generate and add offline NuGet source entries for this tag."
echo "  2. Generate and add offline npm source entries for ShareX.VideoEditor/frontend."
echo "  3. Build with flatpak-builder using the generated manifest with network disabled."
echo "  4. Keep the GitHub release as pre-release until the source-build path passes."
