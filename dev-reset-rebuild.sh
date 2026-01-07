#!/bin/bash
# MonolithFireWall - Development Reset & Rebuild Script
# This script completely resets the development environment and rebuilds everything from scratch

set -e  # Exit on error

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$SCRIPT_DIR"

echo "════════════════════════════════════════════════════════════════"
echo "  MonolithFireWall - Development Reset & Rebuild"
echo "════════════════════════════════════════════════════════════════"
echo ""

# Function to print colored messages
print_step() {
    echo ""
    echo "▶ $1"
    echo "────────────────────────────────────────────────────────────────"
}

print_success() {
    echo "✓ $1"
}

print_warning() {
    echo "⚠ $1"
}

print_error() {
    echo "✗ ERROR: $1"
    exit 1
}

# Check if running as root
if [ "$EUID" -ne 0 ]; then 
    print_error "This script must be run as root (use sudo)"
fi

print_step "Step 1: Stopping all MonolithFireWall services"
systemctl stop monolith-firewall-webui 2>/dev/null || print_warning "WebUI service not running"
systemctl stop monolith-firewall-core 2>/dev/null || print_warning "Core service not running"
print_success "Services stopped"

print_step "Step 2: Removing existing Debian package"
if dpkg -l | grep -q monolith-firewall; then
    apt-get remove --purge -y monolith-firewall 2>/dev/null || true
    print_success "Debian package removed"
else
    print_warning "No existing Debian package found"
fi

print_step "Step 3: Cleaning installation directories"
rm -rf /opt/monolith-firewall 2>/dev/null || true
rm -rf /etc/monolith-firewall 2>/dev/null || true
rm -rf /var/log/monolith-firewall 2>/dev/null || true
rm -rf /var/lib/monolith-firewall 2>/dev/null || true
print_success "Installation directories cleaned"

print_step "Step 4: Removing systemd service files"
rm -f /usr/lib/systemd/system/monolith-firewall-core.service 2>/dev/null || true
rm -f /usr/lib/systemd/system/monolith-firewall-webui.service 2>/dev/null || true
systemctl daemon-reload
print_success "Systemd service files removed"

