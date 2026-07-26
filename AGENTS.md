# XerahS Agent Instructions

**XerahS** - The Avalonia UI implementation of ShareX.  
**Copyright (c) 2007-2026 ShareX Team.**

> **Single Source of Truth**: Start here. Agent-specific compatibility files should defer to this document instead of restating repository policy.

## Critical Instructions
1. **Build Integrity**
   - `dotnet build` must pass with **0 errors** before any push.
   - Exception: if the only tracked change is a version-only bump in the root `Directory.Build.props`, a fresh `dotnet build` is not required before push. In that case, verify the diff is limited to the intended version change and keep the commit prefix aligned with the new version.
   - **Never** disable `<TreatWarningsAsErrors>`. Fix the warnings.
   - **Target Framework**: `net10.0-windows10.0.26100.0` (do not use `net10.0-windows` alone).
   - **SkiaSharp**: keep it aligned with the centrally managed version in root `Directory.Packages.props` (currently **3.119.3-preview.1.1**). Do not reintroduce the legacy `2.88.9` pin.

2. **Build Timeouts**
   - Do not enforce a fixed single-build time limit; packaging and release builds can legitimately take longer on slower PCs.
   - If a build appears stalled rather than merely slow, stop it, clear locks, prefer `-m:1`, kill stale `dotnet` processes, then retry.
   - See [Building Android](.ai/skills/build-android/SKILL.md) for Android-specific lock handling and single-node builds.

3. **Shell Best Practices**
   - Do not use `&&` in this PowerShell environment.
   - Use `;` for unconditional sequencing or `if ($?) { ... }` for conditional execution.
   - Example: `git add .; if ($?) { git commit -m "..." }`

4. **Git Workflow**
   - Sequence: stage (`git add .`) -> commit -> push.
   - Commit format: `[vX.Y.Z] [Type] Use concise description`.
   - **Version prefix must use the next unreleased XerahS app version.** Read root `Directory.Build.props` `<Version>` and compare it with the highest existing XerahS tag (for example `git tag --sort=-v:refname | Select-Object -First 1`). Never use a version prefix that is lower than or equal to the latest tag. If the root version is not ahead of the latest tag, bump `Directory.Build.props` first, then use that bumped version in commit prefixes.
   - Exception: when committing inside shared library repos/submodules such as `ShareX.ImageEditor` (and other libraries shared with ShareX), omit the version prefix and use `[Type] Use concise description` because those commits must not carry the XerahS app version.
   - **Do not create branches** (local or remote) unless a human explicitly requests it. Stay on the current branch; do not invent feature/fix/chore branches, worktree branches, or `cursor/*` branches on your own.
   - If verification passes and the user did not ask to pause, execute the workflow without waiting for extra permission.

