#!/bin/bash
# Monolith FireWall MOTD Script
# Displays web interface URL and system status on login

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color
BOLD='\033[1m'

# Get primary IP address
get_primary_ip() {
    # Try to get IP from default route interface
    local default_iface=$(ip route | grep default | awk '{print $5}' | head -1)
    if [ -n "$default_iface" ]; then
        ip -4 addr show "$default_iface" | grep -oP '(?<=inet\s)\d+(\.\d+){3}' | head -1
    else
        # Fallback: get first non-loopback IPv4 address
        ip -4 addr show | grep -oP '(?<=inet\s)\d+(\.\d+){3}' | grep -v '127.0.0.1' | head -1
    fi
}

# Check if service is running
check_service() {
    systemctl is-active --quiet "$1" 2>/dev/null
}

# Get service status
get_service_status() {
    if check_service "$1"; then
        echo -e "${GREEN}●${NC} Running"
    else
        echo -e "${RED}●${NC} Stopped"
    fi
}

# Main MOTD
PRIMARY_IP=$(get_primary_ip)

echo ""
echo -e "${BOLD}${CYAN}═══════════════════════════════════════════════════════════════${NC}"
echo -e "${BOLD}${CYAN}  Monolith FireWall${NC}"
echo -e "${BOLD}${CYAN}═══════════════════════════════════════════════════════════════${NC}"
echo ""

if [ -n "$PRIMARY_IP" ]; then
    echo -e "${BOLD}Web Interface:${NC}"
    echo -e "  ${GREEN}https://${PRIMARY_IP}:443${NC}"
    echo -e "  ${BLUE}http://${PRIMARY_IP}:80${NC}"
    echo ""
else
    echo -e "${YELLOW}⚠ No network interface configured${NC}"
    echo ""
fi

echo -e "${BOLD}Service Status:${NC}"
echo -e "  Core:    $(get_service_status monolith-firewall-core.service)"
echo -e "  WebUI:   $(get_service_status monolith-firewall-webui.service)"
echo ""

# Show network interfaces
echo -e "${BOLD}Network Interfaces:${NC}"
ip -4 addr show | grep -E "^[0-9]+:|inet " | while read line; do
    if [[ $line =~ ^[0-9]+: ]]; then
        iface=$(echo "$line" | awk '{print $2}' | sed 's/:$//')
        status=$(ip link show "$iface" 2>/dev/null | grep -oP '(?<=state )[A-Z]+' || echo "UNKNOWN")
        if [ "$status" = "UP" ]; then
            echo -e "  ${GREEN}●${NC} $iface"
        else
            echo -e "  ${RED}●${NC} $iface (down)"
        fi
    elif [[ $line =~ inet ]]; then
        ip=$(echo "$line" | awk '{print $2}' | cut -d'/' -f1)
        echo -e "      ${CYAN}→${NC} $ip"
    fi
done
echo ""

echo -e "${BOLD}System Information:${NC}"
echo -e "  Hostname: $(hostname)"
echo -e "  Uptime:   $(uptime -p 2>/dev/null || echo 'N/A')"
echo ""

echo -e "${YELLOW}For help, visit: https://github.com/monolith-firewall${NC}"
echo ""
