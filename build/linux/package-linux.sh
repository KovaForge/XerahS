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

prepare_video_editor_frontend() {
    local frontend_dir="$ROOT/ShareX.VideoEditor/frontend"
    local npm_ci_args=(ci)

    if [ ! -f "$frontend_dir/package.json" ]; then
        echo "Error: ShareX.VideoEditor frontend package.json not found: $frontend_dir"
        exit 1
    fi

    if [ "${XERAHS_NPM_OFFLINE:-}" = "1" ]; then
        npm_ci_args+=(--offline)
    fi

    echo "Building ShareX.VideoEditor frontend..."
    (
        cd "$frontend_dir"
        npm "${npm_ci_args[@]}"
        npm run build
    )

    if [ ! -d "$frontend_dir/dist" ]; then
        echo "Error: ShareX.VideoEditor frontend dist missing after build: $frontend_dir/dist"
        exit 1
    fi
}

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

prepare_video_editor_frontend
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
        -p:EnableWindowsTargeting=true \
        -p:SkipBundlePlugins=true

    validate_daemon_bundle "$PUBLISH_DIR"

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

    printf '%s\0' "${PLUGIN_PROJECTS[@]}" | xargs -0 -n1 -P "$PLUGIN_JOBS" bash -c '
        publish_single_plugin "$1" "$PLUGINS_DIR" "$PUBLISH_DIR" "$ARCH"
    ' _

    if ! find "$PLUGINS_DIR" -mindepth 2 -maxdepth 2 -name "plugin.json" | grep -q .; then
        echo "Error: No plugin manifests found under $PLUGINS_DIR after publish."
        exit 1
    fi

    echo "Published $PLUGIN_COUNT plugins to startup Plugins folder: $PLUGINS_DIR"
    dotnet build-server shutdown >/dev/null 2>&1 || true

    # 2. Package
    echo "Packaging ($ARCH)..."
    echo "Note: rpmbuild is required to produce RPM packages."
    echo "Note: squashfs-tools is required to produce AppImage packages."
    dotnet run --no-restore --project "$PACKAGING_TOOL" -- "$PUBLISH_DIR" "$OUTPUT_DIR" "$VERSION" "$ARCH"
done

echo ""
echo "Done! All packages in $OUTPUT_DIR"
