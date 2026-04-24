using CL.SQLite.Services;
using CodeLogic;

namespace Monolith.FireWall.WebUI.Features.Firewall.Aliases;

public static class AliasesDatabaseInit
{
    public static void InitializeTables()
    {
        try
        {
            var sqlite = CodeLogic.Libraries.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite == null) return;

            var repository = sqlite.GetRepository<FirewallAliasEntity>();
            
            // Table will be created automatically by Repository on first use
            // But we can ensure it exists by trying to query it
            var queryBuilder = sqlite.GetQueryBuilder<FirewallAliasEntity>();
            _ = queryBuilder.Select(e => e).Take(1).ToListAsync().Result;
            
            Console.WriteLine("✓ Firewall Aliases table initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize Aliases table: {ex.Message}");
        }
    }
}
