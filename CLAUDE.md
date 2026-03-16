# CLAUDE.md

This file exists for Claude-compatible agents.

Follow these files in order:
1. [AGENTS.md](AGENTS.md)
2. [developers/guidelines/AGENT_WORKFLOW.md](developers/guidelines/AGENT_WORKFLOW.md)
3. [docs/architecture/MULTI_AGENT_COORDINATION.md](docs/architecture/MULTI_AGENT_COORDINATION.md) when delegating to sub-agents, worktrees, or parallel sessions
4. [developers/lessons-learnt/general.md](developers/lessons-learnt/general.md) for durable repo memory

If any instruction conflicts, `AGENTS.md` wins.

Repo-specific reminders:
- For non-trivial work, start with `Entering plan mode for this task...` when the host supports explicit plan mode.
- Planning is mandatory for meaningful changes, but waiting for approval is only required when the user asks for it or the design is ambiguous or high-risk.
- Do not bypass verification, build timeout, TFM, or package-version rules from `AGENTS.md`.
