#!/usr/bin/env bash
# repair-flatpak-plugins.sh
# Re-publishes all plugin projects into a Flatpak staging directory and
# validates that every plugin DLL is present.
#
# Usage: repair-flatpak-plugins.sh <staging-dir> <arch>
#   staging-dir  Path to the extracted Flatpak staging publish (default: dist/xerahs-flatpak-staging)
#   arch         .NET RID (default: linux-x64)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

STAGING_DIR="${1:-dist/xerahs-flatpak-staging}"
ARCH="${2:-linux-x64}"

# Resolve to absolute path if relative (caller may invoke from repo root)
if [[ "$STAGING_DIR" != /* ]]; then
    STAGING_DIR="$(cd "$(pwd)" && pwd)/$STAGING_DIR"
fi

PLUGINS_STAGING_DIR="$STAGING_DIR/Plugins"

# ---------------------------------------------------------------------------
# publish_single_plugin  (mirrors the version in package-linux.sh)
# ---------------------------------------------------------------------------
dotnet_publish_serial() {
    dotnet publish "$@" \
        --disable-build-servers \
        -p:nodeReuse=false \
        -p:UseSharedCompilation=false \
        -p:BuildInParallel=false \
        -m:1
}

publish_single_plugin() {
    local plugin_project="$1"
    local plugins_dir="$2"
    local publish_dir="$3"
    local arch="$4"

    local plugin_dir plugin_name plugin_id plugin_output id_match assembly_name
    plugin_dir="$(dirname "$plugin_project")"
    plugin_name="$(basename "$plugin_project" .csproj)"
    plugin_id="$plugin_name"
    assembly_name="$plugin_name.dll"

    if [ -f "$plugin_dir/plugin.json" ]; then
        id_match="$(grep -o '"pluginId"[[:space:]]*:[[:space:]]*"[^"]*"' "$plugin_dir/plugin.json" | cut -d'"' -f4 || true)"
        if [ -n "${id_match:-}" ]; then
            plugin_id="$id_match"
        fi
        local assembly_match
        assembly_match="$(grep -o '"assemblyFileName"[[:space:]]*:[[:space:]]*"[^"]*"' "$plugin_dir/plugin.json" | cut -d'"' -f4 || true)"
        if [ -n "${assembly_match:-}" ]; then
            assembly_name="$assembly_match"
        fi
    fi

    echo "  Repairing plugin: $plugin_name ($plugin_id) for $arch"
    plugin_output="$plugins_dir/$plugin_id"

    local attempt
    for attempt in 1 2; do
        rm -rf "$plugin_output"
        mkdir -p "$plugin_output"

        dotnet_publish_serial "$plugin_project" \
            -c Release \
            -r "$arch" \
            -p:OS=Linux \
            -p:RuntimeIdentifiers="$arch" \
            -o "$plugin_output" \
            --no-self-contained \
            -p:PublishSingleFile=false \
            -p:EnableWindowsTargeting=true > /dev/null

        if [ ! -f "$plugin_output/plugin.json" ] && [ -f "$plugin_dir/plugin.json" ]; then
            cp "$plugin_dir/plugin.json" "$plugin_output/plugin.json"
        fi

        # Remove files already present in the main app publish dir.
        local f fname
        for f in "$plugin_output"/*; do
            if [ -f "$f" ]; then
                fname="$(basename "$f")"
                if [ -f "$publish_dir/$fname" ]; then
                    rm "$f"
                fi
            fi
        done

        if [ -f "$plugin_output/$assembly_name" ]; then
            break
        fi

        if [ "$attempt" -eq 1 ]; then
            echo "  Assembly '$assembly_name' missing after first attempt; retrying: $plugin_name" >&2
        fi
    done

    if [ ! -f "$plugin_output/plugin.json" ]; then
        echo "Error: plugin.json missing for plugin '$plugin_id' in $plugin_output" >&2
        return 1
    fi

    if [ ! -f "$plugin_output/$assembly_name" ]; then
        echo "Error: plugin assembly '$assembly_name' missing for plugin '$plugin_id' in $plugin_output" >&2
        return 1
    fi

    echo "  Plugin $plugin_id ready."
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
mapfile -d '' -t PLUGIN_PROJECTS < <(find "$ROOT/src/desktop/plugins" -mindepth 2 -maxdepth 2 -name "*.csproj" -print0 | sort -z)
PLUGIN_COUNT="${#PLUGIN_PROJECTS[@]}"
if [ "$PLUGIN_COUNT" -eq 0 ]; then
    echo "Error: no plugin projects found under $ROOT/src/desktop/plugins" >&2
    exit 1
fi
echo "Repairing $PLUGIN_COUNT plugins into $PLUGINS_STAGING_DIR (arch=$ARCH)..."
mkdir -p "$PLUGINS_STAGING_DIR"

FAILED=""
for plugin_project in "${PLUGIN_PROJECTS[@]}"; do
    if ! publish_single_plugin "$plugin_project" "$PLUGINS_STAGING_DIR" "$STAGING_DIR" "$ARCH"; then
        FAILED="$FAILED $plugin_project"
    fi
done

if [ -n "$FAILED" ]; then
    echo "Error: the following plugins failed to publish:$FAILED" >&2
    exit 1
fi

echo "All $PLUGIN_COUNT plugins validated in staging dir."
