#!/usr/bin/env python3
"""Apply the mechanical part of an ImageEditor port after the manifest has been posted.

Usage:
    python3 sync.py <ShareXRepo> <XerahSRoot> <last_sync> [head] [--only CAT[,CAT]] [--dry-run] [--force]

Run it once per range on a clean submodule; it refuses to run over uncommitted changes.

Uses triage.py's classification, then for each file:
  SAFE_SYNC  write upstream head with REWRITES applied, keeping the target's header state
  NEW        write upstream head with REWRITES applied and the submodule-standard header
             (.cs only); files under Integration/ land in Hosting/
  DIVERGED   3-way merge (git merge-file) of upstream base -> head onto the XerahS file,
             all sides normalized to LF/no BOM/no header with REWRITES applied; conflicts
             are left as <<<<<<< xerahs / >>>>>>> upstream markers for manual resolution
Every other category is reported and left untouched. UP_DELETED files are never deleted:
removing XerahS files is a manifest decision.

Output is UTF-8 without BOM, LF line endings, trailing newline (SKILL.md 2e-headers).
Review `git diff` in the submodule afterwards; this script does not replace the manifest,
the known-adaptation checklist in SKILL.md step 3b, or the build gates.
"""

import argparse
import os
import re
import subprocess
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import triage  # noqa: E402

SUBMODULE_HEADER = """#region License Information (GPL v3)

/*
    ShareX.ImageEditor - The UI-agnostic Editor library for ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

"""


# Types ShareX moved out of ImageEditor (into ShareX.Avalonia) that XerahS keeps in the
# submodule. Upstream drops the `using` when it moves them; re-add it for any synced file
# that still references the type. Extend this when a build fails with CS0103/CS0246.
KEPT_TYPES = {
    "BitmapConversionHelpers": "ShareX.ImageEditor.Presentation.Rendering",
}


def ensure_kept_type_usings(text):
    for type_name, namespace in KEPT_TYPES.items():
        using = f"using {namespace};"
        if not re.search(rf"\b{type_name}\b", text) or using in text or f"namespace {namespace}" in text:
            continue
        plain = [m for m in re.finditer(r"^using (?!static)[^\n]*;\n", text, re.M)]
        if not plain:
            continue
        pos = next((m.start() for m in plain if m.group(0).strip() > using), plain[-1].end())
        text = text[:pos] + using + "\n" + text[pos:]
    return text


def clean(text, rewrite):
    """LF, no BOM, header removed; returns (header, body)."""
    text = text.lstrip("﻿").replace("\r\n", "\n").replace("\r", "\n")
    m = triage.HEADER_RE.match(text)
    header = ""
    if m:
        header = m.group(0)
        text = text[m.end():]
        # keep the blank line that follows the header with the header
        while text.startswith("\n"):
            header += "\n"
            text = text[1:]
    if rewrite:
        for src, dst in triage.REWRITES:
            text = text.replace(src, dst)
    return header, text


def write(path, content):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(content.rstrip("\n") + "\n")


def merge3(xerahs, base, head):
    with tempfile.TemporaryDirectory() as tmp:
        paths = []
        for name, text in (("xerahs", xerahs), ("base", base), ("upstream", head)):
            p = os.path.join(tmp, name)
            with open(p, "w", encoding="utf-8", newline="\n") as f:
                f.write(text)
            paths.append(p)
        r = subprocess.run(["git", "merge-file", "-p", "-L", "xerahs", "-L", "base", "-L", "upstream", *paths],
                           capture_output=True)
        if r.returncode < 0:
            sys.exit(f"git merge-file failed: {r.stderr.decode(errors='replace')}")
        return r.stdout.decode("utf-8"), r.returncode


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("sharex_repo")
    ap.add_argument("xerahs_root")
    ap.add_argument("last_sync")
    ap.add_argument("head", nargs="?", default="HEAD")
    ap.add_argument("--only", default="SAFE_SYNC,NEW,DIVERGED")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--force", action="store_true", help="run even if the submodule src tree is dirty")
    a = ap.parse_args()
    wanted = set(a.only.split(","))

    submodule = os.path.join(a.xerahs_root, "ShareX.ImageEditor")
    dirty = triage.git(submodule, "status", "--porcelain", "--", "src").stdout.strip()
    if dirty and not a.dry_run and not a.force:
        sys.exit("ShareX.ImageEditor/src has uncommitted changes. Commit or stash them first: sync.py\n"
                 "writes whole files and would overwrite resolved conflicts. Use --force to override.")

    code_root = os.path.join(a.xerahs_root, "ShareX.ImageEditor", "src", "ShareX.ImageEditor")
    changes = triage.git(a.sharex_repo, "diff", "--name-status", "--no-renames", f"{a.last_sync}..{a.head}", "--",
                         "ShareX.ImageEditor").stdout.decode().splitlines()

    conflicts, written = [], []
    for line in changes:
        status, path = line.split("\t", 1)
        rel = path[len(triage.PREFIX):]
        if status == "D" or rel.startswith("Localization/"):
            continue
        target_rel = triage.map_target(rel)
        target = os.path.join(code_root, target_rel)
        base_raw = triage.show(a.sharex_repo, a.last_sync, path)
        head_raw = triage.show(a.sharex_repo, a.head, path)
        base_n = triage.norm(base_raw, rewrite=True)
        head_n = triage.norm(head_raw, rewrite=True)
        xerahs_raw = None
        if os.path.exists(target):
            with open(target, encoding="utf-8-sig", errors="replace") as f:
                xerahs_raw = f.read()
        xerahs_n = triage.norm(xerahs_raw)

        if base_n == head_n or xerahs_n == head_n:
            continue
        if xerahs_raw is None:
            cat = "NEW"
        elif base_n is not None and triage.only_namespace_moves(base_n, head_n):
            cat = "AVALONIA_NS"
        elif xerahs_n == base_n:
            cat = "SAFE_SYNC"
        else:
            cat = "DIVERGED"
        if cat not in wanted:
            continue

        _, head_body = clean(head_raw, rewrite=True)
        if cat == "NEW":
            content = (SUBMODULE_HEADER if target.endswith(".cs") else "") + head_body
            n = 0
        elif cat == "SAFE_SYNC":
            header, _ = clean(xerahs_raw, rewrite=False)
            content, n = header + head_body, 0
        else:
            header, xerahs_body = clean(xerahs_raw, rewrite=False)
            _, base_body = clean(base_raw or "", rewrite=True)
            merged, n = merge3(xerahs_body, base_body, head_body)
            content = header + merged

        if target.endswith(".cs"):
            content = ensure_kept_type_usings(content)
        if not a.dry_run:
            write(target, content)
        written.append(f"{cat:9} {target_rel}" + (f"  ({n} conflict(s))" if n else ""))
        if n:
            conflicts.append(target_rel)

    print("\n".join(written))
    print(f"\n{len(written)} file(s) {'would be ' if a.dry_run else ''}written; {len(conflicts)} with conflicts")
    for c in conflicts:
        print(f"  CONFLICT {c}")


if __name__ == "__main__":
    main()
