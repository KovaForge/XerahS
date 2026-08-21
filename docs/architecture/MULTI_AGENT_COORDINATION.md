# Multi-Agent Coordination

Use this document when more than one coding agent, worktree, or parallel session is active on XerahS.

## Core Rules

1. One coordinating agent owns scope, task slicing, merge order, and final verification.
2. Split work by project or folder boundary, not by adjacent files inside the same project.
3. Never have two agents editing the same project at the same time.
4. Keep repository-wide policy files and solution-level settings under coordinator control unless they are explicitly delegated.
5. Every worker agent must return files changed, assumptions, verification, and remaining risks.

## Good Boundaries in This Repo


| Boundary                                                            | Typical Scope                                                           |
| ------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| `src/desktop/app/XerahS.UI`                                         | Avalonia views, controls, XAML, UI wiring                               |
| `src/desktop/app/XerahS.App` and `src/desktop/app/XerahS.Bootstrap` | App startup and composition root                                        |
| `src/desktop/core/*`                                                | Business logic, history, media, services, uploaders, shared view models |
| `src/platform/*`                                                    | Windows, Linux, macOS, and shared platform abstractions                 |
| `src/desktop/plugins/*`                                             | Uploader plugin implementations                                         |
| `src/mobile/*`                                                      | Android and iOS heads                                                   |
| `docs/*` and `developers/*`                                         | Documentation, audits, and process updates                              |


## Typical Reasons to Delegate

- Large refactors across multiple files
- Separate research or audit work
- Focused test authoring or verification
- Mechanical cleanup that would otherwise drown the main context
- Platform-specific reproduction or validation

## Delegation Pattern

1. The coordinator writes a short task brief with scope, boundary, and expected verification.
2. The worker agent stays inside the assigned boundary and does not expand scope silently.
3. The worker returns:
  - Summary of the change
  - Files changed
  - Commands run and results
  - Assumptions, risks, or blockers
4. The coordinator integrates the result, resolves cross-cutting edits, and reruns final verification.

## Protected or High-Conflict Files

- `AGENTS.md`
- `CLAUDE.md`
- `developers/guidelines/AGENT_WORKFLOW.md`
- `Directory.Build.props`
- `Directory.Packages.props`
- `src/desktop/XerahS.sln`
- Shared interfaces, enums, and other abstractions that cross project boundaries
- Docs that define repository-wide policy

## Git Rules

- Use separate branches or worktrees for parallel work when possible.
- Keep commits small and boundary-specific.
- Avoid rebasing shared branches during active multi-agent work.
- After integration, the coordinator should run at least one targeted build or test pass that covers the merged scope.

## Stop Conditions

Pause and escalate when:

- A task needs files outside its assigned boundary
- Two agents need the same project at the same time
- A new shared abstraction or package/version change is required
- A solution-level setting or repository policy file must change

