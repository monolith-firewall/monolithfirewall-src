#!/bin/bash
set -e

echo "==============================================================="
echo "  Building All MonolithFireWall Packages"
echo "==============================================================="
echo ""

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PACKAGES_DIR="$ROOT_DIR/tmp/monolithfirewall-packages"
BUILD_DIR="$ROOT_DIR/build-output/packages"
PACKAGE_SCRIPT="$ROOT_DIR/build-scripts/package-mfwpkg.sh"

# Create build directory
mkdir -p "$BUILD_DIR"

# Check if packages directory exists
if [ ! -d "$PACKAGES_DIR" ]; then
    echo "ERROR: Packages directory not found: $PACKAGES_DIR"
    exit 1
fi

# Check if package script exists
if [ ! -f "$PACKAGE_SCRIPT" ]; then
    echo "ERROR: Package script not found: $PACKAGE_SCRIPT"
    exit 1
fi

# Make sure package script is executable
chmod +x "$PACKAGE_SCRIPT"

# Change to root directory
cd "$ROOT_DIR"

# Find all package directories (directories containing manifest.json)
PACKAGE_COUNT=0
SUCCESS_COUNT=0
FAILED_PACKAGES=()

echo "Scanning for packages in: $PACKAGES_DIR"
echo ""

for PACKAGE_DIR in "$PACKAGES_DIR"/*; do
    if [ ! -d "$PACKAGE_DIR" ]; then
        continue
    fi
    
    PACKAGE_NAME=$(basename "$PACKAGE_DIR")
    
    # Check if this directory has a manifest.json
    if [ ! -f "$PACKAGE_DIR/manifest.json" ]; then
        echo "Skipping $PACKAGE_NAME (no manifest.json found)"
        continue
    fi
    
    # Extract package ID from manifest.json
    PACKAGE_ID=$(grep -o '"id"[[:space:]]*:[[:space:]]*"[^"]*"' "$PACKAGE_DIR/manifest.json" | head -1 | sed 's/.*"id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/' || echo "$PACKAGE_NAME")
    
    PACKAGE_COUNT=$((PACKAGE_COUNT + 1))
    
    echo "---------------------------------------------------------------"
    echo "  Building package $PACKAGE_COUNT: $PACKAGE_ID"
    echo "  Directory: $PACKAGE_NAME"
    echo "---------------------------------------------------------------"
    echo ""
    
    # Build the package (pass directory name, script will extract package ID from manifest)
    if "$PACKAGE_SCRIPT" "$PACKAGE_NAME" 2>&1; then
        # Move the created package to build directory
        if [ -f "$ROOT_DIR/${PACKAGE_ID}.mfwpkg" ]; then
            mv "$ROOT_DIR/${PACKAGE_ID}.mfwpkg" "$BUILD_DIR/"
            echo "✓ Package built successfully: $BUILD_DIR/${PACKAGE_ID}.mfwpkg"
            SUCCESS_COUNT=$((SUCCESS_COUNT + 1))
        else
            echo "✗ ERROR: Package file not created: ${PACKAGE_ID}.mfwpkg"
            FAILED_PACKAGES+=("$PACKAGE_ID")
        fi
    else
        echo "✗ ERROR: Failed to build package: $PACKAGE_ID"
        FAILED_PACKAGES+=("$PACKAGE_ID")
    fi
    
    echo ""
done

echo "==============================================================="
echo "  Build Summary"
echo "==============================================================="
echo ""
echo "Total packages found: $PACKAGE_COUNT"
echo "Successfully built: $SUCCESS_COUNT"
echo "Failed: ${#FAILED_PACKAGES[@]}"
echo ""

if [ ${#FAILED_PACKAGES[@]} -gt 0 ]; then
    echo "Failed packages:"
    for pkg in "${FAILED_PACKAGES[@]}"; do
        echo "  - $pkg"
    done
    echo ""
    exit 1
fi

if [ $PACKAGE_COUNT -eq 0 ]; then
    echo "WARNING: No packages found in $PACKAGES_DIR"
    exit 1
fi

echo "All packages built successfully!"
echo "Output directory: $BUILD_DIR"
echo ""
echo "Built packages:"
ls -lh "$BUILD_DIR"/*.mfwpkg 2>/dev/null | awk '{print "  - " $9 " (" $5 ")"}' || echo "  (none)"
echo ""
