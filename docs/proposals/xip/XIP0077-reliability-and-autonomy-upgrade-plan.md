# XIP0077 Reliability & Autonomy Upgrade Plan

**Status**: Proposed  
**Priority**: High  
**Area**: DevOps | Automation | OpenClaw | CI/CD | Operational Reliability  
**Related**: XIP0063, XIP0076  
**Created**: 2026-06-12  
**Author**: Fable 5 (one-shot run, BriarForge queue)

## Summary

This XIP plans upgrades to the four XerahS operational workflows — pre-release pipeline, URL publishing, issue monitoring, hourly sweep — plus the scheduler layer they all depend on. The plan is based on an evidence snapshot verified 2026-06-12; **no changes were applied in the authoring run**. Illustrative patches appear as diffs only.

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

## 3. Prioritized upgrades

Every upgrade carries the five mandatory fields: **implementation steps**,
**success criterion** (binary), **owner** (human or cron), **timeout/checkpoint
rule**, **rollback plan**. P0 = unblocks live failures; P1 = prevents the
observed silent-failure class; P2 = hardening; P3 = hygiene.

### U1 (P0) — Stale-lock detection and safe self-recovery in the hourly sweep

**Problem.** O1/O2: a dead run's lock defers every later sweep forever; the
protocol's `trap … EXIT` cannot fire when each tool call is a fresh shell.
The skill already knows this ("Workspace lock lifetime in API/tool sessions")
but the fix today is "report and wait for a human".

**Implementation steps.**
1. Extend the lock check in `xerahs-hourly-sweep/SKILL.md` step 1: when the
   lock exists, parse `info` for `owner` and `pid`.
2. If `owner=declan` (agent-owned, directory form) AND the pid is not alive
   AND `started_utc` is older than 2× the sweep interval (8 h), rename — never
   delete — the lock to `.xerahs-workspace.lock.stale-<UTC-ts>` and proceed,
   emitting a `STALE_LOCK_RECOVERED` line in the run summary.
3. If `owner` is anyone else (e.g. `mikhail`, or a plain file with no pid),
   keep today's behavior: defer, exit 75, report owner.
4. Cap retained stale-lock quarantine dirs at 5 (oldest pruned).

Illustrative diff (SKILL.md lock protocol, step 1):

```diff
 if [ -e .xerahs-workspace.lock ]; then
-  echo "Workspace locked; deferring."
-  ...
-  exit 75
+  OWNER=$(sed -n 's/^owner=//p' .xerahs-workspace.lock/info 2>/dev/null)
+  PID=$(sed -n 's/^pid=//p' .xerahs-workspace.lock/info 2>/dev/null)
+  AGE_OK=$(find .xerahs-workspace.lock -maxdepth 0 -mmin +480 | wc -l)
+  if [ "$OWNER" = "declan" ] && [ -n "$PID" ] && ! kill -0 "$PID" 2>/dev/null && [ "$AGE_OK" -eq 1 ]; then
+    mv .xerahs-workspace.lock ".xerahs-workspace.lock.stale-$(date -u +%Y%m%dT%H%M%SZ)"
+    echo "STALE_LOCK_RECOVERED owner=$OWNER pid=$PID"
+  else
+    echo "Workspace locked by $OWNER; deferring."
+    exit 75
+  fi
 fi
```

- **Success criterion:** a synthetic dead-pid declan lock older than 8 h is
  quarantined and the sweep proceeds; a mikhail file-lock still defers. Both
  outcomes reproduced in a dry-run = pass.
- **Owner:** cron (the sweep itself executes the recovery); **human sign-off
  required before landing** because the lock protocol is shared with Mikhail
  (see §5).
- **Timeout/checkpoint rule:** lock parsing/recovery must complete in the first
  2 tool calls of a run; if recovery itself errors, fall back to defer+exit 75
  (never proceed with the lock in place).
- **Rollback plan:** revert the SKILL.md hunk; quarantined locks are renames,
  so any disputed recovery is restorable with a single `mv` back.

### U2 (P0) — Disk-space preflight gate for all four workflows

