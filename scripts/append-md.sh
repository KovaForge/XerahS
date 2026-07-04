#!/usr/bin/env bash
# XIP0077 U8 — Durable markdown append with verification
# Usage: echo "content" | scripts/append-md.sh <file>
# Reads stdin, appends to file, then re-reads the appended window
# and fails loudly on mismatch.

set -euo pipefail

TARGET="${1:?Usage: append-md.sh <file>}"

if [ ! -f "$TARGET" ]; then
    echo "ERROR: target file does not exist: $TARGET" >&2
    exit 1
fi

# Capture stdin into a temp variable
CONTENT=$(cat)

if [ -z "$CONTENT" ]; then
    echo "ERROR: nothing to append (stdin was empty)" >&2
    exit 1
fi

# Record the line count before append
BEFORE_LINES=$(wc -l < "$TARGET" | tr -d ' ')

# Append (ensure leading newline separator)
printf '\n%s\n' "$CONTENT" >> "$TARGET"

# Count lines in the appended content
CONTENT_LINES=$(printf '%s\n' "$CONTENT" | wc -l | tr -d ' ')

# Re-read the appended window (BEFORE_LINES+1 to end)
START_LINE=$((BEFORE_LINES + 1))
READBACK=$(tail -n +"$START_LINE" "$TARGET")

# Verify the content appears in the readback
if printf '%s' "$READBACK" | grep -qF "$(printf '%s' "$CONTENT" | head -1)"; then
    echo "APPEND_OK lines_added=$CONTENT_LINES file=$TARGET"
    exit 0
else
    echo "APPEND_MISMATCH file=$TARGET start_line=$START_LINE" >&2
    echo "Expected first line: $(printf '%s' "$CONTENT" | head -1)" >&2
    echo "Got readback first line: $(printf '%s' "$READBACK" | head -1)" >&2
    exit 1
fi
