#!/usr/bin/env bash
# Standalone tests for the plugin-publish fixup helpers in build/linux/package-linux.sh
# (rewrite_plugin_deps_json + validate_plugin_dependencies).
#
# Run from anywhere. Exits 0 if every case passes, non-zero otherwise.
set -uo pipefail

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
PACKAGE_SCRIPT="$SCRIPT_DIR/../../build/linux/package-linux.sh"

if [ ! -f "$PACKAGE_SCRIPT" ]; then
    echo "FAIL: cannot find package-linux.sh at $PACKAGE_SCRIPT" >&2
    exit 1
fi

# shellcheck disable=SC1090
source <(awk '/^(rewrite_plugin_deps_json|validate_plugin_dependencies)\s*\(\s*\)/,/^}/' "$PACKAGE_SCRIPT")

if ! declare -F rewrite_plugin_deps_json >/dev/null; then
    echo "FAIL: rewrite_plugin_deps_json not loaded from package-linux.sh" >&2
    exit 1
fi
if ! declare -F validate_plugin_dependencies >/dev/null; then
    echo "FAIL: validate_plugin_dependencies not loaded from package-linux.sh" >&2
    exit 1
fi

TMP_ROOT="$(mktemp -d -t xerahs-plugin-fixup-tests.XXXXXX)"
trap 'rm -rf "$TMP_ROOT"' EXIT

PASS=0
FAIL=0

pass() { echo "ok - $*"; PASS=$((PASS + 1)); }
fail() { echo "not ok - $*"; FAIL=$((FAIL + 1)); }

write_deps_json() {
    local deps_path="$1"
    local payload="$2"
    printf '%s' "$payload" > "$deps_path"
}

write_manifest() {
    local manifest_path="$1"
    local payload="$2"
    printf '%s' "$payload" > "$manifest_path"
}

setup_plugin() {
    local name="$1"
    local plugin_dir="$TMP_ROOT/$name"
    mkdir -p "$plugin_dir"
    printf 'assembly-stub' > "$plugin_dir/$name.dll"
    printf '' > "$plugin_dir/${name}.deps.json"
    printf '' > "$plugin_dir/plugin.json"
    echo "$plugin_dir"
}

# --- rewrite_plugin_deps_json ---

test_rewrite_moves_flattened_runtime_path() {
    local plugin_dir
    plugin_dir=$(setup_plugin "amazon3s")
    cp "$plugin_dir/amazon3s.deps.json" "$plugin_dir/.deps.bak"

    write_manifest "$plugin_dir/plugin.json" '{"pluginId":"amazons3","name":"Amazon S3 Uploader","version":"1.0.0","author":"Tests","description":"Test","apiVersion":"1.0","entryPoint":"X","assemblyFileName":"amazon3s.dll","supportedCategories":["Image"],"dependencies":["AWSSDK.Core.dll","AWSSDK.S3.dll"]}'

    # File lives at the plugin root (publish flattens), but deps.json still has
    # the NuGet restore layout.
    printf 'stub' > "$plugin_dir/AWSSDK.S3.dll"
    printf 'stub' > "$plugin_dir/AWSSDK.Core.dll"

    cat > "$plugin_dir/amazon3s.deps.json" <<'JSON'
{
  "targets": {
    ".NETCoreApp,Version=v10.0": {
      "AWSSDK.S3/4.0.102.4": {
        "runtime": {
          "lib/net8.0/AWSSDK.S3.dll": {}
        }
      },
      "AWSSDK.Core/4.0.102.1": {
        "runtime": {
          "lib/net8.0/AWSSDK.Core.dll": {}
        }
      }
    }
  }
}
JSON

    rewrite_plugin_deps_json "$plugin_dir"

    if grep -q '"AWSSDK.S3.dll"' "$plugin_dir/amazon3s.deps.json" \
        && ! grep -q '"lib/net8.0/AWSSDK.S3.dll"' "$plugin_dir/amazon3s.deps.json" \
        && grep -q '"AWSSDK.Core.dll"' "$plugin_dir/amazon3s.deps.json" \
        && ! grep -q '"lib/net8.0/AWSSDK.Core.dll"' "$plugin_dir/amazon3s.deps.json"; then
        pass "rewrite moves lib/net8.0 runtime paths to flattened root"
    else
        fail "rewrite should move lib/net8.0 runtime paths to flattened root"
        diff "$plugin_dir/.deps.bak" "$plugin_dir/amazon3s.deps.json" || true
    fi
}

