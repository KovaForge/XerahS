#!/bin/bash
set -euo pipefail

# Configuration
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
ROOT="$SCRIPT_DIR/../.."
PROJECT="$ROOT/src/desktop/app/XerahS.App/XerahS.App.csproj"
PACKAGING_TOOL="$ROOT/build/linux/XerahS.Packaging/XerahS.Packaging.csproj"
OUTPUT_DIR="$ROOT/dist"

if [ ! -d "$OUTPUT_DIR" ]; then
    mkdir -p "$OUTPUT_DIR"
fi

# Get Version from Directory.Build.props
VERSION=$(grep '<Version>' "$ROOT/Directory.Build.props" | sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' | tr -d '[:space:]')
echo "Building XerahS version $VERSION for Linux..."

restore_project_assets_for_os() {
    local project_path="$1"
    local os_value="$2"

    dotnet restore "$project_path" \
        "${DOTNET_RESTORE_SOURCE_ARGS[@]}" \
        -p:OS="$os_value" \
        --disable-build-servers \
        -p:nodeReuse=false \
        -p:UseSharedCompilation=false \
        -p:BuildInParallel=false \
        -m:1
}

restore_project_assets_for_publish() {
    local project_path="$1"
    local os_value="$2"
    local runtime_identifier="$3"

    dotnet restore "$project_path" \
        "${DOTNET_RESTORE_SOURCE_ARGS[@]}" \
        -r "$runtime_identifier" \
        -p:OS="$os_value" \
        -p:RuntimeIdentifier="$runtime_identifier" \
        -p:RuntimeIdentifiers="$runtime_identifier" \
        -p:DefineConstants=LINUX \
        -p:SelfContained=true \
        -p:PublishSingleFile=true \
        -p:EnableWindowsTargeting=true \
        --disable-build-servers \
        -p:nodeReuse=false \
        -p:UseSharedCompilation=false \
        -p:BuildInParallel=false \
        -m:1
}

restore_project_assets_for_runtime() {
    local project_path="$1"
    local os_value="$2"
    local runtime_identifier="$3"

    dotnet restore "$project_path" \
        "${DOTNET_RESTORE_SOURCE_ARGS[@]}" \
        -r "$runtime_identifier" \
        -p:OS="$os_value" \
        -p:RuntimeIdentifier="$runtime_identifier" \
        -p:RuntimeIdentifiers="$runtime_identifier" \
        -p:DefineConstants=LINUX \
        -p:EnableWindowsTargeting=true \
        --disable-build-servers \
        -p:nodeReuse=false \
        -p:UseSharedCompilation=false \
        -p:BuildInParallel=false \
        -m:1
}

restore_scoped_intermediate_assets() {
    local image_editor_project="$ROOT/ShareX.ImageEditor/src/ShareX.ImageEditor/ShareX.ImageEditor.csproj"
    local ui_project="$ROOT/src/desktop/app/XerahS.UI/XerahS.UI.csproj"
    local arch

    if [ ! -f "$image_editor_project" ]; then
        echo "Error: ShareX.ImageEditor project not found: $image_editor_project"
        exit 1
    fi
    if [ ! -f "$ui_project" ]; then
        echo "Error: XerahS.UI project not found: $ui_project"
        exit 1
    fi

    echo "Restoring scoped intermediate assets for Linux packaging..."
    restore_project_assets_for_os "$image_editor_project" "Unix"
    restore_project_assets_for_os "$image_editor_project" "Linux"
    restore_project_assets_for_os "$ui_project" "Linux"
    for arch in "${ARCHITECTURES[@]}"; do
        # Some XerahS project references intentionally remove OS, so on Linux
        # they resolve ShareX.ImageEditor under os-Unix while direct restores
        # resolve under os-Linux. Pre-restore both RID/self-contained buckets.
        restore_project_assets_for_publish "$image_editor_project" "Unix" "$arch"
        restore_project_assets_for_publish "$image_editor_project" "Linux" "$arch"
        restore_project_assets_for_runtime "$image_editor_project" "Unix" "$arch"
        restore_project_assets_for_runtime "$image_editor_project" "Linux" "$arch"
        restore_project_assets_for_runtime "$ui_project" "Linux" "$arch"
    done
}

dotnet_publish_serial() {
    dotnet publish "$@" \
        "${DOTNET_RESTORE_SOURCE_ARGS[@]}" \
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
    plugin_dir=$(dirname "$plugin_project")
    plugin_name=$(basename "$plugin_project" .csproj)
    plugin_id="$plugin_name"
    assembly_name="$plugin_name.dll"

    # Determine plugin ID and assembly file from plugin.json when available.
    if [ -f "$plugin_dir/plugin.json" ]; then
        id_match=$(grep -o '"pluginId"[[:space:]]*:[[:space:]]*"[^"]*"' "$plugin_dir/plugin.json" | cut -d'"' -f4 || true)
        if [ -n "${id_match:-}" ]; then
            plugin_id="$id_match"
        fi

        local assembly_match
        assembly_match=$(grep -o '"assemblyFileName"[[:space:]]*:[[:space:]]*"[^"]*"' "$plugin_dir/plugin.json" | cut -d'"' -f4 || true)
        if [ -n "${assembly_match:-}" ]; then
            assembly_name="$assembly_match"
        fi
    fi

    echo "  Publishing Plugin: $plugin_name ($plugin_id) for $arch"
    plugin_output="$plugins_dir/$plugin_id"

    # Parallel plugin publishes share intermediate outputs of referenced projects and can
    # silently race, leaving a plugin folder without its assembly (observed with xargs -P > 1).
    # Validate the published assembly and retry once before failing the build.
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
            -p:CopyOutputSymbolsToPublishDirectory=false \
            -p:EnableWindowsTargeting=true > /dev/null

        # Ensure plugin.json exists for runtime discovery.
        if [ ! -f "$plugin_output/plugin.json" ] && [ -f "$plugin_dir/plugin.json" ]; then
            cp "$plugin_dir/plugin.json" "$plugin_output/plugin.json"
        fi

        # Cleanup: remove files that already exist in the main app directory.
        local f fname
        for f in "$plugin_output"/*; do
            if [ -f "$f" ]; then
                fname=$(basename "$f")
                if [ -f "$publish_dir/$fname" ]; then
                    rm "$f"
                fi
            fi
        done

        # Make deps.json runtime/native/resources paths match the published layout
        # and fail the build if any asset is neither at its declared path nor in
        # plugin.json dependencies. See rewrite_plugin_deps_json / validate_plugin_dependencies.
        rewrite_plugin_deps_json "$plugin_output"
        validate_plugin_dependencies "$plugin_output" "$plugin_id"

        if [ -f "$plugin_output/$assembly_name" ]; then
            break
        fi

        if [ "$attempt" -eq 1 ]; then
            echo "  Plugin assembly '$assembly_name' missing after publish; retrying: $plugin_name" >&2
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
}

validate_daemon_bundle() {
    local publish_dir="$1"
    local daemon_path="$publish_dir/xerahs-watchfolder-daemon"
    local runtimeconfig_path="$publish_dir/xerahs-watchfolder-daemon.runtimeconfig.json"

    if [ ! -f "$daemon_path" ]; then
        echo "Error: Missing daemon executable in publish output: $daemon_path"
        exit 1
    fi

    if [ ! -f "$runtimeconfig_path" ]; then
        echo "Error: Missing daemon runtimeconfig in publish output: $runtimeconfig_path"
        exit 1
    fi
}

validate_omaxerahs_bundle() {
    local publish_dir="$1"
    local omaxerahs_path="$publish_dir/omaxerahs"
    local runtimeconfig_path="$publish_dir/omaxerahs.runtimeconfig.json"

    if [ ! -f "$omaxerahs_path" ]; then
        echo "Error: Missing omaxerahs executable in publish output: $omaxerahs_path"
        exit 1
    fi

    if [ ! -f "$runtimeconfig_path" ]; then
        echo "Error: Missing omaxerahs runtimeconfig in publish output: $runtimeconfig_path"
        exit 1
    fi
}

# Rewrite every runtime/native/resources asset path inside the plugin's deps.json
# so it points at the actual file on disk after publish. Without this, deps.json
# keeps the NuGet restore layout (e.g. "lib/net8.0/AWSSDK.S3.dll") while the
# publish step flattens the file to the plugin root, so .NET falls back to
# AppContext.BaseDirectory probing. PluginFolderCleaner then sees a file that is
# neither at the declared deps.json path nor in plugin.json dependencies, and
# quarantines it on every startup (observed with AWSSDK on the amazon3s plugin).
rewrite_plugin_deps_json() {
    local plugin_output="$1"
    local deps_path
    local deps_paths=()

    while IFS= read -r -d '' deps_path; do
        deps_paths+=("$deps_path")
    done < <(find "$plugin_output" -maxdepth 1 -name '*.deps.json' -print0 2>/dev/null)

    if [ "${#deps_paths[@]}" -eq 0 ]; then
        return 0
    fi

    local deps_file
    for deps_file in "${deps_paths[@]}"; do
        python3 - "$plugin_output" "$deps_file" <<'PY'
import json
import os
import sys

plugin_dir, deps_path = sys.argv[1], sys.argv[2]

with open(deps_path, "r", encoding="utf-8") as handle:
    deps = json.load(handle)

targets = deps.get("targets", {})
if not isinstance(targets, dict):
    sys.exit(0)

asset_groups = ("runtime", "native", "resources")
rewritten = False


def visit(node):
    global rewritten
    if isinstance(node, dict):
        for key, value in list(node.items()):
            if key in asset_groups and isinstance(value, dict):
                if rewrite_group(value):
                    rewritten = True
            else:
                visit(value)
    elif isinstance(node, list):
        for item in node:
            visit(item)


def rewrite_group(group):
    changed = False
    for declared_path in list(group.keys()):
        if not declared_path or not declared_path.startswith("lib/"):
            continue
        on_disk = os.path.join(plugin_dir, declared_path)
        if os.path.exists(on_disk):
            continue
        basename = os.path.basename(declared_path)
        candidate = os.path.join(plugin_dir, basename)
        if candidate != on_disk and os.path.exists(candidate):
            group[basename] = group.pop(declared_path)
            changed = True
    return changed


visit(targets)

if rewritten:
    with open(deps_path, "w", encoding="utf-8") as handle:
        json.dump(deps, handle, indent=2)
        handle.write("\n")
PY
    done
}

# Fail the build if any deps.json runtime asset is not at its declared path AND
# its basename is not declared in plugin.json dependencies. Without this guard,
# the bundle ships with files that PluginFolderCleaner will quarantine on first
# startup, breaking the plugin until the user manually restores them.
validate_plugin_dependencies() {
    local plugin_output="$1"
    local plugin_id="$2"

    local deps_path manifest_path
    deps_path=$(find "$plugin_output" -maxdepth 1 -name '*.deps.json' -print -quit 2>/dev/null || true)
    manifest_path="$plugin_output/plugin.json"

    if [ -z "$deps_path" ]; then
        return 0
    fi
    if [ ! -f "$manifest_path" ]; then
        return 0
    fi

    python3 - "$plugin_output" "$deps_path" "$manifest_path" "$plugin_id" <<'PY'
import json
import os
import sys

plugin_dir, deps_path, manifest_path, plugin_id = sys.argv[1:5]

with open(deps_path, "r", encoding="utf-8") as handle:
    deps = json.load(handle)
with open(manifest_path, "r", encoding="utf-8") as handle:
    manifest = json.load(handle)

declared = set()
for entry in manifest.get("dependencies") or []:
    if isinstance(entry, str) and entry:
        declared.add(os.path.basename(entry))

targets = deps.get("targets", {})
if not isinstance(targets, dict):
    sys.exit(0)

missing = []
for target_name, libraries in targets.items():
    if not isinstance(libraries, dict):
        continue
    for lib_name, info in libraries.items():
        if not isinstance(info, dict):
            continue
        # Only runtime and resources are plugin-breakers when quarantined: the
        # plugin's AssemblyLoadContext needs to resolve them at load time, and
        # PluginFolderCleaner moves them out on first startup. Native assets are
        # commonly shared between the main app and plugins (Avalonia pulls in
        # libSkiaSharp.so / libHarfBuzzSharp.so transitively even when the main
        # app is self-contained single-file); quarantining those is harmless.
        for group in ("runtime", "resources"):
            entries = info.get(group)
            if not isinstance(entries, dict):
                continue
            for declared_path in entries:
                if not declared_path or not isinstance(declared_path, str):
                    continue
                full = os.path.join(plugin_dir, declared_path)
                if os.path.exists(full):
                    continue
                if os.path.basename(declared_path) in declared:
                    continue
                missing.append((target_name, lib_name, group, declared_path))

if missing:
    sys.stderr.write(
        "Error: plugin '%s' ships runtime files that PluginFolderCleaner will quarantine.\n"
        % plugin_id
    )
    sys.stderr.write("Each missing entry below must either:\n")
    sys.stderr.write("  * exist at the declared deps.json path in the plugin folder, or\n")
    sys.stderr.write("  * be listed under plugin.json 'dependencies' (basename match).\n")
    for target_name, lib_name, group, declared_path in missing:
        sys.stderr.write(
            "  - target=%s lib=%s group=%s path=%s\n"
            % (target_name, lib_name, group, declared_path)
        )
    sys.exit(1)
PY
}

# Define Architectures to Build
# Override with XERAHS_ARCHITECTURES, e.g. "linux-x64" or "linux-arm64".
if [ -n "${XERAHS_ARCHITECTURES:-}" ]; then
    IFS=',' read -r -a ARCHITECTURES <<< "$XERAHS_ARCHITECTURES"
    for i in "${!ARCHITECTURES[@]}"; do
        ARCHITECTURES[$i]="${ARCHITECTURES[$i]//[[:space:]]/}"
    done
else
    ARCHITECTURES=("linux-x64" "linux-arm64")
fi

DOTNET_RESTORE_SOURCE_ARGS=()
if [ -n "${XERAHS_DOTNET_RESTORE_SOURCES:-}" ]; then
    IFS=';' read -r -a DOTNET_RESTORE_SOURCES <<< "$XERAHS_DOTNET_RESTORE_SOURCES"
    for source_path in "${DOTNET_RESTORE_SOURCES[@]}"; do
        if [ -n "$source_path" ]; then
            DOTNET_RESTORE_SOURCE_ARGS+=(--source "$source_path")
        fi
    done
fi

restore_scoped_intermediate_assets
restore_project_assets_for_os "$PACKAGING_TOOL" "Linux"

for ARCH in "${ARCHITECTURES[@]}"; do
    echo ""
    echo "=========================================="
    echo "Building for Architecture: $ARCH"
    echo "=========================================="
    
    # 1. Clean & Publish
    PUBLISH_DIR="$ROOT/src/desktop/app/XerahS.App/bin/Release/net10.0/$ARCH/publish"
    
    if [ -d "$PUBLISH_DIR" ]; then
        rm -rf "$PUBLISH_DIR"
    fi

    echo "Running dotnet publish ($ARCH)..."
    dotnet build-server shutdown >/dev/null 2>&1 || true
    dotnet_publish_serial "$PROJECT" \
        -c Release \
        -r "$ARCH" \
        -p:OS=Linux \
        -p:RuntimeIdentifiers="$ARCH" \
        -p:DefineConstants=LINUX \
        -p:PublishSingleFile=true \
        --self-contained true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:CopyOutputSymbolsToPublishDirectory=false \
        -p:EnableWindowsTargeting=true \
        -p:SkipBundlePlugins=true

    validate_daemon_bundle "$PUBLISH_DIR"
    validate_omaxerahs_bundle "$PUBLISH_DIR"

    # 1.5 Publish Plugins
    echo "Publishing Plugins ($ARCH)..."
    PLUGINS_DIR="$PUBLISH_DIR/Plugins"
    mkdir -p "$PLUGINS_DIR"

    mapfile -d '' -t PLUGIN_PROJECTS < <(find "$ROOT/src/desktop/plugins" -mindepth 2 -maxdepth 2 -name "*.csproj" -print0 | sort -z)
    PLUGIN_COUNT="${#PLUGIN_PROJECTS[@]}"
    if [ "$PLUGIN_COUNT" -eq 0 ]; then
        echo "Error: No plugins were published for $ARCH."
        exit 1
    fi

    # Publish plugin projects one at a time by default. Parallel publishes (xargs -P > 1)
    # share intermediate outputs of referenced projects and can leave a plugin folder
    # without its assembly, even after a retry (observed on linux-arm64 for Bitly).
    # Override with XERAHS_PLUGIN_JOBS only when you accept that race.
    # Note: dotnet build-server shutdown is NOT called between main app publish and plugin
    # publish. Doing so clears the MSBuild server's in-memory asset resolution state for
    # transitive dependencies (e.g. ShareX.ImageEditor's os-Unix/rid-linux-x64 conditional
    # asset paths), causing plugins to fail with silent MSB4181 errors.
    PLUGIN_JOBS="${XERAHS_PLUGIN_JOBS:-1}"
    if ! [[ "$PLUGIN_JOBS" =~ ^[1-9][0-9]*$ ]]; then
        echo "Error: XERAHS_PLUGIN_JOBS must be a positive integer (received '$PLUGIN_JOBS')."
        exit 1
    fi

    export PLUGINS_DIR PUBLISH_DIR ARCH
    export -f dotnet_publish_serial
    export -f publish_single_plugin
    export -f rewrite_plugin_deps_json
    export -f validate_plugin_dependencies

    printf '%s\0' "${PLUGIN_PROJECTS[@]}" | xargs -0 -n1 -P "$PLUGIN_JOBS" bash -c '
        publish_single_plugin "$1" "$PLUGINS_DIR" "$PUBLISH_DIR" "$ARCH"
    ' _

    if ! find "$PLUGINS_DIR" -mindepth 2 -maxdepth 2 -name "plugin.json" | grep -q .; then
        echo "Error: No plugin manifests found under $PLUGINS_DIR after publish."
        exit 1
    fi

    echo "Published $PLUGIN_COUNT plugins to startup Plugins folder: $PLUGINS_DIR"
    find "$PUBLISH_DIR" \( -name '*.pdb' -o -name 'DirectML*.dll' -o -name 'DirectML*.pdb' -o -name 'onnxruntime.dll' -o -name 'onnxruntime_providers_shared.dll' -o -name 'libe_sqlite3.a' \) -delete
    dotnet build-server shutdown >/dev/null 2>&1 || true

    # 2. Package
    echo "Packaging ($ARCH)..."
    echo "Note: rpmbuild is required to produce RPM packages."
    echo "Note: squashfs-tools is required to produce AppImage packages."
    dotnet run --no-restore --project "$PACKAGING_TOOL" -- "$PUBLISH_DIR" "$OUTPUT_DIR" "$VERSION" "$ARCH"
done

echo ""
echo "Done! All packages in $OUTPUT_DIR"
