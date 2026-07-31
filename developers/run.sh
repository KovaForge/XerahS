#!/usr/bin/env bash
# Run the clawpatch-parser dashboard. Local-only (127.0.0.1).
# Usage:
#   ./run.sh                 # default port 8765
#   ./run.sh --port 9090     # override port
#   PORT=9090 ./run.sh       # env var also works
#
# The script refuses to bind to anything other than 127.0.0.1 / ::1.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
VENV="$HERE/.venv"
if [ ! -d "$VENV" ]; then
    echo "[run.sh] First run — creating venv at $VENV" >&2
    python3 -m venv "$VENV"
    "$VENV/bin/pip" install --quiet 'flask>=3,<4'
fi

HOST="${HOST:-127.0.0.1}"
PORT="${PORT:-8765}"
while [ $# -gt 0 ]; do
    case "$1" in
        --host) HOST="$2"; shift 2 ;;
        --port|-p) PORT="$2"; shift 2 ;;
        --no-debug) DEBUG=0; shift ;;
        *) echo "unknown arg: $1" >&2; exit 2 ;;
    esac
done

# Hard lock to loopback. The skill rule says no auth and localhost-only.
if [ "$HOST" != "127.0.0.1" ] && [ "$HOST" != "::1" ] && [ "$HOST" != "localhost" ]; then
    echo "[run.sh] refusing to bind to '$HOST' — must be loopback (127.0.0.1 / ::1 / localhost)" >&2
    exit 2
fi

DEBUG_FLAG="--debug"
if [ "${DEBUG:-1}" = "0" ]; then
    DEBUG_FLAG=""
fi

# Use Flask's built-in CLI so host/port/auto-reload work the standard way.
cd "$HERE/clawpatch-parser"
exec "$VENV/bin/python" -m flask --app app:app run --host="$HOST" --port="$PORT" $DEBUG_FLAG
