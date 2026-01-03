# Monolith.FireWall.Platform - API Contracts (Draft)

This document describes the initial API surface, validation rules, error model, and command-execution policy for Monolith.FireWall.Platform.

## Goals

- Provide a single, audited system/network API for Core and packages.
- Enforce strict allowlists for command execution (sudo vs non-sudo).
- Standardize validation and error handling across all modules.

## Core IPC actions

All module -> Core calls use Core routes with action=platform.*.
Example actions (first wave):

System:
- platform.system.get-hostname
- platform.system.set-hostname

Network (read):
- platform.network.interfaces.list
- platform.network.interfaces.get
- platform.network.addresses.list
- platform.network.routes.list
- platform.network.dns.get-resolvers

Network (write):
- platform.network.interfaces.set-state
- platform.network.addresses.add
- platform.network.addresses.remove
- platform.network.routes.add
- platform.network.routes.remove
- platform.network.dns.set-resolvers

Filesystem:
- platform.files.read
- platform.files.write

## Request/response DTOs

All actions share the same request/response envelope.

Request
- action: string (platform.*)
- payload: object (action-specific DTO)
- context:
  - correlationId: string
  - packageId: string
  - moduleId: string
  - userId: int?
  - permissions: string[]

Response
- success: bool
- data: object? (action-specific)
- error:
  - code: string (ValidationError, PermissionDenied, CommandFailed, NotSupported, Timeout, NotFound)
  - message: string
  - details: object?
- diagnostics:
  - durationMs: int
  - commandId: string?

## Validation rules (examples)

- Interface name
  - Regex: ^[a-zA-Z0-9._-]+$
  - Must exist in /sys/class/net for set-state, address, and route actions.
- CIDR addresses
  - IPv4/IPv6 parsing with prefix length validation.
- Routes
  - Destination CIDR + gateway OR dev required.
  - Reject default route without gateway/dev.
- DNS resolvers
  - IP address list, no duplicates, max count 6.

Validation errors return code=ValidationError with details:
- details = { field: "iface", issue: "unknown interface" }

## Capability and permission model

Capabilities are assigned per package/module and map to actions.

Capability set (initial):
- System.Read
- System.Write
- Network.Read
- Network.Write
- Filesystem.Read
- Filesystem.Write

Action -> capability mapping (initial):
- system.get-hostname -> System.Read
- system.set-hostname -> System.Write
- network.interfaces.list/get -> Network.Read
- network.addresses.list -> Network.Read
- network.routes.list -> Network.Read
- network.dns.get-resolvers -> Network.Read
- network.interfaces.set-state -> Network.Write
- network.addresses.add/remove -> Network.Write
- network.routes.add/remove -> Network.Write
- network.dns.set-resolvers -> Network.Write
- files.read -> Filesystem.Read
- files.write -> Filesystem.Write

User permission check:
- Module routes already require permissions (e.g., network.dhcp.read/write).
- Platform should additionally require a matching capability for the module.
- Modules with no declared system permissions receive no platform capabilities.

Capability policy source (proposal):
- /etc/monolith-firewall/platform-policy.json
  - package/module -> capabilities allowlist

Module permission declarations (informational, install-time):
- IMonolithModule.GetSystemPermissions() returns a list of SystemPermissionDefinition entries.
- These are surfaced by Core in `get-packages` to show "full root access" vs limited access.

## Command execution policy

Commands are never run directly by modules. Core executor runs only allowlisted commands:

Read-only (non-sudo):
- ip -4 addr show <iface>
- ip -6 addr show <iface>
- ip link show <iface>
- ip route show
- cat /etc/hostname
- cat /etc/resolv.conf

Mutating (sudo required):
- hostnamectl set-hostname <name>
- ip link set <iface> up|down
- ip addr add <cidr> dev <iface>
- ip addr del <cidr> dev <iface>
- ip route add <cidr> via <gw> dev <iface>
- ip route del <cidr> via <gw> dev <iface>
- resolvectl dns <iface> <ip>...

Each action defines its command template and whether sudo is required.

## Filesystem policy

- File actions require module context (packageId + moduleId).
- Path must be absolute and cannot include traversal.
- Path must be listed in module SystemPermissions (FileRead/FileWrite).
- If platform-policy.json includes files.read/files.write entries, requested path must also match policy allowlist.

Policy file example:
```json
{
  "packages": [
    {
      "id": "monolith-network",
      "modules": [
        {
          "id": "dhcp",
          "capabilities": ["NetworkRead", "FilesystemRead", "FilesystemWrite"],
          "actions": [
            "platform.network.interfaces.list",
            "platform.network.addresses.list",
            "platform.files.read",
            "platform.files.write"
          ],
          "files": {
            "read": ["/etc/dhcp/dhcpd.conf"],
            "write": ["/etc/dhcp/dhcpd.conf"]
          }
        }
      ]
    }
  ]
}
```

## Audit logging

Every platform action logs a System or Security entry:
- LogType: System (read) or Security (write)
- Category: Platform
- Source: PlatformExecutor
- Message: action + target (redacted)
- Details: packageId, moduleId, userId, command, args, exitCode, durationMs

Sensitive args (keys, secrets, tokens) must be redacted.
