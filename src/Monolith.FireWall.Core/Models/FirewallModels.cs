using CL.SQLite.Models;

namespace Monolith.FireWall.Core.Models;

[SQLiteTable("firewall_aliases")]
public sealed class FirewallAliasEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, IsUnique = true, DataType = SQLiteDataType.TEXT, Size = 128)]
    public string Name { get; set; } = string.Empty;

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 32)]
    public string Type { get; set; } = "host";

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 256)]
    public string? Description { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "1")]
    public bool Enabled { get; set; } = true;

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime CreatedAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; }
}

[SQLiteTable("firewall_alias_entries")]
[SQLiteIndex(new[] { "AliasId" }, Name = "idx_firewall_alias_entries_alias")]
public sealed class FirewallAliasEntryEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.INTEGER)]
    public int AliasId { get; set; }

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 256)]
    public string Value { get; set; } = string.Empty;

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 256)]
    public string? Comment { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime CreatedAt { get; set; }
}

[SQLiteTable("firewall_nat_rules")]
public sealed class FirewallNatRuleEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.INTEGER)]
    public int RuleNumber { get; set; }

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 32)]
    public string Type { get; set; } = "port_forward";

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 64)]
    public string Interface { get; set; } = string.Empty;

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 16)]
    public string AddressFamily { get; set; } = "ipv4";

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 16)]
    public string Protocol { get; set; } = "tcp";

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 16)]
    public string SourceType { get; set; } = "any";

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 128)]
    public string? SourceValue { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 32)]
    public string? SourcePort { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 16)]
    public string DestinationType { get; set; } = "any";

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 128)]
    public string? DestinationValue { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 32)]
    public string? DestinationPort { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 64)]
    public string? RedirectTargetIp { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 32)]
    public string? RedirectTargetPort { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 16)]
    public string ReflectionMode { get; set; } = "default";

    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "1")]
    public bool Enabled { get; set; } = true;

    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "0")]
    public bool LogEnabled { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int? ScheduleId { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 256)]
    public string? Description { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime CreatedAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; }
}

[SQLiteTable("firewall_nat_settings")]
public sealed class FirewallNatSettingsEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "0")]
    public bool ReflectionEnabled { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 16)]
    public string ReflectionMode { get; set; } = "proxy";

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; }
}

public sealed class FirewallAliasView
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Enabled { get; set; }
    public List<string> Content { get; set; } = new();
}

public class FirewallAliasRequest
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? Description { get; set; }
    public List<string>? Content { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class FirewallAliasUpdateRequest : FirewallAliasRequest
{
    public int Id { get; set; }
}

public sealed class FirewallAliasResolveRequest
{
    public string? Name { get; set; }
}

public sealed class FirewallNatRuleView
{
    public int Id { get; set; }
    public int RuleNumber { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Interface { get; set; } = string.Empty;
    public string AddressFamily { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string? SourceValue { get; set; }
    public string? SourcePort { get; set; }
    public string DestinationType { get; set; } = string.Empty;
    public string? DestinationValue { get; set; }
    public string? DestinationPort { get; set; }
    public string? RedirectTargetIp { get; set; }
    public string? RedirectTargetPort { get; set; }
    public string ReflectionMode { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public bool LogEnabled { get; set; }
    public int? ScheduleId { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class FirewallNatRuleRequest
{
    public string? Type { get; set; }
    public string? Interface { get; set; }
    public string? AddressFamily { get; set; }
    public string? Protocol { get; set; }
    public string? SourceType { get; set; }
    public string? SourceValue { get; set; }
    public string? SourcePort { get; set; }
    public string? DestinationType { get; set; }
    public string? DestinationValue { get; set; }
    public string? DestinationPort { get; set; }
    public string? RedirectTargetIp { get; set; }
    public string? RedirectTargetPort { get; set; }
    public string? ReflectionMode { get; set; }
    public string? Description { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class FirewallNatRuleUpdateRequest : FirewallNatRuleRequest
{
    public int Id { get; set; }
}

public sealed class FirewallNatReorderRequest
{
    public List<int> RuleIds { get; set; } = new();
}

public sealed class FirewallIdRequest
{
    public int Id { get; set; }
}

public sealed class FirewallNatSettingsView
{
    public bool ReflectionEnabled { get; set; }
    public string ReflectionMode { get; set; } = "proxy";
}

public sealed class FirewallNatSettingsRequest
{
    public bool ReflectionEnabled { get; set; }
    public string? ReflectionMode { get; set; }
}

[SQLiteTable("firewall_rules")]
public sealed class FirewallRuleEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.INTEGER)]
    public int RuleNumber { get; set; }

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 64)]
    public string Interface { get; set; } = string.Empty;

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 16)]
    public string Direction { get; set; } = "in";

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 16)]
    public string Action { get; set; } = "pass";

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 16)]
    public string AddressFamily { get; set; } = "ipv4";

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 16)]
    public string Protocol { get; set; } = "any";

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 16)]
    public string SourceType { get; set; } = "any";

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 128)]
    public string? SourceValue { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 32)]
    public string? SourcePort { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 16)]
    public string DestinationType { get; set; } = "any";

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 128)]
    public string? DestinationValue { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 32)]
    public string? DestinationPort { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 64)]
    public string? Gateway { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "0")]
    public bool LogEnabled { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "0")]
    public bool IsManaged { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 32)]
    public string? ManagedSourceType { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 128)]
    public string? ManagedSourceId { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "1")]
    public bool Enabled { get; set; } = true;

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int? ScheduleId { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 256)]
    public string? Description { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime CreatedAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; }
}

