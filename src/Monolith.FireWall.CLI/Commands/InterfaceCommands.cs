using System.CommandLine;
using System.Text.Json;
using Monolith.FireWall.CLI.Services;

namespace Monolith.FireWall.CLI.Commands;

public static class InterfaceCommands
{
    public static Command CreateListCommand()
    {
        var command = new Command("list", "List network interfaces");

        command.SetHandler(async () =>
        {
            var client = new CoreApiClient();
            if (!client.IsAvailable())
            {
                Console.WriteLine("ERROR: Core service is not running");
                Environment.Exit(1);
                return;
            }

            var request = new { action = "interfaces.list" };

            try
            {
                var requestJson = JsonSerializer.Serialize(request);
                var responseJson = await client.SendRequestAsync(requestJson);
                var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

                if (response.TryGetProperty("Success", out var success) && success.GetBoolean())
                {
                    if (response.TryGetProperty("Data", out var data) && data.ValueKind == JsonValueKind.Array)
                    {
                        Console.WriteLine("Network Interfaces:");
                        Console.WriteLine();

                        foreach (var iface in data.EnumerateArray())
                        {
                            var name = iface.TryGetProperty("Name", out var nameEl) ? nameEl.GetString() : "unknown";
                            var role = iface.TryGetProperty("Role", out var roleEl) ? roleEl.GetString() : "unassigned";
                            var status = iface.TryGetProperty("Status", out var statEl) ? statEl.GetString() : "unknown";

                            Console.WriteLine($"  {name}");
                            Console.WriteLine($"    Role: {role}");
                            Console.WriteLine($"    Status: {status}");
                            Console.WriteLine();
                        }
                    }
                    else
                    {
                        Console.WriteLine("No interfaces found");
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

    public static Command CreateAssignCommand()
    {
        var ifaceArgument = new Argument<string>("interface", "Interface name");
        var roleArgument = new Argument<string>("role", "Role to assign (lan/wan/opt)");

        var command = new Command("assign", "Assign interface role");
        command.AddArgument(ifaceArgument);
        command.AddArgument(roleArgument);

        command.SetHandler(async (string iface, string role) =>
        {
            var client = new CoreApiClient();
            if (!client.IsAvailable())
            {
                Console.WriteLine("ERROR: Core service is not running");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"Assigning {iface} to role: {role}...");

            var request = new
            {
                action = "interfaces.assign",
                payload = new
                {
                    @interface = iface,
                    role = role.ToLower()
                }
            };

            try
            {
                var requestJson = JsonSerializer.Serialize(request);
                var responseJson = await client.SendRequestAsync(requestJson);
                var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

                if (response.TryGetProperty("Success", out var success) && success.GetBoolean())
                {
                    Console.WriteLine($"✓ Successfully assigned {iface} to {role}");
                }
                else
                {
                    var error = response.TryGetProperty("Error", out var err) ? err.GetString() : "Unknown error";
                    Console.WriteLine($"✗ Failed to assign interface: {error}");
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Environment.Exit(1);
            }
        }, ifaceArgument, roleArgument);

        return command;
    }
}
