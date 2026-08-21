#!/usr/bin/env python3
"""XIP0077 U3 — Pipeline liveness watchdog (max-staleness alarm).

Checks heartbeat files for all four XerahS operational workflows and
posts a Discord alert when any exceed their max age. The watchdog never
repairs anything; it only reports.

Schedule: daily via OpenClaw cron or launchd.
Runtime: ≤ 60 s, no network calls except the optional Discord post.
"""

from __future__ import annotations

import json
import os
import subprocess
import sys
import time
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path

# ── Configuration ──────────────────────────────────────────────────────

DISCORD_CHANNEL = "channel:1489624037758861415"
STATE_FILE = Path("/tmp/xerahs-watchdog/watchdog-state.json")
FAILED_ALERT_FILE = Path("/tmp/xerahs-watchdog/last-failed-alert.txt")

REPO_DIR = Path("/Users/mike/Projects/KovaForge/xerahs")

# 24-hour dedup window (seconds)
DEDUP_WINDOW = 86400


@dataclass
class HeartbeatCheck:
    name: str
    description: str
    max_age_hours: float
    check_type: str  # "file_mtime", "json_field", "git_drift", "log_dir"
    path: str = ""
    json_field: str = ""
    git_remote: str = ""
    git_branch: str = ""
    max_commits_behind: int = 0


CHECKS: list[HeartbeatCheck] = [
    HeartbeatCheck(
        name="issue-monitor-state",
        description="Issue monitor state file",
        max_age_hours=192,  # 8 days
        check_type="json_field",
        path=str(Path.home() / ".openclaw/state/xerahs-issue-monitor.json"),
        json_field="last_run_at",
    ),
    HeartbeatCheck(
        name="hourly-sweep-state",
        description="Hourly sweep state (last_updated)",
        max_age_hours=12,
        check_type="json_field",
        path=str(REPO_DIR / "docs/reports/hourly_review_state.json"),
        json_field="last_updated",
    ),
    HeartbeatCheck(
        name="origin-develop-drift",
        description="origin/develop vs local develop",
        max_age_hours=0,  # N/A — uses commit count
        check_type="git_drift",
        git_remote="origin",
        git_branch="develop",
        max_commits_behind=10,
    ),
    HeartbeatCheck(
        name="prerelease-build-log",
        description="Pre-release pipeline build log",
        max_age_hours=192,  # 8 days
        check_type="log_dir",
        path="/tmp/xerahs-prerelease-pipeline",
    ),
]


# ── Helpers ────────────────────────────────────────────────────────────


def now_utc() -> datetime:
    return datetime.now(timezone.utc)


def hours_since(ts: datetime) -> float:
    return (now_utc() - ts).total_seconds() / 3600


def parse_iso(s: str) -> datetime | None:
    """Parse ISO 8601 timestamps, tolerant of common formats."""
    for fmt in ("%Y-%m-%dT%H:%M:%S%z", "%Y-%m-%dT%H:%M:%SZ", "%Y-%m-%d %H:%M AWST"):
        try:
            dt = datetime.strptime(s.strip(), fmt)
            if dt.tzinfo is None:
                # AWST = UTC+8
                from datetime import timedelta
                dt = dt.replace(tzinfo=timezone(timedelta(hours=8)))
            return dt
        except ValueError:
            continue
    return None


def load_state() -> dict:
    if STATE_FILE.exists():
        try:
            return json.loads(STATE_FILE.read_text())
        except Exception:
            pass
    return {"alerts": {}}


def save_state_atomic(state: dict) -> None:
    STATE_FILE.parent.mkdir(parents=True, exist_ok=True)
    tmp = STATE_FILE.with_suffix(".tmp")
    tmp.write_text(json.dumps(state, indent=2, default=str))
    os.replace(str(tmp), str(STATE_FILE))


def should_alert(state: dict, check_name: str) -> bool:
    """True if we haven't alerted for this check in the last DEDUP_WINDOW."""
    last = state.get("alerts", {}).get(check_name)
    if last is None:
        return True
    try:
        last_ts = parse_iso(last)
        if last_ts is None:
            return True
        return hours_since(last_ts) >= (DEDUP_WINDOW / 3600)
    except Exception:
        return True


def record_alert(state: dict, check_name: str) -> None:
    state.setdefault("alerts", {})[check_name] = now_utc().isoformat()


# ── Check Implementations ─────────────────────────────────────────────


def check_json_field(check: HeartbeatCheck) -> str | None:
    """Returns alert message or None if OK."""
    path = Path(check.path)
    if not path.exists():
        return f"{check.description}: file missing ({check.path})"
    try:
        data = json.loads(path.read_text())
    except Exception as exc:
        return f"{check.description}: invalid JSON ({exc})"

    value = data.get(check.json_field)
    if not value:
        return f"{check.description}: field '{check.json_field}' missing or empty"

    ts = parse_iso(str(value))
    if ts is None:
        # Try file mtime as fallback
        mtime = datetime.fromtimestamp(path.stat().st_mtime, tz=timezone.utc)
        age_h = hours_since(mtime)
        if age_h > check.max_age_hours:
            return (
                f"{check.description}: field '{check.json_field}' unparseable "
                f"(value={value!r}), file mtime is {age_h:.0f}h old "
                f"(max {check.max_age_hours}h)"
            )
        return None

    age_h = hours_since(ts)
    if age_h > check.max_age_hours:
        return (
            f"{check.description}: stale — {age_h:.0f}h since last update "
            f"(max {check.max_age_hours}h, field={check.json_field})"
        )
    return None


