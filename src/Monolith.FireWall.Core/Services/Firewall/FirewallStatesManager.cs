using System.Text.RegularExpressions;
using CodeLogic;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services;
using Monolith.FireWall.Core.Services.Platform;

namespace Monolith.FireWall.Core.Services.Firewall;

public sealed class FirewallStatesManager
{
    private readonly PlatformCommandRunner _commandRunner;
    private readonly ILogger _logger;
    private readonly InterfaceAssignmentStore? _interfaceStore;
    private int _parseFailures = 0;

    public FirewallStatesManager(PlatformCommandRunner commandRunner, ILogger logger, InterfaceAssignmentStore? interfaceStore = null)
    {
        _commandRunner = commandRunner;
        _logger = logger;
        _interfaceStore = interfaceStore;
    }

    public async Task<FirewallStatesListResponse> ListStatesAsync(FirewallStatesListRequest request, CancellationToken cancellationToken)
    {
        var states = await QueryStatesAsync(cancellationToken);
        
        _logger.LogInformation($"QueryStatesAsync returned {states.Count} total states before filtering");
        
        // Apply filters
        var filtered = ApplyFilters(states, request);
        
        _logger.LogInformation($"After filtering: {filtered.Count} states match the filters");
        
        // Calculate pagination
        var total = filtered.Count;
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Max(1, Math.Min(200, request.PageSize));
        var totalPages = total > 0 ? (int)Math.Ceiling(total / (double)pageSize) : 0;
        
        // Apply pagination
        var paginated = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        
        return new FirewallStatesListResponse
        {
            States = paginated,
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<bool> KillStateAsync(string stateId, CancellationToken cancellationToken)
    {
        try
        {
            // stateId format: protocol_srcIp_srcPort_destIp_destPort
            // Parse it to extract connection details
            var parts = stateId.Split('_');
            if (parts.Length < 5)
            {
                _logger.LogWarning($"Invalid state ID format: {stateId}");
                return false;
            }

            var protocol = parts[0];
            var srcIp = parts[1];
            var srcPort = parts[2];
            var destIp = parts[3];
            var destPort = parts[4];

            // Build conntrack delete command
            var arguments = $"-D -p {protocol} -s {srcIp} -d {destIp}";
            if (!string.IsNullOrEmpty(srcPort) && srcPort != "null")
            {
                arguments += $" --sport {srcPort}";
            }
            if (!string.IsNullOrEmpty(destPort) && destPort != "null")
            {
                arguments += $" --dport {destPort}";
            }

            var command = new PlatformCommand
            {
                FileName = "conntrack",
                Arguments = arguments,
                UseSudo = true,
                TimeoutMs = 5000
            };

            var result = await _commandRunner.RunAsync(command, cancellationToken);
            return result.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to kill connection state {stateId}");
            return false;
        }
    }

    private async Task<List<FirewallStateView>> QueryStatesAsync(CancellationToken cancellationToken)
    {
        var states = new List<FirewallStateView>();

        // Check if connection tracking module is loaded
        if (!IsConnectionTrackingAvailable())
        {
            _logger.LogInformation("Connection tracking not available. Falling back to socket connections from /proc/net/tcp and /proc/net/udp");
            // Fallback to socket connections (shows active connections, not firewall states)
            return await ReadSocketConnectionsAsync(cancellationToken);
        }

        // Always try /proc/net/nf_conntrack first as it has better interface information
        try
        {
            var procStates = await ReadProcNetConntrackAsync(cancellationToken);
            if (procStates.Count > 0)
            {
                _logger.LogDebug($"Found {procStates.Count} connection states from /proc/net/nf_conntrack");
                return procStates;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to read /proc/net/nf_conntrack: {ex.Message}");
        }

        // Fallback to conntrack if /proc didn't work
        if (_commandRunner.CommandExists("conntrack"))
        {
            try
            {
                var command = new PlatformCommand
                {
                    FileName = "conntrack",
                    Arguments = "-L -o extended",
                    UseSudo = true,
                    TimeoutMs = 10000
                };

                var result = await _commandRunner.RunAsync(command, cancellationToken);
                
                if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StdOut))
                {
                    states = ParseConntrackOutput(result.StdOut);
                    _logger.LogDebug($"Found {states.Count} connection states from conntrack");
                }
                else if (result.ExitCode != 0)
                {
                    _logger.LogWarning($"conntrack command failed with exit code {result.ExitCode}: {result.StdErr}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to query conntrack: {ex.Message}");
            }
        }
        else
        {
            _logger.LogInformation("conntrack command not found. Install conntrack-tools package for better connection state viewing.");
        }

        if (states.Count == 0)
        {
            _logger.LogInformation("No connection states found. This is normal if there are no active connections.");
        }

        return states;
    }

    private bool IsConnectionTrackingAvailable()
    {
        // Check if /proc/net/nf_conntrack exists
        if (File.Exists("/proc/net/nf_conntrack"))
        {
            return true;
        }

        // Check if the module is loaded by checking /proc/modules
        try
        {
            if (File.Exists("/proc/modules"))
            {
                var modules = File.ReadAllText("/proc/modules");
                if (modules.Contains("nf_conntrack", StringComparison.OrdinalIgnoreCase))
                {
                    // Module is loaded but /proc/net/nf_conntrack doesn't exist yet
                    // This can happen - the file appears when there are connections
                    return true;
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return false;
    }

    private async Task<List<FirewallStateView>> ReadSocketConnectionsAsync(CancellationToken cancellationToken)
    {
        var states = new List<FirewallStateView>();
        
        // Read TCP connections
        try
        {
            if (File.Exists("/proc/net/tcp"))
            {
                var tcpStates = await ParseSocketFileAsync("/proc/net/tcp", "tcp", cancellationToken);
                states.AddRange(tcpStates);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to read /proc/net/tcp: {ex.Message}");
        }

        // Read UDP connections
        try
        {
            if (File.Exists("/proc/net/udp"))
            {
                var udpStates = await ParseSocketFileAsync("/proc/net/udp", "udp", cancellationToken);
                states.AddRange(udpStates);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to read /proc/net/udp: {ex.Message}");
        }

        return states;
    }

    private async Task<List<FirewallStateView>> ParseSocketFileAsync(string filePath, string protocol, CancellationToken cancellationToken)
    {
        var states = new List<FirewallStateView>();
        
        // Format of /proc/net/tcp and /proc/net/udp:
        // sl  local_address rem_address   st tx_queue rx_queue tr tm->when retrnsmt   uid  timeout inode
        // 0: 0100007F:0019 00000000:0000 0A 00000000:00000000 00:00000000 00000000  1000        0 12345 2 0000000000000000 100 0 0 10 0
        // 
        // Fields:
        // - local_address: IP:port in hex (little-endian)
        // - rem_address: remote IP:port in hex
        // - st: socket state (0A = TCP_ESTABLISHED, 01 = TCP_ESTABLISHED, etc.)
        
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        
        // Skip header line
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4)
                {
                    continue;
                }

                // Parse local address (format: IP:port in hex, little-endian)
                var localAddr = ParseHexAddress(parts[1]);
                // Parse remote address
                var remoteAddr = ParseHexAddress(parts[2]);
                // Parse state
                var stateHex = parts[3];
                var state = ParseSocketState(stateHex, protocol);

                // Skip listening sockets (remote address is 0.0.0.0:0)
                if (remoteAddr.ip == "0.0.0.0" && remoteAddr.port == 0)
                {
                    continue;
                }

                // Determine direction (simplified - assume outbound if local port is high)
                var direction = localAddr.port > 32768 ? "out" : "in";

                var id = $"{protocol}_{localAddr.ip}_{localAddr.port}_{remoteAddr.ip}_{remoteAddr.port}";

                states.Add(new FirewallStateView
                {
                    Id = id,
                    Protocol = protocol,
                    SourceIp = localAddr.ip,
                    SourcePort = localAddr.port,
                    DestIp = remoteAddr.ip,
                    DestPort = remoteAddr.port,
                    State = state,
                    Interface = "unknown", // Socket connections don't have interface info
                    Direction = direction,
                    Age = 0, // Socket connections don't track age
                    PacketsIn = 0,
                    PacketsOut = 0,
                    BytesIn = 0,
                    BytesOut = 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Failed to parse socket line: {ex.Message}");
            }
        }

        return states;
    }

    private (string ip, int port) ParseHexAddress(string hexAddr)
    {
        // Format: "0100007F:0019" (IP in little-endian hex:port in hex)
        var parts = hexAddr.Split(':');
        if (parts.Length != 2)
        {
            return ("0.0.0.0", 0);
        }

        var ipHex = parts[0];
        var portHex = parts[1];

        // Parse IP (little-endian, 8 hex chars = 4 bytes)
        if (ipHex.Length == 8 && int.TryParse(ipHex, System.Globalization.NumberStyles.HexNumber, null, out var ipInt))
        {
            var bytes = BitConverter.GetBytes(ipInt);
            // Reverse because it's little-endian
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            var ip = $"{bytes[0]}.{bytes[1]}.{bytes[2]}.{bytes[3]}";
            
            // Parse port
            if (int.TryParse(portHex, System.Globalization.NumberStyles.HexNumber, null, out var port))
            {
                return (ip, port);
            }
        }

        return ("0.0.0.0", 0);
    }

    private string ParseSocketState(string stateHex, string protocol)
    {
        // TCP states (from /proc/net/tcp):
        // 01 = ESTABLISHED
        // 02 = SYN_SENT
        // 03 = SYN_RECV
        // 04 = FIN_WAIT1
        // 05 = FIN_WAIT2
        // 06 = TIME_WAIT
        // 07 = CLOSE
        // 08 = CLOSE_WAIT
        // 09 = LAST_ACK
        // 0A = LISTEN
        // 0B = CLOSING

        if (int.TryParse(stateHex, System.Globalization.NumberStyles.HexNumber, null, out var state))
        {
            return state switch
            {
                0x01 => "ESTABLISHED",
                0x02 => "SYN_SENT",
                0x03 => "SYN_RECV",
                0x04 => "FIN_WAIT1",
                0x05 => "FIN_WAIT2",
                0x06 => "TIME_WAIT",
                0x07 => "CLOSED",
                0x08 => "CLOSE_WAIT",
                0x09 => "LAST_ACK",
                0x0A => "LISTEN",
                0x0B => "CLOSING",
                _ => $"UNKNOWN({stateHex})"
            };
        }

        return "UNKNOWN";
    }

    private List<FirewallStateView> ParseConntrackOutput(string output)
    {
        var states = new List<FirewallStateView>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        _logger.LogDebug($"Parsing {lines.Length} lines from conntrack output");

        foreach (var line in lines)
        {
            try
            {
                // Skip header lines or empty lines
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("conntrack", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var state = ParseConntrackLine(line);
                if (state != null)
                {
                    states.Add(state);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Failed to parse conntrack line: {line.Substring(0, Math.Min(100, line.Length))}... - {ex.Message}");
            }
        }

        return states;
    }

    private FirewallStateView? ParseConntrackLine(string line)
    {
        // Example conntrack -L -o extended output:
        // tcp      6 117 ESTABLISHED src=192.168.1.100 dst=8.8.8.8 sport=54321 dport=53 packets=120 bytes=15360 src=8.8.8.8 dst=192.168.1.100 sport=53 dport=54321 packets=115 bytes=14720 [ASSURED] mark=0 use=1
        // OR from /proc/net/nf_conntrack:
        // ipv4     2 tcp      6 117 ESTABLISHED src=192.168.1.100 dst=8.8.8.8 sport=54321 dport=53 packets=120 bytes=15360 src=8.8.8.8 dst=192.168.1.100 sport=53 dport=54321 packets=115 bytes=14720 [ASSURED] mark=0 zone=0 use=1
        
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        // Handle /proc format which starts with ipv4/ipv6
        var trimmedLine = line.TrimStart();
        if (trimmedLine.StartsWith("ipv4", StringComparison.OrdinalIgnoreCase) || 
            trimmedLine.StartsWith("ipv6", StringComparison.OrdinalIgnoreCase))
        {
            // Skip address family and next field (usually "2")
            var parts = trimmedLine.Split(new[] { ' ', '\t' }, 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                trimmedLine = parts[2]; // Continue parsing from protocol
            }
        }

        // Use regex to parse the conntrack line more reliably
        // Try with space prefix first, then without (for different formats)
        var srcIpMatch = Regex.Match(line, @"(?:^|\s)src=([^\s]+)");
        var dstIpMatch = Regex.Match(line, @"(?:^|\s)dst=([^\s]+)");
        var sportMatch = Regex.Match(line, @"(?:^|\s)sport=(\d+)");
        var dportMatch = Regex.Match(line, @"(?:^|\s)dport=(\d+)");
        var packetsMatch = Regex.Matches(line, @"(?:^|\s)packets=(\d+)");
        var bytesMatch = Regex.Matches(line, @"(?:^|\s)bytes=(\d+)");
        
        // State is usually the word before "src="
        var stateMatch = Regex.Match(line, @"\s+(\w+)\s+(?:src=|\[)");
        
        // Protocol is first word (or after ipv4/ipv6)
        var protocolMatch = Regex.Match(trimmedLine, @"^(\w+)");
        
        // Timeout is usually the number before the state (format: "protocol num timeout STATE")
        var ageMatch = Regex.Match(line, @"\s+(\d+)\s+(\d+)\s+(\w+)\s+(?:src=|\[)");

        if (!protocolMatch.Success || !srcIpMatch.Success || !dstIpMatch.Success)
        {
            // Only log first few failures to avoid spam
            if (_parseFailures++ < 3)
            {
                _logger.LogDebug($"Failed to parse basic fields - protocol: {protocolMatch.Success}, srcIp: {srcIpMatch.Success}, dstIp: {dstIpMatch.Success}. Line sample: {line.Substring(0, Math.Min(150, line.Length))}");
            }
            return null;
        }
        
        // Reset failure counter on success
        _parseFailures = 0;

        var protocol = protocolMatch.Groups[1].Value.ToLowerInvariant();
        var srcIp = srcIpMatch.Groups[1].Value;
        var dstIp = dstIpMatch.Groups[1].Value;
        var state = stateMatch.Success ? stateMatch.Groups[1].Value.ToLowerInvariant() : "unknown";
        
        int? srcPort = null;
        int? dstPort = null;
        if (sportMatch.Success && int.TryParse(sportMatch.Groups[1].Value, out var sp))
        {
            srcPort = sp;
        }
        if (dportMatch.Success && int.TryParse(dportMatch.Groups[1].Value, out var dp))
        {
            dstPort = dp;
        }

        // Parse packets and bytes - first occurrence is original direction, second is reply
        long packetsOut = 0;
        long bytesOut = 0;
        long packetsIn = 0;
        long bytesIn = 0;
        
        if (packetsMatch.Count > 0 && long.TryParse(packetsMatch[0].Groups[1].Value, out var pkt1))
        {
            packetsOut = pkt1;
        }
        if (packetsMatch.Count > 1 && long.TryParse(packetsMatch[1].Groups[1].Value, out var pkt2))
        {
            packetsIn = pkt2;
        }
        
        if (bytesMatch.Count > 0 && long.TryParse(bytesMatch[0].Groups[1].Value, out var bytes1))
        {
            bytesOut = bytes1;
        }
        if (bytesMatch.Count > 1 && long.TryParse(bytesMatch[1].Groups[1].Value, out var bytes2))
        {
            bytesIn = bytes2;
        }

        // Extract timeout (time until connection expires, not age)
        // Format: "protocol protocolNum timeout STATE"
        // ageMatch groups: [0]=full match, [1]=protocolNum, [2]=timeout, [3]=state
        int timeout = 0;
        if (ageMatch.Success && ageMatch.Groups.Count >= 3)
        {
            if (int.TryParse(ageMatch.Groups[2].Value, out var timeoutVal))
            {
                timeout = timeoutVal;
            }
        }
        
        // Use timeout as approximate age (connections typically last longer than timeout shows)
        // This is a rough estimate - for accurate age, use /proc/net/nf_conntrack with timestamps
        int age = timeout;

        // Try to extract interface from zone or other fields
        var zoneMatch = Regex.Match(line, @"zone=(\d+)");
        var ifaceMatch = Regex.Match(line, @"mark=(\d+)");
        var iface = "unknown";
        
        // Note: Interface info is better extracted from /proc/net/nf_conntrack which has iifname/oifname

        // Determine direction - will be improved in ParseProcConntrackLineDetailed
        var direction = DetermineDirection(srcIp, dstIp, null);

        // Generate a unique ID from connection tuple
        var id = $"{protocol}_{srcIp}_{srcPort?.ToString() ?? "null"}_{dstIp}_{dstPort?.ToString() ?? "null"}";

        return new FirewallStateView
        {
            Id = id,
            Protocol = protocol,
            SourceIp = srcIp,
            SourcePort = srcPort,
            DestIp = dstIp,
            DestPort = dstPort,
            State = state,
            Interface = iface,
            Direction = direction,
            Age = age,
            PacketsIn = packetsIn,
            PacketsOut = packetsOut,
            BytesIn = bytesIn,
            BytesOut = bytesOut
        };
    }

    private async Task<List<FirewallStateView>> ReadProcNetConntrackAsync(CancellationToken cancellationToken)
    {
        var states = new List<FirewallStateView>();
        
        // Try both possible locations
        var procPaths = new[]
        {
            "/proc/net/nf_conntrack",
            "/proc/sys/net/netfilter/nf_conntrack" // Some systems use this
        };

        string? procPath = null;
        foreach (var path in procPaths)
        {
            if (File.Exists(path))
            {
                procPath = path;
                break;
            }
        }

        if (procPath == null)
        {
            _logger.LogDebug("/proc/net/nf_conntrack not found - connection tracking may not be enabled");
            return states;
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(procPath, cancellationToken);
            _logger.LogDebug($"Reading {lines.Length} lines from {procPath}");
            
            int parsedCount = 0;
            int skippedCount = 0;
            
            foreach (var line in lines)
            {
                try
                {
                    // Skip empty lines
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var state = ParseProcConntrackLine(line);
                    if (state != null)
                    {
                        states.Add(state);
                        parsedCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Failed to parse line from {procPath}: {ex.Message}");
                    skippedCount++;
                }
            }
            
            _logger.LogDebug($"Parsed {parsedCount} states, skipped {skippedCount} lines from {procPath}");
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning($"Permission denied reading {procPath} - may need root access");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to read {procPath}");
        }

        return states;
    }

    private FirewallStateView? ParseProcConntrackLine(string line)
    {
        // /proc/net/nf_conntrack format:
        // ipv4     2 tcp      6 117 TIME_WAIT src=192.168.1.100 dst=8.8.8.8 sport=54321 dport=53 packets=120 bytes=15360 src=8.8.8.8 dst=192.168.1.100 sport=53 dport=54321 packets=115 bytes=14720 [ASSURED] mark=0 zone=0 use=1
        // OR with interface info:
        // ipv4     2 tcp      6 117 ESTABLISHED src=192.168.1.100 dst=8.8.8.8 sport=54321 dport=53 packets=120 bytes=15360 src=8.8.8.8 dst=192.168.1.100 sport=53 dport=54321 packets=115 bytes=14720 [ASSURED] mark=0 zone=0 use=1 iifname=eth0 oifname=eth1
        
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        // First try to parse as /proc format (has address family prefix)
        var state = ParseProcConntrackLineDetailed(line);
        if (state != null)
        {
            return state;
        }

        // Fallback to conntrack format parsing
        return ParseConntrackLine(line);
    }

    private FirewallStateView? ParseProcConntrackLineDetailed(string line)
    {
        // Extract interface names from /proc/net/nf_conntrack
        var iifnameMatch = Regex.Match(line, @"iifname=([^\s]+)");
        var oifnameMatch = Regex.Match(line, @"oifname=([^\s]+)");
        
        // Extract timestamp if available (some systems have it)
        var timestampMatch = Regex.Match(line, @"stamp=(\d+)");
        
        // Parse the basic connection info using the conntrack parser
        var baseState = ParseConntrackLine(line);
        if (baseState == null)
        {
            return null;
        }

        // Extract interface information
        string? iface = null;
        if (iifnameMatch.Success)
        {
            iface = iifnameMatch.Groups[1].Value;
        }
        else if (oifnameMatch.Success)
        {
            iface = oifnameMatch.Groups[1].Value;
        }

        // Determine direction based on interfaces
        var direction = DetermineDirection(baseState.SourceIp, baseState.DestIp, iface);

        // Calculate age from timestamp if available
        int age = baseState.Age;
        if (timestampMatch.Success && long.TryParse(timestampMatch.Groups[1].Value, out var timestamp))
        {
            // Timestamp is in jiffies (typically 1/100th of a second on most systems)
            // Convert to seconds and calculate age
            var currentJiffies = Environment.TickCount64; // milliseconds since boot
            var ageJiffies = (currentJiffies / 10) - timestamp; // Convert ms to jiffies (approximate)
            age = (int)(ageJiffies / 100); // Convert jiffies to seconds
            if (age < 0) age = 0;
        }

        return new FirewallStateView
        {
            Id = baseState.Id,
            Protocol = baseState.Protocol,
            SourceIp = baseState.SourceIp,
            SourcePort = baseState.SourcePort,
            DestIp = baseState.DestIp,
            DestPort = baseState.DestPort,
            State = baseState.State,
            Interface = iface ?? "unknown",
            Direction = direction,
            Age = age,
            PacketsIn = baseState.PacketsIn,
            PacketsOut = baseState.PacketsOut,
            BytesIn = baseState.BytesIn,
            BytesOut = baseState.BytesOut
        };
    }

    private string DetermineDirection(string srcIp, string destIp, string? iface)
    {
        // If we have interface assignments, use them to determine direction
        if (_interfaceStore != null)
        {
            try
            {
                var assignments = _interfaceStore.GetAssignmentsAsync().GetAwaiter().GetResult();
                
                // Check if source IP is on a local interface
                var srcOnLocal = assignments.Any(a => 
                    !string.IsNullOrEmpty(a.IpAddress) && 
                    srcIp.StartsWith(a.IpAddress.Split('/')[0], StringComparison.OrdinalIgnoreCase));
                
                // Check if destination IP is on a local interface
                var destOnLocal = assignments.Any(a => 
                    !string.IsNullOrEmpty(a.IpAddress) && 
                    destIp.StartsWith(a.IpAddress.Split('/')[0], StringComparison.OrdinalIgnoreCase));

                // If source is local and dest is not, it's outbound
                if (srcOnLocal && !destOnLocal)
                {
                    return "out";
                }
                
                // If dest is local and source is not, it's inbound
                if (destOnLocal && !srcOnLocal)
                {
                    return "in";
                }

                // If both are local, check interface roles
                if (iface != null)
                {
                    var assignment = assignments.FirstOrDefault(a => 
                        a.InterfaceName.Equals(iface, StringComparison.OrdinalIgnoreCase));
                    
                    if (assignment != null)
                    {
                        // If interface is WAN, likely outbound
                        if (assignment.Role == InterfaceRole.Wan && srcOnLocal)
                        {
                            return "out";
                        }
                        // If interface is LAN and dest is not local, likely outbound
                        if (assignment.Role == InterfaceRole.Lan && !destOnLocal)
                        {
                            return "out";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to determine direction: {ex.Message}");
            }
        }

        // Fallback: Check if source is RFC1918 (private) and dest is public
        if (IsPrivateIp(srcIp) && !IsPrivateIp(destIp))
        {
            return "out";
        }
        
        if (!IsPrivateIp(srcIp) && IsPrivateIp(destIp))
        {
            return "in";
        }

        // Default to outbound
        return "out";
    }

    private static bool IsPrivateIp(string ip)
    {
        if (string.IsNullOrEmpty(ip))
        {
            return false;
        }

        // Check for IPv4 private ranges
        if (System.Net.IPAddress.TryParse(ip, out var addr))
        {
            var bytes = addr.GetAddressBytes();
            
            // 10.0.0.0/8
            if (bytes[0] == 10)
            {
                return true;
            }
            
            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            {
                return true;
            }
            
            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168)
            {
                return true;
            }
            
            // 127.0.0.0/8 (loopback)
            if (bytes[0] == 127)
            {
                return true;
            }
        }

        return false;
    }

    private List<FirewallStateView> ApplyFilters(List<FirewallStateView> states, FirewallStatesListRequest request)
    {
        var filtered = states.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(request.Protocol))
        {
            var protocol = request.Protocol.ToLowerInvariant();
            filtered = filtered.Where(s => s.Protocol.Equals(protocol, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.SourceIp))
        {
            var sourceIp = request.SourceIp.Trim();
            filtered = filtered.Where(s => s.SourceIp.Contains(sourceIp, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.DestIp))
        {
            var destIp = request.DestIp.Trim();
            filtered = filtered.Where(s => s.DestIp.Contains(destIp, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.SourcePort))
        {
            var sourcePort = request.SourcePort.Trim();
            if (int.TryParse(sourcePort, out var port))
            {
                filtered = filtered.Where(s => s.SourcePort == port);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.DestPort))
        {
            var destPort = request.DestPort.Trim();
            if (int.TryParse(destPort, out var port))
            {
                filtered = filtered.Where(s => s.DestPort == port);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.State))
        {
            var stateFilters = request.State.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToLowerInvariant())
                .ToHashSet();
            filtered = filtered.Where(s => stateFilters.Contains(s.State.ToLowerInvariant()));
        }

        if (!string.IsNullOrWhiteSpace(request.Interface))
        {
            var iface = request.Interface.Trim();
            filtered = filtered.Where(s => s.Interface.Contains(iface, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Direction))
        {
            var direction = request.Direction.ToLowerInvariant();
            filtered = filtered.Where(s => s.Direction.Equals(direction, StringComparison.OrdinalIgnoreCase));
        }

        if (request.MinAge.HasValue && request.MinAge.Value > 0)
        {
            filtered = filtered.Where(s => s.Age >= request.MinAge.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
            filtered = filtered.Where(s =>
                s.SourceIp.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.DestIp.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.Protocol.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.State.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (s.SourcePort.HasValue && s.SourcePort.Value.ToString().Contains(search)) ||
                (s.DestPort.HasValue && s.DestPort.Value.ToString().Contains(search))
            );
        }

        return filtered.ToList();
    }
}
