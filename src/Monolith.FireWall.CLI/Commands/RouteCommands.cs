using System.CommandLine;
using System.Text.Json;
using Monolith.FireWall.CLI.Services;

namespace Monolith.FireWall.CLI.Commands;

public static class RouteCommands
{
    public static Command CreateListCommand()
    {
        var command = new Command("list", "List static routes");

        command.SetHandler(async () =>
        {
            var client = new CoreApiClient();
            if (!client.IsAvailable())
            {
                Console.WriteLine("ERROR: Core service is not running");
                Environment.Exit(1);
                return;
            }

            var request = new { action = "routing.list" };

            try
            {
                var requestJson = JsonSerializer.Serialize(request);
                var responseJson = await client.SendRequestAsync(requestJson);
                var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

                if (response.TryGetProperty("Success", out var success) && success.GetBoolean())
                {
                    if (response.TryGetProperty("Data", out var data) && data.ValueKind == JsonValueKind.Array)
                    {
                        Console.WriteLine("Static Routes:");
                        Console.WriteLine();

                        foreach (var route in data.EnumerateArray())
                        {
                            var network = route.TryGetProperty("Network", out var netEl) ? netEl.GetString() : "unknown";
                            var gateway = route.TryGetProperty("Gateway", out var gwEl) ? gwEl.GetString() : "unknown";
                            var iface = route.TryGetProperty("Interface", out var ifEl) ? ifEl.GetString() : "unknown";

                            Console.WriteLine($"  {network} via {gateway} on {iface}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("No static routes configured");
                    }
                }
                else
                {
                    var error = response.TryGetProperty("Error", out var err) ? err.GetString() : "Unknown error";
                    Console.WriteLine($"ERROR: {error}");
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Environment.Exit(1);
            }
        });

        return command;
    }
}
