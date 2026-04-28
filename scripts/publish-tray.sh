#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

VERSION=${1:-$(grep -oP '(?<=<Version>)[^<]+' "$ROOT_DIR/src/Arrr.Service/Arrr.Service.csproj")}

echo "==> Building arrr-tray v$VERSION (linux-x64)"

dotnet publish "$ROOT_DIR/src/Arrr.Tray/Arrr.Tray.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -o "$ROOT_DIR/publish-tray"

echo "==> Packaging"

mkdir -p "$ROOT_DIR/dist"

SIGNING_KEY_FILE=$(mktemp --suffix=.asc)
trap 'rm -f "$SIGNING_KEY_FILE"' EXIT

if [[ -z "${GPG_PASSPHRASE:-}" ]]; then
    read -rsp "GPG passphrase: " GPG_PASSPHRASE
    echo
fi

gpg --batch --yes \
    --pinentry-mode loopback \
    --passphrase "$GPG_PASSPHRASE" \
    --export-secret-keys --armor FE67774DF63A2BB6 > "$SIGNING_KEY_FILE"

export VERSION
export NFPM_SIGNING_KEY_FILE="$SIGNING_KEY_FILE"
export NFPM_DEB_PASSPHRASE="$GPG_PASSPHRASE"
export NFPM_RPM_PASSPHRASE="$GPG_PASSPHRASE"
cd "$ROOT_DIR"

nfpm package --config nfpm-tray.yaml --packager deb       --target "dist/arrr-tray_${VERSION}_amd64.deb"
nfpm package --config nfpm-tray.yaml --packager rpm       --target "dist/arrr-tray-${VERSION}-1.x86_64.rpm"
nfpm package --config nfpm-tray.yaml --packager archlinux --target "dist/arrr-tray-${VERSION}-1-x86_64.pkg.tar.zst"

echo "==> Signing Arch package"
gpg --batch --yes \
    --pinentry-mode loopback \
    --passphrase "$GPG_PASSPHRASE" \
    --detach-sign \
    --local-user FE67774DF63A2BB6 \
    "dist/arrr-tray-${VERSION}-1-x86_64.pkg.tar.zst"

echo ""
echo "==> Done:"
ls -lh "$ROOT_DIR/dist/"
