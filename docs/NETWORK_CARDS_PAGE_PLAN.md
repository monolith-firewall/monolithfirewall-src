# Network Cards Advanced Settings Page - Implementation Plan

## Overview
Add a new "Network Cards" tab to the Advanced Settings page that displays all network interface cards (NICs) detected via PCI, shows their advanced configuration options using `ethtool`, and allows users to configure various NIC settings including offloads, buffers, speeds, and more.

## Goals
1. Display all network cards detected via PCI (`lspci`)
2. Show detailed NIC information and capabilities using `ethtool`
3. Allow configuration of:
   - Ethernet speed and duplex mode
   - Offload features (TSO/GSO/GRO/LRO, checksums, VLAN offload)
   - TX/RX ring buffers
   - Other advanced NIC settings
4. Provide "Apply" button to save and apply changes immediately
5. Provide "Revert to Default" button to restore factory defaults
6. Auto-detect all available options per NIC

## Debian Package Dependencies

### Required Packages
Add to `debian/control` under `Depends:`:
- **ethtool** - Query and control network driver and hardware settings
- **pciutils** - Utilities for inspecting PCI devices (`lspci`)
- **iproute2** - Already included, used for interface management
- **udev** - Already included via systemd, used for device detection

### Package Installation
```bash
# These packages should be added to debian/control
ethtool,
pciutils,
```

Note: `iproute2` and `udev` are already dependencies (iproute2 is listed, udev comes with systemd).

## Backend Implementation

### 1. Data Models

#### New Models in `Monolith.FireWall.Platform/Models/NetworkModels.cs`

