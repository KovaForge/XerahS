"""
parser.py — read hourly_review_state.json + clawpatch reports for the
xerahs developer dashboard. Read-only; no DB. Designed for Flask views
that re-render on each request (call site: app.py).

Public surface used by app.py:
    load_state()                 -> dict (state JSON, fresh)
    load_reports_index()         -> dict[report_path] = {filename, ts, mtime, findings_count}
    load_finding_index()         -> dict[fnd_sig id] = FindingDetail
                                   (joins across all reports; latest report wins)
    parse_candidate(cand_str)    -> (fnd_sig id | None, description | original)
    join_candidates_to_findings(state, idx) -> list[dict]
"""

from __future__ import annotations

import json
import os
import re
from pathlib import Path
from typing import Any


# ── Paths ─────────────────────────────────────────────────────────────
XERAHS_REPO = Path("/Users/mike/Projects/KovaForge/xerahs")
STATE_PATH = XERAHS_REPO / "docs" / "reports" / "hourly_review_state.json"
REPORTS_DIR = XERAHS_REPO / ".clawpatch" / "reports"

# Used by the dev-only `_paths()` helper below for the "View file" link.
REPO_REL_FROM_DEVELOPERS = Path("..")  # developers/ sits at xerahs/developers/


# ── State JSON ────────────────────────────────────────────────────────
def load_state() -> dict[str, Any]:
    """Read the state JSON fresh on each call. Catches parse errors so
    the dashboard can render a polite empty state."""
    if not STATE_PATH.exists():
        return {"_error": f"missing: {STATE_PATH}"}
    try:
        with STATE_PATH.open("r", encoding="utf-8") as f:
            return json.load(f)
    except json.JSONDecodeError as e:
        return {"_error": f"JSON parse failed: {e}", "_path": str(STATE_PATH)}


# ── Clawpatch reports ─────────────────────────────────────────────────
REPORT_FILENAME_RE = re.compile(r"^(?P<ts>\d{8}T\d{6})-(?P<hash>[0-9a-f]+)\.md$")


def _report_meta(path: Path) -> dict[str, Any]:
    m = REPORT_FILENAME_RE.match(path.name)
    return {
        "filename": path.name,
        "path": str(path),
        "ts_compact": m.group("ts") if m else "",
        "hash": m.group("hash") if m else "",
        "mtime": path.stat().st_mtime,
    }


def load_reports_index() -> list[dict[str, Any]]:
    """List of clawpatch report metadata, newest first."""
    if not REPORTS_DIR.exists():
        return []
    out = []
    for p in REPORTS_DIR.glob("*.md"):
        out.append(_report_meta(p))
    out.sort(key=lambda r: r["mtime"], reverse=True)
    return out


def load_report_text(path: str) -> str:
    p = Path(path)
    if not p.exists() or not p.is_file():
        return ""
    return p.read_text(encoding="utf-8")


# ── Findings extraction ──────────────────────────────────────────────
# A "finding block" begins at a heading line and continues until the next
# heading of equal-or-higher level.
#
# We support two heading shapes:
#   ## <severity>: <Title>            (single-finding section)
#   ### cluster N: <Title>            (inside action clusters — bullet list
#                                       of "low/high fnd_sig-..." entries follows)
#
# Inside each block we greedily collect "key: value\n" lines and
# "key:\n  ...\n" block scalars. Lists like evidence are "  - item".

KV_LINE_RE = re.compile(r"^(?P<key>[a-z][a-z _-]*[a-z]):(?:\s+(?P<val>\S.*?))?\s*$")


def _kv_val(m: re.Match | None) -> str:
    """Safe .group('val') — returns '' when the optional group didn't match."""
    if m is None:
        return ""
    g = m.group("val")
    return g or ""
HEADING_RE = re.compile(r"^(#{1,6})\s+(.*)$")
CLUSTER_BULLET_RE = re.compile(
    r"^- (?P<sev>low|medium|high|critical)\/(?P<conf>low|medium|high|critical)\s+"
    r"(?P<id>fnd_sig\S+):\s*(?P<title>.+)$"
)


def _split_blocks(text: str) -> list[tuple[str, str, str]]:
    """Return list of (level, heading_text, body) for each block in the
    report. A block body is everything between its heading line and the
    next heading line of equal-or-shallower depth (i.e. same or fewer
    leading '#'s)."""
    out: list[tuple[str, str, str]] = []
    lines = text.splitlines()
    cur_level: int | None = None
    cur_heading = ""
    cur_body_lines: list[str] = []

    def flush() -> None:
        if cur_level is not None:
            out.append(("#" * cur_level, cur_heading, "\n".join(cur_body_lines)))

    for line in lines:
        m = HEADING_RE.match(line)
        if m:
            # Any new heading starts a new block — don't absorb subheadings
            # into their parent's body. The parent block is complete by
            # the time a child heading arrives.
            flush()
            cur_level = len(m.group(1))
            cur_heading = m.group(2).strip()
            cur_body_lines = []
            continue
        if cur_level is not None:
            cur_body_lines.append(line)
    flush()
    return out


