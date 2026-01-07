using CL.SQLite.Models;

namespace Monolith.FireWall.Core.Models;

[SQLiteTable("static_routes")]
public sealed class StaticRouteEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 64)]
    public string DestinationCidr { get; set; } = string.Empty;

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 64)]
    public string? Gateway { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 64)]
    public string? Interface { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int? Metric { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT)]
    public string? Description { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime CreatedAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; }
}

public sealed class GatewayView
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Interface { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public int? Metric { get; set; }
    public bool IsDefault { get; set; }
}

public sealed class StaticRouteView
{
    public int Id { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string? Gateway { get; set; }
    public string? Interface { get; set; }
    public int? Metric { get; set; }
    public string? Description { get; set; }
    public bool Active { get; set; }
}

public sealed class StaticRouteRequest
{
    public string Destination { get; set; } = string.Empty;
    public string? Gateway { get; set; }
    public string? Interface { get; set; }
    public int? Metric { get; set; }
    public string? Description { get; set; }
}

public sealed class StaticRouteDeleteRequest
{
    public int Id { get; set; }
}

public sealed class RoutingStatusView
{
    public bool IpForwardingEnabled { get; set; }
    public GatewayView? DefaultGateway { get; set; }
    public List<RouteSummaryView> Routes { get; set; } = new();
    public bool NatMasqueradeEnabled { get; set; }
}

public sealed class RouteSummaryView
{
    public string Destination { get; set; } = string.Empty;
    public string? Gateway { get; set; }
    public string? Interface { get; set; }
    public string? Protocol { get; set; }
    public int? Metric { get; set; }
    public bool IsDefault { get; set; }
}
