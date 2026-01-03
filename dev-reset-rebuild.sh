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
find . -type d -name "bin" -o -name "obj" | while read dir; do
    rm -rf "$dir" 2>/dev/null || true
done
rm -rf debian/monolith-firewall 2>/dev/null || true
rm -f debian/*.deb 2>/dev/null || true
rm -f debian/*.buildinfo 2>/dev/null || true
rm -f debian/*.changes 2>/dev/null || true
print_success "Build artifacts cleaned"

print_step "Step 9: Building solution"
cd "$PROJECT_ROOT"
dotnet clean 2>/dev/null || true
dotnet restore || print_error "Failed to restore packages"
dotnet build -c Release || print_error "Failed to build solution"
print_success "Solution built successfully"

print_step "Step 10: Building packages"
cd "$PROJECT_ROOT/packages/monolith-network"
dotnet publish -c Release -r linux-x64 --self-contained false || print_error "Failed to build monolith-network package"
print_success "Packages built successfully"

print_step "Step 11: Building Core service"
cd "$PROJECT_ROOT/src/Monolith.FireWall.Core"
dotnet publish -c Release -r linux-x64 --self-contained false -o "$PROJECT_ROOT/debian/monolith-firewall/opt/monolith-firewall/core" || print_error "Failed to build Core service"
print_success "Core service built"

print_step "Step 12: Building WebUI service"
cd "$PROJECT_ROOT/src/Monolith.FireWall.WebUI"
dotnet publish -c Release -r linux-x64 --self-contained false -o "$PROJECT_ROOT/debian/monolith-firewall/opt/monolith-firewall/webui" || print_error "Failed to build WebUI service"
print_success "WebUI service built"

print_step "Step 13: Preparing package structure"
mkdir -p "$PROJECT_ROOT/debian/monolith-firewall/opt/monolith-firewall/packages/monolith-network/backend"
mkdir -p "$PROJECT_ROOT/debian/monolith-firewall/opt/monolith-firewall/packages/monolith-network/wwwroot"

# Copy package files
cd "$PROJECT_ROOT/packages/monolith-network/bin/Release/net10.0/linux-x64/publish"
cp *.dll "$PROJECT_ROOT/debian/monolith-firewall/opt/monolith-firewall/packages/monolith-network/backend/" 2>/dev/null || true
cp *.so "$PROJECT_ROOT/debian/monolith-firewall/opt/monolith-firewall/packages/monolith-network/backend/" 2>/dev/null || true
cp manifest.json "$PROJECT_ROOT/debian/monolith-firewall/opt/monolith-firewall/packages/monolith-network/" 2>/dev/null || true
cp *.deps.json "$PROJECT_ROOT/debian/monolith-firewall/opt/monolith-firewall/packages/monolith-network/" 2>/dev/null || true
cp -r wwwroot/* "$PROJECT_ROOT/debian/monolith-firewall/opt/monolith-firewall/packages/monolith-network/wwwroot/" 2>/dev/null || true
cp -r Pages "$PROJECT_ROOT/debian/monolith-firewall/opt/monolith-firewall/packages/monolith-network/" 2>/dev/null || true
print_success "Package structure prepared"

print_step "Step 14: Creating systemd service files"
mkdir -p "$PROJECT_ROOT/debian/monolith-firewall/usr/lib/systemd/system"

cat > "$PROJECT_ROOT/debian/monolith-firewall/usr/lib/systemd/system/monolith-firewall-core.service" << 'EOF'
[Unit]
Description=Monolith FireWall Core Service
After=network.target

[Service]
Type=simple
User=monolith-firewall
Group=monolith-firewall
WorkingDirectory=/opt/monolith-firewall/core
ExecStart=/opt/monolith-firewall/core/Monolith.FireWall.Core
Restart=always
RestartSec=10
Environment="DOTNET_ENVIRONMENT=Production"

[Install]
WantedBy=multi-user.target
EOF

cat > "$PROJECT_ROOT/debian/monolith-firewall/usr/lib/systemd/system/monolith-firewall-webui.service" << 'EOF'
[Unit]
Description=Monolith FireWall WebUI Service
After=network.target monolith-firewall-core.service
Requires=monolith-firewall-core.service

[Service]
Type=simple
User=monolith-firewall
Group=monolith-firewall
WorkingDirectory=/opt/monolith-firewall/webui
ExecStart=/opt/monolith-firewall/webui/Monolith.FireWall.WebUI
Restart=always
RestartSec=10
Environment="DOTNET_ENVIRONMENT=Production"

[Install]
WantedBy=multi-user.target
EOF

print_success "Systemd service files created"

print_step "Step 15: Creating Debian control file"
mkdir -p "$PROJECT_ROOT/debian/monolith-firewall/DEBIAN"

cat > "$PROJECT_ROOT/debian/monolith-firewall/DEBIAN/control" << EOF
Package: monolith-firewall
Version: 1.0.0-$(date +%Y%m%d%H%M%S)
Section: admin
Priority: optional
Architecture: amd64
Maintainer: MonolithFireWall Team
Description: MonolithFireWall - Modular Firewall Management System
 A modern, modular firewall management system built on .NET 10.0
 with a web-based user interface.
 .
 Note: This package requires .NET 10.0 Runtime to be installed.
 Install it from: https://dotnet.microsoft.com/download/dotnet/10.0
EOF

print_success "Debian control file created"

print_step "Step 16: Creating post-installation script"
cat > "$PROJECT_ROOT/debian/monolith-firewall/DEBIAN/postinst" << 'EOF'
#!/bin/bash
set -e

# Check for .NET 10.0 Runtime
if ! command -v dotnet &> /dev/null; then
    echo "ERROR: .NET Runtime is not installed."
    echo "Please install .NET 10.0 Runtime from: https://dotnet.microsoft.com/download/dotnet/10.0"
    exit 1
fi

# Check .NET version
DOTNET_VERSION=$(dotnet --version 2>/dev/null || echo "0.0.0")
MAJOR_VERSION=$(echo "$DOTNET_VERSION" | cut -d. -f1)

if [ "$MAJOR_VERSION" -lt 10 ]; then
    echo "ERROR: .NET 10.0 or higher is required, but version $DOTNET_VERSION is installed."
    echo "Please install .NET 10.0 Runtime from: https://dotnet.microsoft.com/download/dotnet/10.0"
    exit 1
fi

# Create user and group
if ! id "monolith-firewall" &>/dev/null; then
    useradd -r -s /bin/false -d /opt/monolith-firewall monolith-firewall
fi

# Create required directories
mkdir -p /var/lib/monolith-firewall
mkdir -p /var/log/monolith-firewall
mkdir -p /etc/monolith-firewall

# Set permissions
chown -R monolith-firewall:monolith-firewall /opt/monolith-firewall
chown -R monolith-firewall:monolith-firewall /var/lib/monolith-firewall
chown -R monolith-firewall:monolith-firewall /var/log/monolith-firewall
chown root:monolith-firewall /etc/monolith-firewall
chmod 755 /var/lib/monolith-firewall
chmod 755 /var/log/monolith-firewall
chmod 755 /etc/monolith-firewall
chmod +x /opt/monolith-firewall/core/Monolith.FireWall.Core
chmod +x /opt/monolith-firewall/webui/Monolith.FireWall.WebUI

# Reload systemd and enable services
systemctl daemon-reload
systemctl enable monolith-firewall-core.service
systemctl enable monolith-firewall-webui.service

echo "MonolithFireWall installed successfully!"
echo "Start services with:"
echo "  sudo systemctl start monolith-firewall-core"
echo "  sudo systemctl start monolith-firewall-webui"
echo ""
echo "Access the WebUI at: http://localhost:80 or https://localhost:443"
echo "Default credentials: admin / admin"

exit 0
EOF

chmod +x "$PROJECT_ROOT/debian/monolith-firewall/DEBIAN/postinst"
print_success "Post-installation script created"

print_step "Step 17: Building Debian package"
cd "$PROJECT_ROOT"
dpkg-deb --build debian/monolith-firewall || print_error "Failed to build Debian package"
DEB_FILE=$(ls debian/*.deb | head -1)
print_success "Debian package built: $DEB_FILE"

print_step "Step 18: Installing Debian package"
dpkg -i "$DEB_FILE" || print_error "Failed to install Debian package"
print_success "Debian package installed"

print_step "Step 19: Starting services"
systemctl start monolith-firewall-core
sleep 3
systemctl start monolith-firewall-webui
sleep 2
print_success "Services started"

print_step "Step 20: Verifying installation"
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
