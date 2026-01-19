#!/bin/bash
# Monolith FireWall First Boot Script
# Installs packages from ISO/CDROM on first boot

set -e

FIRSTBOOT_FLAG="/var/lib/monolith-firewall/.firstboot"
SOCKET_PATH="/var/lib/monolith-firewall/run/monolith-core.sock"
PACKAGES_DIR="/var/lib/monolith-firewall/packages"
CDROM_PACKAGES="/media/cdrom/monolith-packages"

# Check if first boot flag exists
if [ ! -f "$FIRSTBOOT_FLAG" ]; then
    echo "Not first boot, skipping package installation"
    exit 0
fi

echo "═══════════════════════════════════════════════════════════════"
echo "  Monolith FireWall - First Boot Package Installation"
echo "═══════════════════════════════════════════════════════════════"
echo ""

# Wait for Core service to be ready
echo "→ Waiting for Core service to be ready..."
MAX_WAIT=60
WAITED=0
while [ ! -S "$SOCKET_PATH" ] && [ $WAITED -lt $MAX_WAIT ]; do
    sleep 1
    WAITED=$((WAITED + 1))
done

if [ ! -S "$SOCKET_PATH" ]; then
    echo "ERROR: Core service socket not available after ${MAX_WAIT}s"
    echo "Packages will be installed on next boot or manually"
    exit 1
fi

echo "✓ Core service is ready"
echo ""

# Function to install package via Unix socket API
install_package() {
    local package_file="$1"
    local package_name=$(basename "$package_file")
    
    echo "  Installing: $package_name"
    
    # Create JSON request for package installation
    local request_json=$(cat <<EOF
{
    "action": "packages.install",
    "payload": {
        "packageId": null,
        "sourcePath": "$package_file",
        "overwrite": true
    }
}
EOF
)
    
    # Try using monolith CLI tool first (if available), then fallback to socat
    if command -v monolith &> /dev/null; then
        # Use monolith CLI tool (preferred method)
        if monolith package install "$package_file" --overwrite 2>&1 | grep -qi "success\|installed"; then
            echo "    ✓ Successfully installed: $package_name"
            return 0
        else
            echo "    ✗ Failed to install $package_name"
            return 1
        fi
    elif command -v socat &> /dev/null; then
        # Use socat as fallback
        local response=$(echo "$request_json" | socat - UNIX-CONNECT:"$SOCKET_PATH" 2>/dev/null || echo "")
        
        if echo "$response" | grep -q '"Success":true'; then
            echo "    ✓ Successfully installed: $package_name"
            return 0
        else
            local error=$(echo "$response" | grep -o '"Error":"[^"]*"' | sed 's/"Error":"\([^"]*\)"/\1/' || echo "Unknown error")
            echo "    ✗ Failed to install $package_name: $error"
            return 1
        fi
    else
        echo "    ⚠ Cannot install: socat or monolith CLI not available"
        echo "    Package will be installed manually or on next boot"
        return 1
    fi
}

# Install ALL packages found (from preseed copy or CDROM)
INSTALLED=0
FAILED=0
PACKAGES_TO_INSTALL=()

