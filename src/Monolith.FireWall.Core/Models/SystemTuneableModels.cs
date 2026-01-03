using CL.SQLite.Models;

namespace Monolith.FireWall.Core.Models;

[SQLiteTable("system_tuneables")]
public sealed class SystemTuneableEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, IsUnique = true, DataType = SQLiteDataType.TEXT, Size = 128)]
    public string Key { get; set; } = string.Empty;

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT)]
    public string Value { get; set; } = string.Empty;

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime? LastAppliedAt { get; set; }
}

public sealed class TuneableOption
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class TuneableDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? DefaultValue { get; set; }
    public bool Featured { get; set; }
    public List<TuneableOption>? Options { get; set; }
}

public sealed class TuneableView
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? DefaultValue { get; set; }
    public string? CurrentValue { get; set; }
    public string? DesiredValue { get; set; }
    public bool Featured { get; set; }
    public List<TuneableOption>? Options { get; set; }
}

public sealed class TuneableUpdate
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
}

public sealed class TuneableApplyRequest
{
    public List<TuneableUpdate> Items { get; set; } = new();
}

public sealed class TuneableApplyItemResult
{
    public string Key { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? AppliedValue { get; set; }
    public string? CurrentValue { get; set; }
}

public sealed class TuneableApplyResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<TuneableApplyItemResult> Results { get; set; } = new();
}
