#!/usr/bin/env bash
# Packs Arrr.Core into the local NuGet feed so plugins can resolve it
# without a published NuGet release. Run this after any Core change.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOCAL_FEED="$REPO_ROOT/local-packages"

mkdir -p "$LOCAL_FEED"

dotnet pack "$REPO_ROOT/src/Arrr.Core/Arrr.Core.csproj" \
  -c Release \
  -o "$LOCAL_FEED"

echo "Packed Arrr.Core → $LOCAL_FEED"
