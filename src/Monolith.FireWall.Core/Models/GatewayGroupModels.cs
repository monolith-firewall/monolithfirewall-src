using CL.SQLite.Models;

namespace Monolith.FireWall.Core.Models;

// ============================================================================
// Gateway Groups - Multi-WAN failover and load balancing
// ============================================================================

public enum GatewayGroupMode
{
    Failover = 0,
    LoadBalance = 1,
    Weighted = 2
}

public enum GatewayGroupTrigger
{
    MemberDown = 0,
    PacketLoss = 1,
    LatencyHigh = 2,
    Any = 3
}

[SQLiteTable("gateway_groups")]
[SQLiteIndex(new[] { "Name" }, Name = "idx_gateway_groups_name", IsUnique = true)]
public sealed class GatewayGroupEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 64)]
    public string Name { get; set; } = string.Empty;

    [SQLiteColumn(DataType = SQLiteDataType.TEXT)]
    public string? Description { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public GatewayGroupMode Mode { get; set; } = GatewayGroupMode.Failover;

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public GatewayGroupTrigger TriggerLevel { get; set; } = GatewayGroupTrigger.MemberDown;

    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "1")]
    public bool Enabled { get; set; } = true;

    // Threshold settings for trigger evaluation
    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int? PacketLossThreshold { get; set; } = 20;

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int? LatencyThresholdMs { get; set; } = 500;

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime CreatedAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; }
}

[SQLiteTable("gateway_group_members")]
[SQLiteIndex(new[] { "GroupId" }, Name = "idx_gw_group_members_group")]
[SQLiteIndex(new[] { "GatewayId" }, Name = "idx_gw_group_members_gw")]
[SQLiteIndex(new[] { "GroupId", "GatewayId" }, Name = "idx_gw_group_members_unique", IsUnique = true)]
public sealed class GatewayGroupMemberEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.INTEGER)]
    public int GroupId { get; set; }

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.INTEGER)]
    public int GatewayId { get; set; }

    // Tier determines failover order (1 = primary, 2 = secondary, etc.)
    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int Tier { get; set; } = 1;

    // Weight for weighted load balancing mode
    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int Weight { get; set; } = 1;

    // Priority within same tier
    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int Priority { get; set; } = 0;

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime CreatedAt { get; set; }
}

// ============================================================================
// Gateway Health - Real-time health monitoring
// ============================================================================

public enum GatewayHealthStatus
{
    Unknown = 0,
    Online = 1,
    Degraded = 2,
    Offline = 3
}

public enum GatewayMonitorType
{
    Icmp = 0,
    Tcp = 1,
    Http = 2,
    HttpGet = 3
}

[SQLiteTable("gateway_health")]
[SQLiteIndex(new[] { "GatewayId" }, Name = "idx_gateway_health_gwid", IsUnique = true)]
[SQLiteIndex(new[] { "Status" }, Name = "idx_gateway_health_status")]
public sealed class GatewayHealthEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.INTEGER)]
    public int GatewayId { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public GatewayHealthStatus Status { get; set; } = GatewayHealthStatus.Unknown;

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int? LatencyMs { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.REAL)]
    public double? PacketLossPercent { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int ConsecutiveFailures { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int ConsecutiveSuccesses { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime? LastCheckAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime? LastStateChangeAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime? LastSuccessAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime? LastFailureAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT)]
    public string? LastError { get; set; }
}

[SQLiteTable("gateway_monitor_configs")]
[SQLiteIndex(new[] { "GatewayId" }, Name = "idx_gw_monitor_config_gw", IsUnique = true)]
public sealed class GatewayMonitorConfigEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.INTEGER)]
    public int GatewayId { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public GatewayMonitorType MonitorType { get; set; } = GatewayMonitorType.Icmp;

    // Target for probes (IP or hostname); null means use gateway address
    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 256)]
    public string? MonitorTarget { get; set; }

    // For TCP/HTTP, optional port (default 80 for HTTP, 443 for HTTPS)
    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int? MonitorPort { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int IntervalSeconds { get; set; } = 10;

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int TimeoutMs { get; set; } = 1000;

    // Number of consecutive failures before marking down
    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int FailThreshold { get; set; } = 3;

    // Number of consecutive successes before marking up
    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int RecoverThreshold { get; set; } = 2;

    // For averaging latency/packet loss
    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int SampleCount { get; set; } = 10;

    // Threshold for marking gateway as degraded due to high latency
    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int? LatencyThresholdMs { get; set; } = 500;

    // Threshold for marking gateway as degraded due to packet loss (percent)
    [SQLiteColumn(DataType = SQLiteDataType.REAL)]
    public double? PacketLossThreshold { get; set; } = 20;

    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "1")]
    public bool Enabled { get; set; } = true;

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; }
}

