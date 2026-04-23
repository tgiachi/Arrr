#!/usr/bin/env bash
set -euo pipefail

VERSION=${1:-0.1.0}
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "==> Building Arrr v$VERSION (linux-x64)"

dotnet publish "$ROOT_DIR/src/Arrr.Service/Arrr.Service.csproj" \
    -c Release \
    -o "$ROOT_DIR/publish"

echo "==> Packaging"

mkdir -p "$ROOT_DIR/dist"

export VERSION
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
