#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

VERSION=${1:-$(grep -oP '(?<=<Version>)[^<]+' "$ROOT_DIR/src/Arrr.Service/Arrr.Service.csproj")}

echo "==> Building Arrr v$VERSION (linux-x64)"

dotnet publish "$ROOT_DIR/src/Arrr.Service/Arrr.Service.csproj" \
    -c Release \
    -o "$ROOT_DIR/publish"

echo "==> Packaging"

mkdir -p "$ROOT_DIR/dist"

SIGNING_KEY_FILE=$(mktemp)
trap 'rm -f "$SIGNING_KEY_FILE"' EXIT
gpg --batch --export-secret-keys --armor FE67774DF63A2BB6 > "$SIGNING_KEY_FILE"

export VERSION
export NFPM_SIGNING_KEY_FILE="$SIGNING_KEY_FILE"
cd "$ROOT_DIR"

nfpm package --packager deb       --target "dist/arrr_${VERSION}_amd64.deb"
nfpm package --packager archlinux --target "dist/arrr-${VERSION}-1-x86_64.pkg.tar.zst"

echo "==> Signing Arch package"
gpg --batch --yes --detach-sign \
    --local-user FE67774DF63A2BB6 \
    "dist/arrr-${VERSION}-1-x86_64.pkg.tar.zst"

echo ""
echo "==> Done:"
ls -lh "$ROOT_DIR/dist/"
