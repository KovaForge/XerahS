#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd -- "$SCRIPT_DIR/../.." && pwd)"
AUR_DIR="$SCRIPT_DIR/aur/xerahs-git"
OUTPUT_DIR="$ROOT/dist/aur"

if ! command -v makepkg >/dev/null 2>&1; then
    echo "Error: makepkg is required to build the Arch package." >&2
    exit 1
fi

if ! command -v bsdtar >/dev/null 2>&1; then
    echo "Error: bsdtar is required to unpack the portable archive during packaging." >&2
    exit 1
fi

if ! command -v node >/dev/null 2>&1; then
    echo "Error: node is required to build XerahS for Arch packaging." >&2
    exit 1
fi

if ! command -v npm >/dev/null 2>&1; then
    echo "Error: npm is required to build XerahS for Arch packaging." >&2
    exit 1
fi

mkdir -p "$OUTPUT_DIR"

export XERAHS_REPO_ROOT="$ROOT"

cd "$AUR_DIR"
rm -f ./*.pkg.tar.zst ./*.pkg.tar.zst.sig ./*.src.tar.gz

makepkg --cleanbuild --force --noconfirm

shopt -s nullglob
packages=(./*.pkg.tar.zst)
shopt -u nullglob

if [ "${#packages[@]}" -eq 0 ]; then
    echo "Error: makepkg completed without producing a package." >&2
    exit 1
fi

for package_path in "${packages[@]}"; do
    cp -f "$package_path" "$OUTPUT_DIR/"
done

echo ""
echo "Built Arch package(s):"
for package_path in "$OUTPUT_DIR"/*.pkg.tar.zst; do
    echo "  $package_path"
done