def check_git_drift(check: HeartbeatCheck) -> str | None:
    """Returns alert message or None if OK."""
    try:
        # Fetch silently
        subprocess.run(
            ["git", "-C", str(REPO_DIR), "fetch", check.git_remote, check.git_branch],
            capture_output=True, timeout=30, check=False,
        )
        local = subprocess.run(
            ["git", "-C", str(REPO_DIR), "rev-parse", "HEAD"],
            capture_output=True, text=True, timeout=10,
        ).stdout.strip()
        remote_ref = f"refs/remotes/{check.git_remote}/{check.git_branch}"
        remote = subprocess.run(
            ["git", "-C", str(REPO_DIR), "rev-parse", remote_ref],
            capture_output=True, text=True, timeout=10,
        ).stdout.strip()

        if not local or not remote:
            return f"{check.description}: ref resolution failed"

        if local == remote:
            return None

        behind = subprocess.run(
            ["git", "-C", str(REPO_DIR), "rev-list", "--count",
             f"{check.git_remote}/{check.git_branch}..HEAD"],
            capture_output=True, text=True, timeout=10,
        ).stdout.strip()

        try:
            behind_count = int(behind)
        except ValueError:
            behind_count = 0

        if behind_count > check.max_commits_behind:
            # Also check time since last remote commit
            remote_ts = subprocess.run(
                ["git", "-C", str(REPO_DIR), "log", "-1", "--format=%ct", remote_ref],
                capture_output=True, text=True, timeout=10,
            ).stdout.strip()
            try:
                remote_age_days = (time.time() - int(remote_ts)) / 86400
            except (ValueError, TypeError):
                remote_age_days = 0

            if remote_age_days > 7:
                return (
                    f"{check.description}: {check.git_remote}/{check.git_branch} is "
                    f"{behind_count} commits behind local HEAD, "
                    f"remote last updated {remote_age_days:.0f} days ago"
                )
        return None
    except Exception as exc:
        return f"{check.description}: check failed ({exc})"


def check_log_dir(check: HeartbeatCheck) -> str | None:
    """Returns alert message or None if OK."""
    log_dir = Path(check.path)
    if not log_dir.exists():
        return f"{check.description}: log directory missing ({check.path})"

    # Find newest file
    files = sorted(log_dir.glob("*"), key=lambda f: f.stat().st_mtime, reverse=True)
    if not files:
        return f"{check.description}: log directory empty ({check.path})"

    newest = files[0]
    mtime = datetime.fromtimestamp(newest.stat().st_mtime, tz=timezone.utc)
    age_h = hours_since(mtime)
    if age_h > check.max_age_hours:
        return (
            f"{check.description}: newest log is {age_h:.0f}h old "
            f"(max {check.max_age_hours}h, file={newest.name})"
        )
    return None


def run_check(check: HeartbeatCheck) -> str | None:
    """Dispatch to the appropriate checker."""
    if check.check_type == "json_field":
        return check_json_field(check)
    elif check.check_type == "git_drift":
        return check_git_drift(check)
    elif check.check_type == "log_dir":
        return check_log_dir(check)
    else:
        return f"{check.description}: unknown check type '{check.check_type}'"


# ── Discord Posting ───────────────────────────────────────────────────


def post_discord(message: str) -> bool:
    """Post to Discord via openclaw. Returns True on success."""
    try:
        result = subprocess.run(
            [
                "openclaw", "message", "send",
                "--channel", "discord",
                "--account", "default",
                "--target", DISCORD_CHANNEL,
                "--message", message,
            ],
            capture_output=True, text=True, timeout=30,
        )
        return result.returncode == 0
    except Exception:
        return False


# ── Main ──────────────────────────────────────────────────────────────


def main() -> int:
    state = load_state()
    alerts: list[str] = []

    for check in CHECKS:
        msg = run_check(check)
        if msg and should_alert(state, check.name):
            alerts.append(msg)
            record_alert(state, check.name)

    # Also report /tmp/xerahs-* size for U10 visibility
    tmp_report = ""
    try:
        import glob
        tmp_dirs = glob.glob("/tmp/xerahs-*")
        total_files = 0
        total_bytes = 0
        for d in tmp_dirs:
            for root, _, files in os.walk(d):
                for f in files:
                    fp = os.path.join(root, f)
                    try:
                        total_bytes += os.path.getsize(fp)
                        total_files += 1
                    except OSError:
                        pass
        if total_files > 0:
            tmp_report = f"\n/tmp/xerahs-* stats: {total_files} files, {total_bytes // 1024} KiB"
    except Exception:
        pass

    if not alerts:
        print(f"WATCHDOG_OK all {len(CHECKS)} checks passed{tmp_report}")
        save_state_atomic(state)
        return 0

    # Build the alert message
    header = f"🚨 XerahS Liveness Watchdog — {len(alerts)} alert(s) at {now_utc().strftime('%Y-%m-%d %H:%M UTC')}"
    body = "\n".join(f"• {a}" for a in alerts)
    full_message = f"{header}\n\n{body}{tmp_report}"

    print(full_message)

    # Try Discord
    if post_discord(full_message):
        print("WATCHDOG_ALERT_SENT via Discord")
    else:
        # Fallback: write to file
        FAILED_ALERT_FILE.parent.mkdir(parents=True, exist_ok=True)
        FAILED_ALERT_FILE.write_text(full_message)
        print(f"WATCHDOG_ALERT_FAILED discord unreachable; written to {FAILED_ALERT_FILE}")
        save_state_atomic(state)
        return 1

    save_state_atomic(state)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as exc:
        print(f"WATCHDOG_FAILED: {exc}", file=sys.stderr)
        sys.exit(1)