test_rewrite_keeps_path_when_file_exists_at_declared_location() {
    local plugin_dir
    plugin_dir=$(setup_plugin "native")
    mkdir -p "$plugin_dir/lib/net10.0"
    printf 'stub' > "$plugin_dir/lib/net10.0/SkiaSharp.dll"

    cat > "$plugin_dir/native.deps.json" <<'JSON'
{
  "targets": {
    ".NETCoreApp,Version=v10.0": {
      "SkiaSharp/2.0.0": {
        "native": {
          "lib/net10.0/SkiaSharp.dll": {}
        }
      }
    }
  }
}
JSON

    rewrite_plugin_deps_json "$plugin_dir"

    if grep -q '"lib/net10.0/SkiaSharp.dll"' "$plugin_dir/native.deps.json" \
        && ! grep -q '"SkiaSharp.dll"' "$plugin_dir/native.deps.json"; then
        pass "rewrite leaves canonical lib path alone when file is present"
    else
        fail "rewrite should leave canonical lib path alone when file is present"
    fi
}

test_rewrite_leaves_unknown_paths_alone() {
    local plugin_dir
    plugin_dir=$(setup_plugin "missing")

    cat > "$plugin_dir/missing.deps.json" <<'JSON'
{
  "targets": {
    ".NETCoreApp,Version=v10.0": {
      "Example/1.0.0": {
        "runtime": {
          "lib/net8.0/Example.dll": {}
        }
      }
    }
  }
}
JSON

    rewrite_plugin_deps_json "$plugin_dir"

    if grep -q '"lib/net8.0/Example.dll"' "$plugin_dir/missing.deps.json"; then
        pass "rewrite leaves orphan runtime paths for validation to catch"
    else
        fail "rewrite should not silently change paths it cannot resolve"
    fi
}

# --- validate_plugin_dependencies ---

test_validate_passes_when_flattened_dependency_listed_in_manifest() {
    local plugin_dir
    plugin_dir=$(setup_plugin "declared")
    write_manifest "$plugin_dir/plugin.json" '{"pluginId":"declared","name":"Declared","version":"1.0.0","author":"Tests","description":"Test","apiVersion":"1.0","entryPoint":"X","assemblyFileName":"declared.dll","supportedCategories":["Image"],"dependencies":["AWSSDK.S3.dll"]}'

    cat > "$plugin_dir/declared.deps.json" <<'JSON'
{
  "targets": {
    ".NETCoreApp,Version=v10.0": {
      "AWSSDK.S3/4.0.102.4": {
        "runtime": {
          "lib/net8.0/AWSSDK.S3.dll": {}
        }
      }
    }
  }
}
JSON

    if validate_plugin_dependencies "$plugin_dir" "declared" >/dev/null 2>&1; then
        pass "validate accepts flattened runtime asset when listed in plugin.json dependencies"
    else
        fail "validate should accept flattened AWSSDK.S3.dll when listed in plugin.json dependencies"
    fi
}

test_validate_passes_when_file_present_at_declared_path() {
    local plugin_dir
    plugin_dir=$(setup_plugin "canonical")
    mkdir -p "$plugin_dir/lib/net10.0"
    printf 'stub' > "$plugin_dir/lib/net10.0/SkiaSharp.dll"
    write_manifest "$plugin_dir/plugin.json" '{"pluginId":"canonical","name":"Canonical","version":"1.0.0","author":"Tests","description":"Test","apiVersion":"1.0","entryPoint":"X","assemblyFileName":"canonical.dll","supportedCategories":["Image"],"dependencies":[]}'

    cat > "$plugin_dir/canonical.deps.json" <<'JSON'
{
  "targets": {
    ".NETCoreApp,Version=v10.0": {
      "SkiaSharp/2.0.0": {
        "native": {
          "lib/net10.0/SkiaSharp.dll": {}
        }
      }
    }
  }
}
JSON

    if validate_plugin_dependencies "$plugin_dir" "canonical" >/dev/null 2>&1; then
        pass "validate accepts runtime asset that lives at its declared deps.json path"
    else
        fail "validate should accept runtime asset that lives at its declared deps.json path"
    fi
}

