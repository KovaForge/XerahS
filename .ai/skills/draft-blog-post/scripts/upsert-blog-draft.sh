#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: upsert-blog-draft.sh [options]

Options:
  --blog-root <path>         Blog root folder (default: docs/blog)
  --date <yyyy-MM-dd|yyyyMMdd>
  --utc-offset-hours <int>   Offset range: -12..14 (default: 8)
  --section <name>           One of: Features, Fixes, Build and Tooling, Commits Reviewed, Notes
  --bullet <text>            Bullet content to add to the section
  -h, --help                 Show this help
USAGE
}

BLOG_ROOT="docs/blog"
DATE_INPUT=""
UTC_OFFSET_HOURS="8"
SECTION=""
BULLET=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --blog-root)
      BLOG_ROOT="${2:-}"
      shift 2
      ;;
    --date)
      DATE_INPUT="${2:-}"
      shift 2
      ;;
    --utc-offset-hours)
      UTC_OFFSET_HOURS="${2:-}"
      shift 2
      ;;
    --section)
      SECTION="${2:-}"
      shift 2
      ;;
    --bullet)
      BULLET="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Error: unknown option '$1'" >&2
      usage >&2
      exit 1
      ;;
  esac
done

if [[ ! "$UTC_OFFSET_HOURS" =~ ^-?[0-9]+$ ]]; then
  echo "Error: --utc-offset-hours must be an integer." >&2
  exit 1
fi

if (( UTC_OFFSET_HOURS < -12 || UTC_OFFSET_HOURS > 14 )); then
  echo "Error: --utc-offset-hours must be in range -12..14." >&2
  exit 1
fi

if [[ -n "$SECTION" || -n "$BULLET" ]]; then
  if [[ -z "$SECTION" || -z "$BULLET" ]]; then
    echo "Error: provide both --section and --bullet together." >&2
    exit 1
  fi
fi

python3 - "$BLOG_ROOT" "$DATE_INPUT" "$UTC_OFFSET_HOURS" "$SECTION" "$BULLET" <<'PY'
import datetime as dt
import re
import sys
from pathlib import Path

blog_root = Path(sys.argv[1])
date_input = sys.argv[2]
offset_hours = int(sys.argv[3])
section = sys.argv[4]
bullet = sys.argv[5]

allowed_sections = {
    "Features",
    "Fixes",
    "Build and Tooling",
    "Commits Reviewed",
    "Notes",
}

if section and section not in allowed_sections:
    raise SystemExit(
        "Error: --section must be one of: Features, Fixes, Build and Tooling, Commits Reviewed, Notes"
    )

def target_date() -> dt.date:
    if not date_input:
        tz = dt.timezone(dt.timedelta(hours=offset_hours))
        return dt.datetime.now(dt.timezone.utc).astimezone(tz).date()
    for fmt in ("%Y-%m-%d", "%Y%m%d"):
        try:
            return dt.datetime.strptime(date_input, fmt).date()
        except ValueError:
            pass
    raise SystemExit("Error: --date must use yyyy-MM-dd or yyyyMMdd.")

def new_template_content(target: dt.date) -> list[str]:
    date_text = target.strftime("%Y-%m-%d")
    offset_text = f"UTC+{offset_hours}" if offset_hours >= 0 else f"UTC{offset_hours}"
    return [
        f"# XerahS Daily Blog Draft - {date_text}",
        "",
        f"Date: {date_text}",
        f"Time Zone: {offset_text}",
        "Status: Draft",
        "",
        "## Summary",
        "",
        "TBD",
        "",
        "## Features",
        "",
        "- TBD",
        "",
        "## Fixes",
        "",
        "- TBD",
        "",
        "## Build and Tooling",
        "",
        "- TBD",
        "",
        "## Commits Reviewed",
        "",
        "- TBD",
        "",
        "## Notes",
        "",
        "- TBD",
        "",
    ]

def add_section_bullet(path: Path, target_section: str, section_bullet: str) -> None:
    lines = path.read_text(encoding="utf-8").splitlines()
    heading = f"## {target_section}"
    try:
        heading_index = lines.index(heading)
    except ValueError:
        raise SystemExit(f"Error: section '{target_section}' was not found in {path}.")

    end_index = len(lines)
    for i in range(heading_index + 1, len(lines)):
        if lines[i].startswith("## "):
            end_index = i
            break

    normalized = f"- {section_bullet.strip()}"
    for i in range(heading_index + 1, end_index):
        if lines[i].strip() == normalized:
            return

    i = end_index - 1
    while i > heading_index:
        trimmed = lines[i].strip()
        if trimmed in {"- TBD", "TBD"}:
            del lines[i]
            end_index -= 1
        i -= 1

    insert_index = heading_index + 1
    if insert_index >= len(lines) or lines[insert_index] != "":
        lines.insert(insert_index, "")
        insert_index += 1
        end_index += 1
    else:
        insert_index += 1

    while insert_index < end_index and lines[insert_index].startswith("- "):
        insert_index += 1

    if insert_index < len(lines) and lines[insert_index] != "":
        lines.insert(insert_index, "")

    lines.insert(insert_index, normalized)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")

date_value = target_date()
year_folder = date_value.strftime("%Y")
month_folder = date_value.strftime("%Y-%m")
file_name = f"blog-{date_value.strftime('%Y%m%d')}.md"
directory_path = blog_root / year_folder / month_folder
file_path = directory_path / file_name

directory_path.mkdir(parents=True, exist_ok=True)

if not file_path.exists():
    file_path.write_text("\n".join(new_template_content(date_value)) + "\n", encoding="utf-8")

if section:
    add_section_bullet(file_path, section, bullet)

print(str(file_path))
PY
