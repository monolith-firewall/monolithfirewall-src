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

# Install build dependencies
echo "→ Installing build dependencies..."
sudo apt-get update
sudo apt-get install -y \
    debhelper \
    devscripts \
    build-essential \
    wget \
    libicu76 \
    libc6 \
    libgcc-s1 \
    libstdc++6 \
    zlib1g

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

# Make debian scripts executable
chmod +x debian/rules
chmod +x debian/postinst
chmod +x debian/prerm
chmod +x debian/postrm

# Build the package
dpkg-buildpackage -us -uc -b

echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "  Build Complete!"
echo "═══════════════════════════════════════════════════════════════"
echo ""
echo "Package created: ../monolith-firewall_1.0.0-1_amd64.deb"
echo ""
echo "To install:"
echo "  sudo dpkg -i ../monolith-firewall_1.0.0-1_amd64.deb"
echo "  sudo apt-get -f install  # Fix any dependencies"
echo ""
