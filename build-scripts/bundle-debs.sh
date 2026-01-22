#!/bin/bash
# Bundle deb packages and dependencies for a monolith package

set -e

PACKAGE_ID="$1"
PACKAGE_DIR="$2"
OUTPUT_DEBS_DIR="$3"

if [ -z "$PACKAGE_ID" ] || [ -z "$PACKAGE_DIR" ] || [ -z "$OUTPUT_DEBS_DIR" ]; then
    echo "Usage: $0 <package-id> <package-source-dir> <output-debs-dir>"
    exit 1
fi

MANIFEST_FILE="$PACKAGE_DIR/manifest.json"
if [ ! -f "$MANIFEST_FILE" ]; then
    echo "Error: manifest.json not found in $PACKAGE_DIR"
    exit 1
fi

# Check if jq is available
if ! command -v jq &> /dev/null; then
    echo "Error: jq is required but not installed. Install with: apt-get install jq"
    exit 1
fi

# Read aptDependencies from manifest.json
APT_DEPS=$(jq -r '.aptDependencies // [] | .[]' "$MANIFEST_FILE" 2>/dev/null || echo "")

if [ -z "$APT_DEPS" ]; then
    echo "No aptDependencies found in manifest.json for $PACKAGE_ID"
    exit 0
fi

echo "Bundling deb packages for $PACKAGE_ID..."
echo "Dependencies: $(echo "$APT_DEPS" | tr '\n' ' ')"

# Create output directory and get absolute path BEFORE changing directories
mkdir -p "$OUTPUT_DEBS_DIR"
OUTPUT_ABS=$(cd "$OUTPUT_DEBS_DIR" && pwd)

# Create deb cache directory (shared across all package builds)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
DEB_CACHE_DIR="$PROJECT_ROOT/tmp/deb-cache"
mkdir -p "$DEB_CACHE_DIR"

# Create temporary directory for downloads
TEMP_DIR=$(mktemp -d)
# Don't auto-cleanup - we need to move files first
# trap "rm -rf $TEMP_DIR" EXIT

cd "$TEMP_DIR"

# Update apt cache (only if cache is empty or very old)
CACHE_AGE=$(( $(date +%s) - $(stat -c %Y "$DEB_CACHE_DIR" 2>/dev/null || echo 0) ))
if [ ! -d "$DEB_CACHE_DIR" ] || [ -z "$(ls -A "$DEB_CACHE_DIR" 2>/dev/null)" ] || [ $CACHE_AGE -gt 86400 ]; then
    echo "  → Updating package lists..."
    sudo apt-get update -qq
else
    echo "  → Using cached package lists (cache age: $((CACHE_AGE / 3600))h)"
fi

# Function to get package version from apt cache
get_package_version() {
    local pkg_name="$1"
    apt-cache show "$pkg_name" 2>/dev/null | grep "^Version:" | head -1 | awk '{print $2}' || echo ""
}

# Function to find cached deb file
find_cached_deb() {
    local pkg_name="$1"
    local version="$2"
    
    # Deb files are named: package_version_arch.deb
    # Try to find matching deb file in cache
    if [ -n "$version" ]; then
        # Try exact version match (version might have special chars, so use pattern)
        local exact_match=$(find "$DEB_CACHE_DIR" -maxdepth 1 -name "${pkg_name}_${version}"*.deb -type f 2>/dev/null | head -1)
        if [ -n "$exact_match" ] && [ -f "$exact_match" ]; then
            echo "$exact_match"
            return 0
        fi
    fi
    
    # Fallback: find any version of this package
    local any_version=$(find "$DEB_CACHE_DIR" -maxdepth 1 -name "${pkg_name}_*.deb" -type f 2>/dev/null | head -1)
    if [ -n "$any_version" ] && [ -f "$any_version" ]; then
        echo "$any_version"
        return 0
    fi
    
    return 1
}

