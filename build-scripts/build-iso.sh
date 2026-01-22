#!/bin/bash
set -e

# Optional debug controls (used by build-iso-debug.sh)
# - MONOLITH_ISO_WORKDIR: override work directory
# - MONOLITH_ISO_KEEP_WORKDIR=1: don't delete workdir
# - MONOLITH_ISO_DEBUG_LOG: write full build log to this file (also prints to console)
# - MONOLITH_ISO_DEBUG_TRACE=1: enable shell tracing (set -x)
# - MONOLITH_PRESEED_FILE: use an alternate preseed cfg
# - MONOLITH_ISO_LATE_SCRIPT: copy a helper script onto ISO root for late_command use

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ISO_WORKDIR="${MONOLITH_ISO_WORKDIR:-/tmp/monolith-iso}"
KEEP_WORKDIR="${MONOLITH_ISO_KEEP_WORKDIR:-0}"
DEBUG_LOG="${MONOLITH_ISO_DEBUG_LOG:-}"
DEBUG_TRACE="${MONOLITH_ISO_DEBUG_TRACE:-0}"

if [ -n "$DEBUG_LOG" ]; then
    mkdir -p "$(dirname "$DEBUG_LOG")"
    exec > >(tee -a "$DEBUG_LOG") 2>&1
fi

if [ "$DEBUG_TRACE" = "1" ]; then
    set -x
fi

trap 'rc=$?; echo "ERROR: build-iso.sh failed (exit=$rc) at line $LINENO"; echo "Workdir: '"$ISO_WORKDIR"'"; exit $rc' ERR

echo "═══════════════════════════════════════════════════════════════"
echo "  Monolith FireWall - ISO Builder"
echo "═══════════════════════════════════════════════════════════════"
echo ""

# Configuration
VERSION="${1:-1.0.0}"
BUILD_OUTPUT_DIR="${ROOT_DIR}/build-output"
mkdir -p "$BUILD_OUTPUT_DIR"
# Generate timestamp for ISO filename (YYYYMMDD-HHMMSS)
ISO_TIMESTAMP=$(date +"%Y%m%d-%H%M%S")
ISO_OUTPUT="${BUILD_OUTPUT_DIR}/monolith-firewall-${VERSION}-amd64-${ISO_TIMESTAMP}.iso"
DEBIAN_VERSION="13.3.0"

# Check dependencies
echo "→ Checking dependencies..."
MISSING_DEPS=()
# Use xorriso for EFI boot support (preferred) or genisoimage as fallback
if command -v xorriso &> /dev/null; then
    ISO_TOOL="xorriso"
elif command -v genisoimage &> /dev/null; then
    ISO_TOOL="genisoimage"
    echo "WARNING: Using genisoimage (limited EFI support). Consider installing xorriso for full EFI support."
else
    MISSING_DEPS+=("xorriso or genisoimage")
fi

for cmd in wget 7z isohybrid apt-ftparchive; do
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
    echo "  sudo apt-get install -y p7zip-full xorriso syslinux-utils apt-utils"
    exit 1
fi

# Check if .deb package exists
DEB_FILE=$(find "$ROOT_DIR/build-output" -maxdepth 1 -name "monolith-firewall_*.deb" -type f | head -1)
if [ -z "$DEB_FILE" ]; then
    echo "ERROR: Debian package not found in build-output/. Build it first with:"
    echo "  ./build-scripts/build-deb.sh"
    exit 1
fi

echo "Using Debian package: $(basename "$DEB_FILE")"

# Download .NET runtime packages from Microsoft repository
echo "→ Downloading .NET 10.0 runtime packages from Microsoft..."
DOTNET_DEBS_DIR="$ROOT_DIR/build-output/monolith-debs"
mkdir -p "$DOTNET_DEBS_DIR"
DOTNET_TEMP_DIR="$ISO_WORKDIR/dotnet-download"
mkdir -p "$DOTNET_TEMP_DIR"
cd "$DOTNET_TEMP_DIR"

# Check if packages already exist
DOTNET_RUNTIME=$(find "$DOTNET_DEBS_DIR" -maxdepth 1 -name "dotnet-runtime-10.0*.deb" -type f | head -1)
ASPNET_RUNTIME=$(find "$DOTNET_DEBS_DIR" -maxdepth 1 -name "aspnetcore-runtime-10.0*.deb" -type f | head -1)
DOTNET_HOSTFXR=$(find "$DOTNET_DEBS_DIR" -maxdepth 1 -name "dotnet-hostfxr-10.0*.deb" -type f | head -1)
DOTNET_RUNTIME_DEPS=$(find "$DOTNET_DEBS_DIR" -maxdepth 1 -name "dotnet-runtime-deps-10.0*.deb" -type f | head -1)
DOTNET_SDK=$(find "$DOTNET_DEBS_DIR" -maxdepth 1 -name "dotnet-sdk-10.0*.deb" -type f | head -1)

