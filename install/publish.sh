#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/artifacts/publish"
mkdir -p "$OUT"
# Core/Protocols always; App only on Windows agents
dotnet publish "$ROOT/src/HostStation.Protocols/HostStation.Protocols.csproj" -c Release -o "$OUT/protocols"
if [[ "$(uname -s)" == MINGW* || "$(uname -s)" == MSYS* || "$(uname -s)" == CYGWIN* || -n "${FORCE_WPF_PUBLISH:-}" ]]; then
  dotnet publish "$ROOT/src/HostStation.App/HostStation.App.csproj" -c Release -r win-x64 --self-contained false -o "$OUT/app"
fi
echo "published -> $OUT"
