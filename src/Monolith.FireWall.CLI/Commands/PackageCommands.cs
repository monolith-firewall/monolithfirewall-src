using System.CommandLine;
using System.Text.Json;
using Monolith.FireWall.CLI.Services;

namespace Monolith.FireWall.CLI.Commands;

public static class PackageCommands
{
    public static Command CreateInstallCommand()
    {
        var fileArgument = new Argument<FileInfo>("file", "Path to .mfwpkg file");
        var overwriteOption = new Option<bool>(new[] { "--overwrite", "-o" }, "Overwrite if package already installed");

        var command = new Command("install", "Install a package from .mfwpkg file");
        command.AddArgument(fileArgument);
        command.AddOption(overwriteOption);

        command.SetHandler(async (FileInfo file, bool overwrite) =>
        {
            if (!file.Exists)
            {
                Console.WriteLine($"ERROR: File not found: {file.FullName}");
                Environment.Exit(1);
                return;
            }

            if (!file.Name.EndsWith(".mfwpkg", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("ERROR: File must be a .mfwpkg package");
                Environment.Exit(1);
                return;
            }

            // Use longer timeout for package installation (10 minutes) since it may include deb installation
            var client = new CoreApiClient(timeoutMs: 600_000);
            if (!client.IsAvailable())
            {
                Console.WriteLine("ERROR: Core service is not running");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"Installing package: {file.Name}...");

            var request = new
            {
                action = "packages.install",
                payload = new
                {
                    packageId = (string?)null,
                    sourcePath = file.FullName,
                    overwrite
                }
            };

            try
            {
                var requestJson = JsonSerializer.Serialize(request);
                var responseJson = await client.SendRequestAsync(requestJson);
                var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

                if (response.TryGetProperty("Success", out var success) && success.GetBoolean())
                {
                    Console.WriteLine($"✓ Successfully installed: {file.Name}");
                    
                    if (response.TryGetProperty("Data", out var data))
                    {
                        if (data.TryGetProperty("RequiresRestart", out var restart) && restart.GetBoolean())
                        {
                            Console.WriteLine("⚠ Package requires Core service restart to take effect");
                        }
                    }
                }
                else
                {
                    var error = response.TryGetProperty("Error", out var err) ? err.GetString() : "Unknown error";
                    Console.WriteLine($"✗ Failed to install: {error}");
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Environment.Exit(1);
            }
        }, fileArgument, overwriteOption);

        return command;
    }

    public static Command CreateListCommand()
    {
        var command = new Command("list", "List installed packages");

        command.SetHandler(async () =>
        {
            var client = new CoreApiClient();
            if (!client.IsAvailable())
            {
                Console.WriteLine("ERROR: Core service is not running");
                Environment.Exit(1);
                return;
            }

            var request = new
            {
                action = "packages.list"
            };

            try
            {
                var requestJson = JsonSerializer.Serialize(request);
                var responseJson = await client.SendRequestAsync(requestJson);
                var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

                if (response.TryGetProperty("Success", out var success) && success.GetBoolean())
                {
                    if (response.TryGetProperty("Data", out var data) && data.ValueKind == JsonValueKind.Array)
                    {
                        Console.WriteLine("Installed packages:");
                        Console.WriteLine();

                        foreach (var pkg in data.EnumerateArray())
                        {
                            // Handle Id as either string or number (database ID vs package ID)
                            string id = "unknown";
                            if (pkg.TryGetProperty("Id", out var idEl))
                            {
                                id = idEl.ValueKind == JsonValueKind.String 
                                    ? idEl.GetString() ?? "unknown"
                                    : idEl.GetInt32().ToString();
                            }
                            
                            // PackageId is the actual package identifier
                            var packageId = pkg.TryGetProperty("PackageId", out var pkgIdEl) 
                                ? (pkgIdEl.ValueKind == JsonValueKind.String ? pkgIdEl.GetString() : pkgIdEl.ToString())
                                : id;
                            
                            var name = pkg.TryGetProperty("Name", out var nameEl) ? nameEl.GetString() : packageId;
                            var version = pkg.TryGetProperty("Version", out var verEl) ? verEl.GetString() : "unknown";
                            var state = pkg.TryGetProperty("State", out var stateEl) ? stateEl.GetString() : "unknown";

                            Console.WriteLine($"  {name} ({packageId})");
                            Console.WriteLine($"    Version: {version}");
                            Console.WriteLine($"    State: {state}");
                            Console.WriteLine();
                        }
                    }
                    else
                    {
                        Console.WriteLine("No packages installed");
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

    public static Command CreateRemoveCommand()
    {
        var packageIdArgument = new Argument<string>("package-id", "Package ID to remove");

        var command = new Command("remove", "Remove an installed package");
        command.AddArgument(packageIdArgument);

        command.SetHandler(async (string packageId) =>
        {
            var client = new CoreApiClient();
            if (!client.IsAvailable())
            {
                Console.WriteLine("ERROR: Core service is not running");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"Removing package: {packageId}...");

            var request = new
            {
                action = "packages.remove",
                payload = new
                {
                    packageId
                }
            };

            try
            {
                var requestJson = JsonSerializer.Serialize(request);
                var responseJson = await client.SendRequestAsync(requestJson);
                var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

                if (response.TryGetProperty("Success", out var success) && success.GetBoolean())
                {
                    Console.WriteLine($"✓ Successfully removed: {packageId}");
                }
                else
                {
                    var error = response.TryGetProperty("Error", out var err) ? err.GetString() : "Unknown error";
                    Console.WriteLine($"✗ Failed to remove: {error}");
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Environment.Exit(1);
            }
        }, packageIdArgument);

        return command;
    }

    public static Command CreateInfoCommand()
    {
        var packageIdArgument = new Argument<string>("package-id", "Package ID to show information for");

        var command = new Command("info", "Show package information");
        command.AddArgument(packageIdArgument);

        command.SetHandler(async (string packageId) =>
        {
            var client = new CoreApiClient();
            if (!client.IsAvailable())
            {
                Console.WriteLine("ERROR: Core service is not running");
                Environment.Exit(1);
                return;
            }

            var request = new
            {
                action = "packages.info",
                payload = new
                {
                    packageId
                }
            };

            try
            {
                var requestJson = JsonSerializer.Serialize(request);
                var responseJson = await client.SendRequestAsync(requestJson);
                var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

                if (response.TryGetProperty("Success", out var success) && success.GetBoolean())
                {
                    if (response.TryGetProperty("Data", out var data))
                    {
                        Console.WriteLine($"Package: {packageId}");
                        Console.WriteLine();

                        // Print all properties
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
        }, packageIdArgument);

        return command;
    }
}
