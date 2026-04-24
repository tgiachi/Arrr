#!/usr/bin/env bash
# Builds the Arrr Docker image.
# Usage: ./scripts/build-docker.sh [VERSION] [IMAGE_NAME]
#   VERSION    defaults to the version in Arrr.Core.csproj
#   IMAGE_NAME defaults to "arrr"
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

VERSION="${1:-$(grep -oP '(?<=<Version>)[^<]+' "$ROOT_DIR/src/Arrr.Core/Arrr.Core.csproj" | head -1)}"
IMAGE="${2:-arrr}"
TAG="${IMAGE}:${VERSION}"
LATEST="${IMAGE}:latest"

echo "==> Building ${TAG}"

docker build \
    --progress=plain \
    -t "$TAG" \
    -t "$LATEST" \
    "$ROOT_DIR"

echo ""
echo "==> Done: ${TAG} / ${LATEST}"
echo ""
echo "    Run:"
echo "      docker run -d \\"
echo "        --name arrr \\"
echo "        -p 5150:5150 \\"
echo "        -v arrr-data:/data \\"
echo "        ${TAG}"
