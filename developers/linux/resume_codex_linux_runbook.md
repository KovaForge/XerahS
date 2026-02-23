# Resume Runbook (Linux + Codex)

Use this on the Linux machine to resume verification/fixes from the latest `HEAD` on `develop`.

## Prerequisites

- You are on a Wayland KDE session (or equivalent Linux desktop where the issue reproduces).
- `dotnet`, `git`, and `gdbus` are installed.
- `rg` (ripgrep) is recommended for log filtering.

## One-Go Command Block

Run the following in a Linux shell and update `REPO` first:

```bash
set -euo pipefail

REPO="/path/to/XerahS"
cd "$REPO"

echo "== 1) Sync and confirm current HEAD commit =="
git fetch origin
git checkout develop
git pull --ff-only
CURRENT_COMMIT="$(git rev-parse --short=7 HEAD)"
echo "Using current commit: $CURRENT_COMMIT"
git show --name-only --oneline "$CURRENT_COMMIT"

echo "== 2) Build + Linux test smoke =="
dotnet --info
dotnet restore src/desktop/XerahS.sln
dotnet build src/desktop/XerahS.sln -m:1
dotnet test tests/XerahS.Tests/XerahS.Tests.csproj -m:1 --filter "FullyQualifiedName~XerahS.Tests.Platform.Linux"

echo "== 3) Confirm Wayland/portal environment =="
echo "XDG_SESSION_TYPE=${XDG_SESSION_TYPE:-<unset>}"
echo "XDG_CURRENT_DESKTOP=${XDG_CURRENT_DESKTOP:-<unset>}"
gdbus introspect --session \
  --dest org.freedesktop.portal.Desktop \
  --object-path /org/freedesktop/portal/desktop | rg "Screenshot|GlobalShortcuts|InputCapture" || true

echo "== 4) Launch app for manual verification =="
echo "Manual checks to perform once app opens:"
echo "  - Register Print / Ctrl+Print / Shift+Print hotkeys."
echo "  - Confirm no 'Unable to map key Print' log appears."
echo "  - Start region capture and cancel once: should be treated as cancelled (no fullscreen fallback)."
echo "  - Start region capture and accept once: should capture successfully."
dotnet run --project src/desktop/app/XerahS.App/XerahS.App.csproj

echo "== 5) Extract latest XerahS log evidence =="
LATEST_LOG="$(ls -t "$HOME"/Documents/XerahS/Logs/*/* 2>/dev/null | head -n 1 || true)"
if [ -z "$LATEST_LOG" ]; then
  echo "No log file found under ~/Documents/XerahS/Logs"
  exit 1
fi

echo "Latest log: $LATEST_LOG"
rg -n "Unable to map key Print|Portal fallback did not find a screenshot file|No region capture tool available|Portal screenshot request was cancelled by user|Region capture cancelled by provider|XDG Portal capture succeeded|Hotkey registered" "$LATEST_LOG" || true

echo "== Done =="
```

## Run The Following In Codex On Linux

Run the following in Codex after the run above:

```text
Continue Linux verification for the latest HEAD commit on develop on this Wayland KDE machine.
Validate Print/Ctrl+Print/Shift+Print registration and portal-cancel behavior end-to-end using current logs.
If issues remain, fix only relevant Linux classes (especially src/platform/XerahS.Platform.Linux/Capture and Services), run:
- dotnet build src/desktop/XerahS.sln -m:1
- dotnet test tests/XerahS.Tests/XerahS.Tests.csproj -m:1 --filter "FullyQualifiedName~XerahS.Tests.Platform.Linux"
Then commit with [vX.Y.Z] [Fix] ... and push.
```
