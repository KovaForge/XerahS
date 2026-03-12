---
name: xerahs-blog-drafting
description: Maintain XerahS daily development blog drafts under docs/blog using the YYYY/YYYY-MM/blog-YYYYMMDD.md layout. Use when asked to create, update, or consolidate the current UTC+8 blog post from new feature work, bug fixes, build/tooling changes, or recent git history.
---

## Workflow

1. Resolve the target UTC+8 date.
   - Default to today unless the user supplies a specific date.
   - Always write to `docs/blog/YYYY/YYYY-MM/blog-YYYYMMDD.md`.

2. Ensure the daily draft exists with the helper script:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .ai/skills/xerahs-blog-drafting/scripts/upsert-blog-draft.ps1
```

Use an explicit date when needed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .ai/skills/xerahs-blog-drafting/scripts/upsert-blog-draft.ps1 -Date 2026-03-13
```

Append a verified bullet to an existing section without creating a second post for the same day:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .ai/skills/xerahs-blog-drafting/scripts/upsert-blog-draft.ps1 -Date 2026-03-13 -Section Fixes -Bullet "Refreshed ShareX.VideoEditor npm bootstrap after the latest submodule update."
```

3. Gather context before drafting.
   - Inspect the relevant repo or submodule history with `git log --oneline -n 20`.
   - Open specific commits with `git show --stat <commit>`.
   - If the day's work is still in progress, inspect the current diff with `git diff -- <paths>` or `git -C <submodule> diff -- <paths>`.
   - Use only verified details from the repository state. Do not invent shipped behavior.

4. Consolidate the day into one markdown post.
   - Keep one file per UTC+8 day.
   - Update the existing file instead of creating multiple same-day posts.
   - Replace placeholder bullets when you have verified content.
   - Merge related fixes or build notes into concise bullets instead of repeating commit titles verbatim.

5. Keep the post in draft form unless the user asks for a polished publication pass.
   - `## Summary`: short paragraph explaining the day's main outcome.
   - `## Features`: new user-facing or contributor-facing capabilities.
   - `## Fixes`: bug fixes and reliability improvements.
   - `## Build and Tooling`: dependency upgrades, bootstrap changes, build guards, or workflow updates.
   - `## Commits Reviewed`: short hashes or commit subjects that support the draft.
   - `## Notes`: risks, prerequisites, or follow-up work.

6. Before finishing, remove untouched placeholders if the draft is meant to be reviewed by humans.

## Helper Script

Script path:

```powershell
.ai/skills/xerahs-blog-drafting/scripts/upsert-blog-draft.ps1
```

Behavior:
- Creates `docs/blog/YYYY/YYYY-MM/` if it does not exist.
- Creates `blog-YYYYMMDD.md` with the standard daily draft template if it does not exist.
- Prints the resolved file path.
- When `-Section` and `-Bullet` are provided, appends a deduplicated bullet to that section and removes the section placeholder.

Supported append sections:
- `Features`
- `Fixes`
- `Build and Tooling`
- `Commits Reviewed`
- `Notes`