if [ -z "$DOTNET_RUNTIME" ] || [ -z "$ASPNET_RUNTIME" ] || [ -z "$DOTNET_HOSTFXR" ] || [ -z "$DOTNET_RUNTIME_DEPS" ]; then
    echo "  .NET runtime packages missing, downloading from Microsoft..."
    
    # Download Microsoft package repository configuration
    if [ ! -f "$DOTNET_TEMP_DIR/packages-microsoft-prod.deb" ]; then
        echo "  → Downloading Microsoft repository configuration..."
        # Use Debian 12 (Bookworm) config - works with Debian 13 (Trixie)
        wget -q https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O packages-microsoft-prod.deb || {
            echo "  ⚠ Failed to download Microsoft repo config"
            echo "  Continuing without .NET runtime (will need manual installation)"
            cd "$ROOT_DIR"
            rm -rf "$DOTNET_TEMP_DIR"
        }
    fi
    
    if [ -f "$DOTNET_TEMP_DIR/packages-microsoft-prod.deb" ]; then
        # Extract the repository configuration
        echo "  → Configuring Microsoft repository..."
        dpkg-deb -x packages-microsoft-prod.deb "$DOTNET_TEMP_DIR/microsoft-repo" > /dev/null 2>&1 || true
        
        # Find GPG key location
        GPG_KEY=""
        for key_path in "$DOTNET_TEMP_DIR/microsoft-repo/etc/apt/trusted.gpg.d/microsoft.gpg" \
                       "$DOTNET_TEMP_DIR/microsoft-repo/etc/apt/trusted.gpg.d/microsoft-prod.gpg" \
                       "$DOTNET_TEMP_DIR/microsoft-repo/etc/apt/trusted.gpg.d/microsoft-prod.asc"; do
            if [ -f "$key_path" ]; then
                GPG_KEY="$key_path"
                break
            fi
        done
        
        # Create temporary apt sources
        APT_SOURCES="$DOTNET_TEMP_DIR/sources.list"
        APT_STATE="$DOTNET_TEMP_DIR/apt-state"
        APT_KEYRING="$DOTNET_TEMP_DIR/apt-keyring"
        mkdir -p "$APT_STATE/lists/partial"
        mkdir -p "$APT_STATE/archives/partial"
        
        # Add Microsoft repository (with or without GPG key)
        if [ -n "$GPG_KEY" ]; then
            cat > "$APT_SOURCES" <<EOF
deb [arch=amd64,arm64,armhf signed-by=$GPG_KEY] https://packages.microsoft.com/debian/12/prod bookworm main
EOF
            APT_KEYRING_ARG="-o Dir::Etc::TrustedParts=$(dirname "$GPG_KEY")"
        else
            # Try without GPG verification (less secure but may work)
            cat > "$APT_SOURCES" <<EOF
