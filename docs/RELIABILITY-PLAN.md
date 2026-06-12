# XerahS Reliability & Autonomy Upgrade Plan

Date: 2026-06-12 (all evidence verified on this date)
Author: Fable 5 (one-shot run, BriarForge queue)
Scope: the four XerahS operational workflows — pre-release pipeline, URL
publishing, issue monitoring, hourly sweep — plus the scheduler layer they all
depend on. This document plans upgrades; **no changes were applied in this run**.
Illustrative patches appear as diffs only.

Workflow sources of truth (verified 2026-06-12):

- `/Users/mike/Projects/KovaForge/skills/xerahs-prerelease-pipeline/SKILL.md`
- `/Users/mike/Projects/KovaForge/skills/xerahs-url-publishing/SKILL.md`
- `/Users/mike/Projects/KovaForge/skills/xerahs-issue-monitor/SKILL.md`
- `/Users/mike/Projects/KovaForge/skills/xerahs-hourly-sweep/SKILL.md`
- `/Users/mike/Projects/KovaForge/skills/xerahs-kfip-pipeline/SKILL.md` (read for
  context; KFIP shares pre-release's sync/build/test machinery, so pre-release
  upgrades cover it)
- `/Users/mike/Projects/KovaForge/xerahs/` — the repo itself (git, branch
  `develop`)

---

## 1. Observed state (evidence snapshot, 2026-06-12)

Every claim below was verified by command on 2026-06-12. "Observed" failure
modes in §2 trace back to this table; everything else is marked "hypothesized".

| # | Observation | Evidence |
|---|---|---|
| O1 | **Stale workspace lock is live right now.** `.xerahs-workspace.lock/` (owner=declan, pid=47912, started 2026-06-11T22:01:05Z = 06:01 AWST today) exists in the repo root; PID 47912 is dead. Every future sweep will see the lock and defer (exit 75) until a human removes it. | `cat .xerahs-workspace.lock/info`; `ps -p 47912` → not running |
| O2 | **The 06:01 sweep died mid-run.** Lock was created 06:01 AWST 2026-06-12 but the tracker's last entry is 2026-06-10 18:05 AWST and `hourly_review_state.json` `last_updated` is 2026-06-10 18:05. The run acquired the lock, then died before any durable write. | `grep "^### " docs/reports/hourly_review_tracker.md \| tail`; state JSON `last_updated` |
| O3 | **Issue monitor silent for ~5 weeks.** A weekly job, but `/Users/mike/.openclaw/state/xerahs-issue-monitor.json` has `last_run_at: 2026-05-09T03:34:44Z` and mtime May 9. No failure was surfaced anywhere in that window. | `cat`/`ls -la` on the state file |
| O4 | **Disk at 97% (387 MiB free).** Directly explains the sweep's known environmental SQLite "disk I/O error" test failures (5 tests, noted in `next_candidates`) and is the likely cause of O5. | `df -h /` |
| O5 | **Scheduler store had an interrupted write and was migrated.** `~/.openclaw/cron/jobs.json` is gone; an orphaned `jobs.json.<pid>.<hash>.tmp` (0 bytes), `jobs.json.bak` (Jun 6) and `jobs.json.migrated`/`jobs-state.json.migrated` (Jun 11) remain. In the backup, every job's `lastStatus` is `None` — the scheduler keeps no usable success/failure history where an operator can see it. | `ls -la ~/.openclaw/cron/`; parse of `jobs.json.bak` |
| O6 | **`origin/develop` is 14 commits behind local `develop`**, while `declan/develop` == HEAD. Declan's push path works; Vladislava's origin push (the pre-release pipeline's job) has not landed since the last upstream merge. | `git rev-parse HEAD declan/develop origin/develop` |
| O7 | **Skill↔repo drift.** (a) issue-monitor SKILL.md points at `skills/vladislava-xerahs-issue-monitor/scripts/…` which does not exist (script actually lives in `skills/xerahs-issue-monitor/scripts/`); (b) hourly-sweep SKILL.md uses `/Users/mike/Projects/KovaForge/XerahS` (capital X) — same dir only because APFS is case-insensitive; (c) hourly-sweep SKILL.md contains a duplicated "Step 9" block (paste-over defect); (d) the skill is named "hourly" but runs every 4 hours. | `ls` of both paths; SKILL.md lines 33, 274/307 |
| O8 | **Untracked build debris in repo root.** `axaml_error.binlog` + `build.binlog` (3 MB, not git-ignored) and a 228 KiB release-run log (ignored) sit in the repo root, contributing to disk pressure and clone noise. | `du -sh`, `git check-ignore` |

Per the run contract: where a skill contradicts the repo, the repo is trusted
and the drift recorded (→ O7, upgrade U9).

---

## 2. Failure-mode table (4/4 workflows)

Status legend: **observed** = traced to §1 evidence or to a pitfall the skill
itself records from past runs; **hypothesized** = plausible, no failure history.

| Workflow | Failure mode | Status | Addressed by |
|---|---|---|---|
| Hourly sweep | Stale lock from a dead run blocks all subsequent sweeps indefinitely; no one is alerted | observed (O1, O2) | U1, U3 |
| Hourly sweep | Run dies mid-flight (session kill, tool-call ceiling) leaving no durable trace | observed (O2) | U1, U3 |
| Hourly sweep | Disk-full SQLite "disk I/O error" test failures misread as regressions | observed (O4, skill `next_candidates`) | U2 |
| Hourly sweep | SSH auth failure on `declan` remote treated as retryable; success claimed from stale refs | observed (skill pitfall) | U7 |
| Hourly sweep | Tracker/state JSON corrupted by heredoc command-substitution or double-escaped Unicode | observed (skill pitfalls) | U8 |
| Pre-release | Vladislava `origin` push silently not happening; fork drifts ahead locally | observed (O6) | U3, U7 |
| Pre-release | Release build fails NETSDK1004/CS0006 (missing assets/ref metadata) and is misread as regression | observed (skill pitfall) | U6 |
| Pre-release | Submodule pointer conflict (ShareX.ImageEditor) wedges the upstream merge | observed (skill pitfall) | U7 |
| Pre-release | Changelog append via unquoted heredoc executes backticks in release notes | observed (skill pitfall) | U8 |
| URL publishing | Network blip mid-upload: non-zero exit with ambiguous "partial success" text fallback | observed (skill pitfall: JSON parsing failure) | U5, U6 |
| URL publishing | `--name` missing extension → extensionless published URL | observed (skill pitfall) | U6 |
| URL publishing | Upload endpoint returns non-approved MP4 host (contract change at the host) | hypothesized (guard exists in ReClip only) | U5, U6 |
| URL publishing | `XERAHS_COPY_TO_WATCH=true` with watch folder unset → hard error at handoff time | observed (skill pitfall) | U6 |
| Issue monitor | Job silently stops running; nobody notices for weeks | observed (O3, O5) | U3 |
| Issue monitor | State file corruption resets `seen` → one-shot mass re-escalation | observed (skill pitfall) | U4 |
| Issue monitor | GitHub API contract change (field rename/removal) crashes classification or, worse, silently mis-classifies | hypothesized | U6 |
| Issue monitor | Unauthenticated rate limit (60 req/h) starves the fetch | observed (skill pitfall) | U4 |
| Issue monitor | SKILL.md points at a non-existent script path → any fresh operator/agent run fails at step 2 | observed (O7a) | U9 |
| All (scheduler layer) | Cron store write interrupted (full disk) → orphaned tmp, lost/None statuses, no run history | observed (O5) | U2, U3 |

---
