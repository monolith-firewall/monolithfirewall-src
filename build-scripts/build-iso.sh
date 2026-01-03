#!/bin/bash
set -e

echo "═══════════════════════════════════════════════════════════════"
echo "  Monolith FireWall - ISO Builder"
echo "═══════════════════════════════════════════════════════════════"
echo ""

# Configuration
ISO_WORKDIR="/tmp/monolith-iso"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${1:-1.0.0}"
ISO_OUTPUT="${ROOT_DIR}/monolith-firewall-${VERSION}-amd64.iso"
DEBIAN_VERSION="13.2.0"

# Check dependencies
echo "→ Checking dependencies..."
MISSING_DEPS=()
for cmd in wget 7z genisoimage isohybrid apt-ftparchive; do
    if ! command -v "$cmd" &> /dev/null; then
        MISSING_DEPS+=("$cmd")
    fi
done

if [ ${#MISSING_DEPS[@]} -gt 0 ]; then
    echo "ERROR: Missing required dependencies:"
    for dep in "${MISSING_DEPS[@]}"; do
        echo "  - $dep"
    done
    echo ""
    echo "Install with:"
    echo "  sudo apt-get install -y p7zip-full genisoimage syslinux-utils apt-utils"
    exit 1
fi

# Check if .deb package exists
DEB_FILE=$(find "$ROOT_DIR/.." -maxdepth 1 -name "monolith-firewall_*.deb" -type f | head -1)
if [ -z "$DEB_FILE" ]; then
    echo "ERROR: Debian package not found. Build it first with:"
    echo "  ./build-scripts/build-deb.sh"
    exit 1
fi

echo "Using Debian package: $(basename "$DEB_FILE")"
echo ""

# Clean workspace
echo "→ Cleaning workspace..."
rm -rf "$ISO_WORKDIR"
mkdir -p "$ISO_WORKDIR"

# Download Debian netinst ISO
echo "→ Downloading Debian ${DEBIAN_VERSION} netinst ISO..."
DEBIAN_ISO_URL="https://cdimage.debian.org/debian-cd/current/amd64/iso-cd/debian-${DEBIAN_VERSION}-amd64-netinst.iso"
if [ ! -f "$ISO_WORKDIR/debian-netinst.iso" ]; then
    wget -O "$ISO_WORKDIR/debian-netinst.iso" "$DEBIAN_ISO_URL" || {
        echo "ERROR: Failed to download Debian ISO"
        echo "URL: $DEBIAN_ISO_URL"
        exit 1
    }
else
    echo "  Using cached ISO: $ISO_WORKDIR/debian-netinst.iso"
fi

# Extract ISO
echo "→ Extracting ISO..."
mkdir -p "$ISO_WORKDIR/iso_extract"
7z x -o"$ISO_WORKDIR/iso_extract" "$ISO_WORKDIR/debian-netinst.iso" > /dev/null

# Add custom preseed configuration
if [ -f "$ROOT_DIR/iso-build/preseed.cfg" ]; then
    echo "→ Adding preseed configuration..."
    mkdir -p "$ISO_WORKDIR/iso_extract/preseed"
    cp "$ROOT_DIR/iso-build/preseed.cfg" "$ISO_WORKDIR/iso_extract/preseed/"
else
    echo "WARNING: preseed.cfg not found at iso-build/preseed.cfg"
    echo "  ISO will use default Debian installer configuration"
fi

# Add monolith-firewall .deb to ISO
echo "→ Adding monolith-firewall package to ISO..."
mkdir -p "$ISO_WORKDIR/iso_extract/pool/main/m/monolith-firewall"
cp "$DEB_FILE" "$ISO_WORKDIR/iso_extract/pool/main/m/monolith-firewall/"

# Add monolith packages (.mfwpkg) if they exist
PACKAGES_DIR="$ROOT_DIR/build/packages"
if [ -d "$PACKAGES_DIR" ] && [ -n "$(ls -A "$PACKAGES_DIR"/*.mfwpkg 2>/dev/null)" ]; then
    echo "→ Adding monolith packages (.mfwpkg) to ISO..."
    mkdir -p "$ISO_WORKDIR/iso_extract/monolith-packages"
    cp "$PACKAGES_DIR"/*.mfwpkg "$ISO_WORKDIR/iso_extract/monolith-packages/"
    echo "  Added $(ls -1 "$PACKAGES_DIR"/*.mfwpkg 2>/dev/null | wc -l) package(s)"
else
    echo "→ No .mfwpkg packages found (skipping)"
fi

# Update package indices
echo "→ Updating package indices..."
cd "$ISO_WORKDIR/iso_extract"
if [ -d "dists/stable/main/binary-amd64" ]; then
    apt-ftparchive packages pool/main > dists/stable/main/binary-amd64/Packages 2>/dev/null || true
    if [ -f "dists/stable/main/binary-amd64/Packages" ]; then
        gzip -k -f dists/stable/main/binary-amd64/Packages
    fi
else
    echo "WARNING: Could not update package indices (directory structure may differ)"
fi

# Rebuild ISO
echo "→ Rebuilding ISO..."
cd "$ISO_WORKDIR/iso_extract"
genisoimage -r -J -b isolinux/isolinux.bin -c isolinux/boot.cat \
    -no-emul-boot -boot-load-size 4 -boot-info-table \
    -o "$ISO_OUTPUT" . 2>&1 | grep -v "genisoimage:" || {
    echo "ERROR: Failed to build ISO"
    exit 1
}

# Make bootable
echo "→ Making ISO bootable..."
isohybrid "$ISO_OUTPUT" 2>&1 | grep -v "isohybrid:" || {
    echo "WARNING: isohybrid failed (ISO may still be bootable)"
}

echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "  ISO Build Complete!"
echo "═══════════════════════════════════════════════════════════════"
echo ""
echo "ISO created: $ISO_OUTPUT"
echo "Size: $(du -h "$ISO_OUTPUT" | cut -f1)"
echo ""
echo "To test:"
echo "  qemu-system-x86_64 -cdrom $ISO_OUTPUT -m 2048"
echo ""
echo "To burn to USB:"
echo "  sudo dd if=$ISO_OUTPUT of=/dev/sdX bs=4M status=progress"
echo ""
