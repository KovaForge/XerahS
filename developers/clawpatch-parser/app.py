"""
app.py — Flask app for the KovaForge XerahS developer dashboard.

Reads `/Users/mike/Projects/KovaForge/xerahs/docs/reports/hourly_review_state.json`
plus the clawpatch reports under `xerahs/.clawpatch/reports/*.md` at request
time (no DB). Binds to 127.0.0.1 only — localhost by design.

Routes:
    /                  -> dashboard overview (KPIs + recent activity)
    /candidates        -> in-tray: next_candidates joined to clawpatch
    /areas             -> all areas[] with status / priority / last_outcome
    /runs              -> last_runs[] history
    /reports           -> list of clawpatch reports
    /reports/<file>    -> full report viewer (Markdown rendered to HTML)
    /raw               -> raw state JSON (pretty-printed)

Run: see run.sh at /Users/mike/Projects/KovaForge/xerahs/developers/run.sh
"""

from __future__ import annotations

import json
import sys
from collections import Counter
from html import escape
from pathlib import Path

from flask import Flask, abort, render_template, request

# Make parser importable when launched via run.sh regardless of cwd.
sys.path.insert(0, str(Path(__file__).resolve().parent))
import parser  # noqa: E402


# ── Flask app setup ───────────────────────────────────────────────────
app = Flask(
    __name__,
    template_folder=str(Path(__file__).resolve().parent / "templates"),
    static_folder=str(Path(__file__).resolve().parent / "static"),
)


# ── Tiny helpers ──────────────────────────────────────────────────────
def _severity_color(sev: str | None) -> str:
    """Return a CSS class suffix for a severity value."""
    s = (sev or "unknown").lower()
    return {
        "high": "sev-high",
        "medium": "sev-medium",
        "low": "sev-low",
        "critical": "sev-critical",
    }.get(s, "sev-unknown")


def _status_color(status: str | None) -> str:
    s = (status or "").lower()
    return {
        "fixed": "st-fixed",
        "clean": "st-clean",
        "reviewed": "st-reviewed",
        "open": "st-open",
        "blocked": "st-blocked",
    }.get(s, "st-unknown")


def _md_to_html(md: str) -> str:
    """Minimal Markdown -> HTML for the report viewer. Handles headings,
    lists, bullets, code spans, paragraphs, horizontal rules. Deliberately
    NOT a full Markdown parser — we only need to render the report
    text well enough to read in a browser. Anything fancier (nested
    lists, fenced code blocks) is left intact as <pre>."""
    lines = md.splitlines()
    out: list[str] = []
    in_ul = False
    in_p = False
    in_pre = False

    def close_p():
        nonlocal in_p
        if in_p:
            out.append("</p>")
            in_p = False

    def close_ul():
        nonlocal in_ul
        if in_ul:
            out.append("</ul>")
            in_ul = False

    for raw in lines:
        line = raw.rstrip()

        if line.startswith("```"):
            close_p()
            close_ul()
            if not in_pre:
                out.append("<pre><code>")
                in_pre = True
            else:
                out.append("</code></pre>")
                in_pre = False
            continue
        if in_pre:
            out.append(escape(line))
            continue
        if not line.strip():
            close_p()
            close_ul()
            continue
        if line.startswith("### "):
            close_p()
            close_ul()
            out.append(f"<h3>{_inline(line[4:])}</h3>")
            continue
        if line.startswith("## "):
            close_p()
            close_ul()
            out.append(f"<h2>{_inline(line[3:])}</h2>")
            continue
        if line.startswith("# "):
            close_p()
            close_ul()
            out.append(f"<h1>{_inline(line[2:])}</h1>")
            continue
        if line.strip() == "---":
            close_p()
            close_ul()
            out.append("<hr/>")
            continue
        if line.lstrip().startswith(("- ", "* ")):
            close_p()
            content = line.lstrip()[2:]
            if not in_ul:
                out.append("<ul>")
                in_ul = True
            out.append(f"<li>{_inline(content)}</li>")
            continue
        # paragraph
        close_ul()
        if not in_p:
            out.append("<p>")
            in_p = True
            out.append(_inline(line))
        else:
            out.append("<br/>")
            out.append(_inline(line))
    close_p()
    close_ul()
    if in_pre:
        out.append("</code></pre>")
    return "\n".join(out)


