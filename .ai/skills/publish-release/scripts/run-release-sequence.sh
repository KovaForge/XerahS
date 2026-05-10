#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: run-release-sequence.sh [sequence-options] [-- bump-script-options]

Run release flow in strict order:
1) run-maintenance skill
2) update-changelog skill
3) bump-version-commit-tag.sh
4) optional: monitor tag release workflow until complete

Sequence options:
  --skip-maintenance          Skip step 1 maintenance execution (explicit bypass)
  --assume-maintenance-done   Backward-compatible alias for --skip-maintenance
  --assume-changelog-done     Skip interactive confirmation for step 2
  --monitor                   Monitor tag release workflow after step 3
  --monitor-interval <sec>    Poll interval in seconds (default: 120)
  --repo <owner/name>         GitHub repository for gh commands (default: origin remote)
  --set-prerelease            Explicitly mark successful tag release as pre-release (default behavior)
  --no-prerelease             Keep successful tag release as stable (opt out)
  --prepare-flathub-source    Generate Flathub source-build manifest candidate after the pre-release is ready
  -h, --help                  Show this help

All other options are passed through to:
  ./.ai/skills/publish-release/scripts/bump-version-commit-tag.sh
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

run_maintenance_chores() {
  echo "Step 1: running maintenance prep..."
  echo "  - git status --short"
  if [[ -n "$(git status --short)" ]]; then
    echo "Error: working tree has local changes. Commit, stash, or clean them before maintenance pull." >&2
    git status --short >&2
    exit 1
  fi
  echo "  - git submodule foreach --recursive status guard"
  git submodule foreach --recursive '
    if test -n "$(git status --short)"; then
      echo "Error: submodule has local changes: $displaypath" >&2
      git status --short >&2
      exit 1
    fi
  '
  echo "  - git pull --recurse-submodules"
  git pull --recurse-submodules
  echo "  - git submodule update --init --recursive"
  git submodule update --init --recursive
  if [[ -d "ShareX.ImageEditor/.git" || -f "ShareX.ImageEditor/.git" ]]; then
    echo "  - reattach ShareX.ImageEditor to develop"
    git -C ShareX.ImageEditor fetch origin --prune
    git -C ShareX.ImageEditor checkout develop
    git -C ShareX.ImageEditor pull --ff-only origin develop
    local image_editor_branch
    image_editor_branch="$(git -C ShareX.ImageEditor symbolic-ref --short HEAD 2>/dev/null || true)"
    if [[ "$image_editor_branch" != "develop" ]]; then
      echo "Error: ShareX.ImageEditor must be attached to develop after maintenance; current branch: ${image_editor_branch:-detached}" >&2
      exit 1
    fi
  fi
}

run_build_precheck() {
  echo "Step 3 pre-check: dotnet build src/desktop/XerahS.sln -m:1"
  require_cmd dotnet
  dotnet build src/desktop/XerahS.sln -m:1
}

resolve_version_from_props() {
  local version_file="$1"
  local version
  version="$(
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
    ' "$version_file" | tr -d '[:space:]' || true
  )"
  if [[ -z "$version" ]]; then
    echo "Error: failed to resolve <Version> from $version_file" >&2
    exit 1
  fi
  echo "$version"
}

passthrough_has_flag() {
  local flag="$1"
  local arg
  for arg in "${PASSTHROUGH_ARGS[@]}"; do
    if [[ "$arg" == "$flag" ]]; then
      return 0
    fi
  done
  return 1
}

