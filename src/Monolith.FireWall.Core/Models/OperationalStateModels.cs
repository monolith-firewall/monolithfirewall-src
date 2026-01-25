using CL.SQLite.Models;

namespace Monolith.FireWall.Core.Models;

// ============================================================================
// Interface Operational State - Runtime physical state tracking
// ============================================================================

public enum LinkState
{
    Unknown = 0,
    Up = 1,
    Down = 2,
    Dormant = 3
}

public enum InterfaceHealthStatus
{
    Unknown = 0,
    Healthy = 1,
    Degraded = 2,
    Down = 3
}

[SQLiteTable("interface_operational_state")]
[SQLiteIndex(new[] { "InterfaceName" }, Name = "idx_interface_op_state_name", IsUnique = true)]
[SQLiteIndex(new[] { "LinkState" }, Name = "idx_interface_op_state_link")]
[SQLiteIndex(new[] { "HealthStatus" }, Name = "idx_interface_op_state_health")]
public sealed class InterfaceOperationalStateEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 64)]
    public string InterfaceName { get; set; } = string.Empty;

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public LinkState LinkState { get; set; } = LinkState.Unknown;

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 32)]
    public string? MacAddress { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int? SpeedMbps { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 16)]
    public string? Duplex { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int? Mtu { get; set; }

    // Current IPv4 (may differ from configured if DHCP)
    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 64)]
    public string? CurrentIpv4Address { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int? CurrentIpv4Prefix { get; set; }

    // Current IPv6 (may differ from configured if SLAAC/DHCPv6)
    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 128)]
    public string? CurrentIpv6Address { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int? CurrentIpv6Prefix { get; set; }

    // DHCP lease information
    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 64)]
    public string? DhcpServerAddress { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime? DhcpLeaseObtained { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime? DhcpLeaseExpires { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 64)]
    public string? DhcpGateway { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT)]
    public string? DhcpDnsServersJson { get; set; }

    // Health status
    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public InterfaceHealthStatus HealthStatus { get; set; } = InterfaceHealthStatus.Unknown;

    // Timestamps
    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime LastSeenAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime? LastLinkChangeAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime? LastIpChangeAt { get; set; }

    // Traffic stats (optional)
    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public long? RxBytes { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public long? TxBytes { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public long? RxPackets { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public long? TxPackets { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public long? RxErrors { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public long? TxErrors { get; set; }
}

public sealed class InterfaceOperationalStateView
{
    public string InterfaceName { get; set; } = string.Empty;
    public string LinkState { get; set; } = "unknown";
    public string? MacAddress { get; set; }
    public int? SpeedMbps { get; set; }
    public string? Duplex { get; set; }
    public int? Mtu { get; set; }
    public string? CurrentIpv4Address { get; set; }
    public int? CurrentIpv4Prefix { get; set; }
    public string? CurrentIpv6Address { get; set; }
    public int? CurrentIpv6Prefix { get; set; }
    public string? DhcpServerAddress { get; set; }
    public DateTime? DhcpLeaseObtained { get; set; }
    public DateTime? DhcpLeaseExpires { get; set; }
    public string? DhcpGateway { get; set; }
    public List<string>? DhcpDnsServers { get; set; }
    public string HealthStatus { get; set; } = "unknown";
    public DateTime LastSeenAt { get; set; }
    public DateTime? LastLinkChangeAt { get; set; }
    public DateTime? LastIpChangeAt { get; set; }
    public TrafficStatsView? TrafficStats { get; set; }
}

public sealed class TrafficStatsView
{
    public long RxBytes { get; set; }
    public long TxBytes { get; set; }
    public long RxPackets { get; set; }
    public long TxPackets { get; set; }
    public long RxErrors { get; set; }
    public long TxErrors { get; set; }
}

// ============================================================================
// Network State Change Log - Audit trail of state changes
// ============================================================================

public enum NetworkChangeType
{
    Unknown = 0,
    LinkUp = 1,
    LinkDown = 2,
    IpAdded = 3,
    IpRemoved = 4,
    IpChanged = 5,
    GatewayAdded = 6,
    GatewayRemoved = 7,
    GatewayChanged = 8,
    InterfaceAdded = 9,
    InterfaceRemoved = 10,
    DhcpLeaseObtained = 11,
    DhcpLeaseRenewed = 12,
    DhcpLeaseExpired = 13,
    GatewayHealthChanged = 14,
    GatewayGroupFailover = 15
}

public enum ResolutionAction
{
    None = 0,
    Notified = 1,
    AutoRepaired = 2,
    ManualRequired = 3,
    Ignored = 4
}

[SQLiteTable("network_state_changes")]
[SQLiteIndex(new[] { "ChangeType" }, Name = "idx_net_state_change_type")]
[SQLiteIndex(new[] { "InterfaceName" }, Name = "idx_net_state_change_iface")]
[SQLiteIndex(new[] { "OccurredAt" }, Name = "idx_net_state_change_time")]
[SQLiteIndex(new[] { "ResolutionAction" }, Name = "idx_net_state_change_resolution")]
public sealed class NetworkStateChangeEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public long Id { get; set; }

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.INTEGER)]
    public NetworkChangeType ChangeType { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 64)]
    public string? InterfaceName { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int? GatewayId { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int? GatewayGroupId { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT)]
    public string? PreviousValueJson { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT)]
    public string? NewValueJson { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public ResolutionAction ResolutionAction { get; set; } = ResolutionAction.None;

    [SQLiteColumn(DataType = SQLiteDataType.TEXT)]
    public string? ResolutionDetails { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime OccurredAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime? ResolvedAt { get; set; }
}

public sealed class NetworkStateChangeView
{
    public long Id { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string? InterfaceName { get; set; }
    public int? GatewayId { get; set; }
    public int? GatewayGroupId { get; set; }
    public object? PreviousValue { get; set; }
    public object? NewValue { get; set; }
    public string ResolutionAction { get; set; } = "none";
    public string? ResolutionDetails { get; set; }
    public DateTime OccurredAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

// ============================================================================
// Firewall Dynamic Aliases - Runtime-resolved aliases
// ============================================================================

public enum DynamicAliasType
{
    InterfaceIp = 0,
    InterfaceSubnet = 1,
    InterfaceNetwork = 2,
    GatewayAddress = 3
}

[SQLiteTable("firewall_dynamic_aliases")]
[SQLiteIndex(new[] { "InterfaceName" }, Name = "idx_dynamic_alias_iface")]
public sealed class FirewallDynamicAliasEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, IsUnique = true, DataType = SQLiteDataType.TEXT, Size = 64)]
    public string Name { get; set; } = string.Empty;

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.INTEGER)]
    public DynamicAliasType AliasType { get; set; }

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 64)]
    public string InterfaceName { get; set; } = string.Empty;

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 8)]
    public string AddressFamily { get; set; } = "ipv4";

    [SQLiteColumn(DataType = SQLiteDataType.TEXT)]
    public string? Description { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime CreatedAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; }
}

public sealed class FirewallDynamicAliasView
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AliasType { get; set; } = string.Empty;
    public string InterfaceName { get; set; } = string.Empty;
    public string AddressFamily { get; set; } = "ipv4";
    public string? Description { get; set; }
    public string? ResolvedValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class FirewallDynamicAliasRequest
{
    public string? Name { get; set; }
    public string? AliasType { get; set; }
    public string? InterfaceName { get; set; }
    public string? AddressFamily { get; set; }
    public string? Description { get; set; }
}