```csharp
// PCI Device Information
public sealed class PciDeviceInfo
{
    public string Slot { get; set; } = string.Empty;          // PCI slot (e.g., "0000:01:00.0")
    public string Class { get; set; } = string.Empty;         // Device class
    public string Vendor { get; set; } = string.Empty;        // Vendor name
    public string Device { get; set; } = string.Empty;        // Device name
    public string? SubsystemVendor { get; set; }              // Subsystem vendor
    public string? SubsystemDevice { get; set; }              // Subsystem device
    public string? Interface { get; set; }                    // Associated network interface (e.g., "eth0")
}

// Network Card Information (combines PCI + ethtool data)
public sealed class NetworkCardInfo
{
    public string Interface { get; set; } = string.Empty;
    public PciDeviceInfo? PciInfo { get; set; }
    public string Driver { get; set; } = string.Empty;
    public string BusInfo { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string FirmwareVersion { get; set; } = string.Empty;
    public string ExpansionRomVersion { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string? LinkDetected { get; set; }                 // "yes" or "no"
    public string? Speed { get; set; }                         // e.g., "1000Mb/s"
    public string? Duplex { get; set; }                        // "Full" or "Half"
    public string? Port { get; set; }                         // "TP", "FIBRE", "AUI", etc.
    public string? PhyAddress { get; set; }
    public string? Transceiver { get; set; }
    public string? AutoNegotiation { get; set; }              // "on" or "off"
    public List<string> SupportedLinkModes { get; set; } = new();
    public List<string> AdvertisedLinkModes { get; set; } = new();
    public string? SupportedPorts { get; set; }
    public string? SupportedFecModes { get; set; }
    public string? AdvertisedFecModes { get; set; }
    public NetworkCardOffloads Offloads { get; set; } = new();
    public NetworkCardBuffers Buffers { get; set; } = new();
    public NetworkCardFeatures Features { get; set; } = new();
    public Dictionary<string, string> OtherSettings { get; set; } = new();
}

// Offload Features
public sealed class NetworkCardOffloads
{
    // TCP Segmentation Offload
    public bool? Tso { get; set; }                            // tcp-segmentation-offload
    public bool? Ufo { get; set; }                               // udp-fragmentation-offload
    public bool? Gso { get; set; }                               // generic-segmentation-offload
    public bool? Gro { get; set; }                               // generic-receive-offload
    public bool? Lro { get; set; }                               // large-receive-offload
    public bool? Rxvlan { get; set; }                            // rx-vlan-offload
    public bool? Txvlan { get; set; }                            // tx-vlan-offload
    public bool? Rxhash { get; set; }                            // rx-hashing
    public bool? RxAll { get; set; }                             // rx-all
    public bool? TxvlanStagHwInsert { get; set; }                // tx-vlan-stag-hw-insert
    public bool? RxvlanStagFilter { get; set; }                // rx-vlan-stag-filter
    public bool? RxvlanStagHwParse { get; set; }                // rx-vlan-stag-hw-parse
    
    // Checksum Offloads
    public bool? TxChecksumming { get; set; }                    // tx-checksumming
    public bool? RxChecksumming { get; set; }                   // rx-checksumming
    public bool? TxChecksumIpv4 { get; set; }                    // tx-checksum-ipv4
    public bool? TxChecksumIpv6 { get; set; }                    // tx-checksum-ipv6
    public bool? TxChecksumIpGeneric { get; set; }              // tx-checksum-ip-generic
    public bool? TxChecksumSctp { get; set; }                    // tx-checksum-sctp
    public bool? RxChecksumIpv4 { get; set; }                    // rx-checksum-ipv4
    public bool? RxChecksumIpv6 { get; set; }                    // rx-checksum-ipv6
    public bool? RxChecksumIpGeneric { get; set; }              // rx-checksum-ip-generic
    public bool? RxChecksumSctp { get; set; }                    // rx-checksum-sctp
    
    // Other Offloads
    public bool? ScatterGather { get; set; }                     // scatter-gather
    public bool? TxScatterGather { get; set; }                  // tx-scatter-gather
    public bool? TxScatterGatherFragList { get; set; }          // tx-scatter-gather-fraglist
    public bool? TxScatterGatherIpv4 { get; set; }               // tx-scatter-gather-ipv4
    public bool? TxScatterGatherIpv6 { get; set; }              // tx-scatter-gather-ipv6
    public bool? TxNocacheCopy { get; set; }                     // tx-nocache-copy
    public bool? RxUdpTunnelPortOffload { get; set; }           // rx-udp_tunnel-port-offload
    public bool? TxUdpTunnelPortOffload { get; set; }           // tx-udp_tunnel-port-offload
}

// Ring Buffers
public sealed class NetworkCardBuffers
{
    public int? RxMini { get; set; }                             // RX Mini
    public int? Rx { get; set; }                                 // RX
    public int? RxJumbo { get; set; }                            // RX Jumbo
    public int? Tx { get; set; }                                 // TX
    public int? RxMiniMax { get; set; }                          // RX Mini Max
    public int? RxMax { get; set; }                              // RX Max
    public int? RxJumboMax { get; set; }                         // RX Jumbo Max
    public int? TxMax { get; set; }                              // TX Max
}

// Other Features
public sealed class NetworkCardFeatures
{
    public bool? RxUdpTunnelPortOffload { get; set; }
    public bool? TxUdpTunnelPortOffload { get; set; }
    public bool? HighDma { get; set; }                           // highdma
    public bool? RxAll { get; set; }                             // rx-all
    public bool? Loopback { get; set; }                          // loopback
    public bool? Ntuple { get; set; }                             // ntuple-filters
    public bool? ReceiveFlowSteering { get; set; }              // receive-hashing
    public bool? RxFcs { get; set; }                             // rx-fcs
    public bool? RxAllMulticast { get; set; }                   // rx-all-multicast
    public bool? RxVlanFilter { get; set; }                     // rx-vlan-filter
    public bool? RxVlanStagFilter { get; set; }                 // rx-vlan-stag-filter
    public bool? RxVlanStagHwParse { get; set; }                 // rx-vlan-stag-hw-parse
    public bool? L2FwdOffload { get; set; }                      // l2-fwd-offload
    public bool? HwTcOffload { get; set; }                       // hw-tc-offload
    public bool? EspTxOffload { get; set; }                     // esp-hw-offload
    public bool? EspRxOffload { get; set; }                      // esp-hw-offload
    public bool? FcoeOffload { get; set; }                       // fcoe-mtu
    public bool? IscsiOffload { get; set; }                      // iscsi-offload
}

// Speed/Duplex Configuration Request
public sealed class NetworkCardSpeedRequest
{
    public string Interface { get; set; } = string.Empty;
    public string? Speed { get; set; }                            // e.g., "1000", "100", "10"
    public string? Duplex { get; set; }                           // "full" or "half"
    public bool? AutoNegotiation { get; set; }                    // true/false
}

// Offload Configuration Request
public sealed class NetworkCardOffloadRequest
{
    public string Interface { get; set; } = string.Empty;
    public Dictionary<string, bool> Offloads { get; set; } = new();  // Key: offload name, Value: enabled
}

// Buffer Configuration Request
public sealed class NetworkCardBufferRequest
{
    public string Interface { get; set; } = string.Empty;
    public Dictionary<string, int> Buffers { get; set; } = new();    // Key: buffer type, Value: size
}
```