find_tag_run_id() {
  local workflow_name="$1"
  local tag_name="$2"
  local gh_repo="$3"
  local attempt=1
  local max_attempts=30
  local run_id=""

  while [[ $attempt -le $max_attempts ]]; do
    run_id="$(gh run list \
      --repo "$gh_repo" \
      --workflow "$workflow_name" \
      --limit 50 \
      --json databaseId,headBranch \
      --jq "map(select(.headBranch==\"$tag_name\"))[0].databaseId // empty" 2>/dev/null || true)"

    if [[ -n "$run_id" ]]; then
      echo "$run_id"
      return 0
    fi

    echo "Waiting for workflow run for $tag_name (attempt $attempt/$max_attempts)..." >&2
    sleep 10
    attempt=$((attempt + 1))
  done

  return 1
}

monitor_release_run() {
  local run_id="$1"
  local interval="$2"
  local gh_repo="$3"
  local line
  local status=""
  local conclusion=""
  local run_url=""
  local failed_job_id=""
  local failed_job_name=""
  local log_file=""

  while true; do
    line="$(gh run view "$run_id" --repo "$gh_repo" --json status,conclusion,url --jq '[.status, (if (.conclusion == null or .conclusion == "") then "n/a" else .conclusion end), .url] | @tsv')"
    IFS=$'\t' read -r status conclusion run_url <<< "$line"

    echo "Run $run_id: status=$status conclusion=${conclusion:-n/a} url=$run_url"

    if [[ "$status" == "completed" ]]; then
      if [[ "$conclusion" == "success" ]]; then
        echo "Release workflow succeeded."
        return 0
      fi

      echo "Release workflow failed with conclusion '$conclusion'." >&2
      failed_job_id="$(gh run view "$run_id" --repo "$gh_repo" --json jobs --jq '.jobs[] | select(.conclusion=="failure") | .databaseId' | head -n 1 || true)"
      failed_job_name="$(gh run view "$run_id" --repo "$gh_repo" --json jobs --jq '.jobs[] | select(.conclusion=="failure") | .name' | head -n 1 || true)"

      if [[ -n "$failed_job_id" ]]; then
        log_file="release-run-${run_id}-job-${failed_job_id}.log"
        echo "First failing job: ${failed_job_name:-unknown} ($failed_job_id)"
        gh run view "$run_id" --repo "$gh_repo" --job "$failed_job_id" --log > "$log_file" 2>&1 || true
        echo "Saved failing job log to: $log_file"
      fi

      return 1
    fi

    sleep "$interval"
  done
}

wait_for_release() {
  local tag_name="$1"
  local gh_repo="$2"
  local attempt=1
  local max_attempts=90

  while [[ $attempt -le $max_attempts ]]; do
    if gh release view "$tag_name" --repo "$gh_repo" --json url >/dev/null 2>&1; then
      return 0
    fi
    echo "Waiting for release $tag_name (attempt $attempt/$max_attempts)..."
    sleep 10
    attempt=$((attempt + 1))
  done

  return 1
}

standard_release_notes_block() {
  cat <<'EOF'
Change log:
https://xerahs.com/changelog.html

### macOS Troubleshooting ("App is damaged")
If you see a message saying **"XerahS is damaged and can't be opened"**, it is due to macOS security (Gatekeeper) on quarantined downloads. To fix it:

1. Open **Terminal**.
2. Type the following command (do not hit Enter yet):
   ```bash
   xattr -cr 
   ```
3. Drag the **XerahS.app** file from Finder into the Terminal window (this pastes the full path).
4. Only now, press **Enter**.
EOF
}

ensure_standard_release_notes() {
  local tag_name="$1"
  local gh_repo="$2"
  local existing_body=""
  local updated_body_file=""
  local release_url=""

  require_cmd gh

  if ! wait_for_release "$tag_name" "$gh_repo"; then
    echo "Error: release $tag_name was not found. Cannot enforce standard release notes." >&2
    exit 1
  fi

  existing_body="$(gh release view "$tag_name" --repo "$gh_repo" --json body --jq '.body // ""')"
  if [[ "$existing_body" == *"https://xerahs.com/changelog.html"* ]] && [[ "$existing_body" == *"### macOS Troubleshooting (\"App is damaged\")"* ]]; then
    echo "Standard release notes block already present for $tag_name."
    return 0
  fi

  updated_body_file="$(mktemp)"
  {
    if [[ -n "$existing_body" ]]; then
      printf '%s\n\n' "$existing_body"
    fi
    standard_release_notes_block
  } > "$updated_body_file"

  gh release edit "$tag_name" --repo "$gh_repo" --notes-file "$updated_body_file" >/dev/null
  rm -f "$updated_body_file"

  release_url="$(gh release view "$tag_name" --repo "$gh_repo" --json url --jq '.url')"
  echo "Standard release notes block ensured: $release_url"
}

set_release_prerelease() {
  local tag_name="$1"
  local gh_repo="$2"
  local is_prerelease=""
  local release_url=""

  echo "Setting release $tag_name as pre-release..."
  gh release edit "$tag_name" --repo "$gh_repo" --prerelease >/dev/null

  is_prerelease="$(gh release view "$tag_name" --repo "$gh_repo" --json isPrerelease --jq '.isPrerelease')"
  release_url="$(gh release view "$tag_name" --repo "$gh_repo" --json url --jq '.url')"

  if [[ "$is_prerelease" != "true" ]]; then
    echo "Error: release $tag_name was not marked as pre-release." >&2
    exit 1
  fi

  echo "Release marked as pre-release: $release_url"
}

SKIP_MAINTENANCE=0
ASSUME_CHANGELOG_DONE=0
MONITOR=0
MONITOR_INTERVAL=120
SET_PRERELEASE=1
PREPARE_FLATHUB_SOURCE=0
WORKFLOW_NAME="Release Build (All Platforms)"
GH_TARGET_REPO=""

PASSTHROUGH_ARGS=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --assume-maintenance-done)
      SKIP_MAINTENANCE=1
      shift
      ;;
    --skip-maintenance)
      SKIP_MAINTENANCE=1
      shift
      ;;
    --assume-changelog-done)
      ASSUME_CHANGELOG_DONE=1
      shift
      ;;
    --monitor)
      MONITOR=1
      shift
      ;;
    --monitor-interval)
      if [[ $# -lt 2 ]]; then
        echo "Error: --monitor-interval requires a value." >&2
        exit 1
      fi
      MONITOR_INTERVAL="$2"
      shift 2
      ;;
    --repo)
      if [[ $# -lt 2 ]]; then
        echo "Error: --repo requires owner/name." >&2
        exit 1
      fi
      GH_TARGET_REPO="$2"
      shift 2
      ;;
    --set-prerelease)
      SET_PRERELEASE=1
      MONITOR=1
      shift
      ;;
    --no-prerelease)
      SET_PRERELEASE=0
      shift
      ;;
    --prepare-flathub-source)
      PREPARE_FLATHUB_SOURCE=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    --)
      shift
      PASSTHROUGH_ARGS+=("$@")
      break
      ;;
    *)
      PASSTHROUGH_ARGS+=("$1")
      shift
      ;;
  esac
