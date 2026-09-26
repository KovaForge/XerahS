#!/usr/bin/env python3
"""Classify every upstream ShareX.ImageEditor file changed since the last XerahS sync.

Usage:
    python3 triage.py <ShareXRepo> <XerahSRoot> <last_sync> [head] [--diff-dir DIR]

Reads upstream base/head content directly from git objects (no worktree needed) and
compares it with the mapped file under <XerahSRoot>/ShareX.ImageEditor/src/ShareX.ImageEditor.

Categories (see SKILL.md step 2e-triage):
  SKIP_I18N     under Localization/ - never port (core rule 19)
  NO_OP         upstream base == head after normalization and REWRITES (EOL/BOM/header,
                using-order churn, or a move to ShareX.AvaloniaUI.Theming)
  AVALONIA_NS   upstream delta only touches using/xmlns lines not covered by REWRITES;
                confirm the namespace exists in XerahS, otherwise keep XerahS
  UP_DELETED    upstream deleted the file; decide per manifest whether XerahS still needs it
  ALREADY       XerahS already equals upstream head
  SAFE_SYNC     XerahS equals upstream base after REWRITES, so syncing head is safe; use
                sync.py so REWRITES and the target's header/BOM/EOL are applied
  NEW           absent from XerahS (Integration/* is mapped to Hosting/ first)
  DIVERGED      real XerahS adaptation + upstream change; needs a manual merge

With --diff-dir, writes <file>.base-xerahs.diff and <file>.base-head.diff for DIVERGED
and AVALONIA_NS files so the merge can be reviewed quickly.
"""

import argparse
import difflib
import os
import re
import subprocess
import sys
from collections import defaultdict

PREFIX = "ShareX.ImageEditor/"
HEADER_RE = re.compile(r"\A\s*#region License Information.*?#endregion License Information[^\n]*\n", re.S)
USING_RE = re.compile(r"^\s*(global\s+)?using\s+[\w.=\s]+;\s*$")
XMLNS_RE = re.compile(r'^\s*xmlns:\w+="(using|clr-namespace):[^"]*"\s*/?>?\s*$')
URI_RE = re.compile(r"avares://ShareX\.Avalonia[^\"']*")
# Upstream -> XerahS rewrites (core rule 17). Applied to upstream text before comparing, so
# files whose only XerahS divergence is this rewrite classify as SAFE_SYNC/ALREADY/NO_OP.
REWRITES = [
    ("ShareX.AvaloniaUI.Theming", "ShareX.ImageEditor.Presentation.Theming"),
    ("avares://ShareX.Avalonia/Assets#lucide", "avares://ShareX.ImageEditor/Assets#lucide"),
    ("ShareX.ImageEditor.Integration", "ShareX.ImageEditor.Hosting"),
    ("ImageEditorIntegration", "AvaloniaIntegration"),
]
NAME_MAP = [("Integration/ImageEditorIntegration.cs", "Hosting/AvaloniaIntegration.cs"), ("Integration/", "Hosting/")]


def git(repo, *args, check=True):
    r = subprocess.run(["git", "-C", repo, *args], capture_output=True)
    if check and r.returncode != 0:
        sys.exit(f"git {' '.join(args)} failed: {r.stderr.decode(errors='replace')}")
    return r


def show(repo, ref, path):
    r = git(repo, "show", f"{ref}:{path}", check=False)
    return r.stdout.decode("utf-8-sig", errors="replace") if r.returncode == 0 else None


def norm(text, rewrite=False):
    """Strip BOM/EOL/header/blank-line noise, apply XerahS rewrites, and sort usings."""
    if text is None:
        return None
    text = text.lstrip("﻿").replace("\r\n", "\n").replace("\r", "\n")
    text = HEADER_RE.sub("", text, count=1)
    if rewrite:
        for src, dst in REWRITES:
            text = text.replace(src, dst)
    lines = [ln.rstrip() for ln in text.split("\n") if ln.strip()]
    usings = sorted({ln.strip() for ln in lines if USING_RE.match(ln)})
    body = [ln for ln in lines if not USING_RE.match(ln)]
    return "\n".join(usings + body).strip()


