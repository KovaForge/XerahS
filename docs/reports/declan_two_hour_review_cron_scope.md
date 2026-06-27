# XerahS Two-Hour Review Cron Scope

Create your own version of the XerahS review cron job and run it every 2 hours.

## Local Paths

- Repo root: `/Users/mike/Projects/KovaForge/xerahs`
- Main solution: `/Users/mike/Projects/KovaForge/xerahs/XerahS.sln`
- Review tracker: `/Users/mike/Projects/KovaForge/xerahs/docs/reports/hourly_review_tracker.md`
- Submodule: `/Users/mike/Projects/KovaForge/xerahs/ShareX.ImageEditor`
- Submodule solution: `/Users/mike/Projects/KovaForge/xerahs/ShareX.ImageEditor/ShareX.ImageEditor.sln`
- Build/test logs directory: `/tmp/xerahs-hourly-sweep`

## Scope

- Work from `/Users/mike/Projects/KovaForge/xerahs`.
- Fetch and sync `develop` with `origin/develop`.
- Check upstream drift against `upstream/develop`.
- Check submodule state, especially `/Users/mike/Projects/KovaForge/xerahs/ShareX.ImageEditor`.
- Pick one focused risk area per run instead of broad churn.
- Inspect the code path and make a small fix only if there is a real issue.
- Avoid unrelated refactors.
- Build `/Users/mike/Projects/KovaForge/xerahs/XerahS.sln`.
- Run the relevant test suite.
- Bump patch version only when a code fix lands.
- Update `/Users/mike/Projects/KovaForge/xerahs/docs/reports/hourly_review_tracker.md` with timestamp, area reviewed, result, commit, and verification proof.
- Return proof: commit hash, changed files, build/test commands, and log paths.

## Cadence

Run every 2 hours.

## Proof Standard

Every run should report:

- Area reviewed.
- Whether a code change was made.
- Commit hash if a fix landed.
- Changed files.
- Build command and log path.
- Test command and log path.
- Any blocker that prevented a complete run.

Adapt the review area selection to your own judgement, but keep the same proof standard.