# Collect packages from packages directory (from preseed copy)
if [ -d "$PACKAGES_DIR" ] && [ -n "$(ls -A "$PACKAGES_DIR"/*.mfwpkg 2>/dev/null)" ]; then
    for pkg in "$PACKAGES_DIR"/*.mfwpkg; do
        if [ -f "$pkg" ]; then
            PACKAGES_TO_INSTALL+=("$pkg")
        fi
    done
fi

# Also collect from CDROM (in case packages weren't copied during install)
if [ -d "$CDROM_PACKAGES" ] && [ -n "$(ls -A "$CDROM_PACKAGES"/*.mfwpkg 2>/dev/null)" ]; then
    for pkg in "$CDROM_PACKAGES"/*.mfwpkg; do
        if [ -f "$pkg" ]; then
            # Copy to packages directory first
            mkdir -p "$PACKAGES_DIR"
            cp "$pkg" "$PACKAGES_DIR/"
            PACKAGES_TO_INSTALL+=("$PACKAGES_DIR/$(basename "$pkg")")
        fi
    done
fi

# Install ALL collected packages
if [ ${#PACKAGES_TO_INSTALL[@]} -gt 0 ]; then
    echo "→ Installing ALL monolith packages (${#PACKAGES_TO_INSTALL[@]} package(s))..."
    echo ""
    
    for pkg in "${PACKAGES_TO_INSTALL[@]}"; do
        if [ -f "$pkg" ]; then
            if install_package "$pkg"; then
                INSTALLED=$((INSTALLED + 1))
            else
                FAILED=$((FAILED + 1))
            fi
        fi
    done
else
    echo "→ No monolith packages found to install"
    echo "  Packages directory: $PACKAGES_DIR"
    echo "  CDROM directory: $CDROM_PACKAGES"
fi

echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "  Installation Complete"
echo "═══════════════════════════════════════════════════════════════"
echo ""
echo "Installed: $INSTALLED package(s)"
if [ $FAILED -gt 0 ]; then
    echo "Failed: $FAILED package(s)"
fi
echo ""

# Remove first boot flag
rm -f "$FIRSTBOOT_FLAG"
echo "✓ First boot flag removed"
echo ""

# Restart Core service to load new packages
echo "→ Restarting Core service to load new packages..."
systemctl restart monolith-firewall-core.service || true

# Ensure network interfaces are up (in case they didn't get IPs during install)
echo ""
echo "→ Ensuring network interfaces are configured..."
if command -v ifreload &> /dev/null; then
    # Check if any interface has an IP
    HAS_IP=$(ip -4 addr show | grep -c "inet " || echo "0")
    if [ "$HAS_IP" -eq "0" ]; then
        echo "  No interfaces have IP addresses, configuring..."
        # Get primary interface
        PRIMARY_IFACE=$(ip -o link show | grep -v lo | awk -F': ' '{print $2}' | head -1)
        if [ -n "$PRIMARY_IFACE" ]; then
            # Ensure interfaces.d is sourced
            mkdir -p /etc/network/interfaces.d
            if [ ! -f /etc/network/interfaces ]; then
                cat > /etc/network/interfaces <<EOF
auto lo
iface lo inet loopback
source /etc/network/interfaces.d/*
EOF
            else
                if ! grep -q "^source /etc/network/interfaces.d/\\*" /etc/network/interfaces; then
                    echo "source /etc/network/interfaces.d/*" >> /etc/network/interfaces
                fi
            fi

            if [ ! -f "/etc/network/interfaces.d/monolith" ]; then
                cat > /etc/network/interfaces.d/monolith <<EOF
# Network configuration - managed by Monolith
auto $PRIMARY_IFACE
iface $PRIMARY_IFACE inet dhcp
EOF
                echo "  Created network config for $PRIMARY_IFACE"
            else
                echo "  Using existing network config for $PRIMARY_IFACE"
            fi

            # Bring up interfaces
            ip link set dev "$PRIMARY_IFACE" up 2>/dev/null || true
            ifreload -a || {
                echo "  ifreload failed, trying ifup..."
                ifup "$PRIMARY_IFACE" || true
            }
            sleep 3
            if ! ip -4 addr show "$PRIMARY_IFACE" 2>/dev/null | grep -q "inet "; then
                if command -v dhclient &> /dev/null; then
                    echo "  Attempting DHCP via dhclient..."
                    dhclient -v -1 "$PRIMARY_IFACE" || true
                    sleep 2
                fi
            fi
        fi
    fi
    # Show interface status
    echo "Network interface status:"
    ip -4 addr show | grep -E "^[0-9]+:|inet " || echo "  No IPv4 addresses configured"
else
    echo "  WARNING: ifreload not found - attempting ifup..."
    PRIMARY_IFACE=$(ip -o link show | grep -v lo | awk -F': ' '{print $2}' | head -1)
    if command -v ifup &> /dev/null && [ -n "$PRIMARY_IFACE" ]; then
        ip link set dev "$PRIMARY_IFACE" up 2>/dev/null || true
        ifup "$PRIMARY_IFACE" || true
        sleep 3
        if ! ip -4 addr show "$PRIMARY_IFACE" 2>/dev/null | grep -q "inet "; then
            if command -v dhclient &> /dev/null; then
                echo "  Attempting DHCP via dhclient..."
                dhclient -v -1 "$PRIMARY_IFACE" || true
                sleep 2
            fi
        fi
    else
        echo "  Install ifupdown2: apt-get install -y ifupdown2"
    fi
fi

echo ""
echo "First boot package installation complete!"
echo ""