// ============================================================================
// View Models
// ============================================================================

public sealed class GatewayGroupView
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Mode { get; set; } = "failover";
    public string TriggerLevel { get; set; } = "member_down";
    public bool Enabled { get; set; }
    public int? PacketLossThreshold { get; set; }
    public int? LatencyThresholdMs { get; set; }
    public List<GatewayGroupMemberView> Members { get; set; } = new();
    public GatewayGroupStatusView? CurrentStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class GatewayGroupMemberView
{
    public int Id { get; set; }
    public int GatewayId { get; set; }
    public string GatewayName { get; set; } = string.Empty;
    public string GatewayAddress { get; set; } = string.Empty;
    public string? Interface { get; set; }
    public int Tier { get; set; }
    public int Weight { get; set; }
    public int Priority { get; set; }
    public GatewayHealthView? Health { get; set; }
}

public sealed class GatewayGroupStatusView
{
    public int ActiveTier { get; set; }
    public List<int> ActiveGatewayIds { get; set; } = new();
    public int HealthyMemberCount { get; set; }
    public int TotalMemberCount { get; set; }
    public DateTime? LastFailoverAt { get; set; }
}

public sealed class GatewayHealthView
{
    public int GatewayId { get; set; }
    public string Status { get; set; } = "unknown";
    public int? LatencyMs { get; set; }
    public double? PacketLossPercent { get; set; }
    public int ConsecutiveFailures { get; set; }
    public int ConsecutiveSuccesses { get; set; }
    public DateTime? LastCheckAt { get; set; }
    public DateTime? LastStateChangeAt { get; set; }
    public string? LastError { get; set; }
}

public sealed class GatewayMonitorConfigView
{
    public int GatewayId { get; set; }
    public string MonitorType { get; set; } = "icmp";
    public string? MonitorTarget { get; set; }
    public int? MonitorPort { get; set; }
    public int IntervalSeconds { get; set; }
    public int TimeoutMs { get; set; }
    public int FailThreshold { get; set; }
    public int RecoverThreshold { get; set; }
    public int SampleCount { get; set; }
    public bool Enabled { get; set; }
}

/// <summary>
/// Result of a single gateway health check, including status change info.
/// </summary>
public sealed class GatewayHealthCheckResult
{
    public int GatewayId { get; set; }
    public string GatewayName { get; set; } = string.Empty;
    public bool StatusChanged { get; set; }
    public GatewayHealthStatus PreviousStatus { get; set; }
    public GatewayHealthStatus NewStatus { get; set; }
    public int? LatencyMs { get; set; }
    public double? PacketLossPercent { get; set; }
    public string? Error { get; set; }
}

// ============================================================================
// Request Models
// ============================================================================

public sealed class GatewayGroupRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Mode { get; set; }
    public string? TriggerLevel { get; set; }
    public bool? Enabled { get; set; }
    public int? PacketLossThreshold { get; set; }
    public int? LatencyThresholdMs { get; set; }
    public List<GatewayGroupMemberRequest>? Members { get; set; }
}

public sealed class GatewayGroupMemberRequest
{
    public int GatewayId { get; set; }
    public int? Tier { get; set; }
    public int? Weight { get; set; }
    public int? Priority { get; set; }
}

public sealed class GatewayGroupDeleteRequest
{
    public int Id { get; set; }
}

public sealed class GatewayMonitorConfigRequest
{
    public int GatewayId { get; set; }
    public string? MonitorType { get; set; }
    public string? MonitorTarget { get; set; }
    public int? MonitorPort { get; set; }
    public int? IntervalSeconds { get; set; }
    public int? TimeoutMs { get; set; }
    public int? FailThreshold { get; set; }
    public int? RecoverThreshold { get; set; }
    public int? SampleCount { get; set; }
    public bool? Enabled { get; set; }
}

// ============================================================================
// Event Models for state changes
// ============================================================================

public sealed class GatewayStateChange
{
    public int GatewayId { get; set; }
    public GatewayHealthStatus PreviousStatus { get; set; }
    public GatewayHealthStatus NewStatus { get; set; }
    public int? LatencyMs { get; set; }
    public double? PacketLossPercent { get; set; }
    public DateTime OccurredAt { get; set; }
}

public sealed class GatewayGroupFailoverEvent
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public int PreviousTier { get; set; }
    public int NewTier { get; set; }
    public List<int> PreviousActiveGateways { get; set; } = new();
    public List<int> NewActiveGateways { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}
