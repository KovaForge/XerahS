#!/usr/bin/env bash
# Shared GitHub owner/name resolver for publish-release scripts.
# Supports standard github.com remotes and KovaForge per-person SSH aliases
# such as git@github-vladislava:KovaForge/XerahS.git.
#
# Important: do NOT fall back to bare `gh repo view` on forks. On a fork checkout
# with upstream=ShareX/XerahS, `gh` often resolves the parent (ShareX/XerahS)
# instead of the current origin (KovaForge/XerahS).

resolve_github_repo_from_remote_url() {
  local remote_url="${1:-}"
  local repo=""

  if [[ -z "$remote_url" ]]; then
    return 1
  fi

  # Normalize whitespace / CR.
  remote_url="${remote_url%%$'\n'*}"
  remote_url="${remote_url%%$'\r'*}"
  remote_url="${remote_url#"${remote_url%%[![:space:]]*}"}"
  remote_url="${remote_url%"${remote_url##*[![:space:]]}"}"

  # Prefer exact ownership-bearing patterns used in this workspace.
  if [[ "$remote_url" =~ ^https?://github\.com/([A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+)(\.git)?/?$ ]]; then
    repo="${BASH_REMATCH[1]}"
  elif [[ "$remote_url" =~ ^git@github\.com:([A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+)(\.git)?/?$ ]]; then
    repo="${BASH_REMATCH[1]}"
  elif [[ "$remote_url" =~ ^ssh://git@github\.com/([A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+)(\.git)?/?$ ]]; then
    repo="${BASH_REMATCH[1]}"
  elif [[ "$remote_url" =~ ^git@github-[A-Za-z0-9_-]+:([A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+)(\.git)?/?$ ]]; then
    # git@github-<alias>:Owner/Repo.git
    repo="${BASH_REMATCH[1]}"
  elif [[ "$remote_url" =~ ^ssh://git@github-[A-Za-z0-9_-]+/([A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+)(\.git)?/?$ ]]; then
    # ssh://git@github-<alias>/Owner/Repo.git
    repo="${BASH_REMATCH[1]}"
  elif [[ "$remote_url" =~ (^|[@:/])([A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+)(\.git)?/?$ ]]; then
    # Generic trailing owner/name fallback.
    repo="${BASH_REMATCH[2]}"
  else
    return 1
  fi

  repo="${repo%.git}"
  if [[ "$repo" =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ ]]; then
    echo "$repo"
    return 0
  fi

  return 1
}

resolve_github_repo_from_origin() {
  local remote_name="${1:-origin}"
  local remote_url
  remote_url="$(git remote get-url "$remote_name" 2>/dev/null || true)"
  resolve_github_repo_from_remote_url "$remote_url"
}

resolve_github_repo_prefer_origin() {
  # Resolution order intentionally prefers origin over gh defaults.
  local override="${1:-}"
  local repo=""

  if [[ -n "$override" ]]; then
    echo "$override"
    return 0
  fi

  if repo="$(resolve_github_repo_from_origin origin)"; then
    echo "$repo"
    return 0
  fi

  if [[ -n "${GH_REPO:-}" ]]; then
    echo "$GH_REPO"
    return 0
  fi

  # Last resort for checkouts whose remotes use only upstream naming.
  if repo="$(resolve_github_repo_from_origin upstream)"; then
    echo "$repo"
    return 0
  fi

  return 1
}
