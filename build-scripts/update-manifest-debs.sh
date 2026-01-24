#!/bin/bash
# Update manifest.json with BundledDebInfo from .deb files

set -e

PACKAGE_DIR="$1"
DEBS_DIR="$PACKAGE_DIR/debs"
MANIFEST_FILE="$PACKAGE_DIR/manifest.json"

if [ -z "$PACKAGE_DIR" ]; then
    echo "Usage: $0 <package-source-dir>"
    exit 1
fi

if [ ! -f "$MANIFEST_FILE" ]; then
    echo "Error: manifest.json not found in $PACKAGE_DIR"
    exit 1
fi

# Check if jq is available
if ! command -v jq &> /dev/null; then
    echo "Error: jq is required but not installed. Install with: apt-get install jq"
    exit 1
fi

if [ ! -d "$DEBS_DIR" ]; then
    echo "No debs directory found, skipping manifest update"
    exit 0
fi

# Check if dpkg-deb is available
if ! command -v dpkg-deb &> /dev/null; then
    echo "Error: dpkg-deb is required but not installed"
    exit 1
fi

echo "Updating manifest.json with bundled deb information..."

# Parse each .deb file and generate BundledDebInfo
BUNDLED_DEBS_JSON="[]"

for deb_file in "$DEBS_DIR"/*.deb; do
    if [ ! -f "$deb_file" ]; then
        continue
    fi
    
    DEB_NAME=$(basename "$deb_file")
    
    # Extract package info using dpkg-deb
    PACKAGE_NAME=$(dpkg-deb -f "$deb_file" Package 2>/dev/null || echo "")
    VERSION=$(dpkg-deb -f "$deb_file" Version 2>/dev/null || echo "")
    ARCH=$(dpkg-deb -f "$deb_file" Architecture 2>/dev/null || echo "")
    
    if [ -z "$PACKAGE_NAME" ]; then
        echo "  ⚠ Warning: Could not extract package name from $DEB_NAME, skipping"
        continue
    fi
    
    # Extract dependencies (simplified - remove version constraints)
    DEPS=$(dpkg-deb -f "$deb_file" Depends 2>/dev/null | \
        sed 's/,/\n/g' | \
        sed 's/|/\n/g' | \
        sed 's/([^)]*)//g' | \
        awk '{print $1}' | \
        grep -v '^\$' | \
        grep -v '^Depends:' | \
        sed 's/:[a-z0-9][a-z0-9]*$//' | \
        jq -R . | \
        jq -s . 2>/dev/null || echo "[]")
    
    # Create JSON entry
    DEB_JSON=$(jq -n \
        --arg file "$DEB_NAME" \
        --arg pkg "$PACKAGE_NAME" \
        --arg ver "$VERSION" \
        --arg arch "$ARCH" \
        --argjson deps "$DEPS" \
        '{fileName: $file, packageName: $pkg, version: $ver, architecture: $arch, dependencies: $deps}')
    
    BUNDLED_DEBS_JSON=$(echo "$BUNDLED_DEBS_JSON" | jq --argjson deb "$DEB_JSON" '. + [$deb]')
    
    echo "  ✓ Processed: $PACKAGE_NAME ($VERSION)"
done

# Update manifest.json
if [ -f "$MANIFEST_FILE" ]; then
    # Create backup
    cp "$MANIFEST_FILE" "$MANIFEST_FILE.bak"
    
    # Update with bundledDebs
    jq --argjson bundledDebs "$BUNDLED_DEBS_JSON" '.bundledDebs = $bundledDebs' "$MANIFEST_FILE" > "$MANIFEST_FILE.tmp"
    mv "$MANIFEST_FILE.tmp" "$MANIFEST_FILE"
    
    DEB_COUNT=$(echo "$BUNDLED_DEBS_JSON" | jq 'length')
    echo "✓ Updated manifest.json with $DEB_COUNT bundled deb package(s)"
else
    echo "Error: manifest.json not found"
    exit 1
fi
