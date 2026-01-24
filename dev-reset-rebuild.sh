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

print_step "Step 1: Stopping WebUI service (Core needed for package uninstall)"
systemctl stop monolith-firewall-webui 2>/dev/null || print_warning "WebUI service not running"

# Ensure Core service is running for package uninstall
if ! systemctl is-active --quiet monolith-firewall-core; then
    echo "  Starting Core service for package uninstall..."
    systemctl start monolith-firewall-core 2>/dev/null || print_warning "Failed to start Core service"
    sleep 2
fi

print_step "Step 2: Uninstalling all installed .mfwpkg packages"
if command -v monolith-pkgmgr &> /dev/null; then
    # Wait for Core service to be ready
    SOCKET_PATH="/var/lib/monolith-firewall/run/monolith-core.sock"
    if [ -S "$SOCKET_PATH" ]; then
        # List and uninstall all installed packages
        INSTALLED_PACKAGES=$(monolith-pkgmgr package list 2>/dev/null | grep -E "^[a-z-]+" | awk '{print $1}' || true)
        if [ -n "$INSTALLED_PACKAGES" ]; then
            UNINSTALLED_COUNT=0
            for pkg_id in $INSTALLED_PACKAGES; do
                echo "  Uninstalling $pkg_id..."
                if monolith-pkgmgr package uninstall "$pkg_id" 2>/dev/null; then
                    UNINSTALLED_COUNT=$((UNINSTALLED_COUNT + 1))
                    print_success "Uninstalled $pkg_id"
                else
                    print_warning "Failed to uninstall $pkg_id (may not be installed)"
                fi
            done
            if [ $UNINSTALLED_COUNT -gt 0 ]; then
                print_success "Uninstalled $UNINSTALLED_COUNT package(s)"
            else
                print_warning "No packages were uninstalled"
            fi
        else
            print_warning "No installed packages found"
        fi
    else
        print_warning "Core service socket not available, skipping package uninstall (will clean directories manually)"
    fi
else
    print_warning "monolith-pkgmgr not found, skipping package uninstall (will clean directories manually)"
fi

print_step "Step 3: Stopping Core service"
systemctl stop monolith-firewall-core 2>/dev/null || print_warning "Core service not running"
print_success "All services stopped"

print_step "Step 3: Removing existing Debian package"
if dpkg -l | grep -q monolith-firewall; then
    apt-get remove --purge -y monolith-firewall 2>/dev/null || true
    print_success "Debian package removed"
else
    print_warning "No existing Debian package found"
fi

print_step "Step 4: Cleaning installation directories"
rm -rf /opt/monolith-firewall 2>/dev/null || true
rm -rf /etc/monolith-firewall 2>/dev/null || true
rm -rf /var/log/monolith-firewall 2>/dev/null || true

# Clean /var/lib/monolith-firewall but preserve structure for later
# We'll clean specific subdirectories instead of everything
rm -rf /var/lib/monolith-firewall/packages 2>/dev/null || true
rm -rf /var/lib/monolith-firewall/codelogic/Packages 2>/dev/null || true
rm -rf /var/lib/monolith-firewall/codelogic/plugins 2>/dev/null || true
rm -rf /var/lib/monolith-firewall/codelogic/localization 2>/dev/null || true
rm -rf /var/lib/monolith-firewall/run 2>/dev/null || true
rm -rf /var/lib/monolith-firewall/data 2>/dev/null || true
rm -rf /var/lib/monolith-firewall/packages-cache 2>/dev/null || true
rm -f /var/lib/monolith-firewall/.setup-complete 2>/dev/null || true
rm -f /var/lib/monolith-firewall/.setup-progress.json 2>/dev/null || true
rm -f /var/lib/monolith-firewall/codelogic/.codelogic 2>/dev/null || true
print_success "Installation directories cleaned"

print_step "Step 5: Removing systemd service files"
rm -f /usr/lib/systemd/system/monolith-firewall-core.service 2>/dev/null || true
rm -f /usr/lib/systemd/system/monolith-firewall-webui.service 2>/dev/null || true
systemctl daemon-reload 2>/dev/null || true
print_success "Systemd service files removed"

