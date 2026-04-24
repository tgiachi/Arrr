#!/bin/bash
# Requires: go >= 1.22, gcc (for go-sqlite3 CGO)
set -e
cd "$(dirname "$0")"
go get go.mau.fi/whatsmeow@latest
go get github.com/mattn/go-sqlite3@latest
go mod tidy
go build -tags sqlite_fts5 -o whatsapp-bridge .
echo "Built: $(pwd)/whatsapp-bridge"
