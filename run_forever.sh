#!/usr/bin/env bash
set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

ulimit -n 65535 2>/dev/null || true

while true; do
  echo "[$(date '+%Y-%m-%d %H:%M:%S.%3N')] Starting IngressLoadTest.dll" >> log.txt

  dotnet IngressLoadTest.dll
  EXIT_CODE=$?

  echo "[$(date '+%Y-%m-%d %H:%M:%S.%3N')] IngressLoadTest.dll exited. ExitCode=$EXIT_CODE" >> log.txt
  echo "[$(date '+%Y-%m-%d %H:%M:%S.%3N')] Restarting after 5 seconds..." >> log.txt

  sleep 5
done
