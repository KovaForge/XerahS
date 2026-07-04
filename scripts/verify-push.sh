#!/usr/bin/env bash
# XIP0077 U7 — Standardized push verification
# Usage: scripts/verify-push.sh <remote> <branch>
# Prints PUSH_VERIFIED or PUSH_NOT_VERIFIED <details>
# Single-purpose so unattended approval gates are not tripped.

set -euo pipefail

REMOTE="${1:?Usage: verify-push.sh <remote> <branch>}"
BRANCH="${2:?Usage: verify-push.sh <remote> <branch>}"

# Timeout: the fetch must complete within 30 seconds
FETCH_TIMEOUT=30

echo "verify-push: fetching ${REMOTE} ${BRANCH}..."
if ! timeout "$FETCH_TIMEOUT" git fetch "$REMOTE" "$BRANCH" 2>&1; then
    echo "PUSH_NOT_VERIFIED fetch-failed remote=${REMOTE} branch=${BRANCH}"
    exit 1
fi

LOCAL_HEAD=$(git rev-parse HEAD 2>/dev/null || echo "UNKNOWN")
REMOTE_REF=$(git rev-parse "refs/remotes/${REMOTE}/${BRANCH}" 2>/dev/null || echo "UNKNOWN")

if [ "$LOCAL_HEAD" = "UNKNOWN" ] || [ "$REMOTE_REF" = "UNKNOWN" ]; then
    echo "PUSH_NOT_VERIFIED ref-resolution-failed local=${LOCAL_HEAD} remote=${REMOTE_REF}"
    exit 1
fi

if [ "$LOCAL_HEAD" = "$REMOTE_REF" ]; then
    echo "PUSH_VERIFIED remote=${REMOTE} branch=${BRANCH} sha=${LOCAL_HEAD:0:12}"
    exit 0
fi

# Count how far behind
BEHIND=$(git rev-list --count "${REMOTE}/${BRANCH}..HEAD" 2>/dev/null || echo "?")
echo "PUSH_NOT_VERIFIED remote=${REMOTE} branch=${BRANCH} local=${LOCAL_HEAD:0:12} remote_ref=${REMOTE_REF:0:12} local_ahead_by=${BEHIND}"
exit 1
