using CL.SQLite.Models;

namespace Monolith.FireWall.Core.Models;

[SQLiteTable("package_installations")]
public class PackageInstallationEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, IsUnique = true, DataType = SQLiteDataType.TEXT, Size = 128)]
    public string PackageId { get; set; } = string.Empty;

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 32)]
    public string Version { get; set; } = string.Empty;

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 128)]
    public string Source { get; set; } = string.Empty;

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime InstalledAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; }
}

[SQLiteTable("module_states")]
[SQLiteIndex(new[] { "PackageId", "ModuleId" }, IsUnique = true, Name = "idx_module_states_pkg_module")]
public class ModuleStateEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 128)]
    public string PackageId { get; set; } = string.Empty;

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 128)]
    public string ModuleId { get; set; } = string.Empty;

    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "1")]
    public bool Enabled { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; }
}
