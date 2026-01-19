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
            // Find interface by checking /sys/bus/pci/devices/{device.Slot}/net
            // Try both formats: "00:12.0" and "0000:00:12.0"
            var devicePath = $"/sys/bus/pci/devices/{device.Slot}/net";
            var devicePathWithDomain = $"/sys/bus/pci/devices/0000:{device.Slot}/net";

            if (Directory.Exists(devicePath))
            {
                var netDirs = Directory.GetDirectories(devicePath);
                if (netDirs.Length > 0)
                {
                    var ifaceName = Path.GetFileName(netDirs[0]);
                    if (interfaces.Any(i => i.Name == ifaceName))
                    {
                        device.Interface = ifaceName;
                        continue;
                    }
                }
            }
            else if (Directory.Exists(devicePathWithDomain))
            {
                var netDirs = Directory.GetDirectories(devicePathWithDomain);
                if (netDirs.Length > 0)
                {
                    var ifaceName = Path.GetFileName(netDirs[0]);
                    if (interfaces.Any(i => i.Name == ifaceName))
                    {
                        device.Interface = ifaceName;
                        continue;
                    }
                }
            }
            
            // Fallback: try to find by checking each interface's device symlink
            foreach (var iface in interfaces)
            {
                var deviceLink = $"/sys/class/net/{iface.Name}/device";
                if (File.Exists(deviceLink) || Directory.Exists(deviceLink))
                {
                    try
                    {
                        // Read the symlink target - try multiple methods
                        string? linkTarget = null;
                        try
                        {
                            var linkInfo = new FileInfo(deviceLink);
                            // Try LinkTarget property (available in .NET Core 2.1+)
                            if (linkInfo.LinkTarget != null)
                            {
                                linkTarget = linkInfo.LinkTarget;
                            }
                        }
                        catch
                        {
                            // Fallback to reading the link directly
                        }
                        
                        if (string.IsNullOrEmpty(linkTarget))
                        {
                            // Try reading via Path.GetFullPath
                            linkTarget = Path.GetFullPath(deviceLink);
                        }
                        
                        if (!string.IsNullOrEmpty(linkTarget))
                        {
                            // Extract PCI slot from path (format: ../../pci0000:00/0000:00:12.0/...)
                            // or direct path like /sys/devices/pci0000:00/0000:00:12.0/...
                            // Try matching both "00:12.0" and "0000:00:12.0" formats
                            var normalizedDeviceSlot = NormalizePciSlot(device.Slot);
                            var domainDeviceSlot = $"0000:{device.Slot}";
                            
                            // Check for both formats in the link target
                            if (linkTarget.Contains(device.Slot) || 
                                linkTarget.Contains(normalizedDeviceSlot) ||
                                linkTarget.Contains(domainDeviceSlot))
                            {
                                device.Interface = iface.Name;
                                break;
                            }
                            
                            // Also try matching just the last part (e.g., "12.0")
                            var slotParts = device.Slot.Split(':');
                            if (slotParts.Length >= 2)
                            {
                                var lastPart = slotParts[slotParts.Length - 1]; // e.g., "12.0"
                                if (linkTarget.Contains(lastPart))
                                {
                                    // Double-check by verifying the full path structure
                                    var normalizedLink = NormalizePciSlot(linkTarget);
                                    if (normalizedLink.Contains(normalizedDeviceSlot) || linkTarget.Contains(domainDeviceSlot))
                                    {
                                        device.Interface = iface.Name;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Try alternative: check if the device directory name matches
                        try
                        {
                            var deviceDir = Path.GetDirectoryName(deviceLink);
                            if (deviceDir != null && deviceDir.Contains(device.Slot))
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

        // Get cards from PCI devices (these have vendor/device info)
        foreach (var pciDevice in pciDevices)
        {
            if (!string.IsNullOrWhiteSpace(pciDevice.Interface))
            {
                var cardInfo = await GetCardInfoAsync(pciDevice.Interface, cancellationToken);
                if (cardInfo != null)
                {
                    // Always set PCI info from lspci (has vendor/device) - preserve all lspci data
                    if (cardInfo.PciInfo == null)
                    {
                        cardInfo.PciInfo = pciDevice;
                    }
                    else
                    {
                        // Merge: keep lspci vendor/device, but preserve other info from ethtool
                        if (string.IsNullOrEmpty(cardInfo.PciInfo.Vendor) && !string.IsNullOrEmpty(pciDevice.Vendor))
                            cardInfo.PciInfo.Vendor = pciDevice.Vendor;
                        if (string.IsNullOrEmpty(cardInfo.PciInfo.Device) && !string.IsNullOrEmpty(pciDevice.Device))
                            cardInfo.PciInfo.Device = pciDevice.Device;
                        if (string.IsNullOrEmpty(cardInfo.PciInfo.Slot) && !string.IsNullOrEmpty(pciDevice.Slot))
                            cardInfo.PciInfo.Slot = pciDevice.Slot;
                        if (string.IsNullOrEmpty(cardInfo.PciInfo.Class) && !string.IsNullOrEmpty(pciDevice.Class))
                            cardInfo.PciInfo.Class = pciDevice.Class;
                        if (string.IsNullOrEmpty(cardInfo.PciInfo.SubsystemVendor) && !string.IsNullOrEmpty(pciDevice.SubsystemVendor))
                            cardInfo.PciInfo.SubsystemVendor = pciDevice.SubsystemVendor;
                        if (string.IsNullOrEmpty(cardInfo.PciInfo.SubsystemDevice) && !string.IsNullOrEmpty(pciDevice.SubsystemDevice))
                            cardInfo.PciInfo.SubsystemDevice = pciDevice.SubsystemDevice;
                    }
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
                    // Try to match by bus-info if PCI info wasn't set
                    if (cardInfo.PciInfo == null && !string.IsNullOrEmpty(cardInfo.BusInfo))
                    {
                        var busInfoSlot = cardInfo.BusInfo.Trim();
                        if (busInfoSlot.StartsWith("pci@", StringComparison.OrdinalIgnoreCase))
                        {
                            busInfoSlot = busInfoSlot.Substring(4).Trim();
                        }
                        var normalizedBusSlot = NormalizePciSlot(busInfoSlot);
                        
                        // Try to find matching PCI device
                        var matchingPci = pciDevices.FirstOrDefault(d => 
                            d.Slot == busInfoSlot || 
                            d.Slot == normalizedBusSlot ||
                            NormalizePciSlot(d.Slot) == normalizedBusSlot);
                        
                        if (matchingPci != null)
                        {
                            cardInfo.PciInfo = matchingPci;
                        }
                    }
                    
                    cards.Add(cardInfo);
                }
            }
        }

        return cards;
    }

    public async Task<NetworkCardInfo?> GetCardInfoAsync(string interfaceName, CancellationToken cancellationToken)
    {
        var ethtoolPath = GetEthtoolPath();
        if (string.IsNullOrEmpty(ethtoolPath))
        {
            return null;
        }

        try
        {
            // Get driver info first (ethtool -i) - this has better vendor/device info
            var driverInfoCommand = new PlatformCommand
            {
                FileName = ethtoolPath,
                Arguments = $"-i {interfaceName}",
                UseSudo = false,
                TimeoutMs = 3000
            };

            var driverInfoResult = await _commandRunner.RunAsync(driverInfoCommand, cancellationToken);

            // Get general info
            var infoCommand = new PlatformCommand
            {
                FileName = ethtoolPath,
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
                FileName = ethtoolPath,
                Arguments = $"-k {interfaceName}",
                UseSudo = false,
                TimeoutMs = 3000
            };

            var offloadResult = await _commandRunner.RunAsync(offloadCommand, cancellationToken);

            // Get ring buffers
            var bufferCommand = new PlatformCommand
            {
                FileName = ethtoolPath,
                Arguments = $"-g {interfaceName}",
                UseSudo = false,
                TimeoutMs = 3000
            };

            var bufferResult = await _commandRunner.RunAsync(bufferCommand, cancellationToken);

            // Get coalescing parameters
            var coalescingCommand = new PlatformCommand
            {
                FileName = ethtoolPath,
                Arguments = $"-c {interfaceName}",
                UseSudo = false,
                TimeoutMs = 3000
            };

            var coalescingResult = await _commandRunner.RunAsync(coalescingCommand, cancellationToken);

            // Get pause frame parameters
            var pauseCommand = new PlatformCommand
            {
                FileName = ethtoolPath,
                Arguments = $"-a {interfaceName}",
                UseSudo = false,
                TimeoutMs = 3000
            };

            var pauseResult = await _commandRunner.RunAsync(pauseCommand, cancellationToken);

            var cardInfo = ParseEthtoolInfo(interfaceName, infoResult.StdOut ?? string.Empty);
            
            // Parse driver info if available (has better vendor/device info)
            if (driverInfoResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(driverInfoResult.StdOut))
            {
                ParseEthtoolDriverInfo(cardInfo, driverInfoResult.StdOut);
            }
            
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

            // Try to get PCI info from bus-info if available
            if (cardInfo.PciInfo == null && !string.IsNullOrEmpty(cardInfo.BusInfo))
            {
                try
                {
                    // Extract PCI slot from bus-info (e.g., "0000:01:00.0" or "pci@0000:01:00.0")
                    var busInfo = cardInfo.BusInfo.Trim();
                    string pciSlot = busInfo;
                    
                    // Remove "pci@" prefix if present
                    if (busInfo.StartsWith("pci@", StringComparison.OrdinalIgnoreCase))
                    {
                        pciSlot = busInfo.Substring(4).Trim();
                    }
                    
                    // Normalize slot format: "0000:00:12.0" -> "00:12.0" (remove domain prefix)
                    var normalizedSlot = NormalizePciSlot(pciSlot);
                    
                    // Try to find this PCI device in our list (match both formats)
                    var pciDevices = await GetPciDevicesAsync(cancellationToken);
                    var pciDevice = pciDevices.FirstOrDefault(d => 
                        d.Slot == pciSlot || 
                        d.Slot == normalizedSlot ||
                        NormalizePciSlot(d.Slot) == normalizedSlot ||
                        d.Interface == interfaceName);
                    
                    if (pciDevice != null)
                    {
                        cardInfo.PciInfo = pciDevice;
                    }
                    else
                    {
                        // Create a basic PCI info from bus-info
                        cardInfo.PciInfo = new PciDeviceInfo
                        {
                            Slot = normalizedSlot,
                            Interface = interfaceName
                        };
                        // Try to get vendor/device from sysfs
                        await TryGetPciVendorDeviceFromSysfs(cardInfo.PciInfo, pciSlot);
                    }
                }
                catch
                {
                    // Ignore errors
                }
            }
            
            // Fallback: try to get PCI info from interface mapping
            if (cardInfo.PciInfo == null || string.IsNullOrEmpty(cardInfo.PciInfo.Vendor))
            {
                try
                {
                    var pciDevices = await GetPciDevicesAsync(cancellationToken);
                    var pciDevice = pciDevices.FirstOrDefault(d => d.Interface == interfaceName);
                    if (pciDevice != null)
                    {
                        if (cardInfo.PciInfo == null)
                        {
                            cardInfo.PciInfo = pciDevice;
                        }
                        else
                        {
                            // Merge info
                            if (string.IsNullOrEmpty(cardInfo.PciInfo.Vendor) && !string.IsNullOrEmpty(pciDevice.Vendor))
                                cardInfo.PciInfo.Vendor = pciDevice.Vendor;
                            if (string.IsNullOrEmpty(cardInfo.PciInfo.Device) && !string.IsNullOrEmpty(pciDevice.Device))
                                cardInfo.PciInfo.Device = pciDevice.Device;
                            if (string.IsNullOrEmpty(cardInfo.PciInfo.Slot) && !string.IsNullOrEmpty(pciDevice.Slot))
                                cardInfo.PciInfo.Slot = pciDevice.Slot;
                        }
                    }
                }
                catch
                {
                    // Ignore errors getting PCI info
                }
            }
            
            if (offloadResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(offloadResult.StdOut))
            {
                cardInfo.Offloads = ParseOffloads(offloadResult.StdOut);
            }

            if (bufferResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(bufferResult.StdOut))
            {
                cardInfo.Buffers = ParseBuffers(bufferResult.StdOut);
            }

            if (coalescingResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(coalescingResult.StdOut))
            {
                cardInfo.Coalescing = ParseCoalescing(coalescingResult.StdOut);
            }

            if (pauseResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(pauseResult.StdOut))
            {
                cardInfo.Pause = ParsePause(pauseResult.StdOut);
            }

            // Extract supported speeds from link modes
            cardInfo.SupportedSpeeds = ExtractSpeedsFromLinkModes(cardInfo.SupportedLinkModes);
            cardInfo.AdvertisedSpeeds = ExtractSpeedsFromLinkModes(cardInfo.AdvertisedLinkModes);

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

    private void ParseEthtoolDriverInfo(NetworkCardInfo cardInfo, string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex <= 0)
                continue;

            var key = trimmed.Substring(0, colonIndex).Trim();
            var value = trimmed.Substring(colonIndex + 1).Trim();

            if (key.StartsWith("driver", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(cardInfo.Driver))
                    cardInfo.Driver = value;
            }
            else if (key.StartsWith("version", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(cardInfo.Version))
                    cardInfo.Version = value;
            }
            else if (key.StartsWith("firmware-version", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(cardInfo.FirmwareVersion))
                    cardInfo.FirmwareVersion = value;
            }
            else if (key.StartsWith("bus-info", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(cardInfo.BusInfo))
                    cardInfo.BusInfo = value;
            }
        }
    }

    private string NormalizePciSlot(string slot)
    {
        if (string.IsNullOrEmpty(slot))
            return slot;
        
        // Normalize "0000:00:12.0" -> "00:12.0" (remove domain prefix)
        // Also handle "00:12.0" -> "00:12.0" (no change)
        var parts = slot.Split(':');
        if (parts.Length >= 3 && parts[0].Length == 4 && parts[0].All(char.IsDigit))
        {
            // Has domain prefix like "0000", remove it
            return $"{parts[1]}:{parts[2]}";
        }
        
        return slot;
    }

    private async Task TryGetPciVendorDeviceFromSysfs(PciDeviceInfo pciInfo, string pciSlot)
    {
        try
        {
            // Try to read vendor and device from sysfs
            var vendorPath = $"/sys/bus/pci/devices/{pciSlot}/vendor";
            var devicePath = $"/sys/bus/pci/devices/{pciSlot}/device";
            
            if (File.Exists(vendorPath))
            {
                var vendorId = await File.ReadAllTextAsync(vendorPath);
                vendorId = vendorId.Trim();
                // Convert hex to decimal and look up vendor name
                if (vendorId.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    vendorId = vendorId.Substring(2);
                }
                // For now, just store the ID - we could look it up from pci.ids database
                if (string.IsNullOrEmpty(pciInfo.Vendor))
                {
                    // Try to get vendor name from lspci if available
                    pciInfo.Vendor = "Unknown"; // Will be filled by lspci if available
                }
            }
            
            if (File.Exists(devicePath))
            {
                var deviceId = await File.ReadAllTextAsync(devicePath);
                deviceId = deviceId.Trim();
                if (deviceId.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    deviceId = deviceId.Substring(2);
                }
                if (string.IsNullOrEmpty(pciInfo.Device))
                {
                    pciInfo.Device = "Unknown"; // Will be filled by lspci if available
                }
            }
        }
        catch
        {
            // Ignore errors
        }
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
            var value = trimmed.Substring(colonIndex + 1).Trim();

            // Check if parameter is locked (contains "[fixed]" or similar)
            var isLocked = value.Contains("[fixed]", StringComparison.OrdinalIgnoreCase) ||
                          value.Contains("fixed", StringComparison.OrdinalIgnoreCase);

            var valueLower = value.ToLower();
            var isEnabled = valueLower == "on" || valueLower == "fixed" || valueLower == "yes" ||
                           valueLower.StartsWith("on", StringComparison.OrdinalIgnoreCase);

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
                    if (isLocked)
                        offloads.Locked[key] = true;
                    break;
            }
            
            // Track locked state for all offloads
            if (isLocked)
            {
                offloads.Locked[key] = true;
            }
        }

        return offloads;
    }

    private NetworkCardBuffers ParseBuffers(string output)
    {
        var buffers = new NetworkCardBuffers();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var isMaxSection = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            if (trimmed.StartsWith("Ring parameters for"))
                continue;

            // Check if we're in the max section
            if (trimmed.Contains("Pre-set maximums", StringComparison.OrdinalIgnoreCase) || 
                trimmed.Contains("maximums", StringComparison.OrdinalIgnoreCase))
            {
                isMaxSection = true;
                continue;
            }

            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex <= 0)
                continue;

            var key = trimmed.Substring(0, colonIndex).Trim().ToLower().Replace("-", "").Replace(" ", "");
            var value = trimmed.Substring(colonIndex + 1).Trim();

            // Check if parameter is locked (contains "[fixed]" or similar)
            var isLocked = value.Contains("[fixed]", StringComparison.OrdinalIgnoreCase) ||
                          value.Contains("fixed", StringComparison.OrdinalIgnoreCase);

            // Extract numeric value - handle both current and min/max
            var match = Regex.Match(value, @"(\d+)");
            if (!match.Success)
                continue;

            var intValue = int.Parse(match.Groups[1].Value);
            var isMin = value.Contains("min", StringComparison.OrdinalIgnoreCase) && !isMaxSection;

            switch (key)
            {
                case "rxmini":
                    if (isMaxSection)
                        buffers.RxMiniMax = intValue;
                    else if (isMin)
                        buffers.RxMiniMin = intValue;
                    else
                        buffers.RxMini = intValue;
                    if (isLocked)
                        buffers.Locked["rxmini"] = true;
                    break;
                case "rx":
                    if (isMaxSection)
                        buffers.RxMax = intValue;
                    else if (isMin)
                        buffers.RxMin = intValue;
                    else
                        buffers.Rx = intValue;
                    if (isLocked)
                        buffers.Locked["rx"] = true;
                    break;
                case "rxjumbo":
                    if (isMaxSection)
                        buffers.RxJumboMax = intValue;
                    else if (isMin)
                        buffers.RxJumboMin = intValue;
                    else
                        buffers.RxJumbo = intValue;
                    if (isLocked)
                        buffers.Locked["rxjumbo"] = true;
                    break;
                case "tx":
                    if (isMaxSection)
                        buffers.TxMax = intValue;
                    else if (isMin)
                        buffers.TxMin = intValue;
                    else
                        buffers.Tx = intValue;
                    if (isLocked)
                        buffers.Locked["tx"] = true;
                    break;
            }
        }

        return buffers;
    }

    private NetworkCardCoalescing ParseCoalescing(string output)
    {
        var coalescing = new NetworkCardCoalescing();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("Coalesce parameters for"))
                continue;

            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex <= 0)
                continue;

            var key = trimmed.Substring(0, colonIndex).Trim().ToLower().Replace("-", "").Replace("_", "").Replace(" ", "");
            var value = trimmed.Substring(colonIndex + 1).Trim();

            // Check if parameter is locked
            var isLocked = value.Contains("[fixed]", StringComparison.OrdinalIgnoreCase) ||
                          value.Contains("fixed", StringComparison.OrdinalIgnoreCase);

            switch (key)
            {
                case "adaptiverx":
                    if (bool.TryParse(value.Split(' ')[0], out var adaptiveRx))
                        coalescing.AdaptiveRx = adaptiveRx;
                    if (isLocked)
                        coalescing.Locked["adaptiverx"] = true;
                    break;
                case "adaptivetx":
                    if (bool.TryParse(value.Split(' ')[0], out var adaptiveTx))
                        coalescing.AdaptiveTx = adaptiveTx;
                    if (isLocked)
                        coalescing.Locked["adaptivetx"] = true;
                    break;
                case "rxusecs":
                    if (int.TryParse(value.Split(' ')[0], out var rxUsecs))
                        coalescing.RxUsecs = rxUsecs;
                    if (isLocked)
                        coalescing.Locked["rxusecs"] = true;
                    break;
                case "txusecs":
                    if (int.TryParse(value.Split(' ')[0], out var txUsecs))
                        coalescing.TxUsecs = txUsecs;
                    if (isLocked)
                        coalescing.Locked["txusecs"] = true;
                    break;
                case "rxframes":
                    if (int.TryParse(value.Split(' ')[0], out var rxFrames))
                        coalescing.RxFrames = rxFrames;
                    if (isLocked)
                        coalescing.Locked["rxframes"] = true;
                    break;
                case "txframes":
                    if (int.TryParse(value.Split(' ')[0], out var txFrames))
                        coalescing.TxFrames = txFrames;
                    if (isLocked)
                        coalescing.Locked["txframes"] = true;
                    break;
                case "rxusecsirq":
                    if (int.TryParse(value.Split(' ')[0], out var rxUsecsIrq))
                        coalescing.RxUsecsIrq = rxUsecsIrq;
                    if (isLocked)
                        coalescing.Locked["rxusecsirq"] = true;
                    break;
                case "rxframesirq":
                    if (int.TryParse(value.Split(' ')[0], out var rxFramesIrq))
                        coalescing.RxFramesIrq = rxFramesIrq;
                    if (isLocked)
                        coalescing.Locked["rxframesirq"] = true;
                    break;
                case "txusecsirq":
                    if (int.TryParse(value.Split(' ')[0], out var txUsecsIrq))
                        coalescing.TxUsecsIrq = txUsecsIrq;
                    if (isLocked)
                        coalescing.Locked["txusecsirq"] = true;
                    break;
                case "txframesirq":
                    if (int.TryParse(value.Split(' ')[0], out var txFramesIrq))
                        coalescing.TxFramesIrq = txFramesIrq;
                    if (isLocked)
                        coalescing.Locked["txframesirq"] = true;
                    break;
                case "statsblockusecs":
                    if (int.TryParse(value.Split(' ')[0], out var statsBlockUsecs))
                        coalescing.StatsBlockUsecs = statsBlockUsecs;
                    if (isLocked)
                        coalescing.Locked["statsblockusecs"] = true;
                    break;
                case "pktratelow":
                    if (int.TryParse(value.Split(' ')[0], out var pktRateLow))
                        coalescing.PktRateLow = pktRateLow;
                    if (isLocked)
                        coalescing.Locked["pktratelow"] = true;
                    break;
                case "rxusecslow":
                    if (int.TryParse(value.Split(' ')[0], out var rxUsecsLow))
                        coalescing.RxUsecsLow = rxUsecsLow;
                    if (isLocked)
                        coalescing.Locked["rxusecslow"] = true;
                    break;
                case "rxframeslow":
                    if (int.TryParse(value.Split(' ')[0], out var rxFramesLow))
                        coalescing.RxFramesLow = rxFramesLow;
                    if (isLocked)
                        coalescing.Locked["rxframeslow"] = true;
                    break;
                case "txusecslow":
                    if (int.TryParse(value.Split(' ')[0], out var txUsecsLow))
                        coalescing.TxUsecsLow = txUsecsLow;
                    if (isLocked)
                        coalescing.Locked["txusecslow"] = true;
                    break;
                case "txframeslow":
                    if (int.TryParse(value.Split(' ')[0], out var txFramesLow))
                        coalescing.TxFramesLow = txFramesLow;
                    if (isLocked)
                        coalescing.Locked["txframeslow"] = true;
                    break;
                case "pktratehigh":
                    if (int.TryParse(value.Split(' ')[0], out var pktRateHigh))
                        coalescing.PktRateHigh = pktRateHigh;
                    if (isLocked)
                        coalescing.Locked["pktratehigh"] = true;
                    break;
                case "rxusecshigh":
                    if (int.TryParse(value.Split(' ')[0], out var rxUsecsHigh))
                        coalescing.RxUsecsHigh = rxUsecsHigh;
                    if (isLocked)
                        coalescing.Locked["rxusecshigh"] = true;
                    break;
                case "rxframeshigh":
                    if (int.TryParse(value.Split(' ')[0], out var rxFramesHigh))
                        coalescing.RxFramesHigh = rxFramesHigh;
                    if (isLocked)
                        coalescing.Locked["rxframeshigh"] = true;
                    break;
                case "txusecshigh":
                    if (int.TryParse(value.Split(' ')[0], out var txUsecsHigh))
                        coalescing.TxUsecsHigh = txUsecsHigh;
                    if (isLocked)
                        coalescing.Locked["txusecshigh"] = true;
                    break;
                case "txframeshigh":
                    if (int.TryParse(value.Split(' ')[0], out var txFramesHigh))
                        coalescing.TxFramesHigh = txFramesHigh;
                    if (isLocked)
                        coalescing.Locked["txframeshigh"] = true;
                    break;
                case "sampleinterval":
                    if (int.TryParse(value.Split(' ')[0], out var sampleInterval))
                        coalescing.SampleInterval = sampleInterval;
                    if (isLocked)
                        coalescing.Locked["sampleinterval"] = true;
                    break;
            }
        }

        return coalescing;
    }

    private NetworkCardPause ParsePause(string output)
    {
        var pause = new NetworkCardPause();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("Pause parameters for"))
                continue;

            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex <= 0)
                continue;

            var key = trimmed.Substring(0, colonIndex).Trim().ToLower().Replace("-", "").Replace("_", "").Replace(" ", "");
            var value = trimmed.Substring(colonIndex + 1).Trim();

            // Check if parameter is locked
            var isLocked = value.Contains("[fixed]", StringComparison.OrdinalIgnoreCase) ||
                          value.Contains("fixed", StringComparison.OrdinalIgnoreCase);

            switch (key)
            {
                case "autoneg":
                    if (value.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                        pause.Autoneg = true;
                    else if (value.StartsWith("off", StringComparison.OrdinalIgnoreCase))
                        pause.Autoneg = false;
                    if (isLocked)
                        pause.Locked["autoneg"] = true;
                    break;
                case "rx":
                    if (value.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                        pause.Rx = true;
                    else if (value.StartsWith("off", StringComparison.OrdinalIgnoreCase))
                        pause.Rx = false;
                    if (isLocked)
                        pause.Locked["rx"] = true;
                    break;
                case "tx":
                    if (value.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                        pause.Tx = true;
                    else if (value.StartsWith("off", StringComparison.OrdinalIgnoreCase))
                        pause.Tx = false;
                    if (isLocked)
                        pause.Locked["tx"] = true;
                    break;
            }
        }

        return pause;
    }

    private List<string> ExtractSpeedsFromLinkModes(List<string> linkModes)
    {
        var speeds = new HashSet<string>();
        var speedRegex = new Regex(@"(\d+)base", RegexOptions.IgnoreCase);

        foreach (var mode in linkModes)
        {
            var matches = speedRegex.Matches(mode);
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    speeds.Add(match.Groups[1].Value);
                }
            }
        }

        return speeds.OrderByDescending(s => int.TryParse(s, out var speed) ? speed : 0).ToList();
    }

    public async Task<bool> SetSpeedAsync(NetworkCardSpeedRequest request, CancellationToken cancellationToken)
    {
        var ethtoolPath = GetEthtoolPath();
        if (string.IsNullOrEmpty(ethtoolPath))
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
            FileName = ethtoolPath,
            Arguments = string.Join(" ", args),
            UseSudo = true,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        return result.ExitCode == 0;
    }

    private string GetEthtoolPath()
    {
        if (_commandRunner.CommandExists("ethtool"))
        {
            return "ethtool";
        }
        if (File.Exists("/usr/sbin/ethtool"))
        {
            return "/usr/sbin/ethtool";
        }
        if (File.Exists("/sbin/ethtool"))
        {
            return "/sbin/ethtool";
        }
        return string.Empty;
    }

    public async Task<bool> SetOffloadsAsync(NetworkCardOffloadRequest request, CancellationToken cancellationToken)
    {
        var ethtoolPath = GetEthtoolPath();
        if (string.IsNullOrEmpty(ethtoolPath))
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
            FileName = ethtoolPath,
            Arguments = string.Join(" ", args),
            UseSudo = true,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        return result.ExitCode == 0;
    }

    public async Task<bool> SetBuffersAsync(NetworkCardBufferRequest request, CancellationToken cancellationToken)
    {
        var ethtoolPath = GetEthtoolPath();
        if (string.IsNullOrEmpty(ethtoolPath))
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
            FileName = ethtoolPath,
            Arguments = string.Join(" ", args),
            UseSudo = true,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        return result.ExitCode == 0;
    }

    public async Task<bool> SetCoalescingAsync(NetworkCardCoalescingRequest request, CancellationToken cancellationToken)
    {
        var ethtoolPath = GetEthtoolPath();
        if (string.IsNullOrEmpty(ethtoolPath))
        {
            return false;
        }

        if (request.Coalescing.Count == 0)
        {
            return false;
        }

        var args = new List<string> { "-C", request.Interface };

        foreach (var param in request.Coalescing)
        {
            args.Add(param.Key);
            if (param.Value is bool boolValue)
            {
                args.Add(boolValue ? "on" : "off");
            }
            else
            {
                args.Add(param.Value?.ToString() ?? "0");
            }
        }

        var command = new PlatformCommand
        {
            FileName = ethtoolPath,
            Arguments = string.Join(" ", args),
            UseSudo = true,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        return result.ExitCode == 0;
    }

    public async Task<bool> SetPauseAsync(NetworkCardPauseRequest request, CancellationToken cancellationToken)
    {
        var ethtoolPath = GetEthtoolPath();
        if (string.IsNullOrEmpty(ethtoolPath))
        {
            return false;
        }

        var args = new List<string> { "-A", request.Interface };

        if (request.Autoneg.HasValue)
        {
            args.Add("autoneg");
            args.Add(request.Autoneg.Value ? "on" : "off");
        }

        if (request.Rx.HasValue)
        {
            args.Add("rx");
            args.Add(request.Rx.Value ? "on" : "off");
        }

        if (request.Tx.HasValue)
        {
            args.Add("tx");
            args.Add(request.Tx.Value ? "on" : "off");
        }

        if (args.Count == 2) // Only interface, no parameters
        {
            return false;
        }

        var command = new PlatformCommand
        {
            FileName = ethtoolPath,
            Arguments = string.Join(" ", args),
            UseSudo = true,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        return result.ExitCode == 0;
    }

    public async Task<bool> RevertToDefaultsAsync(string interfaceName, CancellationToken cancellationToken)
    {
        var ethtoolPath = GetEthtoolPath();
        if (string.IsNullOrEmpty(ethtoolPath))
        {
            return false;
        }

        // Re-enable auto-negotiation which typically resets to defaults
        var command = new PlatformCommand
        {
            FileName = ethtoolPath,
            Arguments = $"-s {interfaceName} autoneg on",
            UseSudo = true,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        return result.ExitCode == 0;
    }
}
