using System.CommandLine;
using System.Text.Json;
using Monolith.FireWall.CLI.Services;

namespace Monolith.FireWall.CLI.Commands;

public static class SystemCommands
{
    public static Command CreateStatusCommand()
    {
        var command = new Command("status", "Show system status");

        command.SetHandler(async () =>
        {
            var client = new CoreApiClient();
            
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine("  Monolith FireWall - System Status");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine();

            // Check if Core service is available
            if (!client.IsAvailable())
            {
                Console.WriteLine("Core Service: ✗ Not running");
                Console.WriteLine();
                Console.WriteLine("Start the service with:");
                Console.WriteLine("  sudo systemctl start monolith-firewall-core");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine("Core Service: ✓ Running");
            Console.WriteLine();

            // Get system metadata
            try
            {
                var request = new { action = "system.metadata" };
                var requestJson = JsonSerializer.Serialize(request);
                var responseJson = await client.SendRequestAsync(requestJson);
                var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

                if (response.TryGetProperty("Success", out var success) && success.GetBoolean())
                {
                    if (response.TryGetProperty("Data", out var data))
                    {
                        Console.WriteLine("System Information:");
                        Console.WriteLine();

                        if (data.TryGetProperty("Version", out var version))
                            Console.WriteLine($"  Version: {version.GetString()}");
                        if (data.TryGetProperty("Uptime", out var uptime))
                            Console.WriteLine($"  Uptime: {uptime.GetString()}");
                        if (data.TryGetProperty("PackagesLoaded", out var packages))
                            Console.WriteLine($"  Packages Loaded: {packages.GetInt32()}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not retrieve system information: {ex.Message}");
            }

            // Check service status
            Console.WriteLine();
            Console.WriteLine("Service Status:");
            Console.WriteLine();

            var services = new[] { "monolith-firewall-core", "monolith-firewall-webui" };
            foreach (var service in services)
            {
                try
                {
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "systemctl",
                            Arguments = $"is-active {service}",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    process.Start();
                    var output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    
                    var status = output.Trim() == "active" ? "✓ Running" : "✗ Stopped";
                    Console.WriteLine($"  {service}: {status}");
                }
                catch
                {
                    Console.WriteLine($"  {service}: ? Unknown");
                }
            }

            Console.WriteLine();
        });

        return command;
    }

    public static Command CreateServiceCommand()
    {
        var actionArgument = new Argument<string>("action", "Action: start, stop, restart, status");
        var serviceArgument = new Argument<string>("service", "Service: core, webui, or both");

        var command = new Command("service", "Manage services");
        command.AddArgument(actionArgument);
        command.AddArgument(serviceArgument);

        command.SetHandler((string action, string service) =>
        {
            var services = new List<string>();
            
            if (service == "both" || service == "all")
            {
                services.Add("monolith-firewall-core");
                services.Add("monolith-firewall-webui");
            }
            else if (service == "core")
            {
                services.Add("monolith-firewall-core");
            }
            else if (service == "webui")
            {
                services.Add("monolith-firewall-webui");
            }
            else
            {
                Console.WriteLine($"ERROR: Unknown service: {service}");
                Console.WriteLine("Use: core, webui, or both");
                Environment.Exit(1);
                return;
            }

            foreach (var svc in services)
            {
                try
                {
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "sudo",
                            Arguments = $"systemctl {action} {svc}",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        Console.WriteLine($"✓ {action} {svc}");
                    }
                    else
                    {
                        Console.WriteLine($"✗ Failed to {action} {svc}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: {ex.Message}");
                    Environment.Exit(1);
                }
            }
        }, actionArgument, serviceArgument);

        return command;
    }
}
