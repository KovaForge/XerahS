#!/usr/bin/env bash
# Rebuild the XerahS src/ knowledge graph into docs/architecture/graphify-out/
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

VENV="$ROOT/.tools/graphify-venv"
G="$VENV/bin/graphify"
OUT_DIR="$ROOT/docs/architecture"
GRAPH_DIR="$OUT_DIR/graphify-out"

if [[ ! -x "$G" ]]; then
  echo "Installing graphifyy into .tools/graphify-venv ..."
  python3 -m venv "$VENV"
  "$VENV/bin/pip" install -U pip graphifyy
fi

echo "Extracting src/ (code-only AST)..."
"$G" extract src --code-only --out "$OUT_DIR" --max-workers "${GRAPHIFY_MAX_WORKERS:-1}"

echo "Clustering + HTML..."
GRAPHIFY_VIZ_NODE_LIMIT="${GRAPHIFY_VIZ_NODE_LIMIT:-20000}" \
  "$G" cluster-only "$OUT_DIR" --no-label --graph "$GRAPH_DIR/graph.json"

echo "Tree HTML..."
"$G" tree \
  --graph "$GRAPH_DIR/graph.json" \
  --output "$GRAPH_DIR/GRAPH_TREE.html" \
  --root src \
  --label "XerahS src"

# Convenience symlink so default graphify-out/ paths resolve from repo root
if [[ ! -e "$ROOT/graphify-out" ]]; then
  ln -s docs/architecture/graphify-out "$ROOT/graphify-out"
  echo "Created symlink: graphify-out -> docs/architecture/graphify-out"
elif [[ -L "$ROOT/graphify-out" ]]; then
  :
else
  echo "Note: $ROOT/graphify-out exists and is not a symlink; leaving it alone."
fi

echo "Done."
echo "  Report: $GRAPH_DIR/GRAPH_REPORT.md"
echo "  Graph:  $GRAPH_DIR/graph.json"
echo "  HTML:   $GRAPH_DIR/graph.html"
echo "  Tree:   $GRAPH_DIR/GRAPH_TREE.html"