def _parse_kv_block(body: str) -> tuple[dict[str, str], dict[str, list[str]], list[str]]:
    """Pull a single finding's structured fields. Returns:
        (scalars, list_fields, leftover_paragraphs).

    Scalars: "key: value" pairs where value fits on the next line.
    List fields: "key:\\n  - line\\n  - line" lists.
    Leftover paragraphs: free-form prose sections that don't match a key
        line, kept so the dashboard can still show the description."""
    scalars: dict[str, str] = {}
    list_fields: dict[str, list[str]] = {}
    leftovers: list[str] = []
    lines = body.splitlines()
    i = 0
    while i < len(lines):
        line = lines[i]
        stripped = line.strip()
        if not stripped:
            i += 1
            continue
        # Detect "key:" (no inline value) — the value follows on
        # subsequent indented/continued lines until blank line or next
        # top-level "key: value" line.
        kv_inline = KV_LINE_RE.match(line)
        is_bare_key_header = (
            stripped.endswith(":")
            and not stripped.startswith(("- ", "* "))
            and kv_inline is not None
        )
        if is_bare_key_header:
            key = kv_inline.group("key").strip()
            # Only treat as a top-level "key:" header when the key has no
            # inline value.
            if _kv_val(kv_inline):
                # This was actually "key: value" — fall through and treat
                # it as a scalar on the next loop iteration.
                i += 1
                continue
            items: list[str] = []
            j = i + 1
            collecting = True
            while j < len(lines) and collecting:
                inner = lines[j]
                inner_strip = inner.strip()
                if not inner_strip:
                    # Blank line: peek ahead. If the next non-blank line
                    # continues the same key (more bullets / paragraph
                    # lines that don't begin with another "key:" header),
                    # skip the blank and continue; otherwise the list is
                    # done.
                    k = j + 1
                    while k < len(lines) and not lines[k].strip():
                        k += 1
                    if k >= len(lines):
                        break  # end of file → done
                    nxt = lines[k].strip()
                    nxt_kv = KV_LINE_RE.match(lines[k])
                    if nxt.startswith(("- ", "* ")):
                        j = k
                        continue
                    if nxt_kv and _kv_val(nxt_kv):
                        break  # next top-level scalar starts here
                    if nxt_kv and not _kv_val(nxt_kv):
                        break  # next top-level list starts here
                    # Otherwise: paragraph continuation
                    j = k
                    continue
                # bullet "- x" or "* x" -> capture
                if inner_strip.startswith(("- ", "* ")):
                    items.append(inner_strip[2:].strip())
                    j += 1
                    continue
                # another top-level key (with or without inline value) ends the list
                if KV_LINE_RE.match(inner):
                    break
                # otherwise treat as continued paragraph for the same key
                items.append(inner_strip)
                j += 1
            if items:
                list_fields[key] = items
            else:
                scalars[key] = ""
            i = j
            continue
        # Detect "key: value" (single line)
        if kv_inline:
            key = kv_inline.group("key").strip()
            val = _kv_val(kv_inline)
            if not val:
                # Bare "key:" with no value — handled above. Skip.
                i += 1
                continue
            scalars[key] = val
            i += 1
            continue
        # Otherwise: paragraph text — preserve as leftover
        leftovers.append(stripped)
        i += 1
    return scalars, list_fields, leftovers


def _extract_severity_from_heading(heading: str) -> tuple[str | None, str]:
    """Headings often look like 'high: Potential data loss ...'. Returns
    (severity, title). If no severity prefix, returns (None, full_text)."""
    m = re.match(r"^(low|medium|high|critical):\s*(.+)$", heading.strip(), re.IGNORECASE)
    if m:
        return m.group(1).lower(), m.group(2).strip()
    return None, heading.strip()