deb [trusted=yes arch=amd64,arm64,armhf] https://packages.microsoft.com/debian/12/prod bookworm main
EOF
            APT_KEYRING_ARG=""
        fi
        
        # Update apt cache
        echo "  → Updating package lists..."
        apt-get update -o Dir::Etc::SourceList="$APT_SOURCES" \
            -o Dir::State="$APT_STATE" \
            -o Dir::Cache="$DOTNET_TEMP_DIR" \
            -o Dir::Etc::SourceParts="" \
            $APT_KEYRING_ARG \
            2>&1 | grep -v "^Get:" | grep -v "^Hit:" | grep -v "^Ign:" || true
        
        # Download .NET packages
        echo "  → Downloading .NET packages..."
        DOWNLOADED=0
        
        # Function to download a package
        download_dotnet_pkg() {
            local pkg_name="$1"
            # Check if already downloaded
            if [ -f "$DOTNET_DEBS_DIR/${pkg_name}_"*.deb ] 2>/dev/null; then
                return 0
            fi
            if apt-get download -o Dir::Etc::SourceList="$APT_SOURCES" \
                -o Dir::State="$APT_STATE" \
                -o Dir::Cache="$DOTNET_TEMP_DIR" \
                -o Dir::Etc::SourceParts="" \
                $APT_KEYRING_ARG \
                "$pkg_name" 2>/dev/null; then
                local deb_file=$(find "$DOTNET_TEMP_DIR" -maxdepth 1 -name "${pkg_name}_*.deb" -type f | head -1)
                if [ -f "$deb_file" ]; then
                    mv "$deb_file" "$DOTNET_DEBS_DIR/" 2>/dev/null && return 0
                fi
            fi
            return 1
        }
        
        # Download .NET packages and resolve their dependencies recursively
        DOTNET_SEEN=""
        download_dotnet_with_deps() {
            local pkg_name="$1"
            local is_optional="${2:-false}"
            
            if echo "$DOTNET_SEEN" | grep -q "^${pkg_name}$"; then
                return 0
            fi
            DOTNET_SEEN="$DOTNET_SEEN
$pkg_name"

            if download_dotnet_pkg "$pkg_name"; then
                DOWNLOADED=$((DOWNLOADED + 1))
                echo "    ✓ Downloaded $pkg_name"
                
                # Resolve dependencies for this .NET package (recursively)
                local downloaded_deb=$(find "$DOTNET_DEBS_DIR" -maxdepth 1 -name "${pkg_name}_*.deb" -type f | head -1)
                if [ -f "$downloaded_deb" ]; then
                    echo "    → Resolving dependencies for $pkg_name..."
                    # Extract ALL dependencies
                    local deps=$(dpkg-deb -f "$downloaded_deb" Depends 2>/dev/null | sed 's/,/\n/g' | sed 's/|/\n/g' | sed 's/([^)]*)//g' | awk '{print $1}' | grep -v '^\$' || true)
                    for dep in $deps; do
                        dep=$(echo "$dep" | tr -d ' ')
                        if [ -n "$dep" ] && [ "$dep" != "Depends:" ]; then
                            # Only download .NET-related dependencies from Microsoft repo
                            if echo "$dep" | grep -qE "^dotnet-|^aspnetcore-"; then
                                # Recursively download dependencies
                                download_dotnet_with_deps "$dep" "true" || true
                            fi
                        fi
                    done
                fi
                return 0
            else
                if [ "$is_optional" != "true" ]; then
                    echo "    ⚠ Failed to download $pkg_name"
                else
                    echo "    ⚠ Failed to download $pkg_name (optional)"
                fi
                return 1
            fi
        }
        
        if [ -z "$DOTNET_RUNTIME_DEPS" ]; then
            download_dotnet_with_deps "dotnet-runtime-deps-10.0"
        fi

        if [ -z "$DOTNET_HOSTFXR" ]; then
            download_dotnet_with_deps "dotnet-hostfxr-10.0"
        fi

        if [ -z "$DOTNET_RUNTIME" ]; then
            download_dotnet_with_deps "dotnet-runtime-10.0"
        fi

        if [ -z "$ASPNET_RUNTIME" ]; then
            download_dotnet_with_deps "aspnetcore-runtime-10.0"
        fi

        if [ -z "$DOTNET_SDK" ]; then
            download_dotnet_with_deps "dotnet-sdk-10.0" "true"  # SDK is optional
        fi
        
        if [ $DOWNLOADED -gt 0 ]; then
            echo "  ✓ Downloaded $DOWNLOADED .NET package(s)"
        else
            echo "  ⚠ Failed to download .NET packages (may need manual download)"
        fi
        
        # Cleanup
        rm -rf "$DOTNET_TEMP_DIR"
    fi
else
    echo "  ✓ .NET runtime packages already exist in $DOTNET_DEBS_DIR"
fi

cd "$ROOT_DIR"
echo ""

# Build all monolith packages before creating ISO
echo "→ Building all monolith packages..."
PACKAGES_BUILD_DIR="$ROOT_DIR/build-output/packages"
if [ ! -d "$PACKAGES_BUILD_DIR" ] || [ -z "$(ls -A "$PACKAGES_BUILD_DIR"/*.mfwpkg 2>/dev/null)" ]; then
    echo "  No packages found in $PACKAGES_BUILD_DIR"
    echo "  Building all packages from tmp/monolithfirewall-packages/..."
    
    if [ ! -f "$ROOT_DIR/build-scripts/build-all-packages.sh" ]; then
        echo "ERROR: build-all-packages.sh not found"
        exit 1
    fi
    
    chmod +x "$ROOT_DIR/build-scripts/build-all-packages.sh"
    if ! "$ROOT_DIR/build-scripts/build-all-packages.sh"; then
        echo "WARNING: Some packages failed to build, but continuing with ISO creation..."
    fi
else
    echo "  Found existing packages in $PACKAGES_BUILD_DIR"
    echo "  Using: $(ls -1 "$PACKAGES_BUILD_DIR"/*.mfwpkg 2>/dev/null | wc -l) package(s)"
fi
echo ""

# Clean workspace
echo "→ Cleaning workspace..."
if [ "$KEEP_WORKDIR" != "1" ]; then
    rm -rf "$ISO_WORKDIR"
else
    echo "  DEBUG: Keeping workdir, using: $ISO_WORKDIR"
