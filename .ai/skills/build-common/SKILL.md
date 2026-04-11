---
name: build-common
description: Shared XerahS build guardrails for all platforms. Use with platform build skills when handling timeouts, stale dotnet processes, file locks, single-node MSBuild, build-server shutdown, and centrally managed dependency versions.
metadata:
  keywords:
    - build
    - guardrails
    - timeout
    - file-lock
    - dotnet
    - msbuild
    - m:1
    - skia
---

# Build Common Guardrails

Use this skill as the shared preface for platform-specific build skills:
- [build-android](../build-android/SKILL.md)
- [build-windows-exe](../build-windows-exe/SKILL.md)
- [build-linux-binary](../build-linux-binary/SKILL.md)

The platform skills own their command details and artifact checks. This file owns shared failure-handling rules.

## Non-Negotiable Rules

1. Do not wait more than 5 minutes for a single build step without progress unless the platform skill explicitly documents a longer packaging step and a log-monitoring method.
2. Never run two XerahS solution, packaging, publish, or Android builds at the same time.
3. Prefer single-node MSBuild (`-m:1` or `/m:1`) when file locks or shared output races appear.
4. Do not disable `<TreatWarningsAsErrors>`.
5. Keep `net10.0-windows10.0.26100.0` as the Windows target framework moniker; do not downgrade it to `net10.0-windows`.
6. Keep SkiaSharp aligned with the centrally managed root `Directory.Packages.props` version. Do not reintroduce a project-local legacy pin.

## Lock Recovery

Common symptoms:
- `CS2012: Cannot open ... for writing`
- `The process cannot access the file ... because it is being used by another process`
- `CompileAvaloniaXamlTask` copy failures
- `.NET Host`, `dotnet`, `MSBuild`, `VBCSCompiler`, or `csc` named as the lock holder

Windows cleanup:

```powershell
dotnet build-server shutdown
Get-Process | Where-Object {
    $_.Name -like '*dotnet*' -or
    $_.Name -like '*MSBuild*' -or
    $_.Name -like '*VBCSCompiler*' -or
    $_.Name -like '*csc*'
} | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
```

Linux cleanup:

```bash
pkill -f "package-linux.sh" 2>/dev/null || true
pkill -f "dotnet publish" 2>/dev/null || true
pkill -f "dotnet build" 2>/dev/null || true
sleep 2
```

After stopping lock holders, clean only the affected project or output folders first. Remove `obj` folders only when normal clean still fails.

## Shared Output Race

`ShareX.ImageEditor` and some plugins can race on shared build outputs during parallel builds. If lock errors mention `ShareX.ImageEditor.dll`, plugin DLLs, or Android wrapped `.so` files:

```powershell
dotnet build "ShareX.ImageEditor\src\ShareX.ImageEditor\ShareX.ImageEditor.csproj" -c Release -p:UseSharedCompilation=false -m:1
```

Then rerun the platform-specific script from the relevant build skill.

## Build Verification

For normal code/config changes before push:

```powershell
dotnet build src/desktop/XerahS.sln
```

For packaging tasks, also verify the expected artifacts in `dist/` or the platform-specific output path documented by the relevant build skill.
