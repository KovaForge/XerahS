# Implement XIP end to end prompt (reusable)

Use this when asking an assistant to implement one or more existing XerahS XIP files from proposal to shipped behavior. Repository policy and workflow still apply; see root `AGENTS.md` and `developers/guidelines/AGENT_WORKFLOW.md`.

---

## Copy-paste prompt

```text
Implement the following XIP(s) end to end:

- [path/to/XIP####-name.md]
- [optional related XIP path]

Treat this as production work for the XerahS repository, not a prototype. Stay on the current branch unless I explicitly ask for a branch. Follow root AGENTS.md, including build integrity, warnings-as-errors, commit format, and no-new-branch rules.

Goal:
- Deliver the complete user experience described by the XIP(s), including all UI controls being functional.
- Make related XIPs compatible when their behavior overlaps.
- Update the XIP files after implementation so they accurately say what shipped, what was verified, and what remains future hardening.

Discovery:
1. Read the XIP(s) fully before editing.
2. Search for existing implementation, adjacent proposals, related tests, settings, UI views, services, platform abstractions, CLI/MCP surfaces, and history/workflow paths.
3. Identify where the repo already has the right abstraction. Prefer existing service boundaries and local patterns over new architecture.
4. Identify user-visible surfaces affected by the XIP: settings, capture flow, History, assistant, MCP/CLI, workflows, onboarding, and tests.
5. List assumptions, missing details, compatibility risks, and any intentionally deferred hardening.

Plan:
1. Propose a short implementation plan before code edits.
2. Split work into logical batches, such as core service/data model, capture/workflow integration, UI, assistant/MCP/CLI integration, tests, and docs.
3. For broad or parallelizable investigation, use subagents when available for bounded codebase questions, but keep final integration coherent.
4. Do not stop at a plan unless blocked. Implement, test, and update docs.

Implementation:
1. Make the smallest robust implementation that satisfies the XIP behavior.
2. Keep UX labels user-friendly; do not copy internal names literally into UI if a clearer label exists.
3. Make settings explicit and safe by default, especially for privacy-sensitive features.
4. Ensure automatic/background behavior does not accidentally trigger interactive workflows.
5. Reuse local/native services first. Do not add cloud behavior unless the XIP explicitly requires it and the user has an explicit consent path.
6. Persist data in the repo's established storage style. Prefer structured storage/parsers over ad hoc strings.
7. Make every advertised UI control functional or remove/defer it explicitly in the XIP update.
8. Ensure all relevant surfaces use one shared source of truth rather than duplicating search/indexing/business logic.
9. Preserve unrelated user changes and avoid broad refactors that are not necessary for the XIP.

Tests and verification:
1. Add focused tests for the new core behavior and important integrations.
2. Run narrow tests first, then run `dotnet build` before finishing.
3. If the feature affects UI, verify bindings and commands compile and controls map to implemented behavior.
4. If the feature affects assistant/MCP/CLI/workflows, test those contracts or add regression tests around their routing.
5. Report exact commands run and whether they passed.

XIP update:
1. Mark implemented XIPs with the repository-normalized completed status and an implementation date.
2. Add an implementation update section listing shipped behavior, key files, tests, and build verification.
3. Update phase/acceptance criteria with `[Implemented]` markers only for what actually shipped.
4. Keep future work explicit under a remaining hardening/follow-up section.
5. If related XIPs overlap, document the compatibility contract in both files.

Git batching:
1. Commit and push in logical batches as each implementation batch is completed and verified; do not wait until the entire XIP is finished when a coherent batch is ready.
2. Good batch examples: implementation + tests, then XIP/docs update.
3. Use the required version prefix from root `Directory.Build.props` and make sure it is ahead of the latest tag.
4. Push each batch immediately after committing it.
5. Keep the working tree clean between batches except for the next intentional batch.

GitHub follow-up:
1. Search GitHub issues, pull requests, and project items for the XIP number and title.
2. If a matching open item exists, add a concise completion comment linking or naming the shipped commit(s), then close the item once the implementation and XIP update are pushed.
3. If no matching GitHub item exists, state that explicitly in the final deliverables.
4. Do not create a new GitHub issue or project item unless the user explicitly asks for one.

Deliverables:
- What shipped
- Files changed
- Tests/build commands and results
- Remaining hardening/follow-up
- Commit hashes and pushed branch
- Matching GitHub item closed, or confirmation that none was found
```

Replace bracketed placeholders before sending the prompt.
