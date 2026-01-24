#!/bin/bash
set -e

echo "═══════════════════════════════════════════════════════════════"
echo "  Monolith FireWall - Debian Package Builder"
echo "═══════════════════════════════════════════════════════════════"
echo ""

# Check if we're in the right directory
if [ ! -f "debian/control" ]; then
    echo "ERROR: Must run from project root directory"
    exit 1
fi

# Set build output directory
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD_OUTPUT_DIR="$ROOT_DIR/build-output"
mkdir -p "$BUILD_OUTPUT_DIR"

# WSL/NTFS permission caveat (dpkg-deb rejects 777 on /mnt)
if grep -qi microsoft /proc/version 2>/dev/null; then
    case "$PWD" in
        /mnt/*)
            echo "ERROR: Building on /mnt/* (Windows filesystem) causes dpkg-deb permission failures."
            echo "Move the repo to WSL filesystem (e.g., /home/<user>/) or run ./build-in-wsl.sh."
            exit 1
            ;;
    esac
fi

# Fix any broken dependencies first
echo "→ Fixing broken dependencies..."
sudo apt-get update
sudo apt-get install -f -y || true

# Install build dependencies
echo "→ Installing build dependencies..."
sudo apt-get install -y \
    debhelper \
    devscripts \
    build-essential \
    wget \
    libicu76 \
    libc6 \
    libgcc-s1 \
    libstdc++6 \
    zlib1g || {
    echo "⚠ Some build dependencies failed to install, trying to fix..."
    sudo apt-get install -f -y
    # Try again
    sudo apt-get install -y \
        debhelper \
        devscripts \
        build-essential \
        wget \
        libicu76 \
        libc6 \
        libgcc-s1 \
        libstdc++6 \
        zlib1g || {
        echo "✗ ERROR: Failed to install build dependencies"
        echo "Try manually: sudo apt-get install -f -y && sudo apt-get install -y debhelper devscripts build-essential wget libicu76 libc6 libgcc-s1 libstdc++6 zlib1g"
        exit 1
    }
}

# Install .NET SDK if not present
if ! command -v dotnet &> /dev/null; then
    echo "→ Installing .NET 10.0 SDK..."
    wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
    chmod +x /tmp/dotnet-install.sh
    sudo /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet
    sudo ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet
fi

# Ensure dotnet is in PATH
export PATH="$PATH:/usr/share/dotnet:$HOME/.dotnet"
export DOTNET_ROOT="/usr/share/dotnet"

echo ""
echo "→ Building Debian package..."
echo ""

# Generate timestamp-based version (use UTC for consistency)
BUILD_TIMESTAMP=$(date -u +"%Y%m%d-%H%M%S")
PACKAGE_VERSION="1.0.0-${BUILD_TIMESTAMP}"
BUILD_DATE_UTC=$(date -u +"%Y-%m-%d %H:%M:%S UTC")
BUILD_DATE_RFC=$(date -u -R)

echo "  Package version: $PACKAGE_VERSION"
echo "  Build timestamp: $BUILD_DATE_UTC"

# Backup original changelog (find the first non-timestamped entry)
# Remove any existing timestamped entries from the top
TEMP_CHANGELOG=$(mktemp)
IN_TIMESTAMPED_SECTION=false
FOUND_NON_TIMESTAMPED=false

while IFS= read -r line || [ -n "$line" ]; do
    # Check if this is a timestamped version line
    if [[ "$line" =~ ^monolith-firewall\ \(1\.0\.0-[0-9]{8}-[0-9]{6}\) ]]; then
        IN_TIMESTAMPED_SECTION=true
        continue
    fi
    
    # If we're in a timestamped section, skip until we hit the next entry
    if [ "$IN_TIMESTAMPED_SECTION" = true ]; then
        # Check if this is the start of a new entry (non-timestamped)
        if [[ "$line" =~ ^monolith-firewall\ \( ]]; then
            IN_TIMESTAMPED_SECTION=false
            FOUND_NON_TIMESTAMPED=true
            echo "$line" >> "$TEMP_CHANGELOG"
        fi
        continue
    fi
    
    # We're past timestamped entries, write everything
    if [ "$FOUND_NON_TIMESTAMPED" = true ] || [ "$IN_TIMESTAMPED_SECTION" = false ]; then
        echo "$line" >> "$TEMP_CHANGELOG"
    fi
done < debian/changelog

# If we didn't find a non-timestamped entry, keep the original
if [ "$FOUND_NON_TIMESTAMPED" = false ]; then
    cp debian/changelog "$TEMP_CHANGELOG"
fi

# Backup the cleaned changelog
cp "$TEMP_CHANGELOG" debian/changelog.bak

# Create new changelog entry with fresh timestamp
CHANGELOG_ENTRY="monolith-firewall (${PACKAGE_VERSION}) unstable; urgency=medium

  * Automated build with timestamp versioning
  * Build timestamp: ${BUILD_DATE_UTC}

 -- Monolith FireWall Team <dev@monolithfirewall.org>  ${BUILD_DATE_RFC}
"
# Prepend new entry to cleaned changelog
echo "$CHANGELOG_ENTRY" > debian/changelog
cat "$TEMP_CHANGELOG" >> debian/changelog
rm -f "$TEMP_CHANGELOG"

# Make debian scripts executable
chmod +x debian/rules
chmod +x debian/postinst
chmod +x debian/prerm
chmod +x debian/postrm

# Clean the temporary directories and ensure fresh build
rm -rf debian/tmp debian/.debhelper debian/monolith-firewall 2>/dev/null || true
# Also clean any source obj/bin directories to force rebuild
find src -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} \; 2>/dev/null || true
find tmp -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} \; 2>/dev/null || true

# Export version for use in debian/rules
export PACKAGE_VERSION BUILD_TIMESTAMP

# Build the package (must run as non-root user)
dpkg-buildpackage -us -uc -b

# Keep the timestamped changelog entry (don't restore)
# The changelog now has the fresh timestamp at the top
# If you want to restore the original (non-timestamped) changelog after build, uncomment:
# mv debian/changelog.bak debian/changelog 2>/dev/null || true

# Move .deb files to build-output directory
echo ""
echo "→ Moving package files to build-output/..."
mv ../monolith-firewall_*.deb "$BUILD_OUTPUT_DIR/" 2>/dev/null || true
mv ../monolith-firewall_*.changes "$BUILD_OUTPUT_DIR/" 2>/dev/null || true
mv ../monolith-firewall_*.buildinfo "$BUILD_OUTPUT_DIR/" 2>/dev/null || true

DEB_FILE=$(ls "$BUILD_OUTPUT_DIR"/monolith-firewall_*.deb 2>/dev/null | head -1)

echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "  Build Complete!"
echo "═══════════════════════════════════════════════════════════════"
echo ""
if [ -n "$DEB_FILE" ]; then
    echo "Package created: $DEB_FILE"
    echo ""
    echo "To install:"
    echo "  sudo dpkg -i $DEB_FILE"
    echo "  sudo apt-get -f install  # Fix any dependencies"
else
    echo "Package created: $BUILD_OUTPUT_DIR/monolith-firewall_*.deb"
    echo ""
    echo "To install:"
    echo "  sudo dpkg -i $BUILD_OUTPUT_DIR/monolith-firewall_*.deb"
fi
echo ""
