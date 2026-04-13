#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: update-changelog.sh [options]

Options:
  --version <X.Y.Z>
  --from-tag <tag>
  --changelog-path <path>    Default: docs/CHANGELOG.md
  --apply                    Apply generated section to changelog
  --include-merges           Include merge commits
  --output-path <path>       Write generated section to this file
  -h, --help                 Show this help
USAGE
}

VERSION=""
FROM_TAG=""
CHANGELOG_PATH="docs/CHANGELOG.md"
APPLY=0
INCLUDE_MERGES=0
OUTPUT_PATH=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --version)
      VERSION="${2:-}"
      shift 2
      ;;
    --from-tag)
      FROM_TAG="${2:-}"
      shift 2
      ;;
    --changelog-path)
      CHANGELOG_PATH="${2:-}"
      shift 2
      ;;
    --apply)
      APPLY=1
      shift
      ;;
    --include-merges)
      INCLUDE_MERGES=1
      shift
      ;;
    --output-path)
      OUTPUT_PATH="${2:-}"
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

python3 - "$VERSION" "$FROM_TAG" "$CHANGELOG_PATH" "$APPLY" "$INCLUDE_MERGES" "$OUTPUT_PATH" <<'PY'
import os
import re
import subprocess
import sys
from collections import defaultdict
from pathlib import Path

version_arg = sys.argv[1]
from_tag_arg = sys.argv[2]
changelog_path_arg = sys.argv[3]
apply_arg = sys.argv[4] == "1"
include_merges_arg = sys.argv[5] == "1"
output_path_arg = sys.argv[6]

def run(cmd):
    return subprocess.run(cmd, text=True, capture_output=True)

repo_root_proc = run(["git", "rev-parse", "--show-toplevel"])
if repo_root_proc.returncode != 0 or not repo_root_proc.stdout.strip():
    raise SystemExit("Error: not inside a git repository.")

repo_root = Path(repo_root_proc.stdout.strip())
os.chdir(repo_root)

def resolve_version(requested: str) -> str:
    if requested:
        if not re.match(r"^\d+\.\d+\.\d+$", requested):
            raise SystemExit(f"Error: version '{requested}' is invalid. Expected X.Y.Z.")
        return requested

    props_path = repo_root / "Directory.Build.props"
    if not props_path.exists():
        raise SystemExit("Error: Directory.Build.props not found at repository root.")

    m = re.search(r"<Version>\s*(\d+\.\d+\.\d+)\s*</Version>", props_path.read_text(encoding="utf-8"))
    if not m:
        raise SystemExit("Error: could not resolve <Version> from Directory.Build.props.")
    return m.group(1)

def resolve_from_tag(requested: str):
    if requested:
        return requested
    proc = run(["git", "describe", "--tags", "--abbrev=0"])
    if proc.returncode != 0:
        return None
    tag = proc.stdout.strip()
    return tag or None

def normalize_component(raw: str) -> str:
    component = re.sub(r"[_-]+", " ", raw.strip())
    component = re.sub(r"\s+", " ", component)
    if not component:
        return "Core"
    words = [w[:1].upper() + w[1:] if w else w for w in component.split(" ")]
    return " ".join(words)

def categorize_commit(subject: str):
    m = re.match(r"^\[v\d+\.\d+\.\d+\]\s+\[(?P<t>[^\]]+)\]\s+(?P<d>.+)$", subject)
    if m:
        ctype = m.group("t").strip().lower()
        desc = m.group("d").strip()
        component = "Core"
        p = re.match(r"^(?P<c>[A-Za-z0-9\/ .&+\-]+):\s*(?P<r>.+)$", desc)
        if p:
            component = normalize_component(p.group("c"))
            desc = p.group("r").strip()
        return (category_from_type(ctype), component, desc)

    m = re.match(r"^(?P<t>[a-zA-Z]+)(\((?P<s>[^)]+)\))?(!)?:\s*(?P<d>.+)$", subject)
    if m:
        ctype = m.group("t").lower()
        scope = m.group("s")
        desc = m.group("d").strip()
        component = "Core" if not scope else normalize_component(scope)
        return (category_from_type(ctype), component, desc)

    m = re.match(r"^(?P<c>[A-Za-z0-9\/ .&+\-]+):\s*(?P<d>.+)$", subject)
    if m:
        return ("Changed", normalize_component(m.group("c")), m.group("d").strip())

    return ("Changed", "Core", subject.strip())

