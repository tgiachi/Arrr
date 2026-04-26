#!/usr/bin/env bash
# Update the AUR arrr-bin package to a new version.
# Usage: update-aur.sh <version>
# Requires: AUR_SSH_KEY env var (private key PEM), git, curl, sha256sum, python3
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

VERSION=${1:-}
if [[ -z "$VERSION" ]]; then
    echo "Usage: $0 <version>"
    exit 1
fi

PKG_URL="https://github.com/tgiachi/Arrr/releases/download/v${VERSION}/arrr-${VERSION}-1-x86_64.pkg.tar.zst"
AUR_REPO="ssh://aur@aur.archlinux.org/arrr-bin.git"
WORK_DIR=$(mktemp -d)
trap 'rm -rf "$WORK_DIR"' EXIT

echo "==> Setting up AUR SSH key"
mkdir -p ~/.ssh
chmod 700 ~/.ssh
AUR_KEY_FILE="$WORK_DIR/aur_id_rsa"
echo "${AUR_SSH_KEY}" | base64 -d > "$AUR_KEY_FILE"
chmod 600 "$AUR_KEY_FILE"
grep -q "Host aur.archlinux.org" ~/.ssh/config 2>/dev/null || cat >> ~/.ssh/config << EOF

Host aur.archlinux.org
    IdentityFile $AUR_KEY_FILE
    User aur
    StrictHostKeyChecking no
EOF
ssh-keyscan -H aur.archlinux.org >> ~/.ssh/known_hosts 2>/dev/null || true

echo "==> Downloading release asset to compute sha256"
curl -fsSL -o "$WORK_DIR/arrr.pkg.tar.zst" "$PKG_URL"
SHA256=$(sha256sum "$WORK_DIR/arrr.pkg.tar.zst" | cut -d' ' -f1)
echo "    sha256: $SHA256"

echo "==> Cloning AUR repo"
git clone "$AUR_REPO" "$WORK_DIR/aur"

echo "==> Copying packaging files"
cp "$ROOT_DIR/packaging/aur/arrr.service"      "$WORK_DIR/aur/"
cp "$ROOT_DIR/packaging/aur/arrr-bin.install"  "$WORK_DIR/aur/"

echo "==> Updating PKGBUILD"
python3 - "$WORK_DIR/aur/PKGBUILD" "$VERSION" "$SHA256" << 'PYEOF'
import sys, re

path, version, sha256 = sys.argv[1], sys.argv[2], sys.argv[3]
content = open(path).read()

content = re.sub(r'^pkgver=.*$',         f'pkgver={version}',  content, flags=re.MULTILINE)
content = re.sub(r'^pkgrel=.*$',         'pkgrel=1',           content, flags=re.MULTILINE)
# replace the first sha256 entry (the pkg.tar.zst line, not 'SKIP')
content = re.sub(r"('[0-9a-f]{64}')", f"'{sha256}'", content, count=1)

open(path, 'w').write(content)
PYEOF

echo "==> Updating .SRCINFO"
python3 - "$WORK_DIR/aur/.SRCINFO" "$VERSION" "$SHA256" << 'PYEOF'
import sys, re

path, version, sha256 = sys.argv[1], sys.argv[2], sys.argv[3]
content = open(path).read()

content = re.sub(r'(\bpkgver\s*=\s*).*',        rf'\g<1>{version}', content)
content = re.sub(r'(\bpkgrel\s*=\s*).*',        r'\g<1>1',          content)
content = re.sub(r'([0-9a-f]{64})',              sha256,             content, count=1)
# update the versioned source URL and noextract reference
content = re.sub(r'arrr-[\d.]+(-1-x86_64\.pkg\.tar\.zst)', f'arrr-{version}\\1', content)

open(path, 'w').write(content)
PYEOF

echo "==> Committing and pushing to AUR"
cd "$WORK_DIR/aur"
git config user.name  "tgiachi"
git config user.email "tom@orivega.io"
GIT_SSH_COMMAND="ssh -i $AUR_KEY_FILE -o StrictHostKeyChecking=no" \
    git add PKGBUILD .SRCINFO arrr.service arrr-bin.install
GIT_SSH_COMMAND="ssh -i $AUR_KEY_FILE -o StrictHostKeyChecking=no" \
    git commit -m "feat: update to ${VERSION}"
GIT_SSH_COMMAND="ssh -i $AUR_KEY_FILE -o StrictHostKeyChecking=no" \
    git push origin HEAD:master

echo "==> AUR arrr-bin updated to ${VERSION}"
