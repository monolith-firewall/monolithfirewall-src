#!/bin/bash
# MonolithFireWall - Package Update Script
# Use this script to rebuild and update a package without full rebuild

set -e

PACKAGE_NAME=${1:-monolith-network}
PROJECT_ROOT="/home/mlf/monolith-firewall"

echo "════════════════════════════════════════════════════════════════"
echo "  MonolithFireWall - Package Update Script"
echo "════════════════════════════════════════════════════════════════"
echo ""
echo "Package: $PACKAGE_NAME"
echo ""

# Check if package exists
if [ ! -d "$PROJECT_ROOT/packages/$PACKAGE_NAME" ]; then
    echo "✗ ERROR: Package not found: $PACKAGE_NAME"
    exit 1
fi

# Clean old build artifacts for this package
echo "▶ Step 1: Cleaning old build artifacts..."
cd "$PROJECT_ROOT/packages/$PACKAGE_NAME"
rm -rf bin obj
echo "✓ Build artifacts cleaned"
echo ""

# Build the package
echo "▶ Step 2: Building package..."
dotnet publish -c Release -r linux-x64 --self-contained false
if [ $? -ne 0 ]; then
    echo "✗ ERROR: Failed to build package"
    exit 1
fi
echo "✓ Package built successfully"
echo ""

# Copy updated files to deployment
echo "▶ Step 3: Updating deployment files..."
PUBLISH_DIR="$PROJECT_ROOT/packages/$PACKAGE_NAME/bin/Release/net10.0/linux-x64/publish"
DEPLOY_DIR="/opt/monolith-firewall/packages/$PACKAGE_NAME"

if [ ! -d "$PUBLISH_DIR" ]; then
    echo "✗ ERROR: Publish directory not found: $PUBLISH_DIR"
    exit 1
fi

# Ensure deploy directories exist
sudo mkdir -p "$DEPLOY_DIR/backend" "$DEPLOY_DIR/wwwroot"

cd "$PUBLISH_DIR"

# Update backend DLLs (main package DLL goes to backend/)
echo "  → Updating backend DLLs..."
# Copy all DLLs to backend, but prioritize the main package DLL
PACKAGE_DLL=$(ls Monolith.*.dll 2>/dev/null | head -1)
if [ -n "$PACKAGE_DLL" ]; then
    echo "    → Copying main package DLL: $PACKAGE_DLL"
    sudo cp "$PACKAGE_DLL" "$DEPLOY_DIR/backend/" 2>/dev/null || true
fi
sudo cp *.dll "$DEPLOY_DIR/backend/" 2>/dev/null || true
sudo cp *.so "$DEPLOY_DIR/backend/" 2>/dev/null || true

# Update manifest
echo "  → Updating manifest..."
if [ -f "$PROJECT_ROOT/packages/$PACKAGE_NAME/manifest.json" ]; then
    sudo cp "$PROJECT_ROOT/packages/$PACKAGE_NAME/manifest.json" "$DEPLOY_DIR/" 2>/dev/null || true
else
    sudo cp manifest.json "$DEPLOY_DIR/" 2>/dev/null || true
fi
sudo cp *.deps.json "$DEPLOY_DIR/" 2>/dev/null || true

# Update wwwroot
echo "  → Updating wwwroot..."
if [ -d wwwroot ]; then
    sudo cp -r wwwroot/* "$DEPLOY_DIR/wwwroot/" 2>/dev/null || true
fi

# Update Pages if they exist
echo "  → Updating Pages..."
if [ -d "$PROJECT_ROOT/packages/$PACKAGE_NAME/Pages" ]; then
    sudo mkdir -p "$DEPLOY_DIR/Pages"
    sudo cp -r "$PROJECT_ROOT/packages/$PACKAGE_NAME/Pages"/* "$DEPLOY_DIR/Pages/" 2>/dev/null || true
fi

echo "✓ Deployment files updated"
echo ""

# Restart services
echo "▶ Step 4: Restarting services..."
sudo systemctl restart monolith-firewall-core
sleep 3
sudo systemctl restart monolith-firewall-webui
sleep 3
echo "✓ Services restarted"
echo ""

# Check service status
echo "▶ Step 5: Verifying services..."
if sudo systemctl is-active --quiet monolith-firewall-core; then
    echo "✓ Core service: RUNNING"
else
    echo "✗ Core service: FAILED"
    sudo journalctl -u monolith-firewall-core -n 20 --no-pager
    exit 1
fi

if sudo systemctl is-active --quiet monolith-firewall-webui; then
    echo "✓ WebUI service: RUNNING"
else
    echo "✗ WebUI service: FAILED"
    sudo journalctl -u monolith-firewall-webui -n 20 --no-pager
    exit 1
fi
echo ""

echo "════════════════════════════════════════════════════════════════"
echo "  ✓ Package Update Complete!"
echo "════════════════════════════════════════════════════════════════"
echo ""
echo "Package '$PACKAGE_NAME' has been rebuilt and deployed."
echo "Both services are running."
echo ""
echo "Access the WebUI at: http://localhost:8080"
echo ""
echo "View logs with:"
echo "  sudo journalctl -u monolith-firewall-core -f"
echo "  sudo journalctl -u monolith-firewall-webui -f"
echo ""
