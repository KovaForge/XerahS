#!/bin/bash
# Builds the vendored OmaSnap (native/omasnap) and stages it for the Linux x64/arm64
# tarball and the AUR package (XIP0088):
#
#   <stage-dir>/omasnap/omasnap
#   <stage-dir>/omasnap/licenses/{LICENSE-MIT.txt,Neucha-OFL.txt,JetBrainsMono-OFL.txt,Inter-OFL.txt,Lucide-ISC.txt}
#
# OmaSnap is optional. When its toolchain (CMake, Ninja, Qt 6 with LayerShellQt, Wayland
# protocols, libdeflate) is missing this prints a warning and exits 0 without staging
# anything, so XerahS still packages; at runtime the capability probe then reports OmaSnap
# absent and XerahS uses its existing capture chain. Pass --strict to fail instead (the Arch
# CI job does, so a broken OmaSnap build is noticed).
#
# Usage: build/linux/build-omasnap.sh <stage-dir> [--strict]
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SOURCE_DIR="$ROOT/native/omasnap"

STAGE_DIR=""
STRICT=0
for arg in "$@"; do
  case "$arg" in
    --strict) STRICT=1 ;;
    *) STAGE_DIR="$arg" ;;
  esac
done

if [[ -z $STAGE_DIR ]]; then
  echo "Usage: $0 <stage-dir> [--strict]" >&2
  exit 2
fi

skip() {
  if (( STRICT )); then
    echo "Error: OmaSnap build failed: $1" >&2
    exit 1
  fi
  echo "Warning: skipping OmaSnap ($1). XerahS will package without it and fall back to its existing capture chain." >&2
  exit 0
}

if [[ ! -f "$SOURCE_DIR/CMakeLists.txt" ]]; then
  skip "native/omasnap is not checked out; run git submodule update --init native/omasnap"
fi

for tool in cmake ninja pkg-config; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    skip "$tool is not installed"
  fi
done

# Qt 6.8+ and LayerShellQt are found by CMake itself (configure failure below skips).
for module in wayland-client wayland-protocols libdeflate; do
  if ! pkg-config --exists "$module" 2>/dev/null; then
    skip "pkg-config module $module is missing"
  fi
done

BUILD_DIR="$(mktemp -d "${TMPDIR:-/tmp}/xerahs-omasnap-build.XXXXXX")"
INSTALL_DIR="$BUILD_DIR/install"
trap 'rm -rf "$BUILD_DIR"' EXIT

echo "Building OmaSnap from $SOURCE_DIR..."
if ! cmake -S "$SOURCE_DIR" -B "$BUILD_DIR/build" -G Ninja \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_INSTALL_PREFIX="$INSTALL_DIR" \
    -DBUILD_TESTING=OFF; then
  skip "CMake configure failed (Qt 6 too old or a dependency missing)"
fi

if ! cmake --build "$BUILD_DIR/build" --target omasnap; then
  skip "compilation failed"
fi

if ! cmake --install "$BUILD_DIR/build" >/dev/null; then
  skip "install step failed"
fi

BINARY="$INSTALL_DIR/bin/omasnap"
if [[ ! -x $BINARY ]]; then
  skip "the build produced no omasnap binary"
fi

# A binary that cannot report host mode is useless to XerahS.
# --host-capabilities needs no display; it exits 1 on a build host without Hyprland, which is fine.
if ! { "$BINARY" --host-capabilities 2>/dev/null || true; } | grep -q '"hostMode"'; then
  skip "the built omasnap has no host mode (needs omasnap 1.22.0 or newer)"
fi

DEST="$STAGE_DIR/omasnap"
rm -rf "$DEST"
mkdir -p "$DEST/licenses"
install -m 755 "$BINARY" "$DEST/omasnap"
install -m 644 "$SOURCE_DIR/LICENSE" "$DEST/licenses/LICENSE-MIT.txt"
for license in "$INSTALL_DIR"/share/licenses/omasnap/*; do
  install -m 644 "$license" "$DEST/licenses/$(basename "$license")"
done

for required in LICENSE-MIT.txt Neucha-OFL.txt JetBrainsMono-OFL.txt Inter-OFL.txt Lucide-ISC.txt; do
  if [[ ! -f "$DEST/licenses/$required" ]]; then
    echo "Error: OmaSnap license notice $required was not staged." >&2
    exit 1
  fi
done

echo "Staged OmaSnap in $DEST"