def category_from_type(ctype: str) -> str:
    mapping = {
        "feat": "Features",
        "feature": "Features",
        "fix": "Fixes",
        "refactor": "Refactor",
        "build": "Build",
        "ci": "Build",
        "chore": "Build",
        "infra": "Build",
        "infrastructure": "Build",
        "docs": "Documentation",
        "doc": "Documentation",
        "test": "Testing",
        "tests": "Testing",
        "testing": "Testing",
        "perf": "Performance",
        "performance": "Performance",
    }
    return mapping.get(ctype, "Changed")

def get_commit_rows(from_tag, include_merges):
    rng = "HEAD" if not from_tag else f"{from_tag}..HEAD"
    cmd = ["git", "log", rng, "--pretty=format:%h\x1f%s\x1f%an"]
    if not include_merges:
        cmd.append("--no-merges")
    proc = run(cmd)
    if proc.returncode != 0:
        raise SystemExit("Error: failed to read commits from git log.")
    raw = proc.stdout.strip()
    if not raw:
        return []
    rows = []
    for line in raw.splitlines():
        parts = line.split("\x1f")
        if len(parts) < 3:
            continue
        rows.append({"hash": parts[0].strip(), "subject": parts[1].strip(), "author": parts[2].strip()})
    return rows

def build_section(version: str, commits):
    grouped = {}
    release_re = re.compile(r"^\[v\d+\.\d+\.\d+\]\s+\[CI\]\s+Release\s+v\d+\.\d+\.\d+$")

    for row in commits:
        if release_re.match(row["subject"]):
            continue
        category, component, desc = categorize_commit(row["subject"])
        key = (category, component, desc)
        entry = grouped.setdefault(key, {"category": category, "component": component, "desc": desc, "hashes": set()})
        entry["hashes"].add(row["hash"])

    order = ["Features", "Fixes", "Refactor", "Build", "Documentation", "Testing", "Performance", "Changed"]
    by_category = defaultdict(list)
    for entry in grouped.values():
        by_category[entry["category"]].append(entry)

    lines = [f"## v{version}", ""]
    for category in order:
        entries = sorted(by_category.get(category, []), key=lambda e: (e["component"], e["desc"]))
        if not entries:
            continue
        lines.append(f"### {category}")
        for e in entries:
            hashes = ", ".join(sorted(e["hashes"]))
            lines.append(f"- **{e['component']}**: {e['desc']} ({hashes})")
        lines.append("")

    if len(lines) == 2:
        lines.extend(["### Changed", "- No user-facing commits were detected in this range.", ""])

    return "\n".join(lines).rstrip() + "\n"

def upsert_section(content: str, version: str, section: str) -> str:
    escaped = re.escape(version)
    pattern = re.compile(rf"(?ms)^## v{escaped}\s*$.*?(?=^## v\d+\.\d+\.\d+\s*$|\Z)")
    if pattern.search(content):
        return pattern.sub(section.rstrip() + "\n", content)

    m = re.search(r"(?m)^## Unreleased\s*$", content)
    if m:
        idx = m.end()
        insertion = "\n\n" + section.rstrip() + "\n"
        return content[:idx] + insertion + content[idx:]

    return section.rstrip() + "\n\n" + content

resolved_version = resolve_version(version_arg)
resolved_from_tag = resolve_from_tag(from_tag_arg)
commits = get_commit_rows(resolved_from_tag, include_merges_arg)
section = build_section(resolved_version, commits)

if output_path_arg:
    out = Path(output_path_arg)
    if not out.is_absolute():
        out = repo_root / out
    out.write_text(section, encoding="utf-8")

if apply_arg:
    changelog = Path(changelog_path_arg)
    if not changelog.is_absolute():
        changelog = repo_root / changelog
    if not changelog.exists():
        raise SystemExit(f"Error: changelog file not found: {changelog}")
    existing = changelog.read_text(encoding="utf-8")
    updated = upsert_section(existing, resolved_version, section)
    changelog.write_text(updated, encoding="utf-8")

print(f"Target version : v{resolved_version}")
print(f"From tag       : {resolved_from_tag if resolved_from_tag else '(none)'}")
print(f"Commits parsed : {len(commits)}")
if apply_arg:
    print(f"Applied to     : {changelog_path_arg}")
if output_path_arg:
    print(f"Draft output   : {output_path_arg}")
print("")
print(section, end="")
PY
