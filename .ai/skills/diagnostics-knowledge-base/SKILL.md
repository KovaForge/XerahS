---
name: diagnostics-knowledge-base
description: Query, import into, or annotate the XerahS crash-report knowledge base on Neon (users' shared logs, crash signatures, root causes, fixes). Use when triaging XerahS failures, asking what users hit, or recording a fix.
---

# Diagnostics knowledge base

Users share scrubbed logs, system data, monitor layout, hardware and FFmpeg details from the Debug tab. Reports land in the `diagnostics` schema of the Neon project `solitary-dust-20133298` (branch `production`). Failures are grouped into **crash signatures**; each signature carries curated knowledge (root cause, fix, fixed-in version, commit). Retention is permanent.

## Connect

From `web/functions/diagnostics/` (its own `.neon` link and `neon.ts`; run `neon env pull` there to get `.env.local`). `web/` itself is linked to the separate XerahS Cloud project.

```bash
cd web/functions/diagnostics
set -a; . ./.env.local; set +a
psql "$DATABASE_URL_UNPOOLED"
```

Or use the Neon MCP server's SQL tool against project `solitary-dust-20133298`. For a least-privilege agent login, grant a login role `diagnostics_reader` (read + search) or `diagnostics_curator` (also `annotate_signature`).

`neon cs` needs `--role-name neondb_owner` because the diagnostics roles are also branch roles. Do not print connection strings.

## Read

```sql
-- What is hurting users now (30-day volume, OS/arch spread, curated knowledge)
select signature_id, status, kind, reports_30d, os_families, process_archs, title, root_cause
from diagnostics.v_signatures order by reports_30d desc nulls last, last_seen_at desc limit 20;

-- One search over signatures, extracted events and raw log text.
-- websearch syntax: "exact phrase", -exclude, OR
select * from diagnostics.search('DuplicateOutput1 -HDR', 20);

-- Reports for a signature, with system data and monitor layout
select report_id, received_at, app_version, os_description, process_arch, display_count, displays, gpus, ffmpeg_version
from diagnostics.v_reports where primary_signature_id = 1 order by received_at desc;

-- Full scrubbed log of a report (file and line order)
select * from diagnostics.report_log('<report uuid>');

-- Sessions around a crash
select * from diagnostics.report_sessions where report_id = '<report uuid>' order by ordinal;
```

Signature kinds: `exception` (type + top frames), `abnormal_exit` (session ended without the clean-exit marker, or the next start found evidence of a crash; component + last message), `error_line` (logged failure without an exception). An `abnormal_exit` with no exception usually means a native crash (access violation, fail-fast) that .NET could not log.

Log timestamps are the user's local time; `extra->>'utcOffsetMinutes'` gives the offset when the app knew it. Imported reports have `extra->>'source' = 'import'`.

## Record what you learnt

After verifying a cause against current code (see `triage-runtime-logs`), write it back so the next agent starts from it:

```sql
select diagnostics.annotate_signature(
  1, 'claude',
  p_status => 'fixed',            -- new | investigating | known | fixed | wont_fix | noise
  p_root_cause => '…',
  p_fix_summary => '…',
  p_fixed_in_version => '0.32.0',
  p_fix_commit => '824b9ba6',
  p_issue_url => null,
  p_notes => null,
  p_tags => array['windows', 'dxgi']);
```

Null arguments keep existing values. A `fixed` signature that is reported again from `fixed_in_version` or later reopens as `investigating` automatically; reports from older builds do not.

## Import logs a user sent by other means

```bash
cd web
node scripts/import-diagnostics-log.ts --dry-run <log files>   # inspect the scrubbed payload first
node scripts/import-diagnostics-log.ts --comment "GitHub #123" <log files>
```

The importer scrubs with the same rules as the app (`web/functions/diagnostics/analysis.ts`), rebuilds sessions, events and the monitor layout from log lines, and is idempotent per file content.

## Schema changes

Add a new file under `web/db/diagnostics/migrations/` (never edit an applied one), test it on a throwaway branch (`neon branches create --name <tmp> --parent production`), then from `web/` run `node scripts/db-migrate.ts --dir db/diagnostics/migrations --env functions/diagnostics/.env.local`. CI applies every migration to Postgres 17 and runs `web/db/diagnostics/tests/diagnostics_test.sql`.

Redeploy the ingest Function from `web/functions/diagnostics/` with `neon deploy --env .env.local`.

## Removal

Nothing expires. Remove data only when the owner asks: `diagnostics.purge_report(report_id)` or `diagnostics.purge_install(install_id)`. Signatures and their knowledge survive report purges.
