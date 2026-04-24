#!/usr/bin/env bash
# Packs Arrr.Core into the local NuGet feed so plugins can resolve it
# without a published NuGet release. Run this after any Core change.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOCAL_FEED="$REPO_ROOT/local-packages"

mkdir -p "$LOCAL_FEED"

# Remove previous Arrr.Core builds from the feed so the floating `*` version in
# plugins resolves to this new pack and not an older one.
rm -f "$LOCAL_FEED"/Arrr.Core.*.nupkg "$LOCAL_FEED"/Arrr.Core.*.snupkg

dotnet pack "$REPO_ROOT/src/Arrr.Core/Arrr.Core.csproj" \
  -c Release \
  -o "$LOCAL_FEED"

# Also clear the global package cache so the next restore picks up the local version.
rm -rf ~/.nuget/packages/arrr.core

echo "Packed Arrr.Core → $LOCAL_FEED (old versions removed, global cache cleared)"
