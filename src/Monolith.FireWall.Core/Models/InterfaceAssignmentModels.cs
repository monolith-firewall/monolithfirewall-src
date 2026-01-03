using CL.SQLite.Models;

namespace Monolith.FireWall.Core.Models;

public enum InterfaceAssignmentType
{
    Physical = 0,
    Vlan = 1,
    Bridge = 2
}

public enum InterfaceIpMode
{
    None = 0,
    Dhcp = 1,
    Static = 2
}

public enum InterfaceRole
{
    Unknown = 0,
    Lan = 1,
    Wan = 2,
    Opt = 3
}

[SQLiteTable("interface_assignments")]
public sealed class InterfaceAssignmentEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, IsUnique = true, DataType = SQLiteDataType.TEXT, Size = 64)]
    public string InterfaceName { get; set; } = string.Empty;

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 128)]
    public string Name { get; set; } = string.Empty;

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public InterfaceAssignmentType Type { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT)]
    public string? Description { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public InterfaceIpMode IpMode { get; set; } = InterfaceIpMode.None;

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public InterfaceRole Role { get; set; } = InterfaceRole.Unknown;

    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "0")]
    public bool IsManagement { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 64)]
    public string? IpAddress { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int? PrefixLength { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 64)]
    public string? Gateway { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 64)]
    public string? ParentInterface { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int? VlanId { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT)]
    public string? BridgePorts { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "0")]
    public bool BridgeStp { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int? BridgeForwardDelay { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime? LastAppliedAt { get; set; }
}

public sealed class InterfaceAssignmentRequest
{
    public string? Interface { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? Description { get; set; }
    public string? IpMode { get; set; }
    public string? Role { get; set; }
    public bool? IsManagement { get; set; }
    public string? Address { get; set; }
    public int? PrefixLength { get; set; }
    public string? AddressCidr { get; set; }
    public string? Gateway { get; set; }
    public string? ParentInterface { get; set; }
    public int? VlanId { get; set; }
    public List<string>? BridgePorts { get; set; }
    public bool? BridgeStp { get; set; }
    public int? BridgeForwardDelay { get; set; }
}

public sealed class InterfaceAssignmentDeleteRequest
{
    public string Interface { get; set; } = string.Empty;
}

public sealed class InterfaceConfigCheckResult
{
    public bool Ok { get; set; }
    public bool IncludePresent { get; set; }
    public bool ManagedFilePresent { get; set; }
    public string ManagedFile { get; set; } = string.Empty;
    public List<InterfaceConfigIssue> Issues { get; set; } = new();
}

public sealed class InterfaceConfigIssue
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Interface { get; set; }
    public string? File { get; set; }
    public string? Detail { get; set; }
}

public sealed class InterfaceApplyResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ManagedFile { get; set; } = string.Empty;
    public int AssignmentCount { get; set; }
    public string? BackupFile { get; set; }
}

public sealed class InterfaceApplyNowResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public bool TimedOut { get; set; }
    public string? StdOut { get; set; }
    public string? StdErr { get; set; }
}

public sealed class InterfaceAssignmentView
{
    public string Interface { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public bool Managed { get; set; }
    public string? SourceFile { get; set; }
    public InterfaceIpMode IpMode { get; set; }
    public InterfaceRole Role { get; set; } = InterfaceRole.Unknown;
    public bool IsManagement { get; set; }
    public string? ConfigAddress { get; set; }
    public int? ConfigPrefixLength { get; set; }
    public string? Gateway { get; set; }
    public string? ParentInterface { get; set; }
    public int? VlanId { get; set; }
    public List<string>? BridgePorts { get; set; }
    public bool BridgeStp { get; set; }
    public int? BridgeForwardDelay { get; set; }
}

public sealed class InterfaceInventoryView
{
    public string Interface { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
}

public sealed class InterfaceAssignmentsSnapshot
{
    public List<InterfaceAssignmentView> Assigned { get; set; } = new();
    public List<InterfaceInventoryView> Unassigned { get; set; } = new();
    public List<InterfaceAssignmentView> Vlans { get; set; } = new();
    public List<InterfaceAssignmentView> Bridges { get; set; } = new();
    public string ManagedFile { get; set; } = string.Empty;
}
