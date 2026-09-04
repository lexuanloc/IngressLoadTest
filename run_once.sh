#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# A load generator can create many concurrent sockets.
# Ignore failure if the shell/user is not allowed to raise this limit.
ulimit -n 65535 2>/dev/null || true

exec dotnet IngressLoadTest.dll
