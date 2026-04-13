# Feature work: systems-thinking prompt (reusable)

Use this when planning or implementing a XerahS feature with an assistant. Repository policy and workflow still apply; see root `AGENTS.md` and `developers/guidelines/AGENT_WORKFLOW.md`.

---

## Copy-paste prompt

```text
Approach this like a systems thinker.

1. Define the core problem clearly
2. Identify assumptions
3. List constraints and unknowns
4. Use subagents to do online research on the feature and bring back findings into making the implementation more robust
5. Break into sub-problems
6. Propose 3 different approaches
7. Compare tradeoffs
8. Choose best approach
9. Give step-by-step execution
10. Use git in stages while carrying out step 9: commit whenever a logical chunk of the work is complete (for example a sub-problem, phase, or self-contained change that builds and tests cleanly), not only at the very end. Prefer several focused commits over one large catch-all commit unless the work is genuinely atomic. Follow AGENTS.md for commit message format.
11. Highlight failure points
12. Create one new markdown proposal under docs/proposals whose body includes, in this order: (a) a section that summarises steps 1-11 in the file itself, and (b) post-v1 improvements. Use subfolder xip/ for app-wide or cross-cutting work, ieip/ for image editor, veip/ for video editor. Pick the next free XIP#### / IEIP#### / VEIP#### ID (match existing numbering in that folder), name the file PREFIX####-descriptive-slug.md using the correct prefix for that subfolder and the same slug style as sibling files, and follow the proposal structure and naming rules in .ai/skills/write-xip/SKILL.md plus the style of existing proposals in that subfolder.

Problem:
[paste problem description here]
```

Replace `[paste problem description here]` in the block above (or paste the whole block and edit the last line).
