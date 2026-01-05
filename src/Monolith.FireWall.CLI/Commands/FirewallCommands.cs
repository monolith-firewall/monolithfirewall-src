using System.CommandLine;
using System.Text.Json;
using Monolith.FireWall.CLI.Services;

namespace Monolith.FireWall.CLI.Commands;

public static class FirewallCommands
{
    public static Command CreateRulesCommand()
    {
        var listCommand = new Command("list", "List firewall rules");
        listCommand.SetHandler(async () =>
        {
            var client = new CoreApiClient();
            if (!client.IsAvailable())
            {
                Console.WriteLine("ERROR: Core service is not running");
                Environment.Exit(1);
                return;
            }

            var request = new { action = "firewall.rules.list" };

            try
            {
                var requestJson = JsonSerializer.Serialize(request);
                var responseJson = await client.SendRequestAsync(requestJson);
                var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

                if (response.TryGetProperty("Success", out var success) && success.GetBoolean())
                {
                    if (response.TryGetProperty("Data", out var data) && data.ValueKind == JsonValueKind.Array)
                    {
                        Console.WriteLine("Firewall Rules:");
                        Console.WriteLine();

                        foreach (var rule in data.EnumerateArray())
                        {
                            var id = rule.TryGetProperty("Id", out var idEl) ? idEl.GetString() : "unknown";
                            var iface = rule.TryGetProperty("Interface", out var ifEl) ? ifEl.GetString() : "any";
                            var direction = rule.TryGetProperty("Direction", out var dirEl) ? dirEl.GetString() : "unknown";
                            var action = rule.TryGetProperty("Action", out var actEl) ? actEl.GetString() : "unknown";
                            var enabled = rule.TryGetProperty("Enabled", out var enEl) ? enEl.GetBoolean() : false;

                            Console.WriteLine($"  [{id}] {direction} on {iface} -> {action} {(enabled ? "(enabled)" : "(disabled)")}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("No firewall rules configured");
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

        var command = new Command("rules", "Firewall rules management");
        command.AddCommand(listCommand);
        return command;
    }

    public static Command CreateStatusCommand()
    {
        var command = new Command("status", "Show firewall status");

        command.SetHandler(async () =>
        {
            var client = new CoreApiClient();
            if (!client.IsAvailable())
            {
                Console.WriteLine("ERROR: Core service is not running");
                Environment.Exit(1);
                return;
            }

            var request = new { action = "firewall.status" };

            try
            {
                var requestJson = JsonSerializer.Serialize(request);
                var responseJson = await client.SendRequestAsync(requestJson);
                var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

                if (response.TryGetProperty("Success", out var success) && success.GetBoolean())
                {
                    if (response.TryGetProperty("Data", out var data))
                    {
                        Console.WriteLine("Firewall Status:");
                        Console.WriteLine();

                        foreach (var prop in data.EnumerateObject())
                        {
                            var value = prop.Value.ValueKind == JsonValueKind.String 
                                ? prop.Value.GetString() 
                                : prop.Value.ToString();
                            Console.WriteLine($"  {prop.Name}: {value}");
                        }
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

    public static Command CreateApplyCommand()
    {
        var command = new Command("apply", "Apply firewall configuration");

        command.SetHandler(async () =>
        {
            var client = new CoreApiClient();
            if (!client.IsAvailable())
            {
                Console.WriteLine("ERROR: Core service is not running");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine("Applying firewall configuration...");

            var request = new { action = "firewall.apply" };

            try
            {
                var requestJson = JsonSerializer.Serialize(request);
                var responseJson = await client.SendRequestAsync(requestJson);
                var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

                if (response.TryGetProperty("Success", out var success) && success.GetBoolean())
                {
                    Console.WriteLine("✓ Firewall configuration applied successfully");
                }
                else
                {
                    var error = response.TryGetProperty("Error", out var err) ? err.GetString() : "Unknown error";
                    Console.WriteLine($"✗ Failed to apply firewall: {error}");
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
