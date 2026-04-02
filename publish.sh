#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_PATH="$SCRIPT_DIR/SmartMouth.App/SmartMouth.App.csproj"
OUTPUT_DIR="$SCRIPT_DIR/publish/win-x64"

dotnet publish "$PROJECT_PATH" \
  -c Release \
  -f net9.0-windows \
  -r win-x64 \
  --self-contained false \
  -o "$OUTPUT_DIR"

echo "Published to: $OUTPUT_DIR"