print_step "Step 5: Cleaning databases"
rm -f /tmp/monolith*.db 2>/dev/null || true
rm -f /tmp/monolith*.db-* 2>/dev/null || true
rm -f "$PROJECT_ROOT"/*.db 2>/dev/null || true
rm -f "$PROJECT_ROOT"/*.db-* 2>/dev/null || true
print_success "Databases cleaned"

print_step "Step 6: Removing named pipes"
rm -f /tmp/CoreFxPipe_monolith-core* 2>/dev/null || true
print_success "Named pipes removed"

print_step "Step 7: Removing monolith-firewall user and group"
if id "monolith-firewall" &>/dev/null; then
    userdel monolith-firewall 2>/dev/null || true
    groupdel monolith-firewall 2>/dev/null || true
    print_success "User and group removed"
else
    print_warning "User/group not found"
fi

print_step "Step 8: Cleaning build artifacts"
cd "$PROJECT_ROOT"
find . -type d \( -name "bin" -o -name "obj" \) | while read dir; do
    rm -rf "$dir" 2>/dev/null || true
done
rm -rf debian/monolith-firewall 2>/dev/null || true
rm -f debian/*.deb 2>/dev/null || true
rm -f debian/*.buildinfo 2>/dev/null || true
rm -f debian/*.changes 2>/dev/null || true
rm -rf build-output 2>/dev/null || true
rm -f *.mfwpkg 2>/dev/null || true
rm -f ../monolith-firewall_*.deb 2>/dev/null || true
rm -f ../monolith-firewall_*.buildinfo 2>/dev/null || true
rm -f ../monolith-firewall_*.changes 2>/dev/null || true
print_success "Build artifacts cleaned"

print_step "Step 9: Building solution"
cd "$PROJECT_ROOT"
dotnet clean 2>/dev/null || true
dotnet restore || print_error "Failed to restore packages"
dotnet build -c Release || print_error "Failed to build solution"
print_success "Solution built successfully"

print_step "Step 10: Building all .mfwpkg packages"
cd "$PROJECT_ROOT"
if [ -f "build-scripts/build-all-packages.sh" ]; then
    chmod +x build-scripts/build-all-packages.sh
    ./build-scripts/build-all-packages.sh || print_error "Failed to build packages"
    print_success "All packages built successfully"
else
    print_warning "build-all-packages.sh not found, skipping package build"
fi

print_step "Step 11: Building Debian package"
cd "$PROJECT_ROOT"
if [ -f "build-scripts/build-deb.sh" ]; then
    chmod +x build-scripts/build-deb.sh
    ./build-scripts/build-deb.sh || print_error "Failed to build Debian package"
    DEB_FILE=$(ls build-output/monolith-firewall_*.deb 2>/dev/null | head -1)
    if [ -z "$DEB_FILE" ]; then
        DEB_FILE=$(ls ../monolith-firewall_*.deb 2>/dev/null | head -1)
    fi
    if [ -z "$DEB_FILE" ]; then
        print_error "Debian package not found after build"
    fi
    print_success "Debian package built: $DEB_FILE"
else
    print_error "build-deb.sh not found"
fi

print_step "Step 12: Installing Debian package"
if [ -n "$DEB_FILE" ] && [ -f "$DEB_FILE" ]; then
    dpkg -i "$DEB_FILE" || apt-get install -f -y || print_error "Failed to install Debian package"
    print_success "Debian package installed"
else
    print_error "Debian package file not found: $DEB_FILE"
fi

print_step "Step 13: Starting Core service (required for package installation)"
systemctl start monolith-firewall-core || print_error "Failed to start Core service"

# Wait for Core service to be ready (Unix socket exists)
echo "  Waiting for Core service to be ready..."
SOCKET_PATH="/var/lib/monolith-firewall/run/monolith-core.sock"
MAX_WAIT=30
WAIT_COUNT=0
while [ ! -S "$SOCKET_PATH" ] && [ $WAIT_COUNT -lt $MAX_WAIT ]; do
    sleep 1
    WAIT_COUNT=$((WAIT_COUNT + 1))
    echo -n "."
done
echo ""

if [ ! -S "$SOCKET_PATH" ]; then
    print_warning "Core service socket not ready after ${MAX_WAIT}s, continuing anyway..."
else
    print_success "Core service is ready"
fi

print_step "Step 14: Installing .mfwpkg packages"
PACKAGES_DIR="$PROJECT_ROOT/build-output/packages"
PACKAGES_STAGING_DIR="/var/lib/monolith-firewall/packages"
if [ -d "$PACKAGES_DIR" ]; then
    INSTALLED_COUNT=0
    FAILED_COUNT=0
    mkdir -p "$PACKAGES_STAGING_DIR"
    
    # Copy packages to staging directory
    for pkg_file in "$PACKAGES_DIR"/*.mfwpkg; do
        if [ -f "$pkg_file" ]; then
            pkg_name=$(basename "$pkg_file")
            echo "  Copying $pkg_name to staging..."
            cp "$pkg_file" "$PACKAGES_STAGING_DIR/" || print_warning "Failed to copy $pkg_name"
        fi
    done
    
    chown -R monolith-firewall:monolith-firewall "$PACKAGES_STAGING_DIR" 2>/dev/null || true
    
    # Install packages using monolith-pkgmgr CLI
    if command -v monolith-pkgmgr &> /dev/null; then
        for pkg_file in "$PACKAGES_STAGING_DIR"/*.mfwpkg; do
            if [ -f "$pkg_file" ]; then
                pkg_name=$(basename "$pkg_file")
                echo "  Installing $pkg_name..."
                
                # Try installing with overwrite flag
                INSTALL_OUTPUT=$(monolith-pkgmgr package install "$pkg_file" --overwrite 2>&1)
                INSTALL_EXIT=$?
                echo "$INSTALL_OUTPUT" | tee /tmp/pkgmgr-install.log
                
                if [ $INSTALL_EXIT -eq 0 ]; then
                    INSTALLED_COUNT=$((INSTALLED_COUNT + 1))
                    print_success "Installed $pkg_name"
                else
                    # Check if it's already installed (not a real error)
                    if echo "$INSTALL_OUTPUT" | grep -qi "already installed\|already exists"; then
                        INSTALLED_COUNT=$((INSTALLED_COUNT + 1))
                        print_success "$pkg_name already installed (skipped)"
                    elif echo "$INSTALL_OUTPUT" | grep -qi "Core service is not running"; then
                        FAILED_COUNT=$((FAILED_COUNT + 1))
                        print_error "Core service not running - cannot install $pkg_name"
                    else
                        FAILED_COUNT=$((FAILED_COUNT + 1))
                        print_warning "Failed to install $pkg_name (check logs)"
                    fi
                fi
            fi
        done
    else
        print_warning "monolith-pkgmgr not found, packages copied but not installed"
        print_warning "Install manually with: monolith-pkgmgr package install <package.mfwpkg>"
        INSTALLED_COUNT=$(ls -1 "$PACKAGES_STAGING_DIR"/*.mfwpkg 2>/dev/null | wc -l)
    fi
    
    if [ $INSTALLED_COUNT -gt 0 ]; then
        print_success "Installed/prepared $INSTALLED_COUNT package(s)"
        if [ $FAILED_COUNT -gt 0 ]; then
            print_warning "$FAILED_COUNT package(s) failed to install"
        fi
    else
        print_warning "No .mfwpkg packages found to install"
    fi
else
    print_warning "Packages directory not found: $PACKAGES_DIR"
fi

print_step "Step 15: Starting WebUI service"
systemctl start monolith-firewall-webui || print_warning "Failed to start WebUI service"
sleep 2
print_success "Services started"

print_step "Step 16: Verifying installation"
if systemctl is-active --quiet monolith-firewall-core; then
    print_success "Core service is running"
else
    print_error "Core service failed to start"
fi

if systemctl is-active --quiet monolith-firewall-webui; then
    print_success "WebUI service is running"
else
    print_error "WebUI service failed to start"
fi

echo ""
echo "════════════════════════════════════════════════════════════════"
echo "  ✓ Development Reset & Rebuild Complete!"
echo "════════════════════════════════════════════════════════════════"
echo ""
echo "Services Status:"
systemctl status monolith-firewall-core --no-pager -l | head -10
echo ""
systemctl status monolith-firewall-webui --no-pager -l | head -10
echo ""
echo "Access the WebUI at: http://localhost:80 or https://localhost:443"
echo "Default credentials: admin / admin"
echo ""
echo "View logs with:"
echo "  sudo journalctl -u monolith-firewall-core -f"
echo "  sudo journalctl -u monolith-firewall-webui -f"
echo ""
