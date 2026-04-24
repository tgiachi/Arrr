#!/bin/bash
# Requires: go >= 1.25, gcc (for go-sqlite3 CGO)
set -e
cd "$(dirname "$0")"

OS=$(go env GOOS)
ARCH=$(go env GOARCH)

case "$OS" in
  linux)   RID="linux-${ARCH/amd64/x64}" ;;
  windows) RID="win-x64" ;;
  darwin)  RID="osx-${ARCH/amd64/x64}" ;;
  *)       RID="${OS}-x64" ;;
esac
RID="${RID/arm64/arm64}"

EXT=""
[ "$OS" = "windows" ] && EXT=".exe"

mkdir -p "$RID"
go build -tags sqlite_fts5 -o "${RID}/whatsapp-bridge${EXT}" .
echo "Built: $(pwd)/${RID}/whatsapp-bridge${EXT}"
