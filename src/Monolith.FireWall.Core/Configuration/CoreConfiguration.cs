using System.ComponentModel.DataAnnotations;
using CodeLogic.Core.Configuration;

namespace Monolith.FireWall.Core.Configuration;

[ConfigSection("core")]
public class CoreConfiguration : ConfigModelBase
{
    [Required]
    public string Version { get; set; } = "1.0.0";

    [Required]
    public string PackagesDirectory { get; set; } = "/opt/monolith-firewall/packages";

    [Required]
    public string PipeName { get; set; } = "monolith-core";

    [Required]
    public string SocketPath { get; set; } = "/var/lib/monolith-firewall/run/monolith-core.sock";

    [Required]
    public string PlatformPolicyPath { get; set; } = "/etc/monolith-firewall/platform-policy.json";

    [Range(1, 1000)]
    public int MaxConcurrentConnections { get; set; } = 10;

    public bool EnableDebugMode { get; set; } = false;

    /// <summary>
    /// Directory where log files are stored
    /// Default: /var/log/monolith-firewall
    /// </summary>
    public string LogDirectory { get; set; } = "/var/log/monolith-firewall";

    public DatabaseConfig Database { get; set; } = new();
}

public class DatabaseConfig
{
    [Required]
    public string Path { get; set; } = "/var/lib/monolith-firewall/data/core.db";

    [Range(1, 300)]
    public uint ConnectionTimeoutSeconds { get; set; } = 30;

    [Range(1, 100)]
    public int MaxPoolSize { get; set; } = 10;

    public bool UseWAL { get; set; } = true;
}
