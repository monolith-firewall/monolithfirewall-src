namespace Monolith.FireWall.Common.Attributes;

/// <summary>
/// Defines a systemd service managed by this module.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class SystemdServiceAttribute : Attribute
{
    public string Name { get; }
    public string Unit { get; }
    public string[]? RequiredCapabilities { get; set; }

    public SystemdServiceAttribute(string name, string unit)
    {
        Name = name;
        Unit = unit;
    }
}

/// <summary>
/// Defines a service binding (port/protocol) for a module's service.
/// Used to display what ports/protocols the service listens on.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class ServiceBindingAttribute : Attribute
{
    /// <summary>
    /// The port number the service listens on.
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// The protocol (udp, tcp, or udp/tcp).
    /// </summary>
    public string Protocol { get; }

    /// <summary>
    /// The interface role this binding applies to (e.g., "lan", "wan", "any").
    /// </summary>
    public string InterfaceRole { get; set; } = "lan";

    /// <summary>
    /// Address family (ipv4, ipv6, or both).
    /// </summary>
    public string AddressFamily { get; set; } = "ipv4";

    /// <summary>
    /// Description of what this binding is for.
    /// </summary>
    public string? Description { get; set; }

    public ServiceBindingAttribute(int port, string protocol)
    {
        Port = port;
        Protocol = protocol;
    }
}

/// <summary>
/// Defines an APT package dependency.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class AptDependencyAttribute : Attribute
{
    public string PackageName { get; }
    public string? MinVersion { get; set; }
    public string? Description { get; set; }

    public AptDependencyAttribute(string packageName)
    {
        PackageName = packageName;
    }
}