**Problem.** O4/O5: 387 MiB free. Full disk already corrupted the scheduler
store write and produces SQLite I/O test failures that masquerade as
regressions. Builds (`obj/`, binlogs) need GiBs.

**Implementation steps.**
1. Add a shared preflight snippet (new `skills/_shared/preflight-disk.sh`, or
   inline per SKILL.md): `df -k /` → if free < 2 GiB, abort with
   `PREFLIGHT_DISK_LOW <free>` before any git/build/upload action.
2. Wire it as step 0 of: hourly sweep, pre-release, KFIP, issue monitor; for
   URL publishing, gate only uploads of files larger than remaining free space
   (upload itself is not disk-hungry, the staged copy can be).
3. The abort message is a distinct token so the U3 watchdog (below) can count
   consecutive disk-aborts and escalate to Discord after 2.
4. Separately (human, one-off): free disk space now — candidates include the
   repo-root binlogs (U10) and `~/.openclaw/tmp` (507 entries). This is the
   open escalation already tracked from the 2026-06-12 doctor run.

- **Success criterion:** with a simulated `df` reporting <2 GiB, each pipeline
  exits at step 0 with `PREFLIGHT_DISK_LOW` and touches nothing; with ≥2 GiB it
  proceeds. Both branches demonstrated = pass.
- **Owner:** cron (gate executes in-pipeline); human owns the actual cleanup.
- **Timeout/checkpoint rule:** preflight must run and report within the first
  tool call; a preflight that itself errors is treated as "low disk" (fail
  closed).
- **Rollback plan:** delete the step-0 snippet from each SKILL.md; no state or
  behavior persists beyond the gate.

### U3 (P0) — Pipeline liveness watchdog (max-staleness alarm)

**Problem.** O3/O5/O6: the issue monitor was dead for 5 weeks and nothing
noticed; scheduler statuses are all `None`; origin push drift went unseen. The
ecosystem's failure mode is not loud crashes — it is *silence*.

**Implementation steps.**
1. New small script `scripts/xerahs-liveness-watchdog.py` (in the xerahs repo)
   with a static manifest of heartbeat files and max ages:
   - `~/.openclaw/state/xerahs-issue-monitor.json` → max 8 days
   - `docs/reports/hourly_review_state.json` (`last_updated` field) → max 12 h
   - `git log -1 --format=%ct` of `origin/develop` vs local `develop` → alert
     when origin is >7 days and >10 commits behind
   - newest `/tmp/xerahs-prerelease-pipeline/build-*.log` → max 8 days
2. On breach: post one Discord message to `channel:1489624037758861415` per
   breached item per 24 h (dedupe via its own tiny state file, written
   atomically — see U4 pattern).
3. Schedule daily via OpenClaw cron (or launchd, matching the doctor-snapshot
   pattern already in `~/Library/LaunchAgents`).
4. The watchdog never repairs anything; it only reports. Repairs stay with the
   pipelines (U1) or humans.

- **Success criterion:** with the May-9 state file as-is, a manual watchdog run
  emits exactly one issue-monitor staleness alert; after touching the file to
  now, the same run emits none. Both demonstrated = pass.
