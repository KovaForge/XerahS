# XerahS `src/` Knowledge Graph (graphify)

Code-only AST knowledge graph of `/src` for agent navigation and architecture questions.

| Field | Value |
|---|---|
| Scope | `src/` (desktop, platform, tools, mobile) |
| Tool | [graphifyy](https://github.com/Graphify-Labs/graphify) v0.9.13 (`graphify` CLI) |
| Mode | `--code-only` (tree-sitter AST, no LLM) |
| Built from commit | see `GRAPH_REPORT.md` → Graph Freshness |
| Size (approx) | ~15k nodes · ~32k edges · ~650 communities |

## Artifacts

| File | Purpose |
|---|---|
| `GRAPH_REPORT.md` | God nodes, hubs, surprising connections, suggested questions |
| `graph.json` | Queryable graph (`query` / `path` / `explain` / `affected`) |
| `graph.html` | Interactive force graph (large; browser may be slow) |
| `GRAPH_TREE.html` | Collapsible tree view (lighter to open) |
| `manifest.json` | Extraction manifest |
| `cache/` | Local rebuild cache (**gitignored**) |

## Agent usage

Full cross-agent kickoff prompt (copy-paste block + skill/tool paths):

[`developers/guidelines/GRAPHIFY_AGENT_PROMPT.md`](../../../developers/guidelines/GRAPHIFY_AGENT_PROMPT.md)

Prefer these over broad greps for architecture / dependency questions:

```bash
G=.tools/graphify-venv/bin/graphify
GRAPH=docs/architecture/graphify-out/graph.json

$G query "how does region capture start a WorkerTask" --graph "$GRAPH"
$G path "WorkerTask" "AvaloniaUIService" --graph "$GRAPH"
$G explain "WorkerTask" --graph "$GRAPH"
$G affected "ICaptureService" --graph "$GRAPH"
```

If `graphify-out` exists at the repo root (symlink), you can omit `--graph`.

Read `GRAPH_REPORT.md` for a broad orientation pass. Open `GRAPH_TREE.html` or `graph.html` in a browser for visual exploration.

## Rebuild / update

Local CLI lives in the repo-local venv (not committed):

```bash
# one-time (or after cloning)
python3 -m venv .tools/graphify-venv
.tools/graphify-venv/bin/pip install -U pip graphifyy

# full rebuild of src/ — preferred, handles extract + cluster + tree + symlink
scripts/update-graphify.sh

# equivalent manual sequence (what the script runs)
.tools/graphify-venv/bin/graphify extract src --code-only --out docs/architecture --max-workers 1
GRAPHIFY_VIZ_NODE_LIMIT=20000 .tools/graphify-venv/bin/graphify cluster-only docs/architecture --no-label --graph docs/architecture/graphify-out/graph.json
.tools/graphify-venv/bin/graphify tree --graph docs/architecture/graphify-out/graph.json --output docs/architecture/graphify-out/GRAPH_TREE.html --root src --label "XerahS src"

# incremental after code edits (AST only). Note: no --graph flag — graph path
# comes from the repo-root `graphify-out` symlink (default). Do NOT pass `src --graph`
# (--graph is not a valid option for update) and do NOT pass `.`
# (that scans the whole repo root, ~1273 files, instead of src/).
.tools/graphify-venv/bin/graphify update src
```

Help caveat: `graphify <subcommand> --help` is unreliable; use top-level
`graphify --help` only.

## Notes

- Axaml / XAML is not AST-extracted (no `.axaml` language pack); C# / Swift / etc. are.
- iOS Swift `import Foundation` / `SwiftUI` style collisions produce drop-warnings; desktop C# graph is the primary signal.
- Optional second graphs (e.g. `ShareX.ImageEditor`) should live beside this folder, not replace it.