fi
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
PRESEED_SOURCE="${MONOLITH_PRESEED_FILE:-$ROOT_DIR/iso-build/preseed.cfg}"
if [ -f "$PRESEED_SOURCE" ]; then
    echo "→ Adding preseed configuration for automated installation..."
    mkdir -p "$ISO_WORKDIR/iso_extract/preseed"
    cp "$PRESEED_SOURCE" "$ISO_WORKDIR/iso_extract/preseed/preseed.cfg"

    if [ -n "${MONOLITH_ISO_LATE_SCRIPT:-}" ] && [ -f "${MONOLITH_ISO_LATE_SCRIPT:-}" ]; then
        echo "→ Adding debug late_command helper script..."
        cp "${MONOLITH_ISO_LATE_SCRIPT}" "$ISO_WORKDIR/iso_extract/monolith-late-debug.sh"
        chmod +x "$ISO_WORKDIR/iso_extract/monolith-late-debug.sh" 2>/dev/null || true
        echo "  ✓ Added /cdrom/monolith-late-debug.sh"
    fi
    
    # Add MOTD script to ISO
    MOTD_SOURCE="$ROOT_DIR/iso-build/monolith-motd.sh"
    if [ -f "$MOTD_SOURCE" ]; then
        echo "→ Adding MOTD script..."
        cp "$MOTD_SOURCE" "$ISO_WORKDIR/iso_extract/monolith-motd.sh"
        chmod +x "$ISO_WORKDIR/iso_extract/monolith-motd.sh" 2>/dev/null || true
        echo "  ✓ Added /cdrom/monolith-motd.sh"
    else
        echo "  WARNING: monolith-motd.sh not found at $MOTD_SOURCE"
    fi
    
    # Add package installation helper script to ISO
    INSTALL_SCRIPT="$ROOT_DIR/iso-build/install-packages.sh"
    if [ -f "$INSTALL_SCRIPT" ]; then
        echo "→ Adding package installation helper script..."
        cp "$INSTALL_SCRIPT" "$ISO_WORKDIR/iso_extract/install-packages.sh"
        chmod +x "$ISO_WORKDIR/iso_extract/install-packages.sh" 2>/dev/null || true
        echo "  ✓ Added /cdrom/install-packages.sh"
    else
        echo "  WARNING: install-packages.sh not found at $INSTALL_SCRIPT"
    fi
    
    # Modify boot configuration to use preseed for automated installation
    echo "  → Configuring boot parameters for automated installation..."
    
    # Create custom boot menu for BIOS (isolinux)
    echo "  → Creating custom boot menu for BIOS..."
    
    # Create custom menu configuration
    cat > "$ISO_WORKDIR/iso_extract/isolinux/menu.cfg" <<'EOF'
menu hshift 4
menu width 70
menu title Monolith FireWall Installer
menu color title	* #FFFFFFFF *
menu color border	* #00000000 #00000000 none
menu color sel		* #ffffffff #76a1d0ff *
menu color hotsel	1;7;37;40 #ffffffff #76a1d0ff *
menu color tabmsg	* #ffffffff #00000000 *
menu color help		37;40 #ffdddd00 #00000000 none

default autoinstall
label autoinstall
	menu label ^Automated Install
	menu default
	kernel /install.amd/vmlinuz
	append vga=788 initrd=/install.amd/gtk/initrd.gz auto=true priority=critical preseed/file=/cdrom/preseed/preseed.cfg --- quiet

label install
	menu label ^Manual Install
	kernel /install.amd/vmlinuz
	append vga=788 initrd=/install.amd/gtk/initrd.gz --- quiet

label help
	menu label ^Help
	text help
   Press ENTER to start automated installation.
   Select 'Manual Install' for interactive installation.
	endtext
	config prompt.cfg
EOF
    
    # Modify isolinux.cfg to use our custom menu and set timeout
    if [ -f "$ISO_WORKDIR/iso_extract/isolinux/isolinux.cfg" ]; then
        cp "$ISO_WORKDIR/iso_extract/isolinux/isolinux.cfg" "$ISO_WORKDIR/iso_extract/isolinux/isolinux.cfg.bak"
        # Set timeout to 5 seconds (50 = 5 seconds in deciseconds)
        sed -i 's|^timeout.*|timeout 50|g' "$ISO_WORKDIR/iso_extract/isolinux/isolinux.cfg" 2>/dev/null || true
    fi
    
    echo "    ✓ Created custom BIOS boot menu"
    
    # Create custom grub menu for EFI boot
    echo "  → Creating custom boot menu for EFI..."
    
    # Create custom grub.cfg for EFI
    if [ -d "$ISO_WORKDIR/iso_extract/boot/grub" ]; then
        cat > "$ISO_WORKDIR/iso_extract/boot/grub/grub.cfg" <<'EOF'
set default=0
set timeout=5

menuentry 'Automated Install' {
    set background_color=black
    linux    /install.amd/vmlinuz vga=788 auto=true priority=critical preseed/file=/cdrom/preseed/preseed.cfg --- quiet
    initrd   /install.amd/gtk/initrd.gz
}

menuentry 'Manual Install' {
    set background_color=black
    linux    /install.amd/vmlinuz vga=788 --- quiet
    initrd   /install.amd/gtk/initrd.gz
}
EOF
        echo "    ✓ Created custom GRUB menu (boot/grub/grub.cfg)"
    fi
    
    # Create custom grub.cfg for EFI/boot
    if [ -d "$ISO_WORKDIR/iso_extract/EFI/boot" ]; then
        cat > "$ISO_WORKDIR/iso_extract/EFI/boot/grub.cfg" <<'EOF'
