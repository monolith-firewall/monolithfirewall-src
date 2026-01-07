namespace Monolith.FireWall.Platform.Models;

public sealed class InterfaceInfo
{
    public string Name { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public int Mtu { get; set; }
    public string OperState { get; set; } = string.Empty;
    public bool IsUp { get; set; }
}

public sealed class AddressInfo
{
    public string Interface { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int PrefixLength { get; set; }
}

public sealed class RouteInfo
{
    public string Destination { get; set; } = string.Empty;
    public string? Gateway { get; set; }
    public string? Interface { get; set; }
    public string? Protocol { get; set; }
    public string? Scope { get; set; }
}

public sealed class ResolverInfo
{
    public string Source { get; set; } = string.Empty;
    public string[] Servers { get; set; } = Array.Empty<string>();
}

public sealed class InterfaceRequest
{
    public string? Interface { get; set; }
}

public sealed class SetInterfaceStateRequest
{
    public string Interface { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}

public sealed class AddressRequest
{
    public string Interface { get; set; } = string.Empty;
    public string AddressCidr { get; set; } = string.Empty;
}

public sealed class RouteRequest
{
    public string Destination { get; set; } = string.Empty;
    public string? Gateway { get; set; }
    public string? Interface { get; set; }
}

public sealed class DnsResolversRequest
{
    public string? Interface { get; set; }
    public string[] Servers { get; set; } = Array.Empty<string>();
}

// Network Card Models
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

public sealed class NetworkCardOffloads
{
    // TCP Segmentation Offload
    public bool? Tso { get; set; }                            // tcp-segmentation-offload
    public bool? Ufo { get; set; }                             // udp-fragmentation-offload
    public bool? Gso { get; set; }                             // generic-segmentation-offload
    public bool? Gro { get; set; }                             // generic-receive-offload
    public bool? Lro { get; set; }                             // large-receive-offload
    
    // VLAN Offloads
    public bool? Rxvlan { get; set; }                         // rx-vlan-offload
    public bool? Txvlan { get; set; }                         // tx-vlan-offload
    public bool? TxvlanStagHwInsert { get; set; }              // tx-vlan-stag-hw-insert
    public bool? RxvlanStagFilter { get; set; }               // rx-vlan-stag-filter
    public bool? RxvlanStagHwParse { get; set; }               // rx-vlan-stag-hw-parse
    
    // Checksum Offloads
    public bool? TxChecksumming { get; set; }                 // tx-checksumming
    public bool? RxChecksumming { get; set; }                // rx-checksumming
    public bool? TxChecksumIpv4 { get; set; }                  // tx-checksum-ipv4
    public bool? TxChecksumIpv6 { get; set; }                  // tx-checksum-ipv6
    public bool? TxChecksumIpGeneric { get; set; }            // tx-checksum-ip-generic
    public bool? TxChecksumSctp { get; set; }                 // tx-checksum-sctp
    public bool? RxChecksumIpv4 { get; set; }                  // rx-checksum-ipv4
    public bool? RxChecksumIpv6 { get; set; }                  // rx-checksum-ipv6
    public bool? RxChecksumIpGeneric { get; set; }             // rx-checksum-ip-generic
    public bool? RxChecksumSctp { get; set; }                  // rx-checksum-sctp
    
    // Scatter-Gather Offloads
    public bool? ScatterGather { get; set; }                  // scatter-gather
    public bool? TxScatterGather { get; set; }                 // tx-scatter-gather
    public bool? TxScatterGatherFragList { get; set; }         // tx-scatter-gather-fraglist
    public bool? TxScatterGatherIpv4 { get; set; }             // tx-scatter-gather-ipv4
    public bool? TxScatterGatherIpv6 { get; set; }             // tx-scatter-gather-ipv6
    
    // Other Offloads
    public bool? Rxhash { get; set; }                         // rx-hashing
    public bool? RxAll { get; set; }                           // rx-all
    public bool? TxNocacheCopy { get; set; }                   // tx-nocache-copy
    public bool? RxUdpTunnelPortOffload { get; set; }         // rx-udp_tunnel-port-offload
    public bool? TxUdpTunnelPortOffload { get; set; }          // tx-udp_tunnel-port-offload
}

public sealed class NetworkCardBuffers
{
    public int? RxMini { get; set; }                           // RX Mini
    public int? Rx { get; set; }                               // RX
    public int? RxJumbo { get; set; }                          // RX Jumbo
    public int? Tx { get; set; }                               // TX
    public int? RxMiniMax { get; set; }                        // RX Mini Max
    public int? RxMax { get; set; }                            // RX Max
    public int? RxJumboMax { get; set; }                       // RX Jumbo Max
    public int? TxMax { get; set; }                            // TX Max
}

public sealed class NetworkCardFeatures
{
    public bool? HighDma { get; set; }                         // highdma
    public bool? RxAll { get; set; }                           // rx-all
    public bool? Loopback { get; set; }                         // loopback
    public bool? Ntuple { get; set; }                           // ntuple-filters
    public bool? ReceiveFlowSteering { get; set; }             // receive-hashing
    public bool? RxFcs { get; set; }                           // rx-fcs
    public bool? RxAllMulticast { get; set; }                  // rx-all-multicast
    public bool? RxVlanFilter { get; set; }                    // rx-vlan-filter
    public bool? RxVlanStagFilter { get; set; }                // rx-vlan-stag-filter
    public bool? RxVlanStagHwParse { get; set; }                // rx-vlan-stag-hw-parse
    public bool? L2FwdOffload { get; set; }                   // l2-fwd-offload
    public bool? HwTcOffload { get; set; }                     // hw-tc-offload
    public bool? EspTxOffload { get; set; }                    // esp-hw-offload
    public bool? EspRxOffload { get; set; }                    // esp-hw-offload
    public bool? FcoeOffload { get; set; }                     // fcoe-mtu
    public bool? IscsiOffload { get; set; }                     // iscsi-offload
}

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
    public string? Port { get; set; }                          // "TP", "FIBRE", "AUI", etc.
    public string? PhyAddress { get; set; }
    public string? Transceiver { get; set; }
    public string? AutoNegotiation { get; set; }               // "on" or "off"
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

public sealed class NetworkCardSpeedRequest
{
    public string Interface { get; set; } = string.Empty;
    public string? Speed { get; set; }                         // e.g., "1000", "100", "10"
    public string? Duplex { get; set; }                        // "full" or "half"
    public bool? AutoNegotiation { get; set; }                 // true/false
}

public sealed class NetworkCardOffloadRequest
{
    public string Interface { get; set; } = string.Empty;
    public Dictionary<string, bool> Offloads { get; set; } = new();  // Key: offload name, Value: enabled
}

public sealed class NetworkCardBufferRequest
{
    public string Interface { get; set; } = string.Empty;
    public Dictionary<string, int> Buffers { get; set; } = new();    // Key: buffer type, Value: size
}
