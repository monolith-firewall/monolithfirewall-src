#!/bin/bash
set -e

echo "==============================================================="
echo "  Creating MonolithFireWall .mfwpkg Package"
echo "==============================================================="
echo ""

# Accept either package ID or directory name
PACKAGE_ID_OR_DIR="${1:-monolith-system}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Try to find the package directory
PACKAGE_DIR=""
if [ -d "$ROOT_DIR/tmp/monolithfirewall-packages/${PACKAGE_ID_OR_DIR}" ]; then
    PACKAGE_DIR="$ROOT_DIR/tmp/monolithfirewall-packages/${PACKAGE_ID_OR_DIR}"
elif [ -d "$PACKAGE_ID_OR_DIR" ] && [[ "$PACKAGE_ID_OR_DIR" == *monolithfirewall-packages* ]]; then
    PACKAGE_DIR="$PACKAGE_ID_OR_DIR"
elif [ -d "$PACKAGE_ID_OR_DIR" ] && [ -f "$PACKAGE_ID_OR_DIR/manifest.json" ]; then
    PACKAGE_DIR="$PACKAGE_ID_OR_DIR"
else
    echo "ERROR: Package directory not found: tmp/monolithfirewall-packages/${PACKAGE_ID_OR_DIR}"
    echo "Available packages:"
    ls -1 "$ROOT_DIR/tmp/monolithfirewall-packages/" 2>/dev/null | sed 's/^/  - /' || echo "  (none found)"
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
    # Build with Release configuration, filter out XML documentation warnings from external libraries
    if dotnet build -c Release --no-incremental 2>&1 | grep -v "warning CS1570\|warning CS1571\|warning CS1572\|warning CS1573\|warning CS1574\|warning CS1587\|warning CS1591\|warning CS0114"; then
        BUILD_EXIT=0
    else
        BUILD_EXIT=${PIPESTATUS[0]}
    fi
    if [ $BUILD_EXIT -ne 0 ]; then
        echo "ERROR: Build failed"
        cd "$ROOT_DIR"
        exit 1
    fi
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

# Find DLLs in Release output (packages build directly in their directory)
BACKEND_BUILD_DIR=""
if [ -d "$PACKAGE_DIR/bin/Release/net10.0" ]; then
    BACKEND_BUILD_DIR="$PACKAGE_DIR/bin/Release/net10.0"
elif [ -d "$PACKAGE_DIR/backend/bin/Release/net10.0" ]; then
    BACKEND_BUILD_DIR="$PACKAGE_DIR/backend/bin/Release/net10.0"
else
    echo "ERROR: Could not find Release build output directory"
    echo "Searched in:"
    echo "  - $PACKAGE_DIR/bin/Release/net10.0"
    echo "  - $PACKAGE_DIR/backend/bin/Release/net10.0"
    echo ""
    echo "Building package first..."
    cd "$PACKAGE_DIR"
    if dotnet build -c Release --no-incremental 2>&1 | grep -v "warning CS1570\|warning CS1571\|warning CS1572\|warning CS1573\|warning CS1574\|warning CS1587\|warning CS1591\|warning CS0114"; then
        BUILD_EXIT=0
    else
        BUILD_EXIT=${PIPESTATUS[0]}
    fi
    if [ $BUILD_EXIT -ne 0 ]; then
        echo "ERROR: Build failed"
        cd "$ROOT_DIR"
        exit 1
    fi
    cd "$ROOT_DIR"
    # Try again after build
    if [ -d "$PACKAGE_DIR/bin/Release/net10.0" ]; then
        BACKEND_BUILD_DIR="$PACKAGE_DIR/bin/Release/net10.0"
    elif [ -d "$PACKAGE_DIR/backend/bin/Release/net10.0" ]; then
        BACKEND_BUILD_DIR="$PACKAGE_DIR/backend/bin/Release/net10.0"
    else
        echo "ERROR: Still could not find Release build output after building"
        exit 1
    fi
fi

echo "Using build directory: $BACKEND_BUILD_DIR"

# Copy only the package's own DLLs (not dependencies which are loaded from system)
# Get package ID from manifest to determine DLL name
PACKAGE_ID=$(grep -o '"id"[[:space:]]*:[[:space:]]*"[^"]*"' "$PACKAGE_DIR/manifest.json" | head -1 | sed 's/.*"id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/' || echo "$(basename "$PACKAGE_DIR")")

# Convert package ID to DLL name pattern: monolith-network -> Monolith.Network
# Split on hyphens, capitalize first letter of each part, join with dots
PACKAGE_DLL_BASE=$(echo "$PACKAGE_ID" | sed 's/-/./g' | awk -F. '{for(i=1;i<=NF;i++) $i=toupper(substr($i,1,1)) substr($i,2)}1' OFS=.)
PACKAGE_DLL_PATTERN="${PACKAGE_DLL_BASE}*.dll"

shopt -s nullglob
# Copy package DLLs (main and views)
for file in "$BACKEND_BUILD_DIR"/$PACKAGE_DLL_PATTERN; do
    if [ -f "$file" ]; then
        cp "$file" "$STAGING_DIR/backend/"
        echo "  Copied: $(basename "$file")"
    fi
done
# Also copy PDB files for debugging
for file in "$BACKEND_BUILD_DIR"/$PACKAGE_DLL_PATTERN; do
    pdb_file="${file%.dll}.pdb"
    if [ -f "$pdb_file" ]; then
        cp "$pdb_file" "$STAGING_DIR/backend/"
        echo "  Copied: $(basename "$pdb_file")"
    fi
done
shopt -u nullglob

if [ -z "$(ls -A "$STAGING_DIR/backend")" ]; then
    echo "ERROR: No package DLLs found in backend directory"
    echo "Expected pattern: $PACKAGE_DLL_PATTERN"
    echo "Available DLLs:"
    ls -1 "$BACKEND_BUILD_DIR"/*.dll 2>/dev/null | sed 's/^/  - /' || echo "  (none)"
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