[SQLiteTable("firewall_defaults")]
public sealed class FirewallDefaultsEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 16)]
    public string LanDefaultAction { get; set; } = "pass";

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 16)]
    public string WanDefaultAction { get; set; } = "block";

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 16)]
    public string OptDefaultAction { get; set; } = "block";

    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "1")]
    public bool BlockReservedOnWan { get; set; } = true;

    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "1")]
    public bool AllowManagementWebUi { get; set; } = true;

    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "1")]
    public bool AllowSshAccess { get; set; } = true;

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; }
}

[SQLiteTable("firewall_interface_settings")]
public sealed class FirewallInterfaceSettingsEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, IsUnique = true, DataType = SQLiteDataType.TEXT, Size = 64)]
    public string InterfaceName { get; set; } = string.Empty;

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 16)]
    public string DefaultAction { get; set; } = "block";

    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "0")]
    public bool BlockReserved { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "0")]
    public bool BlockBogon { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; }
}

public sealed class FirewallRuleView
{
    public int Id { get; set; }
    public int RuleNumber { get; set; }
    public string Interface { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string AddressFamily { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string? SourceValue { get; set; }
    public string? SourcePort { get; set; }
    public string DestinationType { get; set; } = string.Empty;
    public string? DestinationValue { get; set; }
    public string? DestinationPort { get; set; }
    public string? Gateway { get; set; }
    public bool LogEnabled { get; set; }
    public bool Enabled { get; set; }
    public int? ScheduleId { get; set; }
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public string? SystemTag { get; set; }
    public bool IsManaged { get; set; }
    public string? ManagedBy { get; set; }
}

public class FirewallRuleRequest
{
    public string? Interface { get; set; }
    public string? Direction { get; set; }
    public string? Action { get; set; }
    public string? AddressFamily { get; set; }
    public string? Protocol { get; set; }
    public string? SourceType { get; set; }
    public string? SourceValue { get; set; }
    public string? SourcePort { get; set; }
    public string? DestinationType { get; set; }
    public string? DestinationValue { get; set; }
    public string? DestinationPort { get; set; }
    public string? Gateway { get; set; }
    public bool LogEnabled { get; set; }
    public bool Enabled { get; set; } = true;
    public string? Description { get; set; }
}

public sealed class FirewallRuleUpdateRequest : FirewallRuleRequest
{
    public int Id { get; set; }
}

public sealed class FirewallManagedRuleRequest : FirewallRuleRequest
{
    public string? PackageId { get; set; }
    public string? ModuleId { get; set; }
}

public sealed class FirewallRuleReorderRequest
{
    public string? Interface { get; set; }
    public List<int> RuleIds { get; set; } = new();
}

public sealed class FirewallDefaultsView
{
    public string LanDefaultAction { get; set; } = "pass";
    public string WanDefaultAction { get; set; } = "block";
    public string OptDefaultAction { get; set; } = "block";
    public bool BlockReservedOnWan { get; set; } = true;
    public bool AllowManagementWebUi { get; set; } = true;
    public bool AllowSshAccess { get; set; } = true;
}

public sealed class FirewallDefaultsRequest
{
    public string? LanDefaultAction { get; set; }
    public string? WanDefaultAction { get; set; }
    public string? OptDefaultAction { get; set; }
    public bool BlockReservedOnWan { get; set; }
    public bool AllowManagementWebUi { get; set; }
    public bool AllowSshAccess { get; set; }
}

public sealed class FirewallInterfaceSettingsRequest
{
    public string InterfaceName { get; set; } = string.Empty;
    public string? DefaultAction { get; set; }
    public bool BlockReserved { get; set; }
    public bool BlockBogon { get; set; }
}

public sealed class FirewallInterfaceSettingsUpdateRequest
{
    public string? DefaultAction { get; set; }
    public bool BlockReserved { get; set; }
    public bool BlockBogon { get; set; }
}

public sealed class FirewallInterfaceSettingsView
{
    public int Id { get; set; }
    public string InterfaceName { get; set; } = string.Empty;
    public string DefaultAction { get; set; } = "block";
    public bool BlockReserved { get; set; }
    public bool BlockBogon { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// Firewall States Models
public sealed class FirewallStateView
{
    public string Id { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public string SourceIp { get; set; } = string.Empty;
    public int? SourcePort { get; set; }
    public string DestIp { get; set; } = string.Empty;
    public int? DestPort { get; set; }
    public string State { get; set; } = string.Empty;
    public string Interface { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public int Age { get; set; }
    public long PacketsIn { get; set; }
    public long PacketsOut { get; set; }
    public long BytesIn { get; set; }
    public long BytesOut { get; set; }
}

public sealed class FirewallStatesListRequest
{
    public string? Protocol { get; set; }
    public string? SourceIp { get; set; }
    public string? DestIp { get; set; }
    public string? SourcePort { get; set; }
    public string? DestPort { get; set; }
    public string? State { get; set; }
    public string? Interface { get; set; }
    public string? Direction { get; set; }
    public string? Search { get; set; }
    public int? MinAge { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class FirewallStatesListResponse
{
    public List<FirewallStateView> States { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public sealed class FirewallStateKillRequest
{
    public string Id { get; set; } = string.Empty;
}

// Rule Query Models
public sealed class FirewallRuleQueryRequest
{
    public string? Interface { get; set; }
    public string? RuleType { get; set; }       // "system", "user", "managed", or null for all
    public string? ManagedBy { get; set; }       // Filter managed rules by package
    public bool? Enabled { get; set; }
    public string? Protocol { get; set; }
    public string? Action { get; set; }
}

public sealed class FirewallRuleQueryResponse
{
    public List<FirewallRuleViewExtended> Rules { get; set; } = new();
    public FirewallRuleQuerySummary Summary { get; set; } = new();
}

public sealed class FirewallRuleQuerySummary
{
    public int Total { get; set; }
    public int System { get; set; }
    public int User { get; set; }
    public int Managed { get; set; }
}

public sealed class FirewallRuleViewExtended
{
    public int Id { get; set; }
    public int RuleNumber { get; set; }
    public string Interface { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string AddressFamily { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string? SourceValue { get; set; }
    public string? SourcePort { get; set; }
    public string DestinationType { get; set; } = string.Empty;
    public string? DestinationValue { get; set; }
    public string? DestinationPort { get; set; }
    public string? Gateway { get; set; }
    public bool LogEnabled { get; set; }
    public bool Enabled { get; set; }
    public int? ScheduleId { get; set; }
    public string? Description { get; set; }
    public string RuleType { get; set; } = "user";    // "system", "user", "managed"
    public string? ManagedByPackage { get; set; }
    public string? ManagedByModule { get; set; }
    public bool IsEditable { get; set; }
    public bool IsDeletable { get; set; }
    public List<string> ConflictWarnings { get; set; } = new();
}

public sealed class FirewallRuleTypesResponse
{
    public List<FirewallRuleTypeInfo> Types { get; set; } = new();
}

public sealed class FirewallRuleTypeInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Count { get; set; }
}

// Rule Validation Models
public sealed class FirewallRuleValidateRequest
{
    public string? Interface { get; set; }
    public string? Direction { get; set; }
    public string? Action { get; set; }
    public string? AddressFamily { get; set; }
    public string? Protocol { get; set; }
    public string? SourceType { get; set; }
    public string? SourceValue { get; set; }
    public string? SourcePort { get; set; }
    public string? DestinationType { get; set; }
    public string? DestinationValue { get; set; }
    public string? DestinationPort { get; set; }
    public int? ExcludeRuleId { get; set; }  // For edit validation - exclude the rule being edited
}

public sealed class FirewallRuleValidateResponse
{
    public bool IsValid { get; set; }
    public bool HasConflicts { get; set; }
    public bool CanProceed { get; set; } = true;
    public List<RuleConflict> Conflicts { get; set; } = new();
    public string? Suggestion { get; set; }
}

public sealed class RuleConflict
{
    public string Type { get; set; } = string.Empty;      // "duplicate", "overlap", "action_conflict", etc.
    public string Severity { get; set; } = "warning";     // "error", "warning", "info"
    public int? ConflictingRuleId { get; set; }
    public string ConflictingRuleDescription { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Resolution { get; set; }
}
