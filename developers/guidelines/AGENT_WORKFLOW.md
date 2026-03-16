# Universal Agent Workflow

This document adapts a cross-agent workflow to the XerahS repository. It is written for any coding agent: Codex, Claude, Copilot, Cursor, Windsurf, or similar tools. Agent-specific entry files should point here instead of duplicating policy.

## 1. Plan Before Coding

- Planning is mandatory for work that is likely to take more than a few minutes, touches multiple projects, changes behavior, or affects architecture or contributor process.
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

- Delegate when a task is large, multi-step, or would pollute the main working context.
- Good delegation targets in this repo:
  - `src/desktop/app/*` for desktop UI work
  - `src/desktop/core/*` for business logic and shared services
  - `src/platform/*` for platform integrations
  - `src/desktop/plugins/*` for uploader plugins
  - `src/mobile/*` for Android and iOS heads
  - `docs/*` or `developers/*` for documentation-only tasks
- Split work by project or folder boundary, not by arbitrary lines inside the same project.
- Never have two agents editing the same project at the same time.
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
- Respect XerahS guardrails while verifying:
  - Stop any single build that exceeds 5 minutes
  - Do not disable warnings-as-errors
  - Keep the Windows TFM explicit: `net10.0-windows10.0.26100.0`
  - Keep SkiaSharp on `2.88.9`
- Report concrete results, not generic claims.
- If automation is not possible, document the manual reasoning or repro steps used instead.

## 5. Documentation and Compatibility

- Update docs when behavior, workflow, architecture, or contributor process changes.
- Keep shared guidance centralized:
  - `AGENTS.md` is the entry point
  - `developers/guidelines/AGENT_WORKFLOW.md` holds the detailed workflow
  - `CLAUDE.md` and similar files are compatibility shims only
- Prefer ASCII unless the target file already uses Unicode intentionally.

## Learned Rules

Repo-specific learned rules belong in `developers/lessons-learnt/` so they accumulate without bloating the main workflow document.
