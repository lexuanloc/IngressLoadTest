#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

dotnet publish IngressLoadTest.csproj \
  -c Release \
  --self-contained false \
  -o publish/portable

echo
echo "Portable publish created:"
echo "  $SCRIPT_DIR/publish/portable"
echo
echo "Run on Windows or Ubuntu:"
echo "  dotnet IngressLoadTest.dll"
