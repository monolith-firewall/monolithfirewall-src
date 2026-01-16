# Firewall States Backend Implementation Plan

## Current Status
- ✅ Basic structure in place (`FirewallStatesManager`)
- ✅ Uses `conntrack` command (primary method)
- ✅ Fallback to `/proc/net/nf_conntrack` (if conntrack unavailable)
- ⚠️ Parsing needs improvement for real-world data
- ⚠️ Missing interface information extraction
- ⚠️ Age calculation needs work
- ⚠️ Direction detection needs improvement

## System Context
- **Firewall System**: nftables (not iptables)
- **Connection Tracking**: netfilter conntrack (nf_conntrack)
- **Available Tools**: 
  - `conntrack` (if conntrack-tools package installed)
  - `/proc/net/nf_conntrack` (always available on Linux with netfilter)
  - `nft` command (for nftables, but doesn't show connection states directly)

## Data Requirements

### Required Fields (from FirewallStateView)
1. **Id**: Unique connection identifier
2. **Protocol**: TCP, UDP, ICMP, etc.
3. **SourceIp**: Source IP address
4. **SourcePort**: Source port (if applicable)
5. **DestIp**: Destination IP address
6. **DestPort**: Destination port (if applicable)
7. **State**: Connection state (ESTABLISHED, TIME_WAIT, etc.)
8. **Interface**: Network interface name
9. **Direction**: Inbound/Outbound
10. **Age**: Connection age in seconds
11. **PacketsIn**: Inbound packet count
12. **PacketsOut**: Outbound packet count
13. **BytesIn**: Inbound byte count
14. **BytesOut**: Outbound byte count

## Implementation Plan

### Phase 1: Improve conntrack Parsing (Priority: HIGH)

#### 1.1 Understand Actual Output Format
**Action**: Test `conntrack -L -o extended` output format
- Real output format may differ from assumptions
- Need to handle both IPv4 and IPv6
- Need to parse all fields correctly

**Expected Output Format**:
```
tcp      6 117 ESTABLISHED src=192.168.1.100 dst=8.8.8.8 sport=54321 dport=53 packets=120 bytes=15360 src=8.8.8.8 dst=192.168.1.100 sport=53 dport=54321 packets=115 bytes=14720 [ASSURED] mark=0 use=1
```

**Key Fields to Extract**:
- Protocol (first field)
- Timeout (second field) - used for age calculation
- State (third field)
- Source IP (`src=`)
- Destination IP (`dst=`)
- Source port (`sport=`)
- Destination port (`dport=`)
- Packets (first `packets=`) - outbound
- Bytes (first `bytes=`) - outbound
- Packets (second `packets=`) - inbound
- Bytes (second `bytes=`) - inbound
- Mark (`mark=`) - can contain interface info
- Zone (`zone=`) - if present, indicates interface

#### 1.2 Extract Interface Information
**Current Issue**: Interface is hardcoded as "unknown"

**Solutions**:
1. **Use conntrack zones** (if available):
   - `conntrack -L -o extended -z` shows zone information
   - Zone can map to interface

2. **Use netfilter mark**:
   - Check if mark contains interface information
   - May need to query nftables rules to map marks to interfaces

3. **Use /proc/net/nf_conntrack with interface field**:
   - `/proc/net/nf_conntrack` has an `iifname` field for input interface
   - `/proc/net/nf_conntrack` has an `oifname` field for output interface

4. **Best approach**: Use `/proc/net/nf_conntrack` for interface info
   - More reliable for interface information
   - Has explicit `iifname` and `oifname` fields

#### 1.3 Calculate Accurate Age
**Current Issue**: Age is set to 0

**Solution**:
- Use timeout value from conntrack output
- Calculate: `age = timeout_max - timeout_current`
- Or use timestamp from `/proc/net/nf_conntrack` if available
- Alternative: Track connection start time (complex, requires state tracking)

**Implementation**:
```csharp
// From conntrack: "tcp 6 117 ESTABLISHED"
// Second number (6) is protocol number
// Third number (117) is timeout in seconds
// Age = timeout (connection will expire in 117 seconds)
// But we want age since connection started, not time until expiry
```

**Better approach**: Use `/proc/net/nf_conntrack` which has timestamp
- Format includes `[timestamp=1234567890]` or similar
- Can calculate: `age = current_time - connection_start_time`

#### 1.4 Determine Direction
**Current Issue**: Direction is hardcoded as "out"

**Solution**:
- Check source IP against local interfaces
- If source IP is on a local interface → Outbound
- If destination IP is on a local interface → Inbound
- Use interface assignment information from `InterfaceAssignmentStore`
- Check if source is RFC1918 (private) and dest is public → Outbound
- Check if source is public and dest is RFC1918 → Inbound

### Phase 2: Improve /proc/net/nf_conntrack Parsing (Priority: MEDIUM)

#### 2.1 Parse /proc/net/nf_conntrack Format
**Format** (example):
```
ipv4     2 tcp      6 117 TIME_WAIT src=192.168.1.100 dst=8.8.8.8 sport=54321 dport=53 packets=120 bytes=15360 src=8.8.8.8 dst=192.168.1.100 sport=53 dport=54321 packets=115 bytes=14720 [ASSURED] mark=0 zone=0 use=1
```

**Key Differences from conntrack**:
- First field is address family (`ipv4` or `ipv6`)
- Has `zone=` field (interface zone)
- May have `iifname=` and `oifname=` fields
- Has timestamp information

#### 2.2 Extract All Fields Properly
- Parse address family (ipv4/ipv6)
- Extract interface names from `iifname` and `oifname`
- Extract zone information
- Parse timestamps for age calculation
- Handle both directions properly

### Phase 3: Handle Edge Cases (Priority: MEDIUM)

#### 3.1 ICMP Connections
- ICMP doesn't have ports
- Handle `type` and `code` instead
- Format: `icmp 1 30 ESTABLISHED src=... dst=... type=8 code=0 ...`

#### 3.2 IPv6 Connections
- Handle IPv6 addresses properly
- May need different parsing
- Check for `ipv6` address family

#### 3.3 Connection States
- Map netfilter states to user-friendly names:
  - `ESTABLISHED` → ESTABLISHED
  - `SYN_SENT` → SYN_SENT
  - `SYN_RECV` → SYN_RECV
  - `FIN_WAIT` → FIN_WAIT
  - `TIME_WAIT` → TIME_WAIT
  - `CLOSE` → CLOSED
  - `CLOSE_WAIT` → CLOSE_WAIT
  - `LAST_ACK` → LAST_ACK
  - `LISTEN` → LISTEN (for listening sockets)

### Phase 4: Performance Optimizations (Priority: LOW)

#### 4.1 Caching
- Cache connection states for 1-2 seconds
- Avoid excessive conntrack queries
- Use background refresh for auto-refresh feature

#### 4.2 Streaming for Large Datasets
- For systems with thousands of connections
- Consider streaming/chunked responses
- Implement virtual scrolling on frontend

#### 4.5 Filtering Optimization
- Apply filters early in the query process
- Use conntrack filtering options when possible:
  - `conntrack -L -p tcp` (filter by protocol)
  - `conntrack -L -s 192.168.1.1` (filter by source)
  - `conntrack -L -d 8.8.8.8` (filter by destination)

### Phase 5: Testing & Validation (Priority: HIGH)

#### 5.1 Test with Real Data
- Test on a system with active connections
- Verify all fields are populated correctly
- Check interface information accuracy
- Validate age calculations

#### 5.2 Test Edge Cases
- Empty connection table
- Very large connection tables (1000+)
- Mixed IPv4/IPv6
- ICMP connections
- Connections on different interfaces

#### 5.3 Test Fallbacks
- Test when `conntrack` command is not available
- Test `/proc/net/nf_conntrack` parsing
- Test error handling

## Implementation Steps

### Step 1: Improve conntrack Parsing
1. Update `ParseConntrackLine` to extract all fields correctly
2. Add interface extraction logic
3. Add age calculation
4. Add direction detection

### Step 2: Improve /proc/net/nf_conntrack Parsing
1. Create proper parser for `/proc/net/nf_conntrack` format
2. Extract interface names from `iifname`/`oifname`
3. Extract timestamps for age calculation
4. Handle address family (IPv4/IPv6)

### Step 3: Add Interface Assignment Lookup
1. Use `InterfaceAssignmentStore` to get interface information
2. Map IP addresses to interfaces
3. Determine direction based on interface roles (LAN/WAN)

### Step 4: Test and Refine
1. Test with real firewall states
2. Fix any parsing issues
3. Optimize performance if needed

## Dependencies

### Required System Tools
- **conntrack** (from conntrack-tools package) - Recommended
  - Install: `apt-get install conntrack` or `yum install conntrack-tools`
  - Provides: `conntrack` command
  - Better output format than raw /proc

- **/proc/net/nf_conntrack** - Always available
  - No installation needed
  - Requires root access to read
  - More detailed information (interfaces, timestamps)

### Optional Tools
- **ss** or **netstat** - For listening sockets (future enhancement)
- **nft** - Already available, but doesn't show connection states

## Code Changes Needed

### Files to Modify
1. `src/Monolith.FireWall.Core/Services/Firewall/FirewallStatesManager.cs`
   - Improve `ParseConntrackLine` method
   - Improve `ParseProcConntrackLine` method
   - Add interface extraction
   - Add age calculation
   - Add direction detection
   - Add interface assignment lookup

2. `src/Monolith.FireWall.Core/Services/Firewall/FirewallManager.cs`
   - Pass `InterfaceAssignmentStore` to `FirewallStatesManager` (if needed)

### New Methods Needed
- `ExtractInterfaceFromConntrack(string line)` - Extract interface info
- `CalculateAge(int timeout, DateTime? timestamp)` - Calculate connection age
- `DetermineDirection(string srcIp, string destIp, InterfaceAssignmentStore store)` - Determine direction
- `ParseProcConntrackLineDetailed(string line)` - Detailed /proc parser

## Testing Checklist

- [ ] Test with active TCP connections
- [ ] Test with active UDP connections
- [ ] Test with ICMP connections
- [ ] Test with IPv6 connections
- [ ] Test interface extraction
- [ ] Test age calculation accuracy
- [ ] Test direction detection
- [ ] Test filtering functionality
- [ ] Test with empty connection table
- [ ] Test with large connection table (1000+)
- [ ] Test fallback to /proc when conntrack unavailable
- [ ] Test kill connection functionality

## Success Criteria

1. ✅ All connection states display with correct data
2. ✅ Interface information is accurate
3. ✅ Age is calculated correctly (within reasonable accuracy)
4. ✅ Direction is determined correctly
5. ✅ Filtering works for all filter types
6. ✅ Performance is acceptable (< 2 seconds for 1000 connections)
7. ✅ Kill connection works correctly
8. ✅ Handles edge cases gracefully

## Future Enhancements

1. **Real-time Updates**: WebSocket for live connection state updates
2. **Connection History**: Track connection lifecycle
3. **Bandwidth Graphs**: Per-connection bandwidth visualization
4. **Connection Alerts**: Notify on suspicious connections
5. **Export Functionality**: Export filtered results to CSV/JSON
6. **Bulk Operations**: Kill multiple connections at once
7. **Connection Details**: Expandable rows with detailed packet info