# Function to download a package and its dependencies
download_package_with_deps() {
    local pkg_name="$1"
    local seen_file="$TEMP_DIR/seen_packages.txt"
    
    # Check if already processed
    if grep -q "^${pkg_name}$" "$seen_file" 2>/dev/null; then
        return 0
    fi
    
    echo "$pkg_name" >> "$seen_file"
    
    # Check cache first
    local pkg_version=$(get_package_version "$pkg_name")
    local cached_deb=$(find_cached_deb "$pkg_name" "$pkg_version")
    local deb_file=""
    
    if [ -n "$cached_deb" ] && [ -f "$cached_deb" ]; then
        # Use cached version
        local deb_name=$(basename "$cached_deb")
        cp "$cached_deb" "$TEMP_DIR/$deb_name" 2>/dev/null
        deb_file="$TEMP_DIR/$deb_name"
        local cached_version=$(dpkg-deb -f "$cached_deb" Version 2>/dev/null || echo 'unknown')
        echo "    ✓ Using cached $pkg_name ($cached_version)"
    else
        # Download the package (apt-get download doesn't need sudo)
        if apt-get download "$pkg_name" 2>/dev/null; then
            deb_file=$(find "$TEMP_DIR" -maxdepth 1 -name "${pkg_name}_*.deb" -type f | head -1)
            if [ -f "$deb_file" ]; then
                # Cache the downloaded file for future use
                local deb_name=$(basename "$deb_file")
                cp "$deb_file" "$DEB_CACHE_DIR/$deb_name" 2>/dev/null || true
                echo "    ✓ Downloaded $pkg_name"
            
            fi
        fi
    fi
    
    # Process dependencies if we have a deb file (either from cache or download)
    if [ -n "$deb_file" ] && [ -f "$deb_file" ]; then
        # Extract dependencies and download them recursively
        local deps=$(dpkg-deb -f "$deb_file" Depends 2>/dev/null | \
            sed 's/,/\n/g' | \
            sed 's/|/\n/g' | \
            sed 's/([^)]*)//g' | \
            awk '{print $1}' | \
            grep -v '^\$' | \
            grep -v '^Depends:' | \
            sort -u || true)
        
        for dep in $deps; do
            dep=$(echo "$dep" | tr -d ' ' | sed 's/:[a-z0-9][a-z0-9]*$//')
            if [ -n "$dep" ] && [ "$dep" != "Depends:" ]; then
                # Recursively download dependencies
                download_package_with_deps "$dep" || true
            fi
        done
        return 0
    fi
    
    echo "    ⚠ Failed to download $pkg_name"
    return 1
}

# Download all packages and their dependencies
for pkg in $APT_DEPS; do
    download_package_with_deps "$pkg"
done

# Move all .deb files to output directory
DEB_COUNT=0
# OUTPUT_ABS was already calculated above
# We're still in TEMP_DIR, so move files from current directory
for deb_file in *.deb; do
    if [ -f "$deb_file" ]; then
        if mv "$deb_file" "$OUTPUT_ABS/" 2>/dev/null; then
            DEB_COUNT=$((DEB_COUNT + 1))
        else
            # Fallback: copy and remove
            if cp "$deb_file" "$OUTPUT_ABS/" 2>/dev/null; then
                rm -f "$deb_file"
                DEB_COUNT=$((DEB_COUNT + 1))
            else
                echo "    ⚠ Warning: Failed to move $deb_file to $OUTPUT_ABS"
            fi
        fi
    fi
done

# Clean up temp directory (go back to original directory first)
cd /
rm -rf "$TEMP_DIR"

if [ $DEB_COUNT -gt 0 ]; then
    echo "  ✓ Bundled $DEB_COUNT deb package(s) to $OUTPUT_DEBS_DIR"
    echo "  ℹ Deb cache location: $DEB_CACHE_DIR"
else
    echo "  ⚠ No deb packages were downloaded"
    exit 1
fi
