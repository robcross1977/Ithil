#!/usr/bin/env bash
# Downloads the ONNX embedding model and vocabulary file required by the semantic cache.
# Files are placed in src/Ithil.Gateway/models/ which is gitignored (binaries, ~23 MB total).
# Run once after cloning, or again to update to the latest model files.

set -euo pipefail

DEST="$(cd "$(dirname "$0")/.." && pwd)/src/Ithil.Gateway/models"
mkdir -p "$DEST"

BASE="https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main"

echo "Downloading all-MiniLM-L6-v2.onnx (~22 MB)..."
curl -fL --progress-bar "$BASE/onnx/model.onnx" -o "$DEST/all-MiniLM-L6-v2.onnx"

echo "Downloading vocab.txt..."
curl -fL --progress-bar "$BASE/vocab.txt" -o "$DEST/vocab.txt"

echo "Done. Model files written to $DEST"
