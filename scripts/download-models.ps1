# Downloads the ONNX embedding model and vocabulary file required by the semantic cache.
# Files are placed in src/Ithil.Gateway/models/ which is gitignored (binaries, ~23 MB total).
# Run once after cloning, or again to update to the latest model files.

$ErrorActionPreference = "Stop"

$dest = Join-Path $PSScriptRoot "..\src\Ithil.Gateway\models"
New-Item -ItemType Directory -Force -Path $dest | Out-Null

$base = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main"

Write-Host "Downloading all-MiniLM-L6-v2.onnx (~22 MB)..."
Invoke-WebRequest -Uri "$base/onnx/model.onnx" -OutFile (Join-Path $dest "all-MiniLM-L6-v2.onnx")

Write-Host "Downloading vocab.txt..."
Invoke-WebRequest -Uri "$base/vocab.txt" -OutFile (Join-Path $dest "vocab.txt")

Write-Host "Done. Model files written to $dest"