def parse_report_findings(text: str) -> list[dict[str, Any]]:
    """Walk the report, return a flat list of finding dicts."""
    findings: list[dict[str, Any]] = []
    blocks = _split_blocks(text)
    for level, heading, body in blocks:
        # Skip report-level headings (the `# clawpatch report` root, or
        # `## action clusters` umbrella) — they don't carry an individual
        # finding. Only ## or ### blocks that carry a severity title or
        # nested cluster bullets are kept.
        if level not in ("##", "###"):
            continue
        sev, title = _extract_severity_from_heading(heading)
        scalars, list_fields, paragraphs = _parse_kv_block(body)
        finding_id = scalars.get("id") or None
        if not finding_id:
            # Cluster-style heading: bullets are individual findings.
            for line in body.splitlines():
                m = CLUSTER_BULLET_RE.match(line.strip())
                if m:
                    findings.append({
                        "id": m.group("id"),
                        "title": m.group("title").strip(),
                        "severity": m.group("sev"),
                        "confidence": m.group("conf"),
                        "category": None,
                        "triage": None,
                        "status": "open",
                        "feature": None,
                        "evidence": [],
                        "recommendation": [],
                        "report_excerpt": "",
                        "in_cluster": True,
                        "raw_heading": heading,
                    })
            continue
        findings.append({
            "id": finding_id,
            "title": title,
            "severity": sev,
            "confidence": scalars.get("confidence"),
            "category": scalars.get("category"),
            "triage": scalars.get("triage"),
            "status": scalars.get("status") or "open",
            "feature": scalars.get("feature"),
            "evidence": list_fields.get("evidence", []),
            "recommendation": list_fields.get("recommendation", []),
            "report_excerpt": "\n".join(paragraphs).strip(),
            "in_cluster": False,
            "raw_heading": heading,
        })
    return findings


def load_finding_index() -> dict[str, dict[str, Any]]:
    """Aggregate every report's findings into one dict keyed by finding id.
    When the same id appears in multiple reports, the most recent report
    (by file mtime) wins. Returns {id: FindingDetail}."""
    if not REPORTS_DIR.exists():
        return {}
    index: dict[str, dict[str, Any]] = {}
    report_paths = sorted(REPORTS_DIR.glob("*.md"), key=lambda p: p.stat().st_mtime)
    for p in report_paths:
        meta = _report_meta(p)
        text = p.read_text(encoding="utf-8")
        for f in parse_report_findings(text):
            existing = index.get(f["id"])
            if existing is None or meta["mtime"] >= existing["_mtime"]:
                f["_report_filename"] = meta["filename"]
                f["_mtime"] = meta["mtime"]
                index[f["id"]] = f
    return index


# ── Candidate join ────────────────────────────────────────────────────
# State JSON stores next_candidates as "fnd_sig-feat-library-... -- description".
CAND_RE = re.compile(r"^(?P<id>fnd_sig\S+?)\s+--\s+(?P<desc>.+)$")


def parse_candidate(cand: str) -> tuple[str | None, str, str]:
    """Parse a next_candidates[] string. Returns:
        (id_or_None, title, original)
    where `title` is the description (best-effort fallback to the original
    string when no `--` separator is present)."""
    m = CAND_RE.match(cand.strip())
    if m:
        return m.group("id"), m.group("desc").strip(), cand
    return None, cand.strip(), cand


def join_candidates_to_findings(
    state: dict[str, Any], finding_index: dict[str, dict[str, Any]]
) -> list[dict[str, Any]]:
    """For each entry in next_candidates[], attach the matching finding
    detail (if any) and a small status tag. Shape per item:
        {
            "raw": "...",                 # original string from JSON
            "fnd_id": "fnd_sig-..."       # or None if no id matched
            "title": "...",               # description text after "--"
            "severity": "high|low|..."    # from latest report, or None
            "category": "...",            # or None
            "feature": "...",             # or None
            "evidence": [...],            # file:line citations
            "in_report": True|False       # True if any report had this id
            "report_filename": "..."      # source report
        }
    """
    out = []
    raw_list = state.get("next_candidates", []) or []
    for raw in raw_list:
        fnd_id, title, original = parse_candidate(raw)
        f = finding_index.get(fnd_id) if fnd_id else None
        out.append({
            "raw": original,
            "fnd_id": fnd_id,
            "title": title,
            "severity": (f or {}).get("severity"),
            "category": (f or {}).get("category"),
            "feature": (f or {}).get("feature"),
            "evidence": (f or {}).get("evidence", []),
            "in_report": f is not None,
            "report_filename": (f or {}).get("_report_filename"),
            "confidence": (f or {}).get("confidence"),
        })
    return out


# ── Misc helpers ──────────────────────────────────────────────────────
def repo_path(absolute_path: str) -> str:
    """Convert an absolute path inside xerahs/ to a repo-relative path for
    display (e.g. /Users/.../xerahs/src/foo.cs -> src/foo.cs)."""
    try:
        rel = Path(absolute_path).resolve().relative_to(XERAHS_REPO.resolve())
        return str(rel)
    except ValueError:
        return absolute_path
