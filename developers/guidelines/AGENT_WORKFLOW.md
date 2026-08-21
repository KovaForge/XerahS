# Universal Agent Workflow

This document adapts a cross-agent workflow to the XerahS repository. It is written for any coding agent: Codex, Claude, Copilot, Cursor, Windsurf, or similar tools. Agent-specific entry files should point here instead of duplicating policy.

## 1. Plan Before Coding

- Planning is mandatory for work that is likely to take more than a few minutes, touches multiple projects, changes behavior, or affects architecture or contributor process.
- For architecture, dependency, or unfamiliar-area orientation in `src/`, consult the graphify knowledge graph before broad greps. Kickoff prompt and path index: `developers/guidelines/GRAPHIFY_AGENT_PROMPT.md`. Artifacts: `docs/architecture/graphify-out/`.
- If the host supports explicit plan mode, start with `Entering plan mode for this task...`.
- A useful plan includes:
    1. Goal and assumptions
    2. Files or projects to inspect or change
    3. Implementation steps
    4. Risks and edge cases
    5. Verification steps
- In XerahS, planning does **not** always mean blocking. Wait for approval only when the user asks for a review-first flow, or when the design is ambiguous, architectural, or otherwise high-risk.
- Trivial edits can skip a full plan, but state why.

## 2. Use Sub-Agents for Large Work

- Delegation is required when the host supports sub-agents and the task is large, multi-step, parallelizable, spans distinct boundaries, or would pollute the main working context.
- Codex should satisfy this by calling `spawn_agent` (or the host's current delegation tool). Other agents should use their equivalent sub-agent, worker, or worktree mechanism.
- Do not keep obviously parallel side work in the coordinator by default. If a bounded task can run independently without blocking the immediate next local step, delegate it.
- Good delegation targets in this repo:
  - `src/desktop/app/*` for desktop UI work
  - `src/desktop/core/*` for business logic and shared services
  - `src/platform/*` for platform integrations
  - `src/desktop/plugins/*` for uploader plugins
  - `src/mobile/*` for Android and iOS heads
  - `docs/*` or `developers/*` for documentation-only tasks
- Split work by project or folder boundary, not by arbitrary lines inside the same project.
- Never have two agents editing the same project at the same time.
- If the host does not expose sub-agents, say so and apply the same boundary discipline manually.
- The coordinating agent owns scope, integration, and final verification.
- Each delegated task should return:
  - A concise summary
  - Files changed
  - Assumptions made
  - Verification performed
  - Remaining risks or open questions
- See `docs/architecture/MULTI_AGENT_COORDINATION.md` for the repository-specific coordination rules.

## 3. Self-Improving Memory

- After a correction, failed verification, or user critique:
  1. State the issue plainly
  2. Fix it if it is still in scope
  3. Distill one prevention rule
- Use this rule format:

```md
- Never ...; always ... because ...
```

- Store durable repo lessons in `developers/lessons-learnt/general.md` or the nearest topic-specific lessons file.
- Update `AGENTS.md` only when the lesson becomes a repository-wide policy that every agent must follow.

## 4. Verify Before Claiming Completion

- Never report completion without evidence.
- Run the smallest relevant verification automatically when possible: `dotnet build`, `dotnet test`, targeted project builds, linters, formatters, or a manual reproduction path.
- In XerahS, a version-only root `Directory.Build.props` bump is the narrow exception: if that is the only tracked change, review the diff and version alignment instead of running a fresh `dotnet build`.
- Respect XerahS guardrails while verifying:
  - Stop any single build that exceeds 5 minutes
  - Do not disable warnings-as-errors
  - Keep the Windows TFM explicit: `net10.0-windows10.0.26100.0`
  - Keep SkiaSharp aligned with root central package management (currently `3.119.3-preview.1.1`)
- Report concrete results, not generic claims.
- If automation is not possible, document the manual reasoning or repro steps used instead.

## 5. Documentation and Compatibility

- Update docs when behavior, workflow, architecture, or contributor process changes.
- Keep shared guidance centralized:
  - `AGENTS.md` is the entry point
  - `developers/guidelines/AGENT_WORKFLOW.md` holds the detailed workflow
  - `CLAUDE.md` and similar files are compatibility shims only and should not weaken the delegation requirement
- Prefer ASCII unless the target file already uses Unicode intentionally.

## Learned Rules

Repo-specific learned rules belong in `developers/lessons-learnt/` so they accumulate without bloating the main workflow document.
