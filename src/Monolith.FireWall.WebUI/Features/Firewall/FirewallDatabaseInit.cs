using Monolith.FireWall.WebUI.Features.Firewall.Aliases;
using Monolith.FireWall.WebUI.Features.Firewall.Nat;
using Monolith.FireWall.WebUI.Features.Firewall.VirtualIps;
using Monolith.FireWall.WebUI.Features.Firewall.TrafficShaper;
using Monolith.FireWall.WebUI.Features.Firewall.Schedules;

namespace Monolith.FireWall.WebUI.Features.Firewall;

public static class FirewallDatabaseInit
{
    public static void InitializeAll()
    {
        try
        {
            AliasesDatabaseInit.InitializeTables();
            // TODO: Initialize other module tables when DatabaseInit classes are created
            // NatDatabaseInit.InitializeTables();
            // VirtualIpsDatabaseInit.InitializeTables();
            // TrafficShaperDatabaseInit.InitializeTables();
            // SchedulesDatabaseInit.InitializeTables();
            
            Console.WriteLine("✓ Firewall database tables initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize Firewall database: {ex.Message}");
        }
    }
}