### 2. Service Implementation

#### New Service: `NetworkCardService.cs`
Location: `src/Monolith.FireWall.Core/Services/NetworkCardService.cs`

**Responsibilities:**
- Detect PCI network devices using `lspci`
- Map PCI devices to network interfaces
- Query NIC information using `ethtool`
- Parse `ethtool` output into structured data
- Apply configuration changes using `ethtool -s`
- Revert to defaults using `ethtool -s` with default values

**Key Methods:**
```csharp
public class NetworkCardService
{
    // Detect all PCI network devices
    Task<List<PciDeviceInfo>> GetPciDevicesAsync(CancellationToken cancellationToken);
    
    // Get detailed info for a specific interface
    Task<NetworkCardInfo?> GetCardInfoAsync(string interfaceName, CancellationToken cancellationToken);
    
    // Get info for all network cards
    Task<List<NetworkCardInfo>> GetAllCardsAsync(CancellationToken cancellationToken);
    
    // Apply speed/duplex settings
    Task<bool> SetSpeedAsync(NetworkCardSpeedRequest request, CancellationToken cancellationToken);
    
    // Apply offload settings
    Task<bool> SetOffloadsAsync(NetworkCardOffloadRequest request, CancellationToken cancellationToken);
    
    // Apply buffer settings
    Task<bool> SetBuffersAsync(NetworkCardBufferRequest request, CancellationToken cancellationToken);
    
    // Revert interface to defaults
    Task<bool> RevertToDefaultsAsync(string interfaceName, CancellationToken cancellationToken);
    
    // Parse ethtool output helpers
    private NetworkCardInfo ParseEthtoolInfo(string interfaceName, string ethtoolOutput);
    private NetworkCardOffloads ParseOffloads(string ethtoolOutput);
    private NetworkCardBuffers ParseBuffers(string ethtoolOutput);
}
```

**Implementation Details:**

1. **PCI Detection:**
   - Use `lspci -vmm` for machine-readable output
   - Filter for network controller class (Class: 02)
   - Map PCI slots to interfaces using `/sys/class/net/*/device` symlinks

2. **Ethtool Parsing:**
   - `ethtool <interface>` - Get general info
   - `ethtool -k <interface>` - Get offload features
   - `ethtool -g <interface>` - Get ring buffer parameters
   - `ethtool -a <interface>` - Get pause parameters
   - `ethtool -c <interface>` - Get coalescing parameters
   - `ethtool -S <interface>` - Get statistics
   - Parse output line by line, handle different formats

3. **Configuration Application:**
   - Build `ethtool -s` command with parameters
   - Validate values before applying
   - Apply changes immediately (no persistence by default)
   - Optionally save to `/etc/network/interfaces.d/` for persistence

### 3. Request Handler

#### New Handler: `NetworkCardHandler.cs`
Location: `src/Monolith.FireWall.Core/Transport/Handlers/NetworkCardHandler.cs`

**Actions:**
- `network.cards.list` - List all network cards
- `network.cards.get` - Get detailed info for one card
- `network.cards.speed.set` - Set speed/duplex
- `network.cards.offloads.set` - Set offload features
- `network.cards.buffers.set` - Set ring buffers
- `network.cards.revert` - Revert to defaults

### 4. WebUI Controller

#### New Controller: `NetworkCardController.cs`
Location: `src/Monolith.FireWall.WebUI/Features/System/NetworkCardController.cs`

