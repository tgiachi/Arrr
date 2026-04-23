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
# allow-loopback-pinentry is required for --pinentry-mode loopback to work in the agent
echo "allow-loopback-pinentry" > "$TMPGNUPG/gpg-agent.conf"
trap 'GNUPGHOME="$TMPGNUPG" gpgconf --kill gpg-agent 2>/dev/null; rm -rf "$TMPGNUPG" "$SIGNING_KEY_FILE"' EXIT

read -rsp "GPG passphrase: " GPG_PASSPHRASE
echo

# Import the passphrase-protected key into the isolated keyring
gpg --export-secret-keys --armor FE67774DF63A2BB6 | \
    GNUPGHOME="$TMPGNUPG" gpg --batch --yes \
        --pinentry-mode loopback \
        --passphrase "$GPG_PASSPHRASE" \
        --import

# Strip the passphrase: with --batch + --passphrase, gpg uses the supplied value as
# the old passphrase and sets the new passphrase to empty (documented behavior)
GNUPGHOME="$TMPGNUPG" gpg --batch --yes \
    --pinentry-mode loopback \
    --passphrase "$GPG_PASSPHRASE" \
    --change-passphrase FE67774DF63A2BB6

# Export the now-unprotected key for nfpm (no --passphrase needed for an unprotected key)
GNUPGHOME="$TMPGNUPG" gpg --batch --yes \
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
