# Documentation Standards

- **Update Policy**: Update/add docs when behavior changes.
- **Agent Instructions**: Keep repo-wide agent policy in `AGENTS.md`, detailed workflow guidance in `developers/guidelines/AGENT_WORKFLOW.md`, and agent-specific compatibility files thin.
- **Git + Versioning Workflow**: Use `.ai/skills/git-workflow/SKILL.md` as the single source of truth for commit, push, and version bump rules.
- **Structure**:
  - `developers/guidelines`: Stable developer and agent guidance.
  - `developers/lessons-learnt`: Durable lessons, rules, and postmortem notes.
  - `docs/architecture`: High-level system design and concepts.
  - `docs/audits`: Detailed reviews, gap analyses, and action plans.
  - `docs/planning`: Active plans and requirements.
  - `docs/reports`: Frozen records of past tasks and fix summaries.
- **Format**: Keep instructions in ASCII unless target file is Unicode.
- **Cross-Links**: Verify that referenced files and folders exist before adding links, especially when updating workflow docs.
