#!/usr/bin/env python3

from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path


MOJIBAKE_FRAGMENTS = (
    "ðŸ",
    "â€”",
    "â€“",
    "â†",
    "â€¢",
    "âˆ",
    "â”",
    "âœ",
    "âš",
    "â",
    "Ã",
    "Â",
)


def git_output(repo_root: Path, *args: str) -> list[str]:
    result = subprocess.run(
        ["git", "-C", str(repo_root), *args],
        check=True,
        capture_output=True,
        text=True,
    )
    return [line for line in result.stdout.splitlines() if line]


def get_repo_root() -> Path:
    result = subprocess.run(
        ["git", "rev-parse", "--show-toplevel"],
        check=True,
        capture_output=True,
        text=True,
    )
    return Path(result.stdout.strip())


def tracked_markdown_files(repo_root: Path) -> list[Path]:
    return [repo_root / path for path in git_output(repo_root, "ls-files", "*.md")]


def staged_markdown_files(repo_root: Path) -> list[Path]:
    staged = git_output(repo_root, "diff", "--cached", "--name-only", "--diff-filter=ACM")
    return [repo_root / path for path in staged if path.endswith(".md") and (repo_root / path).is_file()]


def resolve_paths(repo_root: Path, raw_paths: list[str]) -> list[Path]:
    resolved: list[Path] = []
    seen: set[Path] = set()

    for raw_path in raw_paths:
        path = Path(raw_path)
        if not path.is_absolute():
            path = repo_root / path
        path = path.resolve()
        if path.is_file() and path.suffix.lower() == ".md" and path not in seen:
            resolved.append(path)
            seen.add(path)

    return resolved


def scan_markdown(path: Path) -> list[tuple[int, str, str]]:
    findings: list[tuple[int, str, str]] = []
    data = path.read_bytes()

    if data.startswith(b"\xef\xbb\xbf"):
        findings.append((1, "UTF-8 BOM present", "File begins with a BOM marker"))

    try:
        text = data.decode("utf-8")
    except UnicodeDecodeError as exc:
        findings.append((1, "File is not valid UTF-8", str(exc)))
        return findings

    for line_number, line in enumerate(text.splitlines(), 1):
        for fragment in MOJIBAKE_FRAGMENTS:
            if fragment in line:
                excerpt = line.strip().replace("\t", " ")
                findings.append((line_number, f"Suspicious mojibake fragment {fragment!r}", excerpt[:200]))
                break

    return findings


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Detect mojibake and BOM issues in tracked Markdown files."
    )
    parser.add_argument("paths", nargs="*", help="Optional Markdown paths to scan")
    parser.add_argument(
        "--staged",
        action="store_true",
        help="Scan staged Markdown files instead of all tracked Markdown files",
    )
    args = parser.parse_args()

    repo_root = get_repo_root()

    if args.paths:
        markdown_files = resolve_paths(repo_root, args.paths)
    elif args.staged:
        markdown_files = staged_markdown_files(repo_root)
    else:
        markdown_files = tracked_markdown_files(repo_root)

    if not markdown_files:
        print("OK: No Markdown files to scan.")
        return 0

    all_findings: list[tuple[Path, int, str, str]] = []
    for markdown_file in markdown_files:
        for line_number, message, excerpt in scan_markdown(markdown_file):
            all_findings.append((markdown_file, line_number, message, excerpt))

    if not all_findings:
        print(f"OK: Scanned {len(markdown_files)} Markdown file(s); no mojibake or BOM issues found.")
        return 0

    print("Markdown hygiene check failed:")
    for path, line_number, message, excerpt in all_findings:
        relative_path = path.relative_to(repo_root)
        print(f"- {relative_path}:{line_number}: {message}")
        print(f"  {excerpt}")

    print("")
    print("Fix the Markdown encoding issues above before committing.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
