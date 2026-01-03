# Monolith.FireWall.Platform - Phase 0 Inventory

This inventory captures current and planned system/network touchpoints that will move into Monolith.FireWall.Platform.

## Current usage in repo

- packages/monolith-network/Modules/Dhcp/DhcpManager.cs
  - Reads /sys/class/net and /sys/class/net/*/operstate.
  - Runs: ip -4 addr show <iface> via ProcessStartInfo.
  - No logging or permission enforcement.
- src/Libs/CodeLogic3.Libs/CL.SystemStats/Services/Providers/LinuxSystemStatsProvider.cs
  - Reads /proc/* for stats.
  - Runs shell: grep MemTotal /proc/meminfo | awk ...
- src/Monolith.FireWall.WebUI/Program.cs
  - /api/interfaces/* endpoints return placeholder data (TODOs).
- src/Monolith.FireWall.WebUI/wwwroot/js/pages/settings.js
  - Hostname is hard-coded placeholder in UI state.
- src/Monolith.FireWall.WebUI/Features/Dashboard/DashboardController.cs
  - Placeholder DHCP lease widgets.
- src/Monolith.FireWall.Common/Services/LoggingManager.cs
  - Central logging to CL.SQLite (Monolith/System/Security log types).

## Current module -> core flow

- Modules expose actions via IMonolithModule.GetRoutes().
- Core routes are invoked over the Unix socket and run inside Core.
- Module code often instantiates managers with null context (no service access).
- IModuleContext currently provides only CL.SQLite (via GetService<T>() in ModuleContextAdapter).

## Planned platform actions (first wave)

Read-only:
- Hostname read
- Interface list (name, state, MAC, MTU)
- Addresses per interface
- Routes table
- DNS resolver config (resolv.conf or resolvectl)
- System uptime and basic stats (CPU/mem)

Mutating (requires explicit permission + audit):
- Set hostname
- Bring interface up/down
- Add/remove IP address
- Add/remove route
- Set DNS resolvers
- Apply sysctl settings (limited allowlist)

## Command and data sources to standardize

- /sys/class/net, /proc, /etc/hostname, /etc/hosts
- ip (ip addr, ip link, ip route)
- hostnamectl
- resolvectl or /etc/resolv.conf
- sysctl
- ethtool (optional later)
- nft or iptables (later firewall integration)

## Gaps to address

- No centralized validation for interface names, IP/CIDR, or routes.
- No unified error model for command execution.
- No audit trail for command execution in System/Security logs.
- WebUI and packages use placeholder data for settings/interfaces.
