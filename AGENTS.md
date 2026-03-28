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
   - **SkiaSharp**: stay on **2.88.9** (do not upgrade to 3.x).

2. **Build Timeouts**
   - Never wait more than **5 minutes** for a single build.
   - If a build stalls, stop it, clear locks, prefer `-m:1`, kill stale `dotnet` processes, then retry.
   - See [Building Android](.ai/skills/build-android/SKILL.md) for Android-specific lock handling and single-node builds.

3. **Shell Best Practices**
   - Do not use `&&` in this PowerShell environment.
   - Use `;` for unconditional sequencing or `if ($?) { ... }` for conditional execution.
   - Example: `git add .; if ($?) { git commit -m "..." }`

4. **Git Workflow**
   - Sequence: stage (`git add .`) -> commit -> push.
   - Commit format: `[vX.Y.Z] [Type] Use concise description`.
   - **Version prefix must match root `Directory.Build.props` `<Version>`** (currently `0.20.18`). Do not use a higher patch in the message (for example `[v0.20.19]` or `[v0.20.24]`) while the app version remains `0.20.18`. Policy effective from commit `62ff53d357f6480df26d103361f240ac028a62b5` onward; when `<Version>` is bumped, use that new value in commit prefixes until the next bump.
   - Exception: when committing inside shared library repos/submodules such as `ShareX.ImageEditor` (and other libraries shared with ShareX), omit the version prefix and use `[Type] Use concise description` because those commits must not carry the XerahS app version.
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

### Planning
- [Roadmap & Status Snapshot](docs/planning/ROADMAP_SNAPSHOT_JAN_2025.md)
- [XIP Sync](.ai/skills/sync-xips/SKILL.md)
