using CL.SQLite.Services;
using CodeLogic;
using Monolith.FireWall.Common.Models;

namespace Monolith.FireWall.WebUI.Features.SystemLogs;

public static class SystemLogsDatabaseInit
{
    public static void InitializeTables()
    {
        try
        {
            var sqlite = CodeLogic.Libs.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite == null) return;

            var repository = sqlite.CreateRepository<LogEntryEntity>();
            var queryBuilder = sqlite.CreateQueryBuilder<LogEntryEntity>();
            
            // Ensure table exists by trying to query it
            _ = queryBuilder.Select(e => e).Take(1).ExecuteAsync().Result;
            
            Console.WriteLine("✓ System Logs table initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize System Logs table: {ex.Message}");
        }
    }
}
