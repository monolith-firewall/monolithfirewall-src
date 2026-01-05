#!/bin/bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD_OUTPUT_DIR="${ROOT_DIR}/build-output"
mkdir -p "$BUILD_OUTPUT_DIR"

VERSION="${1:-1.0.0}"
TS="$(date -u +%Y%m%dT%H%M%SZ)"

export MONOLITH_ISO_WORKDIR="/tmp/monolith-iso-debug-${TS}"
export MONOLITH_ISO_KEEP_WORKDIR="1"
export MONOLITH_ISO_DEBUG_TRACE="1"
export MONOLITH_ISO_DEBUG_LOG="${BUILD_OUTPUT_DIR}/iso-build-debug-${VERSION}-${TS}.log"
export MONOLITH_PRESEED_FILE="${ROOT_DIR}/iso-build/preseed-debug.cfg"
export MONOLITH_ISO_LATE_SCRIPT="${ROOT_DIR}/iso-build/monolith-late-debug.sh"

echo "Building DEBUG ISO..."
echo "  Workdir: ${MONOLITH_ISO_WORKDIR}"
echo "  Log:     ${MONOLITH_ISO_DEBUG_LOG}"
echo "  Preseed: ${MONOLITH_PRESEED_FILE}"
echo ""

exec "${ROOT_DIR}/build-scripts/build-iso.sh" "$@"

