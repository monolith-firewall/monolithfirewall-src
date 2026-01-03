#!/bin/bash
# MonolithFireWall - Quick Status Check Script
# Run this anytime to verify system status

echo "═══════════════════════════════════════════════════════════════"
echo "  MonolithFireWall - Status Check"
echo "═══════════════════════════════════════════════════════════════"
echo ""

# Check .NET version
echo "System Requirements:"
if command -v dotnet &> /dev/null; then
    DOTNET_VERSION=$(dotnet --version)
    echo "  ✓ .NET Runtime: $DOTNET_VERSION"
else
    echo "  ✗ .NET Runtime: NOT INSTALLED"
fi
echo ""

# Check services
echo "Service Status:"
if sudo systemctl is-active --quiet monolith-firewall-core; then
    echo "  ✓ Core Service: RUNNING"
else
    echo "  ✗ Core Service: STOPPED"
fi

if sudo systemctl is-active --quiet monolith-firewall-webui; then
    echo "  ✓ WebUI Service: RUNNING"
else
    echo "  ✗ WebUI Service: STOPPED"
fi
echo ""

# Check WebUI accessibility
echo "WebUI Accessibility:"
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:8080)
if [ "$HTTP_CODE" = "200" ]; then
    echo "  ✓ WebUI accessible at http://localhost:8080 (HTTP $HTTP_CODE)"
else
    echo "  ✗ WebUI not accessible (HTTP $HTTP_CODE)"
fi
echo ""

# Check directories
echo "Directory Structure:"
if [ -d /var/lib/monolith-firewall ]; then
    PERMS=$(ls -ld /var/lib/monolith-firewall | awk '{print $1 " " $3 ":" $4}')
    echo "  ✓ /var/lib/monolith-firewall ($PERMS)"
else
    echo "  ✗ /var/lib/monolith-firewall: MISSING"
fi

if [ -d /var/log/monolith-firewall ]; then
    PERMS=$(ls -ld /var/log/monolith-firewall | awk '{print $1 " " $3 ":" $4}')
    echo "  ✓ /var/log/monolith-firewall ($PERMS)"
else
    echo "  ✗ /var/log/monolith-firewall: MISSING"
fi

if [ -d /etc/monolith-firewall ]; then
    PERMS=$(ls -ld /etc/monolith-firewall | awk '{print $1 " " $3 ":" $4}')
    echo "  ✓ /etc/monolith-firewall ($PERMS)"
else
    echo "  ✗ /etc/monolith-firewall: MISSING"
fi
echo ""

# Check package installation
echo "Package Status:"
if dpkg -l | grep -q monolith-firewall; then
    VERSION=$(dpkg -l | grep monolith-firewall | awk '{print $3}')
    echo "  ✓ monolith-firewall package installed (version $VERSION)"
else
    echo "  ✗ monolith-firewall package: NOT INSTALLED"
fi
echo ""

echo "═══════════════════════════════════════════════════════════════"
echo "  Quick Commands"
echo "═══════════════════════════════════════════════════════════════"
echo ""
echo "View Logs:"
echo "  sudo journalctl -u monolith-firewall-core -f"
echo "  sudo journalctl -u monolith-firewall-webui -f"
echo ""
echo "Restart Services:"
echo "  sudo systemctl restart monolith-firewall-core"
echo "  sudo systemctl restart monolith-firewall-webui"
echo ""
echo "Access WebUI:"
echo "  http://localhost:8080"
echo "  Default credentials: admin / admin"
echo ""