set default=0
set timeout=5

menuentry 'Automated Install' {
    set background_color=black
    linux    /install.amd/vmlinuz vga=788 auto=true priority=critical preseed/file=/cdrom/preseed/preseed.cfg --- quiet
    initrd   /install.amd/gtk/initrd.gz
}

menuentry 'Manual Install' {
    set background_color=black
    linux    /install.amd/vmlinuz vga=788 --- quiet
    initrd   /install.amd/gtk/initrd.gz
}
EOF
        echo "    ✓ Created custom GRUB menu (EFI/boot/grub.cfg)"
    fi
    
    echo "  ✓ Automated installation configured (BIOS + EFI)"
    echo "    Note: Users can still interrupt by pressing a key during boot"
else
    echo "WARNING: preseed.cfg not found at iso-build/preseed.cfg"
    echo "  ISO will use default Debian installer configuration (interactive)"
fi

# Create a separate flat APT repo for Monolith (do NOT modify Debian repo metadata)
echo "→ Creating monolith offline APT repo (flat)..."
MONOLITH_DEBS_DIR="$ISO_WORKDIR/iso_extract/monolith-debs"
mkdir -p "$MONOLITH_DEBS_DIR"
cp "$DEB_FILE" "$MONOLITH_DEBS_DIR/"

# Copy .NET runtime packages if they exist in build-output
if [ -d "$ROOT_DIR/build-output/monolith-debs" ]; then
    echo "  → Copying .NET packages from build-output/monolith-debs/..."
    DOTNET_COUNT=0
    for dotnet_deb in "$ROOT_DIR/build-output/monolith-debs"/dotnet-*.deb "$ROOT_DIR/build-output/monolith-debs"/aspnetcore-*.deb; do
        if [ -f "$dotnet_deb" ]; then
            cp "$dotnet_deb" "$MONOLITH_DEBS_DIR/"
            DOTNET_COUNT=$((DOTNET_COUNT + 1))
            echo "    ✓ Added: $(basename "$dotnet_deb")"
        fi
    done
    if [ $DOTNET_COUNT -gt 0 ]; then
        echo "  ✓ Copied $DOTNET_COUNT .NET package(s) to ISO"
    else
        echo "    ⚠ No .NET packages found in build-output/monolith-debs/"
        echo "    Note: .NET packages will be downloaded from Microsoft during build"
    fi
fi

# Download and include ALL dependency packages for offline installation
echo "→ Downloading ALL dependency packages for offline installation..."
DEPS_DIR="$ISO_WORKDIR/deps"
mkdir -p "$DEPS_DIR"

# Note: MONOLITH_DEBS_DIR is already created above and contains monolith-firewall .deb and .NET packages

# Extract dependencies from the .deb package (including transitive dependencies)
echo "  Analyzing dependencies (including transitive)..."
DEB_DEPENDS=$(dpkg-deb -f "$DEB_FILE" Depends | sed 's/,/\n/g' | sed 's/|/\n/g' | sed 's/([^)]*)//g' | awk '{print $1}' | grep -v '^\$' | sort -u)

# Required packages from debian/control + SSH + .NET Runtime
# Note: .NET 10.0 runtime should be downloaded separately and included, or installed from Microsoft repo
# Note: DHCP/DNS packages (isc-dhcp-server, bind9, etc.) are now bundled in monolith-network.mfwpkg
#       and will be installed automatically when the package is installed. No need to include them here.
REQUIRED_PACKAGES="openssh-server openssh-client nftables iproute2 systemd sqlite3 sudo procps iputils-ping bridge-utils vlan ifupdown2 tcpdump mtr traceroute iptables socat ethtool pciutils dbus-user-session"

# Combine all required packages
ALL_PACKAGES="$REQUIRED_PACKAGES"
for pkg in $DEB_DEPENDS; do
    if [ -n "$pkg" ] && [ "$pkg" != "Depends:" ]; then
        ALL_PACKAGES="$ALL_PACKAGES $pkg"
    fi
done

# Add .NET packages to dependency resolution if they exist
if [ -d "$DOTNET_DEBS_DIR" ]; then
    for dotnet_deb in "$DOTNET_DEBS_DIR"/dotnet-runtime-10.0*.deb "$DOTNET_DEBS_DIR"/aspnetcore-runtime-10.0*.deb; do
        if [ -f "$dotnet_deb" ]; then
            # Extract package name from .deb filename
            DOTNET_PKG=$(dpkg-deb -f "$dotnet_deb" Package 2>/dev/null || basename "$dotnet_deb" | sed 's/_.*$//')
            if [ -n "$DOTNET_PKG" ]; then
                ALL_PACKAGES="$ALL_PACKAGES $DOTNET_PKG"
            fi
        fi
    done
fi

