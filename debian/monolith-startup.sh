#!/bin/bash
# Monolith FireWall Startup Manager
# Orchestrates all system initialization on boot

set -e

SOCKET_PATH="/var/lib/monolith-firewall/run/monolith-core.sock"
MAX_WAIT=60
WAITED=0

echo "═══════════════════════════════════════════════════════════════"
echo "  Monolith FireWall - Startup Manager"
echo "═══════════════════════════════════════════════════════════════"
echo ""

# Wait for Core service to be ready
echo "→ Waiting for Core service to be ready..."
while [ ! -S "$SOCKET_PATH" ] && [ $WAITED -lt $MAX_WAIT ]; do
    sleep 1
    WAITED=$((WAITED + 1))
done

if [ ! -S "$SOCKET_PATH" ]; then
    echo "ERROR: Core service socket not available after ${MAX_WAIT}s"
    echo "Startup initialization will be skipped"
    exit 1
fi

echo "✓ Core service is ready"
echo ""

# Call Core API to initialize system
echo "→ Initializing system configuration..."
REQUEST_JSON='{"action":"startup.initialize","payload":{}}'

if command -v socat &> /dev/null; then
    # Use socat to call Core API
    RESPONSE=$(echo "$REQUEST_JSON" | timeout 30 socat - UNIX-CONNECT:"$SOCKET_PATH" 2>/dev/null || echo "")
    
    if echo "$RESPONSE" | grep -q '"Success":true'; then
        echo "✓ System initialization completed"
        exit 0
    else
        # Try to extract error message
        ERROR=$(echo "$RESPONSE" | grep -o '"Error":"[^"]*"' | sed 's/"Error":"\([^"]*\)"/\1/' 2>/dev/null || echo "")
        if [ -z "$ERROR" ]; then
            ERROR="Unknown error - check Core service logs"
        fi
        echo "✗ System initialization failed: $ERROR"
        exit 1
    fi
else
    echo "ERROR: socat is not available"
    echo "Cannot initialize system configuration"
    echo "Install socat: apt-get install socat"
    exit 1
fi