print_step "Step 6: Cleaning databases"
# Clean temporary databases
rm -f /tmp/monolith*.db 2>/dev/null || true
rm -f /tmp/monolith*.db-* 2>/dev/null || true
rm -f "$PROJECT_ROOT"/*.db 2>/dev/null || true
rm -f "$PROJECT_ROOT"/*.db-* 2>/dev/null || true

# Clean installed databases
rm -f /var/lib/monolith-firewall/data/*.db 2>/dev/null || true
rm -f /var/lib/monolith-firewall/data/*.db-* 2>/dev/null || true
rm -f /var/lib/monolith-firewall/data/*.db-shm 2>/dev/null || true
rm -f /var/lib/monolith-firewall/data/*.db-wal 2>/dev/null || true
rm -f /var/lib/monolith-firewall/codelogic/CL.cl.sqlite/data/database.db 2>/dev/null || true
rm -f /var/lib/monolith-firewall/codelogic/CL.cl.sqlite/data/database.db-* 2>/dev/null || true
rm -f /var/lib/monolith-firewall/codelogic/CL.cl.sqlite/data/database.db-shm 2>/dev/null || true
rm -f /var/lib/monolith-firewall/codelogic/CL.cl.sqlite/data/database.db-wal 2>/dev/null || true

# Clean CodeLogic data directory
rm -rf /var/lib/monolith-firewall/codelogic/CL.cl.sqlite/data/* 2>/dev/null || true
print_success "Databases cleaned"

print_step "Step 7: Removing named pipes"
rm -f /tmp/CoreFxPipe_monolith-core* 2>/dev/null || true
print_success "Named pipes removed"

print_step "Step 8: Removing monolith-firewall user and group"
if id "monolith-firewall" &>/dev/null; then
    userdel monolith-firewall 2>/dev/null || true
    groupdel monolith-firewall 2>/dev/null || true
    print_success "User and group removed"
else
    print_warning "User/group not found"
fi

print_step "Step 9: Cleaning build artifacts"
cd "$PROJECT_ROOT"
# Get the actual user who invoked sudo (SUDO_USER) or current user
ACTUAL_USER="${SUDO_USER:-$USER}"
if [ -z "$ACTUAL_USER" ] || [ "$ACTUAL_USER" = "root" ]; then
    # If no SUDO_USER, try to get the user from whoami of the original session
    ACTUAL_USER=$(who am i | awk '{print $1}' || echo "$USER")
fi
# If still root, try to find a non-root user with a home directory
if [ "$ACTUAL_USER" = "root" ] || [ -z "$ACTUAL_USER" ]; then
    # Try to find the first non-root user
    for u in $(ls /home 2>/dev/null); do
        if [ "$u" != "root" ]; then
            ACTUAL_USER="$u"
            break
        fi
    done
fi
echo "  Detected user: $ACTUAL_USER (running as: $(whoami))"

# Fix permissions on obj/bin directories first (common issue when running with sudo)
find . -type d -name "obj" -exec chmod -R u+w {} \; 2>/dev/null || true
find . -type d -name "bin" -exec chmod -R u+w {} \; 2>/dev/null || true
find . -type f -path "*/obj/*" -exec chmod u+w {} \; 2>/dev/null || true
find . -type f -path "*/bin/*" -exec chmod u+w {} \; 2>/dev/null || true
# Remove all obj and bin directories (including in tmp/CodeLogic3)
find . -type d \( -name "bin" -o -name "obj" \) | while read dir; do
    rm -rf "$dir" 2>/dev/null || true
done
# Also clean any publish directories
find . -type d -name "publish" -path "*/src/*" | while read dir; do
    rm -rf "$dir" 2>/dev/null || true
done

# Fix ownership of project directory so dpkg-buildpackage can write
# If we're running as root but there's a SUDO_USER, fix ownership to that user
if [ "$(whoami)" = "root" ] && [ -n "$SUDO_USER" ] && [ "$SUDO_USER" != "root" ]; then
    echo "  Fixing ownership of project directory for $SUDO_USER..."
    chown -R "$SUDO_USER:$SUDO_USER" "$PROJECT_ROOT" 2>/dev/null || true
elif [ -n "$ACTUAL_USER" ] && [ "$ACTUAL_USER" != "root" ]; then
    echo "  Fixing ownership of project directory for $ACTUAL_USER..."
    chown -R "$ACTUAL_USER:$ACTUAL_USER" "$PROJECT_ROOT" 2>/dev/null || true
fi
rm -rf debian/monolith-firewall 2>/dev/null || true
rm -f debian/*.deb 2>/dev/null || true
rm -f debian/*.buildinfo 2>/dev/null || true
rm -f debian/*.changes 2>/dev/null || true
# CRITICAL: Remove build-output BEFORE building to ensure fresh build
rm -rf build-output 2>/dev/null || true
rm -f *.mfwpkg 2>/dev/null || true
# Also clean any .deb files in parent directory (dpkg-buildpackage sometimes puts them there)
rm -f ../monolith-firewall_*.deb 2>/dev/null || true
rm -f ../monolith-firewall_*.buildinfo 2>/dev/null || true
rm -f ../monolith-firewall_*.changes 2>/dev/null || true
# Clean changelog backup files that might have old timestamps
rm -f debian/changelog.bak 2>/dev/null || true

# Clean package build artifacts in tmp/monolithfirewall-packages
if [ -d "$PROJECT_ROOT/tmp/monolithfirewall-packages" ]; then
    find "$PROJECT_ROOT/tmp/monolithfirewall-packages" -type d \( -name "bin" -o -name "obj" \) | while read dir; do
        rm -rf "$dir" 2>/dev/null || true
    done
    find "$PROJECT_ROOT/tmp/monolithfirewall-packages" -name "*.mfwpkg" -type f | while read file; do
        rm -f "$file" 2>/dev/null || true
    done
    # Clean bundled deb directories (will be regenerated during build)
    find "$PROJECT_ROOT/tmp/monolithfirewall-packages" -type d -name "debs" | while read dir; do
        rm -rf "$dir" 2>/dev/null || true
    done
    # Clean manifest.json backups created by update-manifest-debs.sh
    find "$PROJECT_ROOT/tmp/monolithfirewall-packages" -name "manifest.json.bak" -type f | while read file; do
        rm -f "$file" 2>/dev/null || true
    done
fi

# Note: deb-cache in tmp/deb-cache is preserved to speed up rebuilds
# To clear the cache, manually delete: rm -rf tmp/deb-cache
print_success "Build artifacts cleaned"

print_step "Step 10: Restoring packages (build will happen in Debian package step)"
cd "$PROJECT_ROOT"
# Clean and fix permissions (ensure we can write to obj directories)
dotnet clean 2>/dev/null || true
# Fix any permission issues with obj/bin directories (especially in tmp/CodeLogic3)
find . -type d -name "obj" -exec chmod -R u+w {} \; 2>/dev/null || true
find . -type d -name "bin" -exec chmod -R u+w {} \; 2>/dev/null || true
find . -type f -path "*/obj/*" -exec chmod u+w {} \; 2>/dev/null || true
find . -type f -path "*/bin/*" -exec chmod u+w {} \; 2>/dev/null || true
# Remove any remaining obj/bin directories that might have permission issues
find . -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} \; 2>/dev/null || true
# Only restore packages - actual build will happen in debian/rules to avoid double compilation
dotnet restore || print_error "Failed to restore packages"
print_success "Packages restored (solution will be built during Debian package creation)"

# Also build packages in tmp/monolithfirewall-packages if they exist
if [ -d "$PROJECT_ROOT/tmp/monolithfirewall-packages" ]; then
    print_step "Step 9a: Building monolith packages"
    for pkg_dir in "$PROJECT_ROOT/tmp/monolithfirewall-packages"/*; do
        if [ -d "$pkg_dir" ] && [ -f "$pkg_dir"/*.csproj ]; then
            pkg_name=$(basename "$pkg_dir")
            echo "  Building $pkg_name..."
            cd "$pkg_dir"
            dotnet clean 2>/dev/null || true
            dotnet restore 2>/dev/null || true
            dotnet build -c Release 2>/dev/null || print_warning "Failed to build $pkg_name"
        fi
    done
    cd "$PROJECT_ROOT"
    print_success "Package projects built"
fi

print_step "Step 11: Building all .mfwpkg packages"
cd "$PROJECT_ROOT"
if [ -f "build-scripts/build-all-packages.sh" ]; then
    chmod +x build-scripts/build-all-packages.sh
    ./build-scripts/build-all-packages.sh || print_error "Failed to build packages"
    print_success "All packages built successfully"
else
    print_warning "build-all-packages.sh not found, skipping package build"
fi

print_step "Step 12: Building Debian package"
cd "$PROJECT_ROOT"
# Clean debian build directories to ensure fresh build
echo "  Cleaning Debian build directories..."
rm -rf debian/tmp debian/.debhelper debian/monolith-firewall 2>/dev/null || true
# Ensure source is clean before building Debian package
echo "  Ensuring source build artifacts are clean..."
find src -type d \( -name "bin" -o -name "obj" \) -exec chmod -R u+w {} \; 2>/dev/null || true
find src -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} \; 2>/dev/null || true
find tmp -type d \( -name "bin" -o -name "obj" \) -exec chmod -R u+w {} \; 2>/dev/null || true
find tmp -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} \; 2>/dev/null || true
# CRITICAL: Do NOT clean build-output here - packages were just built in Step 11!
# Only ensure build-output exists and preserve the packages subdirectory
echo "  Preserving packages in build-output/packages..."
mkdir -p build-output/packages
# Clean only .deb files from build-output (not the packages directory)
rm -f build-output/*.deb 2>/dev/null || true
rm -f build-output/*.buildinfo 2>/dev/null || true
rm -f build-output/*.changes 2>/dev/null || true
# Also clean any .deb files that might exist elsewhere
rm -f ../monolith-firewall_*.deb 2>/dev/null || true
rm -f ../monolith-firewall_*.buildinfo 2>/dev/null || true
rm -f ../monolith-firewall_*.changes 2>/dev/null || true

# Fix ownership so dpkg-buildpackage can write (if running as root, fix to SUDO_USER)
if [ "$(whoami)" = "root" ] && [ -n "$SUDO_USER" ] && [ "$SUDO_USER" != "root" ]; then
    echo "  Ensuring ownership for $SUDO_USER..."
    chown -R "$SUDO_USER:$SUDO_USER" "$PROJECT_ROOT" 2>/dev/null || true
elif [ -n "$ACTUAL_USER" ] && [ "$ACTUAL_USER" != "root" ]; then
    echo "  Ensuring ownership for $ACTUAL_USER..."
    chown -R "$ACTUAL_USER:$ACTUAL_USER" "$PROJECT_ROOT" 2>/dev/null || true
fi

if [ -f "build-scripts/build-deb.sh" ]; then
    chmod +x build-scripts/build-deb.sh
    echo "  Running build-deb.sh (this will generate a fresh timestamp)..."
    ./build-scripts/build-deb.sh || print_error "Failed to build Debian package"
    # Wait a moment for files to be moved
    sleep 1
    DEB_FILE=$(ls build-output/monolith-firewall_*.deb 2>/dev/null | sort -r | head -1)
    if [ -z "$DEB_FILE" ]; then
        DEB_FILE=$(ls ../monolith-firewall_*.deb 2>/dev/null | sort -r | head -1)
    fi
    if [ -z "$DEB_FILE" ]; then
        print_error "Debian package not found after build"
    fi
    # Verify this is a fresh build by checking the timestamp in filename
    DEB_TIMESTAMP=$(basename "$DEB_FILE" | sed -n 's/monolith-firewall_1\.0\.0-\([0-9]\{8\}-[0-9]\{6\}\)_amd64\.deb/\1/p')
    CURRENT_TIMESTAMP=$(date -u +"%Y%m%d-%H%M%S")
    echo "  Package timestamp: $DEB_TIMESTAMP"
    echo "  Current timestamp: $CURRENT_TIMESTAMP"
    # Allow 2 minute difference (build might take time)
    if [ -n "$DEB_TIMESTAMP" ]; then
        print_success "Found Debian package with timestamp: $DEB_TIMESTAMP"
    fi
    # Extract version from filename
    DEB_VERSION=$(basename "$DEB_FILE" | sed -n 's/monolith-firewall_\(.*\)_amd64.deb/\1/p')
    print_success "Debian package built: $DEB_FILE"
    echo "  Package version: $DEB_VERSION"
    
    # Verify the Debian package contains the new Core binary
    echo "  Verifying Debian package contains updated Core binary..."
    TMP_EXTRACT=$(mktemp -d)
    dpkg-deb -x "$DEB_FILE" "$TMP_EXTRACT" 2>/dev/null || true
    
    # Check build info file for actual build timestamp
    if [ -f "$TMP_EXTRACT/opt/monolith-firewall/.build-info" ]; then
        BUILD_DATE=$(grep "Build Date:" "$TMP_EXTRACT/opt/monolith-firewall/.build-info" | cut -d: -f2- | xargs || echo "")
        BUILD_TS=$(grep "Build Timestamp:" "$TMP_EXTRACT/opt/monolith-firewall/.build-info" | cut -d: -f2- | xargs || echo "")
        if [ -n "$BUILD_DATE" ]; then
            echo "    Package build date: $BUILD_DATE"
        fi
    fi
    
    # Check file timestamps (should be current now)
    DEB_CORE_DATE=$(dpkg-deb -c "$DEB_FILE" 2>/dev/null | grep "Monolith.FireWall.Core.dll$" | awk '{print $4, $5}' | head -1 || echo "")
    if [ -n "$DEB_CORE_DATE" ]; then
        echo "    Core DLL in package dated: $DEB_CORE_DATE"
    fi
    
    # Check if binary contains new bundledDebs code (check DLL, not executable wrapper)
    if [ -f "$TMP_EXTRACT/opt/monolith-firewall/core/Monolith.FireWall.Core.dll" ]; then
        if strings "$TMP_EXTRACT/opt/monolith-firewall/core/Monolith.FireWall.Core.dll" 2>/dev/null | grep -qi "bundledDeb\|InstallBundledDebs"; then
            echo "    ✓ Core DLL contains bundledDebs support"
        else
            echo "    ⚠ Warning: Core DLL does not contain bundledDebs support (may be old version)"
        fi
    elif [ -f "$TMP_EXTRACT/opt/monolith-firewall/core/Monolith.FireWall.Core" ]; then
        # Fallback: check executable (though code is usually in DLL)
        if strings "$TMP_EXTRACT/opt/monolith-firewall/core/Monolith.FireWall.Core" 2>/dev/null | grep -qi "bundledDeb\|InstallBundledDebs"; then
            echo "    ✓ Core binary contains bundledDebs support"
        else
            echo "    ⚠ Warning: Core binary does not contain bundledDebs support (may be old version)"
        fi
    fi
    rm -rf "$TMP_EXTRACT" 2>/dev/null || true
else
    print_error "build-deb.sh not found"
fi

print_step "Step 13: Installing Debian package"
if [ -n "$DEB_FILE" ] && [ -f "$DEB_FILE" ]; then
    # Stop services before installing
    systemctl stop monolith-firewall-webui 2>/dev/null || true
    systemctl stop monolith-firewall-core 2>/dev/null || true
    sleep 1
    
    # Get timestamp of Core binary before install
    OLD_CORE_TIME=""
    if [ -f "/opt/monolith-firewall/core/Monolith.FireWall.Core" ]; then
        OLD_CORE_TIME=$(stat -c "%Y" /opt/monolith-firewall/core/Monolith.FireWall.Core 2>/dev/null || echo "0")
    fi
    
    dpkg -i "$DEB_FILE" || apt-get install -f -y || print_error "Failed to install Debian package"
    print_success "Debian package installed"
    
    # Verify the new Core binary is installed and updated
    if [ -f "/opt/monolith-firewall/core/Monolith.FireWall.Core" ]; then
        NEW_CORE_TIME=$(stat -c "%Y" /opt/monolith-firewall/core/Monolith.FireWall.Core 2>/dev/null || echo "0")
        CORE_DATE=$(stat -c "%y" /opt/monolith-firewall/core/Monolith.FireWall.Core 2>/dev/null | cut -d' ' -f1 || echo "")
        CORE_DATETIME=$(stat -c "%y" /opt/monolith-firewall/core/Monolith.FireWall.Core 2>/dev/null || echo "")
        echo "  Core binary installed: $CORE_DATETIME"
        
        # Check build info if available
        if [ -f "/opt/monolith-firewall/.build-info" ]; then
            BUILD_DATE=$(grep "Build Date:" /opt/monolith-firewall/.build-info | cut -d: -f2- | xargs || echo "")
            if [ -n "$BUILD_DATE" ]; then
                echo "  Package build date: $BUILD_DATE"
            fi
        fi
        
        if [ "$NEW_CORE_TIME" != "$OLD_CORE_TIME" ] && [ "$OLD_CORE_TIME" != "0" ]; then
            print_success "Core binary was updated"
            # Verify it has the new code (check DLL, not executable wrapper)
            if [ -f "/opt/monolith-firewall/core/Monolith.FireWall.Core.dll" ]; then
                if strings /opt/monolith-firewall/core/Monolith.FireWall.Core.dll 2>/dev/null | grep -qi "bundledDeb\|InstallBundledDebs"; then
                    echo "  ✓ Core DLL contains bundledDebs support"
                else
                    print_warning "Core DLL does not contain bundledDebs support - rebuild may have failed"
                fi
            elif strings /opt/monolith-firewall/core/Monolith.FireWall.Core 2>/dev/null | grep -qi "bundledDeb\|InstallBundledDebs"; then
                echo "  ✓ Core binary contains bundledDebs support"
            else
                print_warning "Core binary does not contain bundledDebs support - rebuild may have failed"
            fi
        elif [ "$OLD_CORE_TIME" = "0" ]; then
            echo "  (First installation)"
            # Verify it has the new code (check DLL, not executable wrapper)
            if [ -f "/opt/monolith-firewall/core/Monolith.FireWall.Core.dll" ]; then
                if strings /opt/monolith-firewall/core/Monolith.FireWall.Core.dll 2>/dev/null | grep -qi "bundledDeb\|InstallBundledDebs"; then
                    echo "  ✓ Core DLL contains bundledDebs support"
                else
                    print_warning "Core DLL does not contain bundledDebs support - rebuild may have failed"
                fi
            elif strings /opt/monolith-firewall/core/Monolith.FireWall.Core 2>/dev/null | grep -qi "bundledDeb\|InstallBundledDebs"; then
                echo "  ✓ Core binary contains bundledDebs support"
            else
                print_warning "Core binary does not contain bundledDebs support - rebuild may have failed"
            fi
        else
            print_warning "Core binary timestamp unchanged - may be using old version"
        fi
    fi
else
    print_error "Debian package file not found: $DEB_FILE"
fi

print_step "Step 13a: Writing core configuration (packages directory)"
mkdir -p /var/lib/monolith-firewall/codelogic
mkdir -p /var/lib/monolith-firewall/data
mkdir -p /var/lib/monolith-firewall/packages
mkdir -p /var/lib/monolith-firewall/run
cat > /tmp/core-config.json <<'EOF'
{
  "Version": "1.0.0",
  "PackagesDirectory": "/var/lib/monolith-firewall/packages",
  "PipeName": "monolith-core",
  "SocketPath": "/var/lib/monolith-firewall/run/monolith-core.sock",
  "PlatformPolicyPath": "/etc/monolith-firewall/platform-policy.json",
  "MaxConcurrentConnections": 10,
  "EnableDebugMode": false,
  "LogDirectory": "/var/log/monolith-firewall",
  "Database": {
    "Path": "/var/lib/monolith-firewall/data/core.db",
    "ConnectionTimeoutSeconds": 30,
    "MaxPoolSize": 10,
    "UseWAL": true
  }
}
EOF
cp /tmp/core-config.json /var/lib/monolith-firewall/codelogic/core-config.json
chown root:root /var/lib/monolith-firewall/codelogic/core-config.json
chmod 644 /var/lib/monolith-firewall/codelogic/core-config.json
chown -R monolith-firewall:monolith-firewall /var/lib/monolith-firewall/packages 2>/dev/null || true
chown -R monolith-firewall:monolith-firewall /var/lib/monolith-firewall/data 2>/dev/null || true
chown -R monolith-firewall:monolith-firewall /var/lib/monolith-firewall/run 2>/dev/null || true
rm -f /tmp/core-config.json
print_success "core-config.json written with packages path /var/lib/monolith-firewall/packages"

print_step "Step 14: Starting Core service (required for package installation)"
# Ensure Core service is stopped first
systemctl stop monolith-firewall-core 2>/dev/null || true
sleep 2

# Start Core service
systemctl start monolith-firewall-core || print_error "Failed to start Core service"

# Give the service time to initialize (Core needs to load packages, initialize database, etc.)
echo "  Waiting for Core service to initialize..."
sleep 5

# Wait for Core service to be ready (Unix socket exists and service is active)
SOCKET_PATH="/var/lib/monolith-firewall/run/monolith-core.sock"
MAX_WAIT=90
WAIT_COUNT=0
while [ ! -S "$SOCKET_PATH" ] && [ $WAIT_COUNT -lt $MAX_WAIT ]; do
    sleep 1
    WAIT_COUNT=$((WAIT_COUNT + 1))
    if [ $((WAIT_COUNT % 5)) -eq 0 ]; then
        echo -n "."
    fi
    # Check if service failed
    if ! systemctl is-active --quiet monolith-firewall-core; then
        echo ""
        echo "  Service status:"
        systemctl status monolith-firewall-core --no-pager -l | head -10
        print_error "Core service failed to start. Check logs: journalctl -u monolith-firewall-core -n 50"
        exit 1
    fi
done
echo ""

if [ ! -S "$SOCKET_PATH" ]; then
    print_warning "Core service socket not ready after ${MAX_WAIT}s"
    echo "  Checking service status..."
    systemctl status monolith-firewall-core --no-pager -l | head -15
    # Check if service is actually running but socket just isn't ready
    if systemctl is-active --quiet monolith-firewall-core; then
        echo "  Service is active, waiting a bit more..."
        sleep 10
        if [ -S "$SOCKET_PATH" ]; then
            print_success "Core service socket is now ready"
        else
            print_warning "Socket still not ready, but service is running. Continuing..."
        fi
    else
        print_error "Service is not active. Check logs: journalctl -u monolith-firewall-core -n 50"
        exit 1
    fi
else
    print_success "Core service is ready"
    # Test that Core can respond (with longer timeout for package loading)
    if command -v monolith-pkgmgr &> /dev/null; then
        echo "  Testing Core service communication..."
        if timeout 10 monolith-pkgmgr package list &>/dev/null; then
            print_success "Core service is responding"
        else
            print_warning "Core service may not be fully ready yet (but socket exists)"
        fi
    fi
fi

print_step "Step 15: Installing .mfwpkg packages"
PACKAGES_DIR="$PROJECT_ROOT/build-output/packages"
PACKAGES_STAGING_DIR="/var/lib/monolith-firewall/packages"
if [ -d "$PACKAGES_DIR" ]; then
    # Wait for any existing dpkg/apt processes to complete
    echo "  Checking for dpkg locks..."
    MAX_LOCK_WAIT=60
    LOCK_WAIT_COUNT=0
    while [ $LOCK_WAIT_COUNT -lt $MAX_LOCK_WAIT ]; do
        if pgrep -f "(dpkg|apt-get|apt)" >/dev/null 2>&1 || lsof /var/lib/dpkg/lock-frontend /var/lib/dpkg/lock 2>/dev/null | grep -q .; then
            if [ $((LOCK_WAIT_COUNT % 5)) -eq 0 ]; then
                echo "    Waiting for dpkg/apt processes to complete..."
            fi
            sleep 1
            LOCK_WAIT_COUNT=$((LOCK_WAIT_COUNT + 1))
        else
            break
        fi
    done
    if [ $LOCK_WAIT_COUNT -ge $MAX_LOCK_WAIT ]; then
        print_warning "dpkg lock wait timeout, proceeding anyway..."
    else
        echo "  ✓ No dpkg locks detected"
    fi
    
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
                echo "    (This may take a while if package includes bundled deb packages...)"
                
                # Try installing with overwrite flag, with timeout (10 minutes for deb installation)
                # Use timeout command if available, otherwise just run normally
                # Also show progress by running in background and checking periodically
                if command -v timeout &> /dev/null; then
                    # Run with timeout and show progress
                    (
                        timeout 600 monolith-pkgmgr package install "$pkg_file" --overwrite > "/tmp/pkgmgr-install-${pkg_name}.log" 2>&1
                        echo $? > "/tmp/pkgmgr-install-${pkg_name}.exit"
                    ) &
                    INSTALL_PID=$!
                    
                    # Show progress dots while waiting
                    PROGRESS_COUNT=0
                    while kill -0 $INSTALL_PID 2>/dev/null; do
                        sleep 2
                        PROGRESS_COUNT=$((PROGRESS_COUNT + 1))
                        if [ $((PROGRESS_COUNT % 10)) -eq 0 ]; then
                            echo -n "."
                        fi
                        # Check for timeout (10 minutes = 300 seconds, check every 2 seconds = 150 iterations)
                        if [ $PROGRESS_COUNT -gt 300 ]; then
                            kill $INSTALL_PID 2>/dev/null || true
                            break
                        fi
                    done
                    echo ""
                    wait $INSTALL_PID 2>/dev/null || true
                    
                    INSTALL_EXIT=$(cat "/tmp/pkgmgr-install-${pkg_name}.exit" 2>/dev/null || echo "1")
                    INSTALL_OUTPUT=$(cat "/tmp/pkgmgr-install-${pkg_name}.log" 2>/dev/null || echo "Installation failed")
                    rm -f "/tmp/pkgmgr-install-${pkg_name}.exit"
                    
                    if [ $INSTALL_EXIT -eq 124 ] || [ $PROGRESS_COUNT -gt 300 ]; then
                        print_warning "$pkg_name installation timed out after 10 minutes"
                        FAILED_COUNT=$((FAILED_COUNT + 1))
                        continue
                    fi
                else
                    INSTALL_OUTPUT=$(monolith-pkgmgr package install "$pkg_file" --overwrite 2>&1)
                    INSTALL_EXIT=$?
                fi
                
                echo "$INSTALL_OUTPUT" | tee /tmp/pkgmgr-install-${pkg_name}.log
                
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
                        print_warning "Failed to install $pkg_name (exit code: $INSTALL_EXIT)"
                        echo "    Check log: /tmp/pkgmgr-install-${pkg_name}.log"
                        echo "    Last few lines:"
                        echo "$INSTALL_OUTPUT" | tail -5 | sed 's/^/      /'
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

print_step "Step 16: Starting WebUI service"
systemctl start monolith-firewall-webui || print_warning "Failed to start WebUI service"
sleep 2
print_success "Services started"

print_step "Step 17: Verifying installation"
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
