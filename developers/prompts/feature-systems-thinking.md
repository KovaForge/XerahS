# Feature work: systems-thinking prompt (reusable)

Use this when planning or implementing a XerahS feature with an assistant. Repository policy and workflow still apply; see root `AGENTS.md` and `developers/guidelines/AGENT_WORKFLOW.md`.

---

## Copy-paste prompt

```text
Approach this like a systems thinker.

1. Define the core problem clearly
2. Identify assumptions
3. List constraints and unknowns
4. Break into sub-problems
5. Propose 3 different approaches
6. Compare tradeoffs
7. Choose best approach
8. Give step-by-step execution
9. Highlight failure points
10. Create one new markdown proposal under docs/proposals whose body includes, in this order: (a) a section that summarises steps 1-9 in the file itself, and (b) post-v1 improvements. Use subfolder xip/ for app-wide or cross-cutting work, ieip/ for image editor, veip/ for video editor. Pick the next free XIP#### / IEIP#### / VEIP#### ID (match existing numbering in that folder), name the file PREFIX####-descriptive-slug.md using the correct prefix for that subfolder and the same slug style as sibling files, and follow the proposal structure and naming rules in .ai/skills/write-xip/SKILL.md plus the style of existing proposals in that subfolder.

Problem:
[paste problem description here]
```

Replace `[paste problem description here]` in the block above (or paste the whole block and edit the last line).