# Download packages using apt-get download with dependency resolution
echo "  Downloading packages with all dependencies..."
cd "$DEPS_DIR"

# Create a temporary apt sources list pointing to Debian repositories
APT_SOURCES="$DEPS_DIR/sources.list"
APT_PREFS="$DEPS_DIR/preferences"
APT_STATE="$DEPS_DIR/apt-state"
mkdir -p "$APT_STATE/lists/partial"
mkdir -p "$APT_STATE/archives/partial"

cat > "$APT_SOURCES" <<EOF
deb http://deb.debian.org/debian/ trixie main
deb http://deb.debian.org/debian/ trixie-updates main
deb http://security.debian.org/debian-security trixie-security main
EOF

# Update apt cache
echo "  Updating package lists..."
apt-get update -o Dir::Etc::SourceList="$APT_SOURCES" \
    -o Dir::State="$APT_STATE" \
    -o Dir::Cache="$DEPS_DIR" \
    -o Dir::Etc::SourceParts="" \
    2>&1 | grep -v "^Get:" | grep -v "^Hit:" | grep -v "^Ign:" || true

# Download packages with ALL dependencies recursively
echo "  Downloading packages and ALL dependencies..."
DOWNLOADED=0
DOWNLOADED_PKGS=""

# Helper function: extract deps from a .deb and recurse (defined before download_with_deps)
extract_deps_and_recurse() {
    local deb_path="$1"
    if [ ! -f "$deb_path" ]; then
        return
    fi

    # Include both Depends and Pre-Depends (version constraints removed, alternatives split)
    local deps
    deps=$(
        {
            dpkg-deb -f "$deb_path" Pre-Depends 2>/dev/null || true
            dpkg-deb -f "$deb_path" Depends 2>/dev/null || true
        } \
        | sed 's/,/\n/g' \
        | sed 's/|/\n/g' \
        | sed 's/([^)]*)//g' \
        | awk 'NF {print $1}' \
        | sed 's/:[a-z0-9][a-z0-9]*$//' \
        | grep -v '^\$' \
        || true
    )

    for dep in $deps; do
        download_with_deps "$dep"
    done
}

# Function to download package and all its dependencies
download_with_deps() {
    local pkg="$1"
    pkg=$(echo "$pkg" | tr -d ' ')
    pkg=$(echo "$pkg" | sed 's/:[a-z0-9][a-z0-9]*$//')
    
    if [ -z "$pkg" ] || [ "$pkg" = "Depends:" ]; then
        return
    fi
    
    # Skip if already downloaded
    if echo "$DOWNLOADED_PKGS" | grep -q "^${pkg}$"; then
        return
    fi

    # Check if package is already in ISO (copy into monolith-debs and resolve deps)
    ISO_DEB=$(find "$ISO_WORKDIR/iso_extract/pool" -name "${pkg}_*.deb" -type f 2>/dev/null | head -1)
    if [ -n "$ISO_DEB" ] && [ -f "$ISO_DEB" ]; then
        echo "    ✓ $pkg (already in ISO)"
        # Ensure it is available in the monolith offline repo
        if [ -n "$MONOLITH_DEBS_DIR" ] && [ -d "$MONOLITH_DEBS_DIR" ]; then
            BASENAME=$(basename "$ISO_DEB")
            if [ ! -f "$MONOLITH_DEBS_DIR/$BASENAME" ]; then
                cp "$ISO_DEB" "$MONOLITH_DEBS_DIR/" 2>/dev/null || true
            fi
        fi
        DOWNLOADED_PKGS="$DOWNLOADED_PKGS
$pkg"
        extract_deps_and_recurse "$ISO_DEB"
        return
    fi
    
    # Check if package is in monolith-debs (already downloaded from Microsoft or included)
    MONOLITH_DEB=$(find "$MONOLITH_DEBS_DIR" -name "${pkg}_*.deb" -type f 2>/dev/null | head -1)
    if [ -n "$MONOLITH_DEB" ] && [ -f "$MONOLITH_DEB" ]; then
        echo "    ✓ $pkg (already in monolith-debs)"
        DOWNLOADED_PKGS="$DOWNLOADED_PKGS
$pkg"
        extract_deps_and_recurse "$MONOLITH_DEB"
        return
    fi
    
    # Check if package is in DOTNET_DEBS_DIR (downloaded earlier, before copying to ISO)
    if [ -d "$DOTNET_DEBS_DIR" ]; then
        DOTNET_DEB=$(find "$DOTNET_DEBS_DIR" -name "${pkg}_*.deb" -type f 2>/dev/null | head -1)
        if [ -n "$DOTNET_DEB" ] && [ -f "$DOTNET_DEB" ]; then
            echo "    ✓ $pkg (already downloaded from Microsoft)"
            DOWNLOADED_PKGS="$DOWNLOADED_PKGS
$pkg"
            extract_deps_and_recurse "$DOTNET_DEB"
            return
        fi
    fi
    
    # Try to download the package from Debian repos
    if apt-get download -o Dir::Etc::SourceList="$APT_SOURCES" \
        -o Dir::State="$APT_STATE" \
        -o Dir::Cache="$DEPS_DIR" \
        "$pkg" 2>/dev/null; then
        echo "    ✓ Downloaded: $pkg"
        DOWNLOADED=$((DOWNLOADED + 1))
        DOWNLOADED_PKGS="$DOWNLOADED_PKGS
$pkg"
        
        # Resolve dependencies from the downloaded .deb (most recent match)
        DOWNLOADED_DEB=$(find "$DEPS_DIR" -maxdepth 1 -name "${pkg}_*.deb" -type f 2>/dev/null | head -1)
        if [ -n "$DOWNLOADED_DEB" ] && [ -f "$DOWNLOADED_DEB" ]; then
            extract_deps_and_recurse "$DOWNLOADED_DEB"
        fi
    else
        # Check if it's a .NET package (expected to not be in Debian repos)
        if echo "$pkg" | grep -qE "^dotnet-|^aspnetcore-"; then
            # This is expected - .NET packages are from Microsoft, not Debian
            # They should already be in monolith-debs (checked above)
            echo "    ℹ $pkg (from Microsoft repo, should be in monolith-debs)"
        else
            echo "    ⚠ Could not download: $pkg (may be in base system or virtual package)"
        fi
    fi
}