**Endpoints:**
- `GET /api/system/network-cards` - List all cards
- `GET /api/system/network-cards/{interface}` - Get card details
- `POST /api/system/network-cards/{interface}/speed` - Set speed/duplex
- `POST /api/system/network-cards/{interface}/offloads` - Set offloads
- `POST /api/system/network-cards/{interface}/buffers` - Set buffers
- `POST /api/system/network-cards/{interface}/revert` - Revert to defaults

## Frontend Implementation

### 1. JavaScript Module

#### New File: `wwwroot/js/pages/network-cards.js`

**Structure:**
```javascript
var NetworkCards = {
    cards: [],
    
    init: function() {
        this.render();
        this.bindEvents();
        this.loadCards();
    },
    
    render: function() {
        // Render tab content
    },
    
    loadCards: async function() {
        // Fetch all network cards
    },
    
    renderCard: function(card) {
        // Render individual card with all options
    },
    
    applyCardSettings: async function(interface) {
        // Apply all changes for a card
    },
    
    revertCard: async function(interface) {
        // Revert card to defaults
    }
};
```

### 2. UI Structure

**Tab Content:**
```html
<div class="tab-pane fade" id="advanced-network-cards" role="tabpanel">
    <div class="card mb-4">
        <div class="card-header">
            <h5 class="mb-0">Network Interface Cards</h5>
        </div>
        <div class="card-body" id="network-cards-container">
            <!-- Cards will be rendered here -->
        </div>
    </div>
</div>
```

**Card Display (per NIC):**
- **Header Section:**
  - Interface name (e.g., "eth0")
  - PCI device info (vendor, device, slot)
  - Driver name
  - Link status (up/down, speed, duplex)
  - MAC address

- **Speed/Duplex Section:**
  - Current speed/duplex
  - Dropdown for speed selection (10/100/1000/2500/10000, etc.)
  - Dropdown for duplex (Full/Half)
  - Auto-negotiation toggle
  - Apply button

- **Offloads Section:**
  - Toggle switches for each offload feature
  - Grouped by category:
    - Segmentation Offloads (TSO, GSO, GRO, LRO)
    - Checksum Offloads (TX/RX IPv4/IPv6/Generic/SCTP)
    - VLAN Offloads (RX/TX VLAN, VLAN STAG)
    - Scatter-Gather Offloads
    - Other Offloads
  - Apply button

- **Ring Buffers Section:**
  - Current values and maximums
  - Input fields for each buffer type:
    - RX Mini
    - RX
    - RX Jumbo
    - TX
  - Apply button

- **Other Settings Section:**
  - Additional features/options
  - Display-only information (firmware version, etc.)

- **Action Buttons:**
  - "Apply All Changes" - Apply all settings for this card
  - "Revert to Defaults" - Reset all settings to factory defaults

### 3. Integration with Advanced Settings

**Modify `advanced-settings.js`:**
- Add new tab button in the nav-tabs
- Add tab pane for network cards
- Initialize NetworkCards module when tab is shown

## Ethtool Commands Reference

### Information Gathering:
```bash
# General info
ethtool eth0

# Offload features
ethtool -k eth0

# Ring buffers
ethtool -g eth0

# Pause parameters
ethtool -a eth0

# Coalescing parameters
ethtool -c eth0

# Statistics
ethtool -S eth0

# Driver info
ethtool -i eth0
```

### Configuration:
```bash
# Set speed/duplex
ethtool -s eth0 speed 1000 duplex full autoneg on

# Set offloads
ethtool -K eth0 tso on gro on gso on
ethtool -K eth0 tx-checksumming on rx-checksumming on

# Set ring buffers
ethtool -G eth0 rx 4096 tx 4096

# Revert to defaults (requires knowing defaults)
ethtool -s eth0 autoneg on  # Re-enable autoneg usually resets to defaults
```

## PCI Detection

### Using lspci:
```bash
# Machine-readable format
lspci -vmm -d ::0200

# Output format:
# Slot:   0000:01:00.0
# Class:  Network controller
# Vendor: Intel Corporation
# Device: I211 Gigabit Network Connection
```

### Mapping to Interfaces:
```bash
# Find interface for PCI device
readlink /sys/class/net/eth0/device
# Returns: ../../../0000:01:00.0

# Reverse lookup: find interface for PCI slot
for iface in /sys/class/net/*; do
    if [ "$(readlink $iface/device)" = "../../../0000:01:00.0" ]; then
        echo $(basename $iface)
    fi
done
```

