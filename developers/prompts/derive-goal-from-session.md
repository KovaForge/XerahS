# Derive /goal from session and repo prompt (reusable)

Use this when an assistant session already contains the discussion, constraints, and intent for a XerahS task, and you want Codex to turn that context into a self-contained `/goal` prompt. Repository policy and workflow still apply; see root `AGENTS.md` and `developers/guidelines/AGENT_WORKFLOW.md`.

---

## Copy-paste prompt

```text
Read this repo, analyze deeply the exact intent and goals we are looking to achieve here, then write me the /goal prompt for this.

Make sure to dig into history and docs we have to be 100% clear.

If you are not sure about certain parts, or want to ask me a few questions to clarify certain goals further, don't hesitate.

Output requirements:
- Return only the final prompt text unless clarification is needed first.
- Start the final prompt with `/goal`.
- Make the prompt self-contained enough that Codex can continue in this session and repo nonstop until completion.
- Include concrete goals, constraints, relevant history/docs to inspect, implementation expectations, verification expectations, and completion criteria.
- Preserve XerahS repository rules: stay on `main` unless explicitly told otherwise, follow root `AGENTS.md`, do not create branches or GitHub issues unless asked, and verify with the narrowest relevant tests plus `dotnet build` when required.
```

Paste this into Codex from the session you want to convert. If Codex returns a prompt that does not already start with `/goal`, change the initial part to `/goal` before running it.