test_validate_fails_when_runtime_asset_is_orphan() {
    local plugin_dir
    plugin_dir=$(setup_plugin "orphan")
    write_manifest "$plugin_dir/plugin.json" '{"pluginId":"orphan","name":"Orphan","version":"1.0.0","author":"Tests","description":"Test","apiVersion":"1.0","entryPoint":"X","assemblyFileName":"orphan.dll","supportedCategories":["Image"],"dependencies":[]}'

    cat > "$plugin_dir/orphan.deps.json" <<'JSON'
{
  "targets": {
    ".NETCoreApp,Version=v10.0": {
      "AWSSDK.S3/4.0.102.4": {
        "runtime": {
          "lib/net8.0/AWSSDK.S3.dll": {}
        }
      }
    }
  }
}
JSON

    local err_log="$TMP_ROOT/orphan.err"
    if validate_plugin_dependencies "$plugin_dir" "orphan" >/dev/null 2>"$err_log"; then
        fail "validate must fail when a runtime asset is neither colocated nor declared"
    else
        if grep -q 'AWSSDK.S3.dll' "$err_log" && grep -q "plugin 'orphan'" "$err_log"; then
            pass "validate fails with a clear error pointing at the orphan runtime asset"
        else
            fail "validate failure should mention the missing asset and the plugin id"
            cat "$err_log" >&2
        fi
    fi
}

test_rewrite_then_validate_passes_for_amazonss3_layout() {
    local plugin_dir
    plugin_dir=$(setup_plugin "amazons3")
    write_manifest "$plugin_dir/plugin.json" '{"pluginId":"amazons3","name":"Amazon S3 Uploader","version":"1.0.0","author":"Tests","description":"Test","apiVersion":"1.0","entryPoint":"X","assemblyFileName":"amazons3.dll","supportedCategories":["Image"],"dependencies":["AWSSDK.Core.dll","AWSSDK.S3.dll"]}'

    printf 'stub' > "$plugin_dir/AWSSDK.S3.dll"
    printf 'stub' > "$plugin_dir/AWSSDK.Core.dll"

    cat > "$plugin_dir/amazons3.deps.json" <<'JSON'
{
  "targets": {
    ".NETCoreApp,Version=v10.0": {
      "AWSSDK.S3/4.0.102.4": {
        "runtime": {
          "lib/net8.0/AWSSDK.S3.dll": {}
        }
      },
      "AWSSDK.Core/4.0.102.1": {
        "runtime": {
          "lib/net8.0/AWSSDK.Core.dll": {}
        }
      }
    }
  }
}
JSON

    rewrite_plugin_deps_json "$plugin_dir"

    if validate_plugin_dependencies "$plugin_dir" "amazons3" >/dev/null 2>&1; then
        pass "rewrite + validate together produce a clean amazon3s plugin"
    else
        fail "rewrite + validate together should produce a clean amazon3s plugin"
        validate_plugin_dependencies "$plugin_dir" "amazons3" || true
    fi
}

# --- run ---

test_rewrite_moves_flattened_runtime_path
test_rewrite_keeps_path_when_file_exists_at_declared_location
test_rewrite_leaves_unknown_paths_alone
test_validate_passes_when_flattened_dependency_listed_in_manifest
test_validate_passes_when_file_present_at_declared_path
test_validate_fails_when_runtime_asset_is_orphan
test_rewrite_then_validate_passes_for_amazonss3_layout

echo ""
echo "Passed: $PASS"
echo "Failed: $FAIL"

if [ "$FAIL" -gt 0 ]; then
    exit 1
fi