5. **Agent Workflow**
   - Plan before non-trivial work. Use the host tool's plan mode when available; otherwise post a numbered plan in chat before editing.
   - For large, multi-step, parallelizable, or context-heavy work, the coordinating agent must use sub-agents when the host supports them. In Codex, use `spawn_agent` (or the host's current equivalent) for bounded side tasks instead of keeping all work in one thread.
   - Do not create competing instruction sets. Keep shared workflow rules in [Universal Agent Workflow](developers/guidelines/AGENT_WORKFLOW.md) and keep compatibility shims thin.
   - Record durable lessons in [Lessons Learnt](developers/lessons-learnt/general.md) or the nearest topic-specific lessons file.

## GitHub Issues
- Do not create GitHub issues automatically when a bug or feature is discussed.
- Create or update GitHub issues only when the user explicitly asks for it.

## Documentation Index

### Shared Workflow
- [Universal Agent Workflow](developers/guidelines/AGENT_WORKFLOW.md)
- [Graphify Agent Prompt](developers/guidelines/GRAPHIFY_AGENT_PROMPT.md) — kickoff prompt + skill/tool paths for every agent using the `src/` knowledge graph
- [Multi-Agent Coordination](docs/architecture/MULTI_AGENT_COORDINATION.md)
- [Lessons Learnt](developers/lessons-learnt/general.md)

### Development
- [Coding Standards & License Headers](developers/guidelines/CODING_STANDARDS.md)
- [Release & Versioning](.ai/skills/git-workflow/SKILL.md)
- [Building Windows Executables](.ai/skills/build-windows-exe/SKILL.md)
- [Building Android (MAUI / Avalonia, adb deploy)](.ai/skills/build-android/SKILL.md)
- [Testing Guidelines](developers/guidelines/TESTING.md)
- [Documentation Standards](developers/guidelines/DOCUMENTATION_STANDARDS.md)
- [CLI Reference](developers/guidelines/CLI.md)

### Architecture
- [Porting Guide & Platform Abstractions](docs/architecture/PORTING_GUIDE.md)
- [XerahS Architecture Map](docs/architecture/xerahs_architecture_map.md)
- [src/ Knowledge Graph (graphify)](docs/architecture/graphify-out/README.md) — queryable AST graph for agent navigation (`GRAPH_REPORT.md`, `graph.json`, HTML). Skill: [`.ai/skills/graphify/SKILL.md`](.ai/skills/graphify/SKILL.md). Agent prompt: [GRAPHIFY_AGENT_PROMPT.md](developers/guidelines/GRAPHIFY_AGENT_PROMPT.md). Rebuild: `scripts/update-graphify.sh`.

### Planning
- [Roadmap & Status Snapshot](docs/planning/ROADMAP_SNAPSHOT_JAN_2025.md)
- [XIP Sync](.ai/skills/sync-xips/SKILL.md)

---

# AGENTS.md

## Git identity and wrappers (mandatory)

All git activity in this repo MUST go through a per-person wrapper. No bare `git push`.

| Agent | Wrapper |
|---|---|
| Aoife | `git-aoife` |
| Mikhail | `git-mikhail` |
| Declan | `git-declan` |
| Vladislava | `git-vladislava` |

Whoever pushes uses their own wrapper. Example: Declan pushes with `git-declan push`, Vladislava with `git-vladislava push`. Wrappers set committer identity and route the push to the correct per-person remote on the matching `github-<person>` SSH host.

Run `git-<person> whoami` to confirm identity and remote before pushing.

## Source of truth

Inherited from `/Users/mike/Projects/KovaForge/AGENTS.md`. When this file and the parent conflict, the parent wins until this file is updated to match.

## Cursor Cloud specific instructions

These notes are for Cursor Cloud agents running on the Linux VM (Ubuntu 24.04). The per-person git wrappers above do **not** apply here; cloud agents use plain `git` on `cursor/*` branches.

### Toolchain (already provisioned in the VM snapshot)
- **.NET 10 SDK** lives in `~/.dotnet` and is on `PATH`/`DOTNET_ROOT` via `~/.bashrc`. If `dotnet` is not found in a non-login shell, use the full path `~/.dotnet/dotnet`.
- **Node.js** (>= 22.12) and `npm` are preinstalled system-wide and satisfy the `ShareX.VideoEditor` frontend `engines` requirement.
- On Linux the desktop projects build as **`net10.0`** (not `net10.0-windows...`); the Windows TFM only applies on Windows. `XerahS.Platform.Linux` is pulled in automatically.

### Build / test / run (Linux desktop scope)
- Build everything runnable on Linux: `dotnet build src/desktop/XerahS.sln -m:1 -p:nodeReuse=false -p:UseSharedCompilation=false` (single-node flags avoid MSBuild lock flakiness; see README "Desktop Quick Start").
- Tests: `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj`.
- Run the app: `dotnet run --project src/desktop/app/XerahS.App/XerahS.App.csproj` (add `--no-build` if already built).
- Lint/style is enforced by the build itself via `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` + Roslyn analyzers. `dotnet format --verify-no-changes` reports many pre-existing ENDOFLINE/CHARSET findings and is **not** the repo's style gate — do not treat those as regressions.

### Non-obvious gotchas
- **First build builds the VideoEditor web UI**: an MSBuild target runs `npm ci` + `vite build` in `ShareX.VideoEditor/frontend` and fails if `frontend/dist` is missing. This needs network on the first build; later builds skip it when deps are current.
- **GUI needs a display**: `XerahS.App` `Program.cs` validates `DISPLAY`/`WAYLAND_DISPLAY` on Linux and will not start headless. The cloud VM already exposes an X11 display at `DISPLAY=:1` (the desktop the computer-use tools see). Export `DISPLAY=:1` before `dotnet run` if it is unset.
- Screen recording on Linux falls back to the FFmpeg `x11grab` backend (no ScreenCast portal in the VM).
- Submodules (`ShareX.ImageEditor`, `ShareX.VideoEditor`) are required to build; `XerahS.UI`/`XerahS.CLI` reference them directly. They are refreshed by the startup update script (`git submodule update --init --recursive`).
