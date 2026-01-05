using System.CommandLine;
using System.Text.Json;
using Monolith.FireWall.CLI.Commands;
using Monolith.FireWall.CLI.Services;

namespace Monolith.FireWall.CLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Monolith FireWall CLI - Command-line management tool");

        // Package commands
        var packageCommand = new Command("package", "Package management");
        packageCommand.AddCommand(PackageCommands.CreateInstallCommand());
        packageCommand.AddCommand(PackageCommands.CreateListCommand());
        packageCommand.AddCommand(PackageCommands.CreateRemoveCommand());
        packageCommand.AddCommand(PackageCommands.CreateInfoCommand());
        rootCommand.AddCommand(packageCommand);

        // Firewall commands
        var firewallCommand = new Command("firewall", "Firewall management");
        firewallCommand.AddCommand(FirewallCommands.CreateRulesCommand());
        firewallCommand.AddCommand(FirewallCommands.CreateStatusCommand());
        firewallCommand.AddCommand(FirewallCommands.CreateApplyCommand());
        rootCommand.AddCommand(firewallCommand);

        // Interface commands
        var interfaceCommand = new Command("interface", "Network interface management");
        interfaceCommand.AddCommand(InterfaceCommands.CreateListCommand());
        interfaceCommand.AddCommand(InterfaceCommands.CreateAssignCommand());
        rootCommand.AddCommand(interfaceCommand);

        // Route commands
        var routeCommand = new Command("route", "Routing management");
        routeCommand.AddCommand(RouteCommands.CreateListCommand());
        rootCommand.AddCommand(routeCommand);

        // System commands
        var systemCommand = new Command("system", "System management");
        systemCommand.AddCommand(SystemCommands.CreateServiceCommand());
        rootCommand.AddCommand(systemCommand);

        // Status command (root level shortcut)
        rootCommand.AddCommand(SystemCommands.CreateStatusCommand());

        return await rootCommand.InvokeAsync(args);
    }
}
