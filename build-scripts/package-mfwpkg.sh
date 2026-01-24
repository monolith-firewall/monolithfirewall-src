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

# Build package - find .csproj file
CSPROJ_FILE=""
for csproj in "$PACKAGE_DIR"/*.csproj; do
    if [ -f "$csproj" ]; then
        CSPROJ_FILE="$csproj"
        break
    fi
done

if [ -z "$CSPROJ_FILE" ]; then
    echo "ERROR: No .csproj file found in package directory"
    exit 1
fi

echo "Building package..."
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

# Single build output location (packages build directly in their directory)
BACKEND_BUILD_DIR="$PACKAGE_DIR/bin/Release/net10.0"

if [ ! -d "$BACKEND_BUILD_DIR" ]; then
    echo "ERROR: Build output directory not found: $BACKEND_BUILD_DIR"
    echo "Package must be built before packaging"
    exit 1
fi

echo "Using build directory: $BACKEND_BUILD_DIR"

# Convert package ID to DLL name: monolith-network -> Monolith.Network.dll
# Split on hyphens, capitalize first letter of each part, join with dots
PACKAGE_DLL_BASE=$(echo "$PACKAGE_ID" | sed 's/-/./g' | awk -F. '{for(i=1;i<=NF;i++) $i=toupper(substr($i,1,1)) substr($i,2)}1' OFS=.)
PACKAGE_DLL="${PACKAGE_DLL_BASE}.dll"

# Copy main DLL (Razor views are embedded in main DLL when using Microsoft.NET.Sdk.Razor)
if [ -f "$BACKEND_BUILD_DIR/$PACKAGE_DLL" ]; then
    cp "$BACKEND_BUILD_DIR/$PACKAGE_DLL" "$STAGING_DIR/backend/"
    echo "  Copied: $PACKAGE_DLL"
    
    # Also copy PDB file for debugging if it exists
    PDB_FILE="${PACKAGE_DLL%.dll}.pdb"
    if [ -f "$BACKEND_BUILD_DIR/$PDB_FILE" ]; then
        cp "$BACKEND_BUILD_DIR/$PDB_FILE" "$STAGING_DIR/backend/"
        echo "  Copied: $PDB_FILE"
    fi
else
    echo "ERROR: Package DLL not found: $PACKAGE_DLL"
    echo "Expected location: $BACKEND_BUILD_DIR/$PACKAGE_DLL"
    echo "Available DLLs:"
    ls -1 "$BACKEND_BUILD_DIR"/*.dll 2>/dev/null | sed 's/^/  - /' || echo "  (none)"
    exit 1
fi

# Copy wwwroot (static files)
if [ -d "$PACKAGE_DIR/wwwroot" ]; then
    echo "Copying wwwroot..."
    cp -r "$PACKAGE_DIR/wwwroot" "$STAGING_DIR/"
fi

# Bundle deb packages if aptDependencies exist
if command -v jq &> /dev/null && [ -f "$PACKAGE_DIR/manifest.json" ]; then
    if jq -e '.aptDependencies // [] | length > 0' "$PACKAGE_DIR/manifest.json" >/dev/null 2>&1; then
        echo ""
        echo "Bundling deb packages..."
        DEBS_DIR="$PACKAGE_DIR/debs"
        mkdir -p "$DEBS_DIR"
        
        # Download and bundle all debs
        if [ -f "$ROOT_DIR/build-scripts/bundle-debs.sh" ]; then
            "$ROOT_DIR/build-scripts/bundle-debs.sh" "$PACKAGE_ID" "$PACKAGE_DIR" "$DEBS_DIR"
            
            # Update manifest.json with BundledDebInfo
            if [ -f "$ROOT_DIR/build-scripts/update-manifest-debs.sh" ]; then
                "$ROOT_DIR/build-scripts/update-manifest-debs.sh" "$PACKAGE_DIR"
                # Re-copy updated manifest.json to staging
                cp "$PACKAGE_DIR/manifest.json" "$STAGING_DIR/"
            fi
        else
            echo "  ⚠ Warning: bundle-debs.sh not found, skipping deb bundling"
        fi
        
        # Copy debs directory to staging if it exists and has files
        if [ -d "$DEBS_DIR" ] && [ -n "$(ls -A "$DEBS_DIR"/*.deb 2>/dev/null)" ]; then
            echo "Copying bundled deb packages..."
            cp -r "$DEBS_DIR" "$STAGING_DIR/"
            DEB_COUNT=$(ls -1 "$DEBS_DIR"/*.deb 2>/dev/null | wc -l)
            echo "  ✓ Copied $DEB_COUNT deb package(s)"
        fi
    fi
elif [ -f "$PACKAGE_DIR/manifest.json" ] && grep -q '"aptDependencies"' "$PACKAGE_DIR/manifest.json"; then
    echo ""
    echo "  ⚠ Warning: jq not found, cannot bundle deb packages"
    echo "  Install jq to enable deb bundling: apt-get install jq"
fi

# Note: Razor Pages are compiled into the main DLL, so we don't copy the Pages directory

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
echo "  - backend/ (with main DLL containing Razor views)"
if [ -d "$STAGING_DIR/wwwroot" ]; then
    echo "  - wwwroot/ (static assets)"
fi
if [ -d "$STAGING_DIR/debs" ]; then
    DEB_COUNT=$(ls -1 "$STAGING_DIR/debs"/*.deb 2>/dev/null | wc -l)
    echo "  - debs/ ($DEB_COUNT bundled deb package(s))"
fi
echo ""
