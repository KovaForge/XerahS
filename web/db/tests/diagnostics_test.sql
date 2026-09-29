-- Behavioural tests for db/migrations/0001_diagnostics.sql.
-- Run against a scratch database after applying the migration:
--   for f in db/migrations/*.sql; do psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f "$f"; done
--   psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f db/tests/diagnostics_test.sql
-- Any failed assertion raises and aborts the run.

update diagnostics.settings set allow_ingest = true;

create temporary table t_report (template jsonb);
insert into t_report values ($json$
{
  "schemaVersion": 1, "redactionVersion": 1, "trigger": "manual",
  "installId": "11111111-1111-4111-8111-111111111111",
  "clientReportId": "22222222-2222-4222-8222-222222222222",
  "app": {"version": "0.31.3", "buildFlavor": "Release", "dotnetVersion": "10.0.12"},
  "os": {"family": "windows", "description": "Microsoft Windows 10.0.26200", "version": "10.0.26200",
         "architecture": "X64", "processArchitecture": "X64", "elevated": false},
  "capture": {"captureBackend": "dxgi", "ffmpeg": {"version": "7.1", "source": "bundled", "hwEncoders": ["h264_nvenc"]}},
  "hardware": {"cpuModel": "AMD Ryzen 9", "logicalCores": 32, "ramGbBucket": 64, "gpus": [{"name": "NVIDIA RTX 4090"}]},
  "displays": [{"ordinal": 0, "width": 3840, "height": 2160, "scale": 1.5, "rotation": 0, "isHdr": false},
               {"ordinal": 3, "width": 1440, "height": 2560, "scale": 1.0, "rotation": 270, "isHdr": false}],
  "window": {"kind": "24h", "start": "2026-09-28T14:00:00Z", "end": "2026-09-29T14:00:00Z"},
  "sessions": [{"startedAt": "2026-09-29T12:11:48Z", "endedAt": "2026-09-29T13:45:18Z", "appVersion": "0.31.3",
                "exitKind": "abnormal", "lastComponent": "CaptureFullScreenDxgi",
                "lastMessageTemplate": "EnumDisplaySettings orientation for <display> => dmDisplayOrientation=3, mappedRotation=Rotate270"}],
  "events": [
    {"kind": "abnormal_exit", "component": "CaptureFullScreenDxgi",
     "messageTemplate": "EnumDisplaySettings orientation for <display> => dmDisplayOrientation=3, mappedRotation=Rotate270"},
    {"kind": "error_line", "component": "DxgiOutputDuplicationHelper",
     "messageTemplate": "DuplicateOutput1 failed, using DuplicateOutput. HRESULT: [0x80070057]",
     "sampleMessage": "DuplicateOutput1 failed E_INVALIDARG", "occurrences": 12},
    {"kind": "exception", "exceptionType": "System.NullReferenceException", "messageTemplate": "Object reference not set",
     "topFrames": ["XerahS.Core.Foo.Bar()", "XerahS.Core.Foo.Baz()"]}
  ],
  "logs": [{"fileName": "XerahS-20260929.log",
            "content": "2026-09-29 13:45:18.110 - Capture start: Job=RectangleRegion\n2026-09-29 13:45:18.173 - CaptureFullScreenDxgi: EnumDisplaySettings orientation for DISPLAY4 => mappedRotation=Rotate270\n2026-09-29 13:45:28.441 - XerahS starting."}]
}
$json$);

create temporary table t_results (name text primary key, result jsonb);

create or replace function pg_temp.report(p_client_report_id text, p_patch jsonb default '{}')
returns jsonb language sql as $$
  select (select template from t_report) || jsonb_build_object('clientReportId', p_client_report_id) || p_patch
$$;

create or replace function pg_temp.token(p_byte text)
returns bytea language sql as $$ select decode(repeat(p_byte, 32), 'hex') $$;

do $$
declare
  a jsonb;
  dup jsonb;
  v_count integer;
  v_row record;
begin
  -- Normalization keeps identifiers and HRESULTs, collapses standalone numbers and handles.
  assert diagnostics.normalize_template('DuplicateOutput1 failed HRESULT [0x80070057] hwnd=0x17103C took 25 ms')
       = 'DuplicateOutput1 failed HRESULT [0x80070057] hwnd=<hex> took # ms',
    'normalize_template keeps identifiers and HRESULT codes';

  a := diagnostics.submit_report(pg_temp.report('22222222-2222-4222-8222-222222222222'), pg_temp.token('ab'), null);
  assert (a ->> 'duplicate')::boolean = false, 'first submit is new';
  insert into t_results values ('a', a);

  dup := diagnostics.submit_report(pg_temp.report('22222222-2222-4222-8222-222222222222'), pg_temp.token('ab'), null);
  assert (dup ->> 'duplicate')::boolean and dup ->> 'reportId' = a ->> 'reportId', 'resubmit is idempotent';

  insert into t_results values ('b',
    diagnostics.submit_report(pg_temp.report('33333333-3333-4333-8333-333333333333'), pg_temp.token('ab'), null));

  select count(*) into v_count from diagnostics.crash_signatures;
  assert v_count = 3, format('same failures group into 3 signatures, got %s', v_count);
  assert not exists (select 1 from diagnostics.crash_signatures where report_count <> 2), 'each signature counted twice';
  assert (select occurrence_count from diagnostics.crash_signatures where kind = 'error_line') = 24, 'occurrences summed';

  select * into v_row from diagnostics.v_reports where report_id = (a ->> 'reportId')::uuid;
  assert v_row.abnormal_exit_count = 1 and v_row.event_count = 3 and v_row.display_count = 2, 'report summary columns';
  assert v_row.primary_signature_id = (a ->> 'primarySignatureId')::bigint, 'primary signature returned';
  assert (select kind from diagnostics.crash_signatures where signature_id = v_row.primary_signature_id) = 'abnormal_exit',
    'abnormal exit outranks other events';
  assert (select reports_30d from diagnostics.v_signatures where signature_id = v_row.primary_signature_id) = 2,
    'daily rollup feeds v_signatures';

  -- Search spans knowledge base, events and raw log text.
  assert exists (select 1 from diagnostics.search('DuplicateOutput1', 20) where source = 'signature'), 'signature search';
  assert exists (select 1 from diagnostics.search('E_INVALIDARG', 20) where source = 'event'), 'event search';
  assert exists (select 1 from diagnostics.search('RectangleRegion', 20) where source = 'log'), 'log search';

  select count(*) into v_count from diagnostics.report_log((a ->> 'reportId')::uuid);
  assert v_count = 1, 'report_log returns the chunk';
  assert exists (
    select 1 from diagnostics.report_log_chunks
    where report_id = (a ->> 'reportId')::uuid and line_count = 3 and started_at < ended_at),
    'chunk records line count and timestamp range';
end;
$$;

-- Large logs split at line boundaries into <= 64 KB chunks.
do $$
declare
  c jsonb;
  v_row record;
begin
  c := diagnostics.submit_report(
    pg_temp.report('44444444-4444-4444-8444-444444444444', jsonb_build_object('logs', jsonb_build_array(jsonb_build_object(
      'fileName', 'big.log',
      'content', (select string_agg('2026-09-29 10:00:00.000 - line ' || i || ' ' || repeat('x', 40), E'\n')
                  from generate_series(0, 3999) i))))),
    pg_temp.token('ab'), null);
  select count(*) as n, max(length(content)) as m, sum(line_count) as l into v_row
  from diagnostics.report_log_chunks where report_id = (c ->> 'reportId')::uuid;
  assert v_row.n > 1 and v_row.m <= 65536 and v_row.l = 4000, format('chunking: %s', row_to_json(v_row));
end;
$$;

-- Knowledge base annotation, and a fixed signature that recurs in a newer build reopens.
do $$
declare
  v_sid bigint := (select (result ->> 'primarySignatureId')::bigint from t_results where name = 'a');
begin
  perform diagnostics.annotate_signature(v_sid, 'test',
    p_status => 'fixed',
    p_root_cause => 'DuplicateOutput1 native access violation on SDR x64 outputs',
    p_fixed_in_version => '0.32.0',
    p_fix_commit => '824b9ba6');
  assert exists (select 1 from diagnostics.search('access violation', 5) where signature_id = v_sid),
    'root cause text is searchable';

  -- An older, still-broken build reporting the same failure is not a regression.
  perform diagnostics.submit_report(
    pg_temp.report('55555555-5555-4555-8555-555555555550', '{"app": {"version": "0.31.3", "buildFlavor": "Release"}}'),
    pg_temp.token('ab'), null);
  assert (select status from diagnostics.crash_signatures where signature_id = v_sid) = 'fixed',
    'reports from versions before the fix keep the signature fixed';

  perform diagnostics.submit_report(
    pg_temp.report('55555555-5555-4555-8555-555555555555', '{"app": {"version": "0.32.1", "buildFlavor": "Release"}}'),
    pg_temp.token('ab'), null);
  assert (select status from diagnostics.crash_signatures where signature_id = v_sid) = 'investigating',
    'regression reopens a fixed signature';

  assert diagnostics.version_array('v0.32.1-beta') = array[0, 32, 1], 'version_array parses tags';
  assert diagnostics.version_array('unknown') is null, 'version_array rejects non-versions';

  begin
    perform diagnostics.annotate_signature(v_sid, ' ');
    assert false, 'annotate without author must fail';
  exception when raise_exception then
    null;
  end;
end;
$$;

-- Self-service delete needs the right token and cascades.
do $$
declare
  v_id uuid := (select (result ->> 'reportId')::uuid from t_results where name = 'b');
begin
  assert not diagnostics.delete_report_with_token(v_id, pg_temp.token('cd')), 'wrong token is rejected';
  assert diagnostics.delete_report_with_token(v_id, pg_temp.token('ab')), 'right token deletes';
  assert not exists (select 1 from diagnostics.report_events where report_id = v_id), 'events cascade';
  assert not exists (select 1 from diagnostics.report_keys where report_id = v_id), 'key removed';
end;
$$;

-- Rate limit: 10 per install per day (6 used above, including the deleted one).
do $$
declare
  i integer;
  v_limited boolean := false;
begin
  for i in 1 .. 10 loop
    begin
      perform diagnostics.submit_report(
        pg_temp.report(format('66666666-6666-4666-8666-%s', lpad(i::text, 12, '0'))), pg_temp.token('ab'), null);
    exception when others then
      assert sqlerrm = 'diagnostics_rate_limited', sqlerrm;
      v_limited := true;
      exit;
    end;
  end loop;
  assert v_limited, 'install rate limit applies';
end;
$$;

-- Ingest switch, partitions on demand, owner purge.
do $$
begin
  update diagnostics.settings set allow_ingest = false;
  begin
    perform diagnostics.submit_report(pg_temp.report('77777777-7777-4777-8777-777777777777'), pg_temp.token('ab'), null);
    assert false, 'disabled ingest must fail';
  exception when others then
    assert sqlerrm = 'diagnostics_ingest_disabled', sqlerrm;
  end;

  perform diagnostics.ensure_month_partitions(clock_timestamp() + interval '5 months');
  perform diagnostics.ensure_month_partitions(clock_timestamp() + interval '5 months');

  assert diagnostics.purge_install('11111111-1111-4111-8111-111111111111') > 0, 'purge_install removes reports';
  assert not exists (select 1 from diagnostics.reports), 'no reports remain';
  assert exists (select 1 from diagnostics.crash_signatures), 'knowledge base survives report purges';
end;
$$;

select 'diagnostics tests passed' as result;