# Download all packages with their dependencies
for pkg in $ALL_PACKAGES; do
    download_with_deps "$pkg"
done

# Also resolve dependencies for .NET packages if they exist in monolith-debs
if [ -d "$DOTNET_DEBS_DIR" ]; then
    echo "  → Resolving dependencies for .NET packages..."
    for dotnet_deb in "$DOTNET_DEBS_DIR"/dotnet-*.deb "$DOTNET_DEBS_DIR"/aspnetcore-*.deb "$DOTNET_DEBS_DIR"/dotnet-sdk-*.deb; do
        if [ -f "$dotnet_deb" ]; then
            DOTNET_PKG=$(dpkg-deb -f "$dotnet_deb" Package 2>/dev/null || basename "$dotnet_deb" | sed 's/_.*$//')
            if [ -n "$DOTNET_PKG" ]; then
                echo "    Resolving dependencies for $DOTNET_PKG..."
                # Extract dependencies from the .NET package .deb file
                extract_deps_and_recurse "$dotnet_deb"
            fi
        fi
    done
fi

# Copy downloaded packages to monolith offline APT repo (flat)
if [ $DOWNLOADED -gt 0 ]; then
    echo "  → Adding $DOWNLOADED downloaded package(s) to monolith offline repo..."
    for deb_file in "$DEPS_DIR"/*.deb; do
        if [ -f "$deb_file" ]; then
            cp "$deb_file" "$MONOLITH_DEBS_DIR/"
        fi
    done
    echo "  ✓ Added dependency packages to monolith offline repo"
else
    echo "  → No additional packages needed (all dependencies in base ISO)"
fi

# Also copy any dependencies that were already present in the Debian ISO pool
# (because our late_command installs from monolith-debs only)
echo "  → Adding ISO-provided packages to monolith offline repo..."
echo "$DOWNLOADED_PKGS" | while read -r pkg; do
    pkg=$(echo "$pkg" | tr -d ' ')
    if [ -z "$pkg" ]; then
        continue
    fi

    # Copy any matching debs from ISO pool
    for deb in $(find "$ISO_WORKDIR/iso_extract/pool" -name "${pkg}_*.deb" -type f 2>/dev/null); do
        cp -n "$deb" "$MONOLITH_DEBS_DIR/" 2>/dev/null || true
    done
done
echo "  ✓ ISO-provided packages added (if needed)"

# Generate Packages index for the monolith flat repo (installer will not use Debian's dists/ for this)
echo "→ Generating monolith offline repo index..."
cd "$ISO_WORKDIR/iso_extract"
apt-ftparchive packages monolith-debs > monolith-debs/Packages 2>/dev/null || true
gzip -9 -f -k monolith-debs/Packages 2>/dev/null || true
echo "  ✓ Created monolith offline repo index (monolith-debs/Packages.gz)"

# Add ALL monolith packages (.mfwpkg) to ISO
PACKAGES_DIR="$ROOT_DIR/build-output/packages"
if [ -d "$PACKAGES_DIR" ] && [ -n "$(ls -A "$PACKAGES_DIR"/*.mfwpkg 2>/dev/null)" ]; then
    echo "→ Adding ALL monolith packages (.mfwpkg) to ISO..."
    mkdir -p "$ISO_WORKDIR/iso_extract/monolith-packages"
    
    PACKAGE_COUNT=0
    for pkg in "$PACKAGES_DIR"/*.mfwpkg; do
        if [ -f "$pkg" ]; then
            cp "$pkg" "$ISO_WORKDIR/iso_extract/monolith-packages/"
            PACKAGE_COUNT=$((PACKAGE_COUNT + 1))
            echo "  ✓ Added: $(basename "$pkg")"
        fi
    done
    
    echo "  → Total: $PACKAGE_COUNT package(s) added to ISO"
    echo "  These will be automatically installed on first boot"
else
    echo "→ WARNING: No .mfwpkg packages found in $PACKAGES_DIR"
    echo "  Run ./build-scripts/build-all-packages.sh first to build default packages"
    echo "  ISO will be created without monolith packages"
fi

# NOTE:
# We intentionally DO NOT modify Debian's dists/ metadata (Packages/Release/InRelease).
# The base installer must remain stock. Monolith is installed later from /monolith-debs/.

# Rebuild ISO with EFI support
echo "→ Rebuilding ISO with EFI boot support..."
cd "$ISO_WORKDIR/iso_extract"

# Check if EFI boot files exist
HAS_EFI=false
if [ -d "EFI" ] && [ -f "EFI/boot/bootx64.efi" ]; then
    HAS_EFI=true
    echo "  Found EFI boot files"
fi

if [ "$ISO_TOOL" = "xorriso" ]; then
    # Use xorriso for full EFI support
    XORRISO_CMD="xorriso -as mkisofs"
    XORRISO_CMD="$XORRISO_CMD -r -J"
    XORRISO_CMD="$XORRISO_CMD -b isolinux/isolinux.bin -c isolinux/boot.cat"
    XORRISO_CMD="$XORRISO_CMD -no-emul-boot -boot-load-size 4 -boot-info-table"
    
    if [ "$HAS_EFI" = "true" ]; then
        # EFI boot configuration
        XORRISO_CMD="$XORRISO_CMD -eltorito-alt-boot"
        XORRISO_CMD="$XORRISO_CMD -e EFI/boot/bootx64.efi"
        XORRISO_CMD="$XORRISO_CMD -no-emul-boot"
        # Create hybrid ISO (BIOS + EFI) - GPT partition table for EFI, MBR for BIOS
        XORRISO_CMD="$XORRISO_CMD -isohybrid-gpt-basdat"
        # Find isohdpfx.bin for MBR boot sector
        ISOHDPFX=""
        for path in /usr/lib/ISOLINUX/isohdpfx.bin /usr/lib/syslinux/isohdpfx.bin /usr/share/syslinux/isohdpfx.bin; do
            if [ -f "$path" ]; then
                ISOHDPFX="$path"
                break
            fi
        done
        if [ -n "$ISOHDPFX" ]; then
            XORRISO_CMD="$XORRISO_CMD -isohybrid-mbr $ISOHDPFX"
        else
            echo "  WARNING: isohdpfx.bin not found, BIOS boot may not work"
        fi
    fi
    
    XORRISO_CMD="$XORRISO_CMD -o \"$ISO_OUTPUT\" ."
    
    eval "$XORRISO_CMD" 2>&1 | grep -v "^xorriso" | grep -v "^libisofs" || {
        echo "ERROR: Failed to build ISO with xorriso"
        exit 1
    }
    
    echo "  ISO built with xorriso (EFI support: $HAS_EFI)"
    
    # Note: xorriso with -isohybrid-gpt-basdat and -isohybrid-mbr should create a hybrid ISO
    # No need for additional isohybrid step when using xorriso with these options
else
    # Fallback to genisoimage (limited EFI support)
    genisoimage -r -J -b isolinux/isolinux.bin -c isolinux/boot.cat \
        -no-emul-boot -boot-load-size 4 -boot-info-table \
        -o "$ISO_OUTPUT" . 2>&1 | grep -v "genisoimage:" || {
        echo "ERROR: Failed to build ISO"
        exit 1
    }
    
    if [ "$HAS_EFI" = "true" ]; then
        echo "  WARNING: genisoimage has limited EFI support. EFI boot may not work."
        echo "  Consider installing xorriso for full EFI support."
    fi
fi

# Make bootable (BIOS and EFI) - only if not already done by xorriso
if [ "$ISO_TOOL" != "xorriso" ] || [ "$HAS_EFI" != "true" ]; then
    echo "→ Making ISO bootable..."
    if [ "$HAS_EFI" = "true" ]; then
        # Use isohybrid with EFI support
        isohybrid --uefi "$ISO_OUTPUT" 2>&1 | grep -v "isohybrid:" || {
            # Fallback to regular isohybrid if --uefi not supported
            isohybrid "$ISO_OUTPUT" 2>&1 | grep -v "isohybrid:" || {
                echo "WARNING: isohybrid failed (ISO may still be bootable)"
            }
        }
    else
        # BIOS only
        isohybrid "$ISO_OUTPUT" 2>&1 | grep -v "isohybrid:" || {
            echo "WARNING: isohybrid failed (ISO may still be bootable)"
        }
    fi
fi

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
