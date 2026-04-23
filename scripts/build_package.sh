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

SIGNING_KEY_FILE=$(mktemp --suffix=.asc)
TMPGNUPG=$(mktemp -d)
chmod 700 "$TMPGNUPG"
trap 'rm -rf "$TMPGNUPG" "$SIGNING_KEY_FILE"' EXIT

read -rsp "GPG passphrase: " GPG_PASSPHRASE
echo

# Import the passphrase-protected key into a throwaway keyring
gpg --export-secret-keys --armor FE67774DF63A2BB6 | \
    gpg --batch --yes \
        --pinentry-mode loopback \
        --passphrase "$GPG_PASSPHRASE" \
        --homedir "$TMPGNUPG" \
        --import

# Strip the passphrase: provide old pass then empty new pass via stdin
printf '%s\n\n\n' "$GPG_PASSPHRASE" | \
    gpg --batch --yes \
        --pinentry-mode loopback \
        --passphrase-fd 0 \
        --homedir "$TMPGNUPG" \
        --change-passphrase FE67774DF63A2BB6

# Export the now-unprotected key for nfpm
gpg --batch --yes \
    --pinentry-mode loopback \
    --passphrase "" \
    --homedir "$TMPGNUPG" \
    --export-secret-keys --armor FE67774DF63A2BB6 > "$SIGNING_KEY_FILE"

export VERSION
export NFPM_SIGNING_KEY_FILE="$SIGNING_KEY_FILE"
cd "$ROOT_DIR"

nfpm package --packager deb       --target "dist/arrr_${VERSION}_amd64.deb"
nfpm package --packager archlinux --target "dist/arrr-${VERSION}-1-x86_64.pkg.tar.zst"

echo "==> Signing Arch package"
gpg --batch --yes \
    --pinentry-mode loopback \
    --passphrase "$GPG_PASSPHRASE" \
    --detach-sign \
    --local-user FE67774DF63A2BB6 \
    "dist/arrr-${VERSION}-1-x86_64.pkg.tar.zst"

echo ""
echo "==> Done:"
ls -lh "$ROOT_DIR/dist/"
