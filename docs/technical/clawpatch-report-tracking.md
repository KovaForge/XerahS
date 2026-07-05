# Clawpatch report tracking policy

**Status:** Active policy since 2026-07-05 (introduced by Milena after a sweep
session revealed that every other checkout's `.clawpatch/` was being
silently dropped from git).

## Decision

Track clawpatch reports as a durable artefact in this repository, but do
not track the runtime workspace files. Concretely, only ignore the
subdirectory entries below — keep everything under `.clawpatch/reports/`
visible to git.

| Path | In git? | Why |
|------|---------|-----|
| `.clawpatch/reports/*.md` | ✅ Yes | Durable audit trail of what clawpatch said each run. Backs the `source` and `report` references in `docs/reports/hourly_review_state.json`. |
| `.clawpatch/features/` | ❌ No | Per-feature JSON blobs. Regurgitated per run; no durable knowledge that isn't already in the report. |
| `.clawpatch/findings/` | ❌ No | Same — recomputed per run from the same feature JSONs. |
| `.clawpatch/locks/` | ❌ No | Per-process lock files. Would conflict on merge. |
| `.clawpatch/patches/` | ❌ No | Scratch output of `clawpatch fix`. Each patch is one-shot and consumed by the next build. |
| `.clawpatch/runs/` | ❌ No | Per-invocation log (startedAt, claimedFeatureIds, headSha). Regenerated per `clawpatch review`. |
| `.clawpatch/config.json` | ❌ No | Workspace-local (paths to `.env.local`, picked-feature manifest). Per-clone. |
| `.clawpatch/project.json` | ❌ No | Workspace-local projection config. Per-clone. |

This split is enforced by the project `.gitignore` (the rule lives there,
not in `.git/info/exclude`, so every checkout agrees).

## Why this matters

The hourly sweep (`xerahs-review` skill) reads from
`.clawpatch/reports/*.md` during Step 4.5 ingest. Without git tracking,
two consequences fall out:

1. **Lost auditability.** If a finding becomes a follow-up that the
   tracker records, the report that triggered it exists only on the
   machine that ran clawpatch. A reviewer six months later cannot read
   what clawpatch actually said — only the alias and a summary line.

2. **Silently different state across clones.** The historical
   arrangement (an entry in `.git/info/exclude`) was per-clone; each
   developer had a different view of "what files exist." Promoting the
   ignore rule to the project `.gitignore` makes the repo behave the
   same way for everyone.

Reports themselves are small (≤ a few hundred lines each), grow only on
sweep runs (a few per day at most), and never conflict on merge because
their filenames include the UTC timestamp (e.g.
`20260705T041158-efccfc.md`).

## Operational notes

- The pre-commit hook (`.githooks/pre-commit.bash`) already
  lints markdown mojibake — it will run on new report entries the same
  way it does for tracker entries.
- Don't `git add .clawpatch` blindly; the runtime artefacts in
  `features/`, `findings/`, `locks/`, `patches/` should stay ignored.
  Stage only `.clawpatch/reports/*.md` (the sweep already does this
  via its `git-<agent> add` calls).
- If a future clawpatch version emits more durable artefacts
  (for example, a per-finding JSON ledger), add a tracking rule here
  before lifting the ignore.

## Owner

`xerahs-review` skill (`sweep_owner` in its front matter). Step 4.5 of
the skill should `git-milena add .clawpatch/reports/<ts>-<hash>.md` after
ingesting findings.