done

if [[ ! "$MONITOR_INTERVAL" =~ ^[0-9]+$ ]] || [[ "$MONITOR_INTERVAL" -le 0 ]]; then
  echo "Error: --monitor-interval must be a positive integer." >&2
  exit 1
fi

repo_root="$(git rev-parse --show-toplevel 2>/dev/null || true)"
if [[ -z "$repo_root" ]]; then
  echo "Error: not inside a git repository." >&2
  exit 1
fi
cd "$repo_root"
repo_root="$(pwd -P)"

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
echo "GitHub repo target: $GH_TARGET_REPO"

maintenance_skill="$repo_root/.ai/skills/run-maintenance/SKILL.md"
changelog_skill="$repo_root/.ai/skills/update-changelog/SKILL.md"
bump_script="$repo_root/.ai/skills/publish-release/scripts/bump-version-commit-tag.sh"
flathub_source_script="$repo_root/.ai/skills/publish-release/scripts/prepare-flathub-source-build.sh"

if [[ ! -f "$maintenance_skill" ]]; then
  echo "Error: required skill file not found: $maintenance_skill" >&2
  exit 1
fi
if [[ ! -f "$changelog_skill" ]]; then
  echo "Error: required skill file not found: $changelog_skill" >&2
  exit 1
fi
if [[ ! -f "$bump_script" ]]; then
  echo "Error: required script file not found: $bump_script" >&2
  exit 1
fi
if [[ $PREPARE_FLATHUB_SOURCE -eq 1 && ! -f "$flathub_source_script" ]]; then
  echo "Error: required script file not found: $flathub_source_script" >&2
  exit 1
fi

if [[ $SKIP_MAINTENANCE -eq 0 ]]; then
  run_maintenance_chores
else
  echo "Step 1 skipped by request (--skip-maintenance)."
fi

if [[ $ASSUME_CHANGELOG_DONE -eq 0 ]]; then
  echo "Step 2 required: run changelog update skill second:"
  echo "  $changelog_skill"
  read -r -p "Type 'done' after finishing step 2: " response
  if [[ "$response" != "done" ]]; then
    echo "Aborted: changelog step not confirmed."
    exit 1
  fi
fi

echo "Step 3: running bump/tag automation..."
run_build_precheck
bash "$bump_script" "${PASSTHROUGH_ARGS[@]}"

if passthrough_has_flag "--dry-run"; then
  if [[ $MONITOR -eq 1 || $SET_PRERELEASE -eq 1 ]]; then
    echo "Skipping monitor/prerelease because bump step used --dry-run."
  fi
  exit 0
fi

if passthrough_has_flag "--no-tag" || passthrough_has_flag "--no-push"; then
  if [[ $MONITOR -eq 1 || $SET_PRERELEASE -eq 1 ]]; then
    echo "Error: --monitor/--set-prerelease requires tag creation and push." >&2
    exit 1
  fi
  echo "Done: bump step completed without remote tag push."
  exit 0
fi

version_file="Directory.Build.props"
new_version="$(resolve_version_from_props "$version_file")"
tag_name="v${new_version}"

if [[ $MONITOR -eq 1 ]]; then
  require_cmd gh

  echo "Step 4: monitoring workflow '$WORKFLOW_NAME' for tag $tag_name every ${MONITOR_INTERVAL}s..."
  run_id="$(find_tag_run_id "$WORKFLOW_NAME" "$tag_name" "$GH_TARGET_REPO" || true)"
  if [[ -z "$run_id" ]]; then
    echo "Error: could not find workflow run for tag $tag_name." >&2
    exit 1
  fi

  echo "Found run id: $run_id"
  if ! monitor_release_run "$run_id" "$MONITOR_INTERVAL" "$GH_TARGET_REPO"; then
    echo "Release run failed. Fix the issue, then retry with the next patch release." >&2
    exit 1
  fi
fi

echo "Step 5: ensuring standard release notes for $tag_name..."
ensure_standard_release_notes "$tag_name" "$GH_TARGET_REPO"

if [[ $SET_PRERELEASE -eq 1 ]]; then
  set_release_prerelease "$tag_name" "$GH_TARGET_REPO"
fi

if [[ $PREPARE_FLATHUB_SOURCE -eq 1 ]]; then
  echo "Step 8: preparing Flathub source-build manifest candidate for $tag_name..."
  bash "$flathub_source_script" --tag "$tag_name" --repo "$GH_TARGET_REPO" --lint
fi

echo "Release sequence completed for $tag_name."
