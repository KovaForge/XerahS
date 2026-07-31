# Graphify Agent Prompt — XerahS `src/` Knowledge Graph

Use this document as a **system / kickoff prompt** for any coding agent working in the XerahS repo (Cursor, Claude Code, Codex, Copilot, Gemini CLI, Hermes, OpenClaw, Windsurf, Aider, or similar).

It tells the agent how to use the checked-in graphify knowledge graph instead of blind repo greps for architecture and dependency questions.

---

## Copy-paste prompt (for other agents)

```text
You are working in the XerahS repository (Avalonia ShareX port).

Before broad Grep/Glob sweeps for architecture, dependency, or
"how does X connect to Y" questions, use the checked-in graphify
knowledge graph of `src/`.

### Canonical graph artifacts (read these)
- docs/architecture/graphify-out/README.md
- docs/architecture/graphify-out/GRAPH_REPORT.md
- docs/architecture/graphify-out/graph.json
- docs/architecture/graphify-out/GRAPH_TREE.html   (lighter browser view)
- docs/architecture/graphify-out/graph.html        (full force graph; large)

Root convenience symlink (may exist): graphify-out/ → docs/architecture/graphify-out/

### Skills to read / follow
- AGENTS.md
- developers/guidelines/AGENT_WORKFLOW.md
- developers/guidelines/GRAPHIFY_AGENT_PROMPT.md   (this file)
- .ai/skills/graphify/SKILL.md
- .ai/skills/architecture-guidelines/SKILL.md
- .ai/skills/coding-standards/SKILL.md
- .ai/skills/git-workflow/SKILL.md
- docs/architecture/PORTING_GUIDE.md
- docs/architecture/xerahs_architecture_map.md
- docs/architecture/MULTI_AGENT_COORDINATION.md

### Tools / CLI paths
Preferred graphify binary (repo-local venv — always use this exact path; do not invent a global command):
  .tools/graphify-venv/bin/graphify

Package: `graphifyy` (double-y). The repo-local venv is gitignored. A global `uv tool install graphifyy` / `pipx install graphifyy` is an optional fallback — it is NOT required to use the graph.

Graph JSON:
  docs/architecture/graphify-out/graph.json

Rebuild script (preferred — handles extract + cluster + tree + symlink):
  scripts/update-graphify.sh

If the venv is missing, bootstrap once:
  python3 -m venv .tools/graphify-venv
  .tools/graphify-venv/bin/pip install -U pip graphifyy

Query examples (run from repo root):
  G=.tools/graphify-venv/bin/graphify
  GRAPH=docs/architecture/graphify-out/graph.json

  $G query    "how does region capture start a WorkerTask" --graph "$GRAPH"
  $G path     "WorkerTask" "AvaloniaUIService"          --graph "$GRAPH"
  $G explain  "WorkerTask"                              --graph "$GRAPH"
  $G affected "WorkerTask"                              --graph "$GRAPH"
  # Note: `affected` requires a name that UNIQUELY matches a node.
  # If you see "No unique node match", widen the name to the exact symbol
  # (e.g. WorkerTask, UploaderInstanceViewModel, GitHubUpdateChecker).

Help caveat: `graphify <subcommand> --help` is unreliable and may even
be mis-parsed as a query. Use the top-level form only:
  $G --help

### When to use graphify
USE FIRST for:
- architecture orientation in unfamiliar areas of src/
- finding paths between types / projects
- "what depends on X?" / blast-radius questions
- cross-boundary desktop ↔ platform ↔ plugins questions

DO NOT require graphify first for:
- ordinary line-level edits once you already know the files
- targeted Read of a known path
- build / test / format / commit workflows

### After large structural edits
Preferred — just rerun the script (it scopes to src/ and refreshes
graphify-out/ + the root symlink):
  scripts/update-graphify.sh

If you must drive the CLI directly, scope to src/ (not the whole repo root)
and DO NOT pass --graph to update — it is not a valid flag:
  $G update src          # updates the graph in place; graph path comes from
                         # the repo-root graphify-out/ symlink (default).
  # WRONG, do NOT use:
  #   $G update .                       # rescans the whole repo root (~1273 files)
  #   $G update src --graph "$GRAPH"    # error: unknown update option: --graph

Keep --code-only unless the user explicitly wants a docs/LLM semantic pass.

### Guardrails
- Follow AGENTS.md (TFM, TreatWarningsAsErrors, SkiaSharp pin, git wrappers).
- Do not invent APIs from graph nodes alone; open the cited source file to confirm.
- Axaml is not in the AST graph; for UI markup use Read/Grep on .axaml after code orientation.
```

