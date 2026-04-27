#!/usr/bin/env bash
# Push the arrr-git AUR package to AUR.
# The PKGBUILD is static (pkgver() is generated at install time).
# Usage: update-aur-git.sh
# Requires: AUR_SSH_KEY env var (private key PEM, base64-encoded), git
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

AUR_REPO="ssh://aur@aur.archlinux.org/arrr-git.git"
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

echo "==> Cloning AUR repo"
GIT_SSH_COMMAND="ssh -i $AUR_KEY_FILE -o StrictHostKeyChecking=no" \
    git clone "$AUR_REPO" "$WORK_DIR/aur"

echo "==> Copying packaging files"
cp "$ROOT_DIR/packaging/aur-git/PKGBUILD"          "$WORK_DIR/aur/"
cp "$ROOT_DIR/packaging/aur-git/.SRCINFO"          "$WORK_DIR/aur/"
cp "$ROOT_DIR/packaging/aur-git/arrr-git.install"  "$WORK_DIR/aur/"

echo "==> Committing and pushing to AUR"
cd "$WORK_DIR/aur"
git config user.name  "tgiachi"
git config user.email "tom@orivega.io"
GIT_SSH_COMMAND="ssh -i $AUR_KEY_FILE -o StrictHostKeyChecking=no" \
    git add PKGBUILD .SRCINFO arrr-git.install
if git diff --cached --quiet; then
    echo "==> No changes to push, AUR is already up to date"
else
    GIT_SSH_COMMAND="ssh -i $AUR_KEY_FILE -o StrictHostKeyChecking=no" \
        git commit -m "chore: update arrr-git PKGBUILD"
    GIT_SSH_COMMAND="ssh -i $AUR_KEY_FILE -o StrictHostKeyChecking=no" \
        git push origin HEAD:master
    echo "==> AUR arrr-git pushed"
fi
