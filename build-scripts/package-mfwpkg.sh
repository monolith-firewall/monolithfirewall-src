#!/bin/bash
set -e

echo "==============================================================="
echo "  Creating MonolithFireWall .mfwpkg Package"
echo "==============================================================="
echo ""

# Accept either package ID or directory name
PACKAGE_ID_OR_DIR="${1:-monolith-system}"
ROOT_DIR="$(pwd)"

# Try to find the package directory
PACKAGE_DIR=""
if [ -d "packages/${PACKAGE_ID_OR_DIR}" ]; then
    PACKAGE_DIR="packages/${PACKAGE_ID_OR_DIR}"
elif [ -d "$PACKAGE_ID_OR_DIR" ] && [[ "$PACKAGE_ID_OR_DIR" == packages/* ]]; then
    PACKAGE_DIR="$PACKAGE_ID_OR_DIR"
else
    echo "ERROR: Package directory not found: packages/${PACKAGE_ID_OR_DIR}"
    exit 1
fi

if [ ! -f "$PACKAGE_DIR/manifest.json" ]; then
    echo "ERROR: Package manifest not found: $PACKAGE_DIR/manifest.json"
    exit 1
fi

# Extract package ID from manifest.json
PACKAGE_ID=$(grep -o '"id"[[:space:]]*:[[:space:]]*"[^"]*"' "$PACKAGE_DIR/manifest.json" | head -1 | sed 's/.*"id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/' || echo "$(basename "$PACKAGE_DIR")")
OUTPUT_FILE="${PACKAGE_ID}.mfwpkg"

echo "Building package: $PACKAGE_ID"
echo "Package directory: $PACKAGE_DIR"
echo ""

# Build package backend - find .csproj file
CSPROJ_FILE=""
for csproj in "$PACKAGE_DIR"/*.csproj; do
    if [ -f "$csproj" ]; then
        CSPROJ_FILE="$csproj"
        break
    fi
done

if [ -n "$CSPROJ_FILE" ]; then
    echo "Building package backend..."
    echo "Project file: $(basename "$CSPROJ_FILE")"
    cd "$PACKAGE_DIR"
    dotnet build -c Release
    cd "$ROOT_DIR"
else
    echo "WARN: No .csproj found, skipping build. Ensure DLLs are already built."
fi

echo "Creating package archive..."
STAGING_DIR="$(mktemp -d)"

cleanup() {
    if [ -d "$STAGING_DIR" ]; then
        rm -rf "$STAGING_DIR"
    fi
}
trap cleanup EXIT

# Remove old package if exists
rm -f "$OUTPUT_FILE"

echo "Staging package files..."

# Copy manifest
cp "$PACKAGE_DIR/manifest.json" "$STAGING_DIR/"

# Copy backend DLLs
mkdir -p "$STAGING_DIR/backend"

# Find DLLs in Release output
BACKEND_BUILD_DIR=""
if [ -d "$PACKAGE_DIR/backend/bin/Release/net10.0" ]; then
    BACKEND_BUILD_DIR="$PACKAGE_DIR/backend/bin/Release/net10.0"
elif [ -d "$PACKAGE_DIR/bin/Release/net10.0" ]; then
    BACKEND_BUILD_DIR="$PACKAGE_DIR/bin/Release/net10.0"
else
    echo "ERROR: Could not find Release build output directory"
    exit 1
fi

shopt -s nullglob
for file in "$BACKEND_BUILD_DIR"/*.dll; do
    cp "$file" "$STAGING_DIR/backend/"
done
for file in "$BACKEND_BUILD_DIR"/*.pdb; do
    cp "$file" "$STAGING_DIR/backend/"
done
shopt -u nullglob

if [ -z "$(ls -A "$STAGING_DIR/backend")" ]; then
    echo "ERROR: No DLLs found in backend directory"
    exit 1
fi

# Copy wwwroot (static files)
if [ -d "$PACKAGE_DIR/wwwroot" ]; then
    echo "Copying wwwroot..."
    cp -r "$PACKAGE_DIR/wwwroot" "$STAGING_DIR/"
fi

# Copy Pages (Razor views)
if [ -d "$PACKAGE_DIR/Pages" ]; then
    echo "Copying Pages..."
    cp -r "$PACKAGE_DIR/Pages" "$STAGING_DIR/"
fi

echo "Writing package archive..."
cd "$STAGING_DIR"
zip -r "$OUTPUT_FILE" . > /dev/null
cd "$ROOT_DIR"
mv "$STAGING_DIR/$OUTPUT_FILE" .

echo ""
echo "==============================================================="
echo "  Package Created Successfully!"
echo "==============================================================="
echo ""
echo "Package: $OUTPUT_FILE"
echo "Size: $(du -h "$OUTPUT_FILE" | cut -f1)"
echo ""
echo "Package contents:"
echo "  - manifest.json"
echo "  - backend/ (with DLLs)"
echo ""
