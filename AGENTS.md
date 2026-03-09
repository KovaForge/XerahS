# XerahS Agent Instructions

**XerahS** - The Avalonia UI implementation of ShareX.  
**Copyright (c) 2007-2026 ShareX Team.**

> **Single Source of Truth**: Start here. Agent-specific compatibility files should defer to this document instead of restating repository policy.

## Critical Instructions
1. **Build Integrity**
   - `dotnet build` must pass with **0 errors** before any push.
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
   - If verification passes and the user did not ask to pause, execute the workflow without waiting for extra permission.

5. **Agent Workflow**
   - Plan before non-trivial work. Use the host tool's plan mode when available; otherwise post a numbered plan in chat before editing.
   - Do not create competing instruction sets. Keep shared workflow rules in [Universal Agent Workflow](developers/guidelines/AGENT_WORKFLOW.md) and keep compatibility shims thin.
   - Record durable lessons in [Lessons Learnt](developers/lessons-learnt/general.md) or the nearest topic-specific lessons file.

## GitHub Issue Creation Workflow
When a bug or feature is identified or provided by the user, follow this process:

1. **Inspect** the relevant code files, classes, components, and functions.
2. **Identify** the most important affected class names, file paths, and component names.
3. **Classify**
   - Broken, incorrect, error, crash -> **BUG** -> Type `Fix` -> label `bug`
   - New feature, missing capability, improvement -> **FEATURE REQUEST** -> Type `Feature` -> label `enhancement`
4. **Create a GitHub issue using `gh` CLI**
   - Title: `[BUG] ...` or `[FEATURE] ...`
   - Body: real multi-line Markdown with reproduction or use case, expected vs actual where relevant, bolded affected **ClassName**, **FilePath**, **Component**, and any logs or errors.
   - Use a PowerShell here-string or a file for the body. Do not embed `\n` escape sequences inside a single-line body string.
   - Example: `gh issue create --title "[BUG] ..." --body $body --label bug`
5. **Report back**
   - The exact `gh` command that was run
   - The created issue URL
   - The final classification and labels

## Documentation Index

### Shared Workflow
- [Universal Agent Workflow](developers/guidelines/AGENT_WORKFLOW.md)
- [Multi-Agent Coordination](docs/architecture/MULTI_AGENT_COORDINATION.md)
- [Lessons Learnt](developers/lessons-learnt/general.md)

### Development
- [Coding Standards & License Headers](developers/guidelines/CODING_STANDARDS.md)
- [Release & Versioning](.ai/skills/xerahs-workflow/SKILL.md)
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
- [XIP Sync](.ai/skills/xip-sync/SKILL.md)
