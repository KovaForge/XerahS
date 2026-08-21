# clawpatch-parser — Developer Dashboard

Local web UI for the KovaForge XerahS hourly review state. Reads
`docs/reports/hourly_review_state.json` and the clawpatch reports under
`.clawpatch/reports/*.md` at request time. **No database, no external
network, localhost-only.**

## What it shows

| Page          | Path             | Surfaces                                              |
|---------------|------------------|-------------------------------------------------------|
| Overview      | `/`              | KPI cards, severity distribution, recent runs, newest reports |
| Candidates    | `/candidates`    | The `next_candidates[]` in-tray, joined to clawpatch reports by finding id |
| Areas         | `/areas`         | `areas[]` array: status, priority, last outcome, follow-up |
| Runs          | `/runs`          | `last_runs[]` history (timestamp, outcome, files, version, commit) |
| Reports       | `/reports`       | List of clawpatch reports under `.clawpatch/reports/` |
| Report view   | `/reports/<file>` | One report rendered with simple Markdown→HTML         |
| Raw JSON      | `/raw`           | The full state JSON, pretty-printed                  |

## Run it

The wrapper script lives one directory up, at `../run.sh` (i.e.
`/Users/mike/Projects/KovaForge/xerahs/developers/run.sh`).

```bash
# From this directory:
../run.sh                          # http://127.0.0.1:8765
PORT=9090 ../run.sh                # custom port
HOST=127.0.0.1 ../run.sh -p 9000   # explicit
```

The wrapper builds a venv at `../.venv` on first run and installs Flask
3.x. Subsequent runs are instant.

`run.sh` refuses to bind anywhere other than loopback. Don't even try.

## Architecture

```
developers/                                 # /Users/mike/Projects/KovaForge/xerahs/developers/
├── run.sh                                  # wrapper: venv + flask CLI on localhost
├── .gitignore                              # ignores .venv/ + __pycache__/
├── .venv/                                  # virtualenv (gitignored)
└── clawpatch-parser/                       # this app
    ├── app.py                              # Flask app + routes
    ├── parser.py                           # state JSON + clawpatch report parsing/joining
    ├── README.md                           # this file
    ├── templates/                          # Jinja templates (base + 7 pages)
    │   ├── base.html
    │   ├── dashboard.html
    │   ├── candidates.html
    │   ├── areas.html
    │   ├── runs.html
    │   ├── reports.html
    │   ├── report_view.html
    │   └── raw.html
    └── static/
        └── style.css                       # hand-written, no build step
```

## How candidate-to-report joining works

The state JSON stores `next_candidates[]` as flat strings like:

```
fnd_sig-feat-library-108dac94d4-1a29_bf6987a0b0 -- Potential data loss when PlatformServices are not initialized during capture
```

That flat format is what `xerahs-review` writes after ingestion. The full
structured data (severity, category, confidence, evidence, recommendation,
feature) lives in the clawpatch reports themselves.

`parser.parse_candidate()` extracts the `fnd_sig-...` id from each
candidate string. `parser.load_finding_index()` walks every clawpatch
report and builds a `dict[id -> FindingDetail]` — the most recent report
wins when an id appears in multiple reports.

`/candidates` then renders: each row shows the candidate's title plus
severity/category/evidence, and a link to the originating report when a
join succeeded. Items where no current report carries the id get an
"unmatched" visual treatment (slightly faded) — these are queue items
the producer has not yet refreshed against the latest clawpatch run.

In the current state:
- **95 candidates** in `next_candidates[]`
- **32 of 95** joined to a current clawpatch report
- **63 of 95** unmatched (older queue items that no clawpatch report
  re-confirms; candidates the `xerahs-bugfix` skill will treat as pivots
  and document as such)

## Caveats

- **Reads JSON on every request.** No caching layer. If the file is huge
  or the disk is slow, page-load latency will reflect that. For the
  current ~70 KB JSON and 12 reports this is fine.
- **No write paths.** This is a read-only viewer. Edits to
  `hourly_review_state.json` happen via `xerahs-review` /
  `xerahs-bugfix`, not here.
- **Markdown rendering is minimal.** Lists, headings, paragraphs, code
  spans, links, bold, hr — that's it. Fenced code blocks work too.
  Anything fancier (tables, footnotes) renders as plain text.
- **`fnd_sig-...` ids are the join key, period.** If the producer ever
  changes the candidate string format, the join silently degrades to
  "unmatched" — open `parser.py` and update `parse_candidate()` +
  `CAND_RE`.
- **No CSRF / no auth.** Bound to `127.0.0.1` only. Don't expose to a
  network.
