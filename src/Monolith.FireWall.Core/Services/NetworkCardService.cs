using System.Text.RegularExpressions;
using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Platform.Models;

namespace Monolith.FireWall.Core.Services;

public sealed class NetworkCardService
{
    private readonly PlatformCommandRunner _commandRunner;
    private readonly NetworkInventoryService _networkInventory;

    public NetworkCardService(PlatformCommandRunner commandRunner, NetworkInventoryService networkInventory)
    {
        _commandRunner = commandRunner;
        _networkInventory = networkInventory;
    }

    public async Task<List<PciDeviceInfo>> GetPciDevicesAsync(CancellationToken cancellationToken)
    {
        var devices = new List<PciDeviceInfo>();
        
        if (!_commandRunner.CommandExists("lspci"))
        {
            return devices;
        }

        try
        {
            // Get network controller devices (class 02)
            var command = new PlatformCommand
            {
                FileName = "lspci",
                Arguments = "-vmm -d ::0200",
                UseSudo = false,
                TimeoutMs = 5000
            };

            var result = await _commandRunner.RunAsync(command, cancellationToken);
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StdOut))
            {
                return devices;
            }

            // Parse lspci output (machine-readable format)
            var lines = result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            PciDeviceInfo? currentDevice = null;

            foreach (var line in lines)
            {
                if (line.StartsWith("Slot:"))
                {
                    if (currentDevice != null)
                    {
                        devices.Add(currentDevice);
                    }
                    currentDevice = new PciDeviceInfo
                    {
                        Slot = line.Substring(5).Trim()
                    };
                }
                else if (currentDevice != null)
                {
                    var parts = line.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();

                        switch (key)
                        {
                            case "Class":
                                currentDevice.Class = value;
                                break;
                            case "Vendor":
                                currentDevice.Vendor = value;
                                break;
                            case "Device":
                                currentDevice.Device = value;
                                break;
                            case "SVendor":
                                currentDevice.SubsystemVendor = value;
                                break;
                            case "SDevice":
                                currentDevice.SubsystemDevice = value;
                                break;
                        }
                    }
                }
            }

            if (currentDevice != null)
            {
                devices.Add(currentDevice);
            }

            // Map PCI devices to network interfaces
            await MapPciToInterfacesAsync(devices, cancellationToken);
        }
        catch (Exception)
        {
            // Return empty list on error
        }

        return devices;
    }

    private async Task MapPciToInterfacesAsync(List<PciDeviceInfo> devices, CancellationToken cancellationToken)
    {
        var interfaces = await _networkInventory.ListInterfacesAsync();
        
        foreach (var device in devices)
        {
            // Find interface by checking /sys/class/net/*/device symlink
            var pciSlot = device.Slot.Replace(":", @"\:");
            var devicePath = $"/sys/bus/pci/devices/{device.Slot}/net";

            if (Directory.Exists(devicePath))
            {
                var netDirs = Directory.GetDirectories(devicePath);
                if (netDirs.Length > 0)
                {
                    var ifaceName = Path.GetFileName(netDirs[0]);
                    if (interfaces.Any(i => i.Name == ifaceName))
                    {
                        device.Interface = ifaceName;
                    }
                }
            }
            else
            {
                // Fallback: try to find by checking each interface's device symlink
                foreach (var iface in interfaces)
                {
                    var deviceLink = $"/sys/class/net/{iface.Name}/device";
                    if (File.Exists(deviceLink) || Directory.Exists(deviceLink))
                    {
                        try
                        {
                            var linkTarget = Path.GetFullPath(deviceLink);
                            // Check if link target contains the PCI slot
                            var pciSlotNormalized = device.Slot.Replace(":", @"\:");
                            if (linkTarget.Contains(device.Slot) || linkTarget.Contains(pciSlotNormalized))
                            {
                                device.Interface = iface.Name;
                                break;
                            }
                        }
                        catch
                        {
                            // Ignore errors
                        }
                    }
                }
            }
        }
    }

    public async Task<List<NetworkCardInfo>> GetAllCardsAsync(CancellationToken cancellationToken)
    {
        var cards = new List<NetworkCardInfo>();
        var pciDevices = await GetPciDevicesAsync(cancellationToken);
        var interfaces = await _networkInventory.ListInterfacesAsync();

        // Get cards from PCI devices
        foreach (var pciDevice in pciDevices)
        {
            if (!string.IsNullOrWhiteSpace(pciDevice.Interface))
            {
                var cardInfo = await GetCardInfoAsync(pciDevice.Interface, cancellationToken);
            if (cardInfo != null)
            {
                cardInfo.PciInfo = pciDevice;
                cards.Add(cardInfo);
            }
            }
        }

        // Also include interfaces that might not be detected via PCI (virtual interfaces, etc.)
        foreach (var iface in interfaces)
        {
            if (!cards.Any(c => c.Interface == iface.Name))
            {
                var cardInfo = await GetCardInfoAsync(iface.Name, cancellationToken);
                if (cardInfo != null)
                {
                    cards.Add(cardInfo);
                }
            }
        }

        return cards;
    }

    public async Task<NetworkCardInfo?> GetCardInfoAsync(string interfaceName, CancellationToken cancellationToken)
    {
        if (!_commandRunner.CommandExists("ethtool"))
        {
            return null;
        }

        try
        {
            // Get general info
            var infoCommand = new PlatformCommand
            {
                FileName = "ethtool",
                Arguments = interfaceName,
                UseSudo = false,
                TimeoutMs = 3000
            };

            var infoResult = await _commandRunner.RunAsync(infoCommand, cancellationToken);
            if (infoResult.ExitCode != 0)
            {
                return null;
            }

            // Get offload features
            var offloadCommand = new PlatformCommand
            {
                FileName = "ethtool",
                Arguments = $"-k {interfaceName}",
                UseSudo = false,
                TimeoutMs = 3000
            };

            var offloadResult = await _commandRunner.RunAsync(offloadCommand, cancellationToken);

            // Get ring buffers
            var bufferCommand = new PlatformCommand
            {
                FileName = "ethtool",
                Arguments = $"-g {interfaceName}",
                UseSudo = false,
                TimeoutMs = 3000
            };

            var bufferResult = await _commandRunner.RunAsync(bufferCommand, cancellationToken);

            var cardInfo = ParseEthtoolInfo(interfaceName, infoResult.StdOut ?? string.Empty);
            
            // Get MAC address from network inventory
            try
            {
                var interfaces = await _networkInventory.ListInterfacesAsync();
                var iface = interfaces.FirstOrDefault(i => i.Name == interfaceName);
                if (iface != null)
                {
                    cardInfo.MacAddress = iface.MacAddress;
                }
            }
            catch
            {
                // Ignore errors getting MAC address
            }
            
            if (offloadResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(offloadResult.StdOut))
            {
                cardInfo.Offloads = ParseOffloads(offloadResult.StdOut);
            }

            if (bufferResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(bufferResult.StdOut))
            {
                cardInfo.Buffers = ParseBuffers(bufferResult.StdOut);
            }

            return cardInfo;
        }
        catch
        {
            return null;
        }
    }

    private NetworkCardInfo ParseEthtoolInfo(string interfaceName, string output)
    {
        var cardInfo = new NetworkCardInfo
        {
            Interface = interfaceName
        };

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            // Parse key-value pairs
            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex <= 0)
                continue;

            var key = trimmed.Substring(0, colonIndex).Trim();
            var value = trimmed.Substring(colonIndex + 1).Trim();

            switch (key)
            {
                case "Settings for":
                    // Interface name, already set
                    break;
                case "Supported ports":
                    cardInfo.SupportedPorts = value;
                    break;
                case "Supported link modes":
                    cardInfo.SupportedLinkModes = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    break;
                case "Supported pause frame use":
                case "Supports auto-negotiation":
                    // Store in OtherSettings
                    cardInfo.OtherSettings[key] = value;
                    break;
                case "Advertised link modes":
                    cardInfo.AdvertisedLinkModes = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    break;
                case "Advertised pause frame use":
                case "Advertised auto-negotiation":
                    cardInfo.OtherSettings[key] = value;
                    break;
                case "Speed":
                    cardInfo.Speed = value;
                    break;
                case "Duplex":
                    cardInfo.Duplex = value;
                    break;
                case "Port":
                    cardInfo.Port = value;
                    break;
                case "PHYAD":
                    cardInfo.PhyAddress = value;
                    break;
                case "Transceiver":
                    cardInfo.Transceiver = value;
                    break;
                case "Auto-negotiation":
                    cardInfo.AutoNegotiation = value.ToLower();
                    break;
                case "Link detected":
                    cardInfo.LinkDetected = value.ToLower();
                    break;
                case "Supported FEC modes":
                    cardInfo.SupportedFecModes = value;
                    break;
                case "Advertised FEC modes":
                    cardInfo.AdvertisedFecModes = value;
                    break;
                default:
                    // Try to parse driver info
                    if (key.StartsWith("driver:", StringComparison.OrdinalIgnoreCase))
                    {
                        cardInfo.Driver = value;
                    }
                    else if (key.StartsWith("version:", StringComparison.OrdinalIgnoreCase))
                    {
                        cardInfo.Version = value;
                    }
                    else if (key.StartsWith("firmware-version:", StringComparison.OrdinalIgnoreCase))
                    {
                        cardInfo.FirmwareVersion = value;
                    }
                    else if (key.StartsWith("expansion-rom-version:", StringComparison.OrdinalIgnoreCase))
                    {
                        cardInfo.ExpansionRomVersion = value;
                    }
                    else if (key.StartsWith("bus-info:", StringComparison.OrdinalIgnoreCase))
                    {
                        cardInfo.BusInfo = value;
                    }
                    else
                    {
                        cardInfo.OtherSettings[key] = value;
                    }
                    break;
            }
        }

        return cardInfo;
    }

    private NetworkCardOffloads ParseOffloads(string output)
    {
        var offloads = new NetworkCardOffloads();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("Features for"))
                continue;

            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex <= 0)
                continue;

            var key = trimmed.Substring(0, colonIndex).Trim().ToLower().Replace("-", "").Replace("_", "");
            var value = trimmed.Substring(colonIndex + 1).Trim().ToLower();

            var isEnabled = value == "on" || value == "fixed" || value == "yes";

            // Map to properties
            switch (key)
            {
                case "tcpsegmentationoffload":
                case "tso":
                    offloads.Tso = isEnabled;
                    break;
                case "udpfragmentationoffload":
                case "ufo":
                    offloads.Ufo = isEnabled;
                    break;
                case "genericsegmentationoffload":
                case "gso":
                    offloads.Gso = isEnabled;
                    break;
                case "genericreceiveoffload":
                case "gro":
                    offloads.Gro = isEnabled;
                    break;
                case "largereceiveoffload":
                case "lro":
                    offloads.Lro = isEnabled;
                    break;
                case "rxvlanoffload":
                case "rxvlan":
                    offloads.Rxvlan = isEnabled;
                    break;
                case "txvlanoffload":
                case "txvlan":
                    offloads.Txvlan = isEnabled;
                    break;
                case "txchecksumming":
                    offloads.TxChecksumming = isEnabled;
                    break;
                case "rxchecksumming":
                    offloads.RxChecksumming = isEnabled;
                    break;
                case "txchecksumipv4":
                    offloads.TxChecksumIpv4 = isEnabled;
                    break;
                case "txchecksumipv6":
                    offloads.TxChecksumIpv6 = isEnabled;
                    break;
                case "txchecksumipgeneric":
                    offloads.TxChecksumIpGeneric = isEnabled;
                    break;
                case "txchecksumsctp":
                    offloads.TxChecksumSctp = isEnabled;
                    break;
                case "rxchecksumipv4":
                    offloads.RxChecksumIpv4 = isEnabled;
                    break;
                case "rxchecksumipv6":
                    offloads.RxChecksumIpv6 = isEnabled;
                    break;
                case "rxchecksumipgeneric":
                    offloads.RxChecksumIpGeneric = isEnabled;
                    break;
                case "rxchecksumsctp":
                    offloads.RxChecksumSctp = isEnabled;
                    break;
                case "scattergather":
                    offloads.ScatterGather = isEnabled;
                    break;
                case "txscattergather":
                    offloads.TxScatterGather = isEnabled;
                    break;
                case "txscattergatherfraglist":
                    offloads.TxScatterGatherFragList = isEnabled;
                    break;
                case "txscattergatheripv4":
                    offloads.TxScatterGatherIpv4 = isEnabled;
                    break;
                case "txscattergatheripv6":
                    offloads.TxScatterGatherIpv6 = isEnabled;
                    break;
                case "txnocachecopy":
                    offloads.TxNocacheCopy = isEnabled;
                    break;
                case "rxhashing":
                case "rxhash":
                    offloads.Rxhash = isEnabled;
                    break;
                case "rxall":
                    offloads.RxAll = isEnabled;
                    break;
                case "txvlanstaghwinsert":
                    offloads.TxvlanStagHwInsert = isEnabled;
                    break;
                case "rxvlanstagfilter":
                    offloads.RxvlanStagFilter = isEnabled;
                    break;
                case "rxvlanstaghwparse":
                    offloads.RxvlanStagHwParse = isEnabled;
                    break;
                case "rxudptunnelportoffload":
                    offloads.RxUdpTunnelPortOffload = isEnabled;
                    break;
                case "txudptunnelportoffload":
                    offloads.TxUdpTunnelPortOffload = isEnabled;
                    break;
            }
        }

        return offloads;
    }

    private NetworkCardBuffers ParseBuffers(string output)
    {
        var buffers = new NetworkCardBuffers();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("Ring parameters for"))
                continue;

            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex <= 0)
                continue;

            var key = trimmed.Substring(0, colonIndex).Trim().ToLower().Replace("-", "").Replace(" ", "");
            var value = trimmed.Substring(colonIndex + 1).Trim();

            // Extract numeric value (handle "Pre-set maximums:" format)
            var match = Regex.Match(value, @"(\d+)");
            if (!match.Success)
                continue;

            var intValue = int.Parse(match.Groups[1].Value);

            switch (key)
            {
                case "rxmini":
                    if (value.Contains("max"))
                        buffers.RxMiniMax = intValue;
                    else
                        buffers.RxMini = intValue;
                    break;
                case "rx":
                    if (value.Contains("max"))
                        buffers.RxMax = intValue;
                    else
                        buffers.Rx = intValue;
                    break;
                case "rxjumbo":
                    if (value.Contains("max"))
                        buffers.RxJumboMax = intValue;
                    else
                        buffers.RxJumbo = intValue;
                    break;
                case "tx":
                    if (value.Contains("max"))
                        buffers.TxMax = intValue;
                    else
                        buffers.Tx = intValue;
                    break;
            }
        }

        return buffers;
    }

    public async Task<bool> SetSpeedAsync(NetworkCardSpeedRequest request, CancellationToken cancellationToken)
    {
        if (!_commandRunner.CommandExists("ethtool"))
        {
            return false;
        }

        var args = new List<string> { "-s", request.Interface };

        if (request.AutoNegotiation.HasValue)
        {
            args.Add("autoneg");
            args.Add(request.AutoNegotiation.Value ? "on" : "off");
        }

        if (!string.IsNullOrWhiteSpace(request.Speed))
        {
            args.Add("speed");
            args.Add(request.Speed);
        }

        if (!string.IsNullOrWhiteSpace(request.Duplex))
        {
            args.Add("duplex");
            args.Add(request.Duplex);
        }

        if (args.Count <= 2)
        {
            return false; // No parameters to set
        }

        var command = new PlatformCommand
        {
            FileName = "ethtool",
            Arguments = string.Join(" ", args),
            UseSudo = true,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        return result.ExitCode == 0;
    }

    public async Task<bool> SetOffloadsAsync(NetworkCardOffloadRequest request, CancellationToken cancellationToken)
    {
        if (!_commandRunner.CommandExists("ethtool"))
        {
            return false;
        }

        if (request.Offloads.Count == 0)
        {
            return false;
        }

        var args = new List<string> { "-K", request.Interface };

        foreach (var offload in request.Offloads)
        {
            args.Add(offload.Key);
            args.Add(offload.Value ? "on" : "off");
        }

        var command = new PlatformCommand
        {
            FileName = "ethtool",
            Arguments = string.Join(" ", args),
            UseSudo = true,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        return result.ExitCode == 0;
    }

    public async Task<bool> SetBuffersAsync(NetworkCardBufferRequest request, CancellationToken cancellationToken)
    {
        if (!_commandRunner.CommandExists("ethtool"))
        {
            return false;
        }

        if (request.Buffers.Count == 0)
        {
            return false;
        }

        var args = new List<string> { "-G", request.Interface };

        foreach (var buffer in request.Buffers)
        {
            args.Add(buffer.Key.ToLower());
            args.Add(buffer.Value.ToString());
        }

        var command = new PlatformCommand
        {
            FileName = "ethtool",
            Arguments = string.Join(" ", args),
            UseSudo = true,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        return result.ExitCode == 0;
    }

    public async Task<bool> RevertToDefaultsAsync(string interfaceName, CancellationToken cancellationToken)
    {
        if (!_commandRunner.CommandExists("ethtool"))
        {
            return false;
        }

        // Re-enable auto-negotiation which typically resets to defaults
        var command = new PlatformCommand
        {
            FileName = "ethtool",
            Arguments = $"-s {interfaceName} autoneg on",
            UseSudo = true,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        return result.ExitCode == 0;
    }
}
