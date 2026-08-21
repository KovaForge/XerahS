#!/usr/bin/env bash
# XIP0077 U2 — Disk-space preflight gate
# Usage: source this or run as: scripts/preflight-disk.sh [min_gib]
# Exit 0 if enough disk, exit 1 with PREFLIGHT_DISK_LOW if not.
# Default minimum: 2 GiB.

set -euo pipefail

MIN_GIB="${1:-2}"
MIN_KIB=$((MIN_GIB * 1024 * 1024))

# df -k / → 1K-blocks, portable across macOS and Linux
FREE_KIB=$(df -k / | awk 'NR==2 { print $4 }')

if [ -z "$FREE_KIB" ]; then
    echo "PREFLIGHT_DISK_LOW 0 (unable to read df; fail closed)"
    exit 1
fi

if [ "$FREE_KIB" -lt "$MIN_KIB" ]; then
    FREE_MIB=$((FREE_KIB / 1024))
    echo "PREFLIGHT_DISK_LOW ${FREE_MIB}MiB (minimum: ${MIN_GIB}GiB)"
    exit 1
fi

FREE_MIB=$((FREE_KIB / 1024))
echo "PREFLIGHT_DISK_OK ${FREE_MIB}MiB free (minimum: ${MIN_GIB}GiB)"
exit 0
