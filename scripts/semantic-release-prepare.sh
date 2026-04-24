#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 || -z "${1:-}" ]]; then
  echo "Usage: $0 <version>"
  exit 1
fi

VERSION="$1"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

# Bump all .csproj versions
while IFS= read -r file; do
  [[ -f "$file" ]] || continue
  sed -i.bak -E "s|<Version>[^<]+</Version>|<Version>${VERSION}</Version>|g" "$file"
  rm -f "${file}.bak"
done < <(find src templates plugins -name "*.csproj" -type f | sort)

# Bump ui/package.json
if [[ -f "ui/package.json" ]]; then
  sed -i.bak -E "s|\"version\": \"[^\"]+\"|\"version\": \"${VERSION}\"|" "ui/package.json"
  rm -f "ui/package.json.bak"
fi

echo "Updated project versions to ${VERSION}"
