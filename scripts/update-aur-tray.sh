#!/usr/bin/env bash
# Update the AUR arrr-tray-bin package to a new version.
# Usage: update-aur-tray.sh <version>
# Requires: AUR_SSH_KEY env var (private key PEM, base64-encoded), git, curl, sha256sum, python3
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

VERSION=${1:-}
if [[ -z "$VERSION" ]]; then
    echo "Usage: $0 <version>"
    exit 1
fi

PKG_URL="https://github.com/tgiachi/Arrr/releases/download/v${VERSION}/arrr-tray-${VERSION}-1-x86_64.pkg.tar.zst"
AUR_REPO="ssh://aur@aur.archlinux.org/arrr-tray-bin.git"
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
curl -fsSL -o "$WORK_DIR/arrr-tray.pkg.tar.zst" "$PKG_URL"
SHA256=$(sha256sum "$WORK_DIR/arrr-tray.pkg.tar.zst" | cut -d' ' -f1)
echo "    sha256: $SHA256"

echo "==> Cloning AUR repo"
if ! GIT_SSH_COMMAND="ssh -i $AUR_KEY_FILE -o StrictHostKeyChecking=no" \
        git clone "$AUR_REPO" "$WORK_DIR/aur" 2>/dev/null; then
    echo "    AUR repo does not exist yet — bootstrapping"
    mkdir -p "$WORK_DIR/aur"
    cd "$WORK_DIR/aur"
    git init -b master >/dev/null
    git remote add origin "$AUR_REPO"
    cd "$ROOT_DIR"
fi

echo "==> Copying packaging files"
cp "$ROOT_DIR/packaging/aur-tray/arrr-tray.desktop"      "$WORK_DIR/aur/"
cp "$ROOT_DIR/packaging/aur-tray/arrr-tray-bin.install"  "$WORK_DIR/aur/"
cp "$ROOT_DIR/packaging/aur-tray/PKGBUILD"               "$WORK_DIR/aur/"

echo "==> Updating PKGBUILD"
python3 - "$WORK_DIR/aur/PKGBUILD" "$VERSION" "$SHA256" << 'PYEOF'
import sys, re

path, version, sha256 = sys.argv[1], sys.argv[2], sys.argv[3]
content = open(path).read()

content = re.sub(r'^pkgver=.*$', f'pkgver={version}', content, flags=re.MULTILINE)
content = re.sub(r'^pkgrel=.*$', 'pkgrel=1',          content, flags=re.MULTILINE)
# replace the first sha256 entry (the pkg.tar.zst line)
content = re.sub(r"'SKIP'", f"'{sha256}'", content, count=1)

open(path, 'w').write(content)
PYEOF

echo "==> Regenerating .SRCINFO"
cd "$WORK_DIR/aur"
if command -v makepkg >/dev/null 2>&1; then
    makepkg --printsrcinfo > .SRCINFO
else
    # Fallback: regenerate from template
    python3 - "$WORK_DIR/aur/PKGBUILD" > .SRCINFO << 'PYEOF'
import sys, re

content = open(sys.argv[1]).read()

def field(name):
    m = re.search(rf'^{name}=(.*)$', content, re.MULTILINE)
    return m.group(1).strip("'\"") if m else ''

def fields(name):
    m = re.search(rf'^{name}=\((.*?)\)', content, re.MULTILINE | re.DOTALL)
    if not m:
        return []
    return [x.strip("'\" ") for x in re.findall(r"'[^']+'|\"[^\"]+\"|\S+", m.group(1))]

pkgname  = field('pkgname')
pkgver   = field('pkgver')
pkgrel   = field('pkgrel')
pkgdesc  = field('pkgdesc')
url      = field('url')
install  = field('install')
arch     = fields('arch')
license_ = fields('license')
depends  = fields('depends')
optdepends = fields('optdepends')
provides = fields('provides')
conflicts = fields('conflicts')
noextract = fields('noextract')
source   = fields('source')
sha256s  = fields('sha256sums')

print(f"pkgbase = {pkgname}")
print(f"\tpkgdesc = {pkgdesc}")
print(f"\tpkgver = {pkgver}")
print(f"\tpkgrel = {pkgrel}")
print(f"\turl = {url}")
print(f"\tinstall = {install}")
for a in arch:      print(f"\tarch = {a}")
for l in license_:  print(f"\tlicense = {l}")
for d in depends:   print(f"\tdepends = {d}")
for o in optdepends:print(f"\toptdepends = {o}")
for p in provides:  print(f"\tprovides = {p}")
for c in conflicts: print(f"\tconflicts = {c}")
for n in noextract: print(f"\tnoextract = {n}")
for s in source:    print(f"\tsource = {s}")
for h in sha256s:   print(f"\tsha256sums = {h}")
print()
print(f"pkgname = {pkgname}")
PYEOF
fi

# Substitute templated ${pkgver} with actual version in .SRCINFO if present
sed -i "s/\${pkgver}/${VERSION}/g" .SRCINFO

echo "==> Committing and pushing to AUR"
git config user.name  "tgiachi"
git config user.email "tom@orivega.io"
GIT_SSH_COMMAND="ssh -i $AUR_KEY_FILE -o StrictHostKeyChecking=no" \
    git add PKGBUILD .SRCINFO arrr-tray.desktop arrr-tray-bin.install
GIT_SSH_COMMAND="ssh -i $AUR_KEY_FILE -o StrictHostKeyChecking=no" \
    git commit -m "feat: update to ${VERSION}"
GIT_SSH_COMMAND="ssh -i $AUR_KEY_FILE -o StrictHostKeyChecking=no" \
    git push origin HEAD:master

echo "==> AUR arrr-tray-bin updated to ${VERSION}"