def _inline(text: str) -> str:
    """Apply inline Markdown (code spans, bold, links) and HTML-escape."""
    s = escape(text)
    # `code`
    import re

    s = re.sub(r"`([^`]+)`", r"<code>\1</code>", s)
    # **bold**
    s = re.sub(r"\*\*([^*]+)\*\*", r"<strong>\1</strong>", s)
    # [text](url)
    s = re.sub(
        r"\[([^\]]+)\]\((https?://[^\s)]+)\)",
        r'<a href="\2" target="_blank" rel="noopener noreferrer">\1</a>',
        s,
    )
    return s


# ── Context: stuff every template wants ──────────────────────────────
@app.context_processor
def _inject_globals() -> dict:
    state = parser.load_state()
    return {
        "state": state,
        "state_error": state.get("_error"),
        "n_candidates": len(state.get("next_candidates", []) or []),
        "n_areas": len(state.get("areas", []) or []),
        "n_runs": len(state.get("last_runs", []) or []),
        "severity_color": _severity_color,
        "status_color": _status_color,
    }


# ── Routes ────────────────────────────────────────────────────────────
@app.route("/")
def dashboard():
    state = parser.load_state()
    reports = parser.load_reports_index()
    finding_index = parser.load_finding_index()
    joined = parser.join_candidates_to_findings(state, finding_index)

    # KPIs
    sev_counts = Counter((j.get("severity") or "unknown") for j in joined)
    area_status_counts = Counter(
        (a.get("status") or "unknown") for a in state.get("areas", []) or []
    )
    matched = sum(1 for j in joined if j["in_report"])
    unmatched = len(joined) - matched

    # Last 5 runs as "recent activity"
    last_runs_recent = list(reversed(state.get("last_runs", []) or []))[:5]

    return render_template(
        "dashboard.html",
        joined_count=len(joined),
        matched=matched,
        unmatched=unmatched,
        sev_counts=dict(sev_counts),
        area_status_counts=dict(area_status_counts),
        reports=reports[:5],
        last_runs_recent=last_runs_recent,
        severity_color=_severity_color,
        status_color=_status_color,
    )


@app.route("/candidates")
def candidates():
    state = parser.load_state()
    finding_index = parser.load_finding_index()
    joined = parser.join_candidates_to_findings(state, finding_index)

    # Filters from query string
    sev_filter = request.args.get("severity", "").lower()
    only_unmatched = request.args.get("unmatched") == "1"
    q_filter = request.args.get("q", "").strip().lower()

    if sev_filter:
        joined = [j for j in joined if (j.get("severity") or "").lower() == sev_filter]
    if only_unmatched:
        joined = [j for j in joined if not j["in_report"]]
    if q_filter:
        joined = [
            j
            for j in joined
            if q_filter in (j.get("title") or "").lower()
            or q_filter in (j.get("raw") or "").lower()
            or q_filter in (j.get("category") or "").lower()
        ]

    return render_template(
        "candidates.html",
        joined=joined,
        severity_color=_severity_color,
        sev_filter=sev_filter,
        only_unmatched=only_unmatched,
        q_filter=q_filter,
    )


@app.route("/areas")
def areas():
    state = parser.load_state()
    areas = state.get("areas", []) or []
    # Light normalization: status variants collapse to lowercase where possible.
    return render_template(
        "areas.html",
        areas=areas,
        status_color=_status_color,
    )


@app.route("/runs")
def runs():
    state = parser.load_state()
    runs_ = state.get("last_runs", []) or []
    return render_template("runs.html", runs=runs_)


@app.route("/reports")
def reports_list():
    reports = parser.load_reports_index()
    return render_template("reports.html", reports=reports)


@app.route("/reports/<path:filename>")
def report_view(filename: str):
    # Only allow filenames within REPORTS_DIR, no traversal.
    candidate = (parser.REPORTS_DIR / filename).resolve()
    if not str(candidate).startswith(str(parser.REPORTS_DIR.resolve())):
        abort(404)
    if not candidate.exists() or not candidate.is_file():
        abort(404)
    text = parser.load_report_text(str(candidate))
    html = _md_to_html(text)
    return render_template(
        "report_view.html", filename=filename, body_html=html
    )


@app.route("/raw")
def raw():
    state = parser.load_state()
    pretty = json.dumps(state, indent=2, ensure_ascii=False)
    return render_template("raw.html", pretty=pretty)


if __name__ == "__main__":
    # Imported by `flask --app app run` from run.sh. Kept for direct
    # `python app.py` invocations only.
    app.run(host="127.0.0.1", port=8765, debug=True, use_reloader=False)
