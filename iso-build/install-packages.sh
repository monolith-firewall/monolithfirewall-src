#!/bin/bash
# Helper script for non-interactive package installation during preseed

set -e

export DEBIAN_FRONTEND=noninteractive
export APT_LISTBUGS_FRONTEND=none
export APT_LISTCHANGES_FRONTEND=none

LOG="/var/log/monolith-install.log"

# Use the local offline repo and disable CDROM sources to avoid apt-cdrom errors
if [ -d /var/cache/monolith-debs ]; then
    echo "Configuring offline APT repo..." >> "$LOG"
    echo "deb [trusted=yes] file:/var/cache/monolith-debs ./" > /etc/apt/sources.list.d/monolith-offline.list
fi

# Disable CDROM sources (installer environment may not have apt-cdrom configured)
sed -i 's|^deb cdrom|#deb cdrom|g' /etc/apt/sources.list 2>/dev/null || true
for list in /etc/apt/sources.list.d/*.list; do
    [ -f "$list" ] && sed -i 's|^deb cdrom|#deb cdrom|g' "$list" 2>/dev/null || true
done

# Update package lists
echo "Updating package lists..." >> "$LOG"
apt-get -o Acquire::Languages=none \
        -o Acquire::Check-Valid-Until=false \
        -o Acquire::AllowInsecureRepositories=true \
        -o Acquire::AllowDowngradeToInsecureRepositories=true \
        -o Dpkg::Options::="--force-confdef" \
        -o Dpkg::Options::="--force-confold" \
        update >> "$LOG" 2>&1 || true

# Note: ifupdown2 will be installed as a dependency of monolith-firewall
# We skip installing it here because it requires python3 which may not be in offline repo
# The monolith-firewall package will pull it in with all its dependencies
echo "Skipping ifupdown2 installation (will be installed with monolith-firewall)..." >> "$LOG"

# Keep ifupdown installed for now to avoid losing network if monolith install fails.
# It can be removed later once ifupdown2 is confirmed installed.
if dpkg -l | grep -q "^ii.*ifupdown "; then
    echo "Keeping ifupdown (will remove after monolith install if needed)..." >> "$LOG"
fi

# Install .NET runtime packages first (monolith depends on them)
# Install in dependency order: deps -> hostfxr -> runtime -> aspnetcore
echo "Installing .NET runtime packages..." >> "$LOG"
DOTNET_INSTALLED=0

# Install in correct dependency order
for dotnet_pkg in dotnet-runtime-deps-10.0 dotnet-hostfxr-10.0 dotnet-runtime-10.0 aspnetcore-runtime-10.0; do
    DOTNET_DEB=$(find /var/cache/monolith-debs -maxdepth 1 -name "${dotnet_pkg}_*.deb" -type f | head -1)
    if [ -f "$DOTNET_DEB" ]; then
        echo "Installing $(basename "$DOTNET_DEB")..." >> "$LOG"
        dpkg -i "$DOTNET_DEB" >> "$LOG" 2>&1 || {
            # Try to fix dependencies
            echo "Fixing dependencies for $dotnet_pkg..." >> "$LOG"
            apt-get -f install -y >> "$LOG" 2>&1 || true
        }
        DOTNET_INSTALLED=$((DOTNET_INSTALLED + 1))
    else
        echo "WARNING: $dotnet_pkg not found in /var/cache/monolith-debs" >> "$LOG"
    fi
done

if [ $DOTNET_INSTALLED -gt 0 ]; then
    echo "Installed $DOTNET_INSTALLED .NET package(s)" >> "$LOG"
else
    echo "WARNING: No .NET runtime packages found - monolith may not install" >> "$LOG"
fi

# Install openssh
echo "Installing openssh-server and openssh-client..." >> "$LOG"
apt-get -o Acquire::Languages=none \
        -o Dpkg::Options::="--force-confdef" \
        -o Dpkg::Options::="--force-confold" \
        install -y --no-install-recommends \
        openssh-server openssh-client >> "$LOG" 2>&1 || {
    echo "openssh installation failed" >> "$LOG"
    exit 1
}

# Install monolith-firewall (after .NET runtime)
echo "Installing monolith-firewall..." >> "$LOG"
apt-get -o Acquire::Languages=none \
        -o Dpkg::Options::="--force-confdef" \
        -o Dpkg::Options::="--force-confold" \
        install -y --no-install-recommends \
        monolith-firewall >> "$LOG" 2>&1 || {
    echo "monolith-firewall installation failed - will retry on first boot" >> "$LOG"
    # Check if it's a dependency issue
    if grep -q "depends on" "$LOG" 2>/dev/null; then
        echo "Dependency issue detected - .NET runtime may be missing" >> "$LOG"
    fi
    exit 0  # Don't fail the entire installation
}

echo "Package installation complete." >> "$LOG"

# Ensure network interfaces get IP addresses on first boot
echo "Configuring network for first boot..." >> "$LOG"

# Get the primary interface (first non-loopback, not already configured)
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

    if [ -f /etc/network/interfaces.d/monolith ]; then
        echo "Found existing network config (/etc/network/interfaces.d/monolith), bringing interface up..." >> "$LOG"
    else
        echo "Interface $PRIMARY_IFACE has no config, creating DHCP config..." >> "$LOG"
        cat > /etc/network/interfaces.d/monolith <<EOF
# Temporary network configuration - will be managed by Monolith after first boot
auto $PRIMARY_IFACE
iface $PRIMARY_IFACE inet dhcp
EOF
    fi

    # Bring up interfaces (prefer ifreload, fallback to ifup)
    ip link set dev "$PRIMARY_IFACE" up 2>/dev/null || true
    if [ -f /usr/sbin/ifreload ]; then
        echo "Bringing up network interface $PRIMARY_IFACE (ifreload)..." >> "$LOG"
        /usr/sbin/ifreload -a >> "$LOG" 2>&1 || true
    elif command -v ifup &> /dev/null; then
        echo "Bringing up network interface $PRIMARY_IFACE (ifup)..." >> "$LOG"
        /sbin/ifup "$PRIMARY_IFACE" >> "$LOG" 2>&1 || true
    else
        echo "WARNING: no ifreload/ifup available, network may not be configured" >> "$LOG"
    fi
    # Wait a moment for DHCP
    sleep 3
    # Check if we got an IP
    if ip -4 addr show "$PRIMARY_IFACE" 2>/dev/null | grep -q "inet "; then
        echo "✓ Interface $PRIMARY_IFACE configured with IP address" >> "$LOG"
    else
        echo "⚠ Interface $PRIMARY_IFACE still has no IP address" >> "$LOG"
        if command -v dhclient &> /dev/null; then
            echo "Attempting DHCP via dhclient..." >> "$LOG"
            dhclient -v -1 "$PRIMARY_IFACE" >> "$LOG" 2>&1 || true
            sleep 2
            if ip -4 addr show "$PRIMARY_IFACE" 2>/dev/null | grep -q "inet "; then
                echo "✓ DHCP lease acquired via dhclient" >> "$LOG"
            else
                echo "⚠ dhclient did not obtain an IP address" >> "$LOG"
            fi
        fi
    fi
else
    echo "WARNING: No network interface found for configuration" >> "$LOG"
fi

exit 0