- **Owner:** cron (daily); human receives alerts in `#xerahs`.
- **Timeout/checkpoint rule:** whole run ≤ 60 s, no network calls except the
  Discord post; if Discord is unreachable, write the alert to
  `/tmp/xerahs-watchdog/last-failed-alert.txt` and exit non-zero (so the
  scheduler's own failure surface still has a trace).
- **Rollback plan:** disable the cron entry; the script is read-only on all
  pipeline state, so removal has no side effects.

### U4 (P1) — Issue-monitor state durability + path repair

**Problem.** O3/O7a + skill pitfalls: corrupt state triggers mass
re-escalation; SKILL.md's command references a non-existent script path, so a
fresh agent following instructions fails at step 2; unauthenticated runs rate-limit.

**Implementation steps.**
1. Fix SKILL.md execution path to
   `/Users/mike/Projects/KovaForge/skills/xerahs-issue-monitor/scripts/xerahs-issue-monitor.py`
   (verified to exist 2026-06-12).
2. In the script: write state atomically — `json.dump` to
   `xerahs-issue-monitor.json.tmp` then `os.replace()`; keep 3 rotated copies
   (`.1`, `.2`, `.3`) written on each successful run.
3. On state-load failure, restore the newest parseable rotation instead of
   resetting `seen` to `{}`; only reset if all rotations fail, and then emit
   `XERAHS_ISSUE_MONITOR_STATE_RESET` so the digest consumer knows the
   re-escalation burst is synthetic.
4. Preflight the GitHub token (`GITHUB_TOKEN`/`gh-vladislava` hosts.yml) and
   fail fast with `XERAHS_ISSUE_MONITOR_FAILED: no-auth` rather than burning
   the 60/h anonymous quota.

- **Success criterion:** (a) truncating the state file mid-byte and re-running
  produces a restore from rotation with no mass re-escalation; (b) the SKILL.md
  command runs as pasted on a clean shell. Both = pass.
- **Owner:** cron (weekly job, unchanged cadence); human reviews the one-time
  SKILL.md fix.
- **Timeout/checkpoint rule:** the script already has `--dry-run`; mandate a
  dry-run as the verification gate after any script edit. Runtime cap 5 min;
  state writes happen only after a fully successful classify+notify phase.
- **Rollback plan:** the script is a single file in git — revert the commit;
  rotations are additive files that can be deleted harmlessly.

### U5 (P1) — Upload retry with verification (URL publishing)

**Problem.** Network blips mid-upload currently yield a non-zero exit with an
ambiguous text fallback ("partial success" per the skill's own pitfall), and
nothing re-tries. ReClip's 300 s timeout kills slow transfers without retry.

**Implementation steps.**
1. Wrap the upload in a retry policy: 3 attempts, backoff 5 s / 20 s, fresh
   invocation each time (ReClip's 8-char random suffix makes retried names
   collision-free; orphaned partials on the host are inert).
2. After any reported success, verify: parse JSON strictly (U6), then
   `curl -sI` the returned URL expecting HTTP 200 and, for ReClip MP4s, the
   approved host `mike.getsharex.com`. Only then write job metadata.
3. On final failure: write `status=failed`, exit code, and the last stderr
   excerpt into the ReClip job JSON; never leave a job in a pending state.
4. Apply to both triggers: Trigger A (agent CLI use — encode the retry/verify
   loop in SKILL.md) and Trigger B (ReClip `run_xerahs_bridge()` /
   `upload_with_xerahs()` in `app.py`).

Illustrative diff (ReClip `app.py`, around the existing upload call):

```diff
-    result = upload_with_xerahs(file_path, display_name)
+    last_err = None
+    for attempt, delay in enumerate((0, 5, 20)):
+        time.sleep(delay)
+        try:
+            result = upload_with_xerahs(file_path, display_name)
+            verify_published_url(result["url"])   # HEAD 200 + host check
+            break
+        except (UploadError, VerificationError) as e:
+            last_err = e
+            log.warning("xerahs upload attempt %d failed: %s", attempt + 1, e)
+    else:
+        mark_job_failed(job_id, last_err)
+        raise RuntimeError(f"XerahS upload failed after 3 attempts: {last_err}")
```

- **Success criterion:** with the network dropped after attempt 1 and restored
  before attempt 2 (simulated), the job ends with a verified URL; with the
  network down throughout, the job JSON contains `status=failed` + diagnostic
  and no pending job remains. Both = pass (this is Scenario S1, §4).
- **Owner:** cron/app (ReClip LaunchAgent path) and agent (Trigger A); human
  sign-off required for the `app.py` change (published-URL adjacent, §5).
- **Timeout/checkpoint rule:** per-attempt timeout stays `XERAHS_TIMEOUT_SECONDS`
  (300 s); whole retry envelope ≤ 12 min; checkpoint = job JSON updated after
  every attempt, not only at the end.
- **Rollback plan:** revert the `app.py` commit and SKILL.md hunk; behavior
  returns to single-attempt. Job-JSON schema additions are backward-compatible
  (extra keys only).

### U6 (P1) — Contract validation at every external boundary

**Problem.** Three boundaries consume external output with no schema check:
the `xerahs` CLI JSON (URL publishing), the GitHub issues API (issue monitor),
and `dotnet build`/`test` log shapes (pre-release/sweep treat known transient
errors via grep folklore). A quiet upstream contract change becomes silent
misbehavior. Status: hypothesized (no observed contract break yet) — this is
the prevention investment.

**Implementation steps.**
1. URL publishing: after `--json`, require parseable JSON with non-empty `url`
   (https), `filename` containing a file extension; otherwise treat as failure
   even when exit code is 0. Encode in SKILL.md (Trigger A) and `app.py`
   (Trigger B).
2. Issue monitor: validate each issue dict for the exact fields the classifier
   reads (`number`, `updated_at`, `comments`, `labels[].name`, `user.login`)
   before classification; on missing/renamed fields raise `ContractError`,
   print `XERAHS_ISSUE_MONITOR_FAILED: contract <details>`, and **skip the
   state write** so `seen` is preserved (no false "all clear", no mass churn).
3. Pre-release/sweep: keep the documented NETSDK1004/CS0006/restore retries,
   but bound them — one retry per class, then report as blocker. Add the
   missing case: `dotnet test` exiting non-zero with zero parsed test results
   (runner crash) must report "runner crash", not "tests failed".
4. Document each boundary's expected schema in a short
   `docs/technical/external-contracts.md` so future validators have one source
   of truth.

- **Success criterion:** feeding the issue monitor a fixture with `labels` as
  objects-missing-`name` produces `XERAHS_ISSUE_MONITOR_FAILED: contract …`,
  an unchanged state file, and a Discord failure alert; feeding valid fixtures
  produces a normal run. Both = pass (this is Scenario S2, §4).
- **Owner:** cron (validators run inside the pipelines).
- **Timeout/checkpoint rule:** validation adds no network calls and must be
  O(payload); any validator exception = fail closed (treat as contract
  failure, never proceed unvalidated).
- **Rollback plan:** validators are pure additions guarded at entry points —
  revert the commits; no data migration involved.

### U7 (P2) — Remote/identity matrix + standardized push verification

**Problem.** O6 + skill pitfalls: six remotes (`origin`=vladislava, `declan`,
`mikhail`, `aoife`, `vladislava`, `upstream`) with per-agent wrappers; each
skill re-derives "which ref proves my push" and gets it subtly wrong (stale
`declan/develop` accepted as proof; `origin` 14 behind unnoticed; SSH auth
failures retried with alternate syntaxes).

**Implementation steps.**
1. Add `docs/technical/remote-identity-matrix.md`: one row per agent — wrapper,
   remote name, SSH host alias, the exact ref to verify after push, and the
   rule "fetch before compare; a stale remote-tracking ref is not proof".
2. Add `scripts/verify-push.sh <remote> <branch>`: fetches the remote, compares
   `HEAD` to `refs/remotes/<remote>/<branch>`, prints `PUSH_VERIFIED` or
   `PUSH_NOT_VERIFIED <details>`; single-purpose so unattended approval gates
   are not tripped by compound commands (a documented sweep pitfall).
3. Reference the script from all four pipeline SKILL.mds in place of their
   bespoke verification prose.
4. Classify `Permission denied (publickey)` as a hard blocker in all skills
   (the sweep already does; pre-release and KFIP do not).

- **Success criterion:** `verify-push.sh declan develop` prints `PUSH_VERIFIED`
  on the current repo, and `verify-push.sh origin develop` prints
  `PUSH_NOT_VERIFIED` (it is 14 behind today) — both observed outputs match
  reality = pass.
- **Owner:** cron (pipelines call the script); human writes the matrix once.
- **Timeout/checkpoint rule:** script ≤ 30 s (one fetch); on fetch failure
  print `PUSH_NOT_VERIFIED fetch-failed` — never report success on stale data.
- **Rollback plan:** delete the script + doc, restore prior SKILL.md prose
  (kept in git history).

### U8 (P2) — Durable-write hygiene for tracker/state/changelog

**Problem.** Skill-documented corruption classes: unquoted heredocs executing
backticks in changelog/tracker content; JSON patches double-escaping Unicode
or truncating `last_runs`; `patch`-tool failures on repeated lines.

**Implementation steps.**
1. Add `scripts/append-md.sh <file>`: reads stdin, appends, then re-reads the
   appended window and fails loudly on mismatch — replacing ad-hoc `cat >>`
   heredocs in the sweep and pre-release changelog steps.
2. Add a `.githooks/pre-commit` check (the hooks dir already exists): if
   `docs/reports/hourly_review_state.json` is staged, run
   `python3 -m json.tool` on it and verify `last_runs` length did not shrink
   by more than 1 vs HEAD; reject the commit otherwise.
3. SKILL.md edits: make the quoted-heredoc rule a hard rule with the helper as
   the default path, not a pitfall note.

- **Success criterion:** a staged state JSON that is invalid (or drops 3
  `last_runs` entries) is rejected by pre-commit; a valid append passes. Both
  = pass.
- **Owner:** cron (hooks/helpers run inside pipeline commits).
- **Timeout/checkpoint rule:** hook ≤ 5 s; if the hook itself crashes, it must
  exit non-zero (fail closed) — a broken guard must not wave commits through.
- **Rollback plan:** hooks are opt-in via `.githooks` setup; remove the check
  from `pre-commit` to restore prior behavior. Helper scripts are additive.

### U9 (P2) — Skill↔repo drift lint (wire into skill-sustainer)

**Problem.** O7: dead script path, case-drifted workspace path, duplicated
Step 9 block, "hourly" misnomer. Each is small; together they make every
fresh-context run roll dice. A sustain audit system already exists in
`/Users/mike/Projects/KovaForge/skills` (`sustain.sh`, live since 2026-06-12)
— extend it rather than build new.

**Implementation steps.**
1. Add checks to the sustainer for the five xerahs skills: (a) every absolute
   path mentioned in SKILL.md exists on disk (allowlist for `/tmp/...`
   templates); (b) no duplicated H3 headings; (c) workspace paths are
   byte-exact `…/KovaForge/xerahs` (case-sensitivity portability); (d)
   schedule words in the name/description match the declared cadence.
2. File the four known O7 defects as sustainer proposals immediately (two are
   one-line fixes: path + dedupe).
3. Sustainer remains propose-only (its existing gate); a human approves merges.

- **Success criterion:** sustainer run flags exactly the four O7 defects on
  current skills, and zero after the fixes land. Both = pass.
- **Owner:** cron (sustainer audit), human (approval of proposals).
- **Timeout/checkpoint rule:** lint is offline/static, ≤ 30 s for all five
  skills; lint errors never block pipeline runs — they only file proposals.
- **Rollback plan:** remove the xerahs checks from the sustainer config; the
  proposals are files that can be closed without action.

### U10 (P3) — Artifact hygiene: binlogs, stray logs, tmp rotation

**Problem.** O8 + O4: 3 MB of unignored binlogs in repo root, a 228 KiB
release log, `/tmp/xerahs-*` logs that accumulate without rotation — all on a
97%-full disk.

**Implementation steps.**
1. Add `*.binlog` and `release-run-*.log` to `.gitignore` (root section).
2. Human deletes the existing `axaml_error.binlog`, `build.binlog`,
   `release-run-25249540090-job-74039372317.log` after confirming they are not
   referenced by open investigations.
3. Add to the sweep's step 1: prune `/tmp/xerahs-hourly-sweep/` and
   `/tmp/xerahs-prerelease-pipeline/` files older than 14 days (a `find -mtime
   +14 -delete` scoped strictly to those two directories).
4. The U3 watchdog reports the count/size of `/tmp/xerahs-*` so growth is
   visible.

- **Success criterion:** `git status` shows no binlog noise after step 1–2, and
  a sweep run on a fixture dir with 15-day-old files removes only those files.
  Both = pass.
- **Owner:** human (one-off deletes, .gitignore), cron (rotation thereafter).
- **Timeout/checkpoint rule:** prune step ≤ 10 s and is hard-scoped to the two
  named `/tmp` dirs — any path-expansion failure aborts the prune, never
  widens it.
- **Rollback plan:** .gitignore line removal restores tracking-eligibility;
  rotation is removable from SKILL.md; deleted logs are accepted as
  unrecoverable (hence the human confirmation in step 2).

---

## 4. Simulated failure scenarios (walked against the upgraded design)

### S1 — Network blip mid-upload (URL publishing, ReClip Trigger B)

Setup: ReClip finishes downloading `clip.mp4`, hands off via
`run_xerahs_bridge()`. U2, U5, U6 are in place. The network drops 60% into the
first upload and recovers 40 s later.

1. **Preflight (U2):** disk gate passes (file fits in free space). Proceed.
2. **Attempt 1:** `xerahs upload clip.mp4 --name clip-a1b2c3d4.mp4 --json
   --as-file` — connection reset mid-transfer. CLI exits non-zero; stderr
   captured into the job log. The orphaned partial on the host is inert (the
   host only publishes completed objects; even if it published a truncated
   one, the next step's verification would catch it because the retried name
   differs and only the verified URL is recorded).
3. **Checkpoint (U5 rule):** job JSON updated — `attempt: 1, status: retrying,
   stderr: "connection reset…"`. No pending-forever state exists at any point.
4. **Attempt 2 (after 5 s backoff):** network still down → same handling,
   `attempt: 2` checkpointed.
5. **Attempt 3 (after 20 s backoff):** network is back. Upload completes. JSON
   parsed strictly (U6): `url` present, https, host `mike.getsharex.com`,
   filename ends `.mp4`. `curl -sI` returns HTTP 200.
6. Job JSON gets the verified URL; ReClip pipeline continues normally.

**Outcome: recovered.** Total delay ≈ 65 s + transfer time, within the 12-min
retry envelope.

Counterfactual branch — network stays down: attempt 3 fails, the `else` branch
marks the job `status=failed` with exit code + last stderr, raises the
RuntimeError that ReClip already knows how to surface, and the U3 watchdog's
daily pass plus the job JSON give the operator the exact diagnostic.
**Outcome: failed safely + diagnostic emitted.** Under today's design, by
contrast, the single attempt fails and the skill's own pitfall notes the
result can be misread as partial success.

### S2 — Upstream API contract change (issue monitor)

Setup: GitHub changes the issues list payload — `labels` entries no longer
carry a top-level `name` (hypothetical rename to `label_name`). U3, U4, U6 are
in place. The weekly run fires Sunday.

1. **Preflight (U4):** token resolves; rate limit OK; state file loads (seen
   map = 9 issues).
2. **Fetch:** HTTP 200 — nothing transport-level is wrong. This is the
   dangerous case: without U6 the classifier would either crash with a raw
   `KeyError` *after* partial processing, or silently classify every issue's
   labels as empty → wrong severities → wrong digest (and state written, so
   the error compounds silently week over week).
3. **Contract validation (U6):** the per-issue validator checks
   `labels[].name` before any classification. First issue fails →
   `ContractError("issue 246: labels[0] missing 'name'; got keys
   ['label_name', 'color']")`.
4. **Fail-closed handling (U6 step 2):** script prints
   `XERAHS_ISSUE_MONITOR_FAILED: contract issue 246: labels[0] missing 'name'…`
   and exits non-zero. **The state file is not written** — `seen` still holds
   the pre-failure baseline; rotations (U4) are untouched.
5. **Alerting:** the OpenClaw job captures the `XERAHS_ISSUE_MONITOR_FAILED`
   token (the skill already mandates this reporting contract) and the run
   report reaches `#xerahs`. Even if that delivery layer is itself broken —
   the O3/O5 lesson — the U3 watchdog independently alerts within 8 days
   because the state file's `last_run_at` goes stale.
6. **Repair + resume:** a human (or a queued fix run) updates the field
   mapping; the next run validates clean, classifies against the *preserved*
   `seen` map, and emits a normal digest — no mass re-escalation burst,
   because state was never reset.

**Outcome: failed safely + diagnostic emitted**, with bounded
time-to-detection (≤ 8 days via watchdog even under total notification
failure) and clean resume semantics.

---

## 5. Needs human sign-off (surface, don't act)

Per the run contract, nothing below was changed; each item alters shared
protocol, published-URL behavior, or release cadence and needs Mike's (or the
named agent owner's) explicit approval:

1. **U1 lock-protocol change** — the lock is a shared contract with Mikhail's
   manual-review workflow. The proposed rule never auto-clears a non-declan or
   file-form lock, but Mikhail should confirm the 8-hour expiry and the
   quarantine-rename semantics.
2. **U5 ReClip `app.py` retry/verify change** — touches the automated upload
   path that produces published URLs. Retries add new HEAD requests against
   `mike.getsharex.com` and can re-upload after ambiguous failures (new URL
   each time). No URL format changes, but it is publish-adjacent.
3. **Immediate manual action: the live stale lock (O1).** The sweep is blocked
   *right now*. Per the current protocol only a human should remove another
   run's lock: `rm -rf /Users/mike/Projects/KovaForge/xerahs/.xerahs-workspace.lock`
   after confirming pid 47912 is dead (verified dead 2026-06-12 14:30 AWST).
4. **Immediate manual action: disk cleanup (O4).** 387 MiB free is below safe
   operating floor for every workflow here. Deleting the repo-root binlogs and
   pruning `~/.openclaw/tmp` are candidates; both delete data and need a human.
5. **Release/publish cadence** — no changes proposed; listed to confirm the
   boundary was honored. Weekly pre-release (Wed 20:00 per SKILL.md vs Sat
   18:00 per the cron backup — see drift note D5) should be reconciled by a
   human since it *is* release cadence.

## 6. Drift findings (skill vs repo/scheduler — repo trusted)

- **D1 (observed):** `xerahs-issue-monitor/SKILL.md` step 2 runs the script
  from `skills/vladislava-xerahs-issue-monitor/scripts/…` — path does not
  exist; the script lives in `skills/xerahs-issue-monitor/scripts/`. → U4/U9.
- **D2 (observed):** `xerahs-hourly-sweep/SKILL.md` workspace is
  `…/KovaForge/XerahS` (capital X); on-disk dir is `…/KovaForge/xerahs`. Works
  only because APFS is case-insensitive. → U9.
- **D3 (observed):** `xerahs-hourly-sweep/SKILL.md` contains two identical
  "Step 9: Generate run summary" blocks plus orphaned list fragments — a
  paste-over defect that also breaks the "steps 1 through 10" completeness
  rule's numbering. → U9.
- **D4 (observed):** the skill is named "hourly sweep" but declares and runs a
  4-hour cadence. Cosmetic, but staleness thresholds (U3) must key off the real
  cadence. → U9.
- **D5 (observed):** pre-release SKILL.md says "Weekly Wednesday 20:00 Perth";
  the scheduler backup (`jobs.json.bak`, 2026-06-06) has the job at
  `0 18 * * 6` (Saturday 18:00 Perth). Release-cadence question → human, §5.5.
- **D6 (observed):** pre-release SKILL.md numbers build/test (steps 2–3)
  *before* upstream sync (step 4), so a green build does not attest to the
  merged tree it then pushes. Reorder when next editing the skill (fold into
  U6's bounded-retry edit).

## 7. Sequencing summary

Week 1 (unblock + see): human actions §5.3 and §5.4, then U1, U2, U3.
Week 2 (stop the silent class): U4, U5, U6.
Week 3+ (harden + tidy): U7, U8, U9, U10.

The dependency edges that matter: U3's thresholds depend on D4/D5 cadence
truth; U5's verification depends on U6's JSON contract; U9 lands fastest by
extending the existing skill-sustainer rather than new tooling.
