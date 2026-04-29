#!/usr/bin/env bash
# Push the arrr-tray-git AUR package to AUR.
# The PKGBUILD is static (pkgver() is generated at install time).
# Usage: update-aur-tray-git.sh
# Requires: AUR_SSH_KEY env var (private key PEM, base64-encoded), git
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

AUR_REPO="ssh://aur@aur.archlinux.org/arrr-tray-git.git"
WORK_DIR=$(mktemp -d)
trap 'rm -rf "$WORK_DIR"' EXIT

echo "==> Setting up AUR SSH key"
mkdir -p ~/.ssh
chmod 700 ~/.ssh
AUR_KEY_FILE="$WORK_DIR/aur_id_rsa"
if [[ -n "${AUR_SSH_KEY:-}" ]]; then
    echo "${AUR_SSH_KEY}" | base64 -d > "$AUR_KEY_FILE"
else
    read -rp "Path to AUR SSH private key [~/.ssh/aur]: " AUR_KEY_PATH
    AUR_KEY_PATH="${AUR_KEY_PATH:-$HOME/.ssh/aur}"
    cp "${AUR_KEY_PATH/#\~/$HOME}" "$AUR_KEY_FILE"
fi
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
cp "$ROOT_DIR/packaging/aur-tray-git/PKGBUILD"                 "$WORK_DIR/aur/"
cp "$ROOT_DIR/packaging/aur-tray-git/.SRCINFO"                 "$WORK_DIR/aur/"
cp "$ROOT_DIR/packaging/aur-tray-git/arrr-tray-git.install"    "$WORK_DIR/aur/"
cp "$ROOT_DIR/packaging/aur-tray-git/arrr-tray.desktop"        "$WORK_DIR/aur/"

echo "==> Committing and pushing to AUR"
cd "$WORK_DIR/aur"
git config user.name  "tgiachi"
git config user.email "tom@orivega.io"
GIT_SSH_COMMAND="ssh -i $AUR_KEY_FILE -o StrictHostKeyChecking=no" \
    git add PKGBUILD .SRCINFO arrr-tray-git.install arrr-tray.desktop
if git diff --cached --quiet; then
    echo "==> No changes to push, AUR is already up to date"
else
    GIT_SSH_COMMAND="ssh -i $AUR_KEY_FILE -o StrictHostKeyChecking=no" \
        git commit -m "chore: update arrr-tray-git PKGBUILD"
    GIT_SSH_COMMAND="ssh -i $AUR_KEY_FILE -o StrictHostKeyChecking=no" \
        git push origin HEAD:master
    echo "==> AUR arrr-tray-git pushed"
fi
