# External Contracts

> XIP0077 U6 — Schema documentation for every external boundary the XerahS
> operational workflows consume. Validators in pipelines check against these
> contracts; this file is the single source of truth for the expected shapes.

## 1. XerahS CLI Upload JSON (`xerahs upload --json`)

**Consumer:** URL publishing (Trigger A: SKILL.md, Trigger B: ReClip `app.py`)

### Expected Response (HTTP 200, exit code 0)

```json
{
  "url": "https://mike.getsharex.com/ShareX/2026/05/filename-xxxxxxxx.mp4",
  "filename": "filename-xxxxxxxx.mp4",
  "size": 1234567,
  "type": "application/octet-stream"
}
```

### Validation Rules

| Field | Type | Required | Constraint |
|---|---|---|---|
| `url` | string | ✅ | Must start with `https://`; for MP4 uploads, host must be `mike.getsharex.com` |
| `filename` | string | ✅ | Must contain a file extension (`.` followed by 1+ chars) |
| `size` | integer | ❌ | Informational |
| `type` | string | ❌ | Informational |

### Failure Modes

- **Non-zero exit + text on stdout:** Treat as upload failure even if text looks like partial success.
- **Exit 0 + invalid JSON:** Treat as failure — the CLI sometimes falls back to plain text.
- **Exit 0 + valid JSON but missing `url`:** Treat as failure.
- **`url` host mismatch (ReClip only):** `mike.getsharex.com` required for MP4. Other hosts = contract violation.

---

## 2. GitHub Issues API (`GET /repos/{owner}/{repo}/issues`)

**Consumer:** Issue monitor (`xerahs-issue-monitor.py`)

### Expected Issue Object Fields (consumed by classifier)

| Field | Type | Required by Classifier | Notes |
|---|---|---|---|
| `number` | integer | ✅ | Issue number |
| `title` | string | ✅ | Used in text classification |
| `html_url` | string | ✅ | Link for reports |
| `user.login` | string | ✅ | Author login; `mcored` → special classification |
| `created_at` | string (ISO 8601) | ✅ | |
| `updated_at` | string (ISO 8601) | ✅ | Used in signature computation |
| `body` | string | ✅ | Used in text classification |
| `labels[].name` | string | ✅ | Each label must have a `name` field |
| `comments` | integer | ✅ | Used in signature computation |

### Validation Rules

Before classification, validate **each issue** for:
1. `number` exists and is an integer
2. `user` exists and has a `login` string
3. `labels` is a list where every element has a `name` string
4. `updated_at` exists and is non-empty
5. `comments` exists and is a non-negative integer

### Failure Mode (Contract Violation)

On any validation failure:
- Print `XERAHS_ISSUE_MONITOR_FAILED: contract <details>`
- **Do not write state** (preserve `seen` map from last successful run)
- Exit non-zero
- U3 watchdog will independently detect staleness within 8 days

---

## 3. `dotnet build` / `dotnet test` Log Shapes

**Consumer:** Pre-release pipeline, hourly sweep, KFIP

### Known Transient Errors (retry once, then report as blocker)

| Pattern | Error Class | Retry Action |
|---|---|---|
| `NETSDK1004: Assets file .../project.assets.json not found` | Missing NuGet assets | `dotnet restore` → rebuild |
| `CS0006: Metadata file .../ref/...dll could not be found` | Missing ref metadata | Prebuild the SDK project → rebuild |

### Runner Crash Detection

If `dotnet test` exits non-zero **and** zero test results are parsed (no `Passed:`, `Failed:`, or `Total tests` lines in output), report as **"runner crash"**, not "tests failed".

### Retry Policy

- One retry per error class, then report as blocker
- Never retry more than once per class per run
- Include both initial and retry log paths in the report

---

## 4. Disk Space (`df -k /`)

**Consumer:** All four workflows via `scripts/preflight-disk.sh` (U2)

### Expected Output

```
Filesystem  1K-blocks  Used  Available  Use%  Mounted
/dev/diskN  NNNNNN     NNNN  NNNNNN     NN%   /
```

### Validation Rule

- Parse `Available` (field 4) from second line
- If < 2 GiB (2097152 KiB): abort with `PREFLIGHT_DISK_LOW`
- If `df` itself fails: treat as low disk (fail closed)