---

## Path index

### Graph artifacts

| Path | Role |
|---|---|
| `docs/architecture/graphify-out/README.md` | Human + agent entry for this graph |
| `docs/architecture/graphify-out/GRAPH_REPORT.md` | God nodes, hubs, surprising links, suggested questions |
| `docs/architecture/graphify-out/graph.json` | Machine graph for `query` / `path` / `explain` / `affected` |
| `docs/architecture/graphify-out/graph.html` | Interactive force graph (~21MB; may be slow) |
| `docs/architecture/graphify-out/GRAPH_TREE.html` | Collapsible tree view (prefer in browser) |
| `docs/architecture/graphify-out/manifest.json` | Extraction manifest |
| `graphify-out/` | Optional root symlink to the folder above |

### Skills and policy docs

| Path | Role |
|---|---|
| `AGENTS.md` | Single source of truth for repo agent policy |
| `developers/guidelines/AGENT_WORKFLOW.md` | Universal plan / delegate / verify workflow |
| `developers/guidelines/GRAPHIFY_AGENT_PROMPT.md` | This prompt + path index |
| `developers/guidelines/CODING_STANDARDS.md` | Coding / license standards |
| `developers/guidelines/DOCUMENTATION_STANDARDS.md` | Where docs belong |
| `developers/lessons-learnt/general.md` | Durable repo lessons |
| `.ai/skills/graphify/SKILL.md` | Graphify skill for assistants that load `.ai/skills` |
| `.ai/skills/architecture-guidelines/SKILL.md` | Platform abstraction / porting rules |
| `.ai/skills/coding-standards/SKILL.md` | Coding standards skill |
| `.ai/skills/git-workflow/SKILL.md` | Commit / version / push rules |
| `.ai/skills/port-imageeditor/SKILL.md` | ImageEditor port (separate from `src/` graph) |
| `docs/architecture/PORTING_GUIDE.md` | Porting + platform abstractions |
| `docs/architecture/xerahs_architecture_map.md` | Architecture map |
| `docs/architecture/MULTI_AGENT_COORDINATION.md` | Multi-agent boundaries |

### Tools and scripts

| Path | Role |
|---|---|
| `.tools/graphify-venv/bin/graphify` | Preferred local CLI (`graphifyy` package; venv is gitignored) |
| `.tools/graphify-venv/bin/python` | Interpreter that can `import graphify` |
| `scripts/update-graphify.sh` | Full rebuild of the `src/` graph into `docs/architecture/graphify-out/` |
| `uv tool install graphifyy` / `pipx install graphifyy` | Alternate global installs if the repo venv is unavailable |

### Host-specific skill installs (optional)

These may exist on a given machine after `graphify install` / `graphify cursor install`. Prefer the **repo-tracked** paths above so every agent sees the same instructions.

| Path | Host |
|---|---|
| `.cursor/rules/graphify.mdc` | Cursor (tracked exception under `.cursor/`) |
| `~/.agents/skills/graphify/SKILL.md` | Agents / Gemini-style skill home |
| Host `graphify install --platform …` targets | Claude Code, Codex, OpenCode, Hermes, etc. |

---

## Recommended orientation sequence

1. Skim `docs/architecture/graphify-out/GRAPH_REPORT.md` (God Nodes + Surprising Connections).
2. Run `graphify query` / `path` / `explain` with `--graph docs/architecture/graphify-out/graph.json`.
3. Open the cited `.cs` / `.swift` / etc. files to confirm.
4. Only then Grep/Read for markup (`.axaml`) or strings the AST graph cannot see.
5. After large structural changes, run `scripts/update-graphify.sh`.

---

## Scope notes

- Graph coverage: `src/` only (desktop, platform, tools, mobile). Not the full repo root.
- Mode: `--code-only` AST (tree-sitter). No LLM cost to query the checked-in graph.
- Not covered well: `.axaml`, pure docs, `ShareX.ImageEditor` submodule (build a second graph beside this one if needed).
- Freshness: see `GRAPH_REPORT.md` → Graph Freshness (commit SHA). Compare with `git rev-parse HEAD`.