## Implementation Steps

### Phase 1: Backend Foundation
1. ✅ Add `ethtool` and `pciutils` to `debian/control`
2. Create data models in `NetworkModels.cs`
3. Implement `NetworkCardService` with detection and parsing
4. Create `NetworkCardHandler` for Core API
5. Create `NetworkCardController` for WebUI API
6. Register handler in `UnixSocketListener`

### Phase 2: Frontend Basic Display
1. Create `network-cards.js` module
2. Add tab to Advanced Settings page
3. Implement card listing and basic info display
4. Test with various NIC types

### Phase 3: Configuration UI
1. Implement speed/duplex configuration
2. Implement offload toggles
3. Implement buffer configuration
4. Add validation and error handling

### Phase 4: Apply & Revert
1. Implement "Apply" functionality
2. Implement "Revert to Defaults"
3. Add user feedback (toasts, loading states)
4. Add confirmation dialogs for destructive actions

### Phase 5: Testing & Polish
1. Test with different NIC types (Intel, Realtek, Broadcom, etc.)
2. Test edge cases (no link, unsupported features)
3. Add tooltips and help text
4. Improve error messages
5. Add persistence option (save to interfaces.d)

## Error Handling

### Common Scenarios:
1. **Interface not found** - Show error, don't crash
2. **Ethtool not available** - Show warning, disable features
3. **Unsupported feature** - Hide/disable that option
4. **Permission denied** - Show error message
5. **Invalid values** - Validate before applying
6. **Apply failure** - Show specific error, don't apply partial changes

## Security Considerations

1. **Input Validation:**
   - Validate interface names (prevent command injection)
   - Validate numeric values (buffers, speeds)
   - Validate boolean values (offloads)

2. **Command Execution:**
   - Use `PlatformCommandRunner` (already has validation)
   - Never construct commands from user input directly
   - Use parameterized command building

3. **Permissions:**
   - All ethtool operations require root (UseSudo: true)
   - Validate user has appropriate permissions

## Persistence

### Option 1: Runtime Only (Default)
- Changes apply immediately but don't persist across reboots
- Use case: Testing, temporary optimizations

### Option 2: Save to interfaces.d (Future)
- Save ethtool commands to `/etc/network/interfaces.d/<interface>`
- Apply on interface up via `post-up ethtool ...`
- More persistent but requires interface restart

## Testing Checklist

- [ ] Detect PCI network devices correctly
- [ ] Map PCI devices to interfaces correctly
- [ ] Parse ethtool output for various NIC types
- [ ] Display all available options
- [ ] Apply speed/duplex changes
- [ ] Apply offload changes
- [ ] Apply buffer changes
- [ ] Revert to defaults works
- [ ] Error handling for missing tools
- [ ] Error handling for unsupported features
- [ ] UI responsive and user-friendly
- [ ] Works with Intel NICs
- [ ] Works with Realtek NICs
- [ ] Works with Broadcom NICs
- [ ] Works with virtual interfaces (if applicable)

## Future Enhancements

1. **Statistics Display:**
   - Show real-time NIC statistics
   - Packet counts, errors, drops

2. **Coalescing Settings:**
   - Interrupt coalescing configuration
   - Adaptive/interrupt moderation

3. **Wake-on-LAN:**
   - Configure WoL settings
   - Magic packet, pattern matching

4. **EEE (Energy Efficient Ethernet):**
   - Configure EEE settings
   - Show EEE status

5. **Pause Frame Settings:**
   - Configure flow control
   - TX/RX pause frames

6. **Port Selection:**
   - For multi-port NICs
   - Select active port

7. **FEC (Forward Error Correction):**
   - Configure FEC modes
   - Show supported/advertised FEC

8. **Export/Import Configuration:**
   - Export card settings to JSON
   - Import from JSON

## Notes

- Some settings may require interface restart to take effect
- Some features may not be available on all NICs
- Virtual interfaces (bridges, VLANs) may not support all features
- Changes are applied immediately but may not persist across reboots (unless saved to interfaces.d)
- Always validate that ethtool and required tools are installed before attempting operations