def only_namespace_moves(base, head):
    """True when every changed line is a using/xmlns/avares URI line."""
    changed = [ln[1:] for ln in difflib.unified_diff(base.split("\n"), head.split("\n"), lineterm="", n=0)
               if ln[:1] in "+-" and not ln.startswith(("+++", "---"))]
    return bool(changed) and all(USING_RE.match(ln) or XMLNS_RE.match(ln) or URI_RE.search(ln) or not ln.strip()
                                 for ln in changed)


def map_target(rel):
    for src, dst in NAME_MAP:
        if rel.startswith(src):
            return dst + rel[len(src):]
    return rel


def write_diff(diff_dir, rel, name, a, b):
    out = os.path.join(diff_dir, rel.replace("/", "__") + f".{name}.diff")
    with open(out, "w", encoding="utf-8") as f:
        f.writelines(ln + "\n" for ln in difflib.unified_diff((a or "").split("\n"), (b or "").split("\n"),
                                                              f"a/{rel}", f"b/{rel}", lineterm=""))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("sharex_repo")
    ap.add_argument("xerahs_root")
    ap.add_argument("last_sync")
    ap.add_argument("head", nargs="?", default="HEAD")
    ap.add_argument("--diff-dir")
    a = ap.parse_args()

    if git(a.sharex_repo, "cat-file", "-e", f"{a.last_sync}^{{commit}}", check=False).returncode != 0:
        sys.exit(f"{a.last_sync} is not in {a.sharex_repo}. If it is a shallow clone, run:\n"
                 f"  git -C \"{a.sharex_repo}\" fetch --shallow-since=<date before last sync> origin <branch>")

    code_root = os.path.join(a.xerahs_root, "ShareX.ImageEditor", "src", "ShareX.ImageEditor")
    changes = git(a.sharex_repo, "diff", "--name-status", "--no-renames", f"{a.last_sync}..{a.head}", "--",
                  "ShareX.ImageEditor").stdout.decode().splitlines()
    if a.diff_dir:
        os.makedirs(a.diff_dir, exist_ok=True)

    result = defaultdict(list)
    for line in changes:
        status, path = line.split("\t", 1)
        rel = path[len(PREFIX):]
        target_rel = map_target(rel)
        label = rel if target_rel == rel else f"{rel} -> {target_rel}"

        if rel.startswith("Localization/"):
            result["SKIP_I18N"].append(label)
            continue

        base = norm(show(a.sharex_repo, a.last_sync, path), rewrite=True)
        head = norm(show(a.sharex_repo, a.head, path), rewrite=True)
        target_path = os.path.join(code_root, target_rel)
        xerahs = None
        if os.path.exists(target_path):
            with open(target_path, encoding="utf-8-sig", errors="replace") as f:
                xerahs = norm(f.read())

        if status == "D":
            cat = "UP_DELETED" if xerahs is not None else "ALREADY"
        elif base == head:
            cat = "NO_OP"
        elif xerahs is None:
            cat = "NEW"
        elif xerahs == head:
            cat = "ALREADY"
        elif base is not None and only_namespace_moves(base, head):
            cat = "AVALONIA_NS"
        elif xerahs == base:
            cat = "SAFE_SYNC"
        else:
            cat = "DIVERGED"

        result[cat].append(label)
        if a.diff_dir and cat in ("DIVERGED", "AVALONIA_NS"):
            write_diff(a.diff_dir, rel, "base-xerahs", base, xerahs)
            write_diff(a.diff_dir, rel, "base-head", base, head)

    order = ["DIVERGED", "NEW", "SAFE_SYNC", "UP_DELETED", "AVALONIA_NS", "ALREADY", "NO_OP", "SKIP_I18N"]
    print("Summary: " + ", ".join(f"{c}={len(result[c])}" for c in order if result[c]))
    for c in order:
        if result[c]:
            print(f"\n## {c} ({len(result[c])})")
            for item in sorted(result[c]):
                print(f"  {item}")


if __name__ == "__main__":
    main()
