# States Page Implementation Plan

## Overview
Create a new "States" page under the Status menu that displays active firewall connection states (connection tracking table). This page will be similar to pfSense's States page but with improved UI, better filtering, and enhanced functionality.

## Goals
- Display active connection states from the firewall's connection tracking table
- Provide comprehensive filtering capabilities
- Real-time refresh functionality
- Better UX than pfSense with modern UI patterns
- Efficient data loading and display

## Architecture

### 1. Frontend Components

#### 1.1 Razor Page
**File**: `src/Monolith.FireWall.WebUI/Pages/Status/States.cshtml`
- Route: `/status/states`
- Minimal Razor page following the pattern of other Status pages
- Container div for JavaScript to render content

#### 1.2 JavaScript Module
**File**: `src/Monolith.FireWall.WebUI/wwwroot/js/pages/status.js` (extend existing)
- Add `renderStates()` method to the Status module
- Handle filtering, refresh, and data display
- Real-time updates with auto-refresh option

#### 1.3 CSS Styling
**File**: `src/Monolith.FireWall.WebUI/wwwroot/css/status.css` (create if needed)
- Styles for states table
- Filter panel styling
- Responsive design

### 2. Backend Components

#### 2.1 API Endpoint (WebUI)
**File**: `src/Monolith.FireWall.WebUI/Program.cs`
- Add `/api/firewall/states` endpoint
- Accepts query parameters for filtering
- Forwards requests to Core via Unix socket

#### 2.2 Core Handler
**File**: `src/Monolith.FireWall.Core/Transport/Handlers/FirewallHandler.cs` (extend)
- Add handler for `firewall.states.list` action
- Parse filter parameters from request

#### 2.3 States Service
**File**: `src/Monolith.FireWall.Core/Services/Firewall/FirewallStatesManager.cs` (new)
- Service to query connection tracking states
- Parse conntrack/nftables output
- Apply filters
- Return structured state data

#### 2.4 Models
**File**: `src/Monolith.FireWall.Core/Models/FirewallModels.cs` (extend)
- `FirewallStateView` - represents a connection state
- `FirewallStatesListRequest` - filter parameters
- `FirewallStatesListResponse` - response with states and metadata

## Implementation Details

### 3. Connection State Data Structure

Each state entry should include:
- **Protocol**: TCP, UDP, ICMP, etc.
- **Source IP**: Source address
- **Source Port**: Source port (if applicable)
- **Destination IP**: Destination address
- **Destination Port**: Destination port (if applicable)
- **State**: Connection state (ESTABLISHED, SYN_SENT, TIME_WAIT, etc.)
- **Interface**: Network interface
- **Direction**: Inbound/Outbound
- **Age**: Connection age/duration
- **Packets**: Packet count (in/out)
- **Bytes**: Byte count (in/out)
- **ID**: Connection tracking ID

### 4. Filter Options

The state filter panel should support:
- **Protocol Filter**: Dropdown (All, TCP, UDP, ICMP, Other)
- **Source IP**: Text input with IP address/range
- **Destination IP**: Text input with IP address/range
- **Source Port**: Text input (single port or range)
- **Destination Port**: Text input (single port or range)
- **State Filter**: Multi-select (ESTABLISHED, SYN_SENT, TIME_WAIT, etc.)
- **Interface Filter**: Dropdown (All interfaces + specific interfaces)
- **Direction**: Radio buttons (All, Inbound, Outbound)
- **Search**: General text search across all fields
- **Age Filter**: Slider or input (show connections older than X seconds)

### 5. UI Components

#### 5.1 Header Section
- Page title: "Firewall States"
- Refresh button (manual refresh)
- Auto-refresh toggle with interval selector (5s, 10s, 30s, 60s, Off)
- Connection count badge (total active states)

#### 5.2 Filter Panel
- Collapsible filter panel at the top
- All filter controls in a responsive grid
- "Clear Filters" button
- "Apply Filters" button (auto-applies on change)

#### 5.3 States Table
- Sortable columns
- Pagination (configurable page size: 25, 50, 100, 200)
- Virtual scrolling for performance (if > 1000 states)
- Color coding by state:
  - ESTABLISHED: Green
  - TIME_WAIT: Yellow
  - SYN_SENT: Blue
  - CLOSED/FIN_WAIT: Gray
- Expandable rows for detailed view
- Action buttons:
  - Kill connection (with confirmation)
  - View details (modal)

#### 5.4 Statistics Panel (Optional)
- Total connections
- Connections by protocol
- Top source IPs
- Top destination IPs
- Bandwidth usage

### 6. Technical Implementation

#### 6.1 Querying Connection States

**Option A: conntrack (nf_conntrack)**
- Use `conntrack -L` command
- Parse output
- Pros: Standard tool, well-documented
- Cons: Requires conntrack-tools package

**Option B: nftables**
- Use `nft list conntrack` or direct kernel access
- Parse nftables output
- Pros: Native to nftables (system uses nftables)
- Cons: Less standard output format

**Option C: /proc/net/nf_conntrack**
- Read directly from proc filesystem
- Parse binary/text format
- Pros: No external dependencies
- Cons: Requires parsing, may be slower

**Recommended**: Start with Option A (conntrack), fallback to Option C if conntrack not available.

#### 6.2 Data Flow

```
User Request → WebUI API → Core Unix Socket → FirewallStatesManager
                                                      ↓
                                            PlatformCommandRunner
                                                      ↓
                                            Execute conntrack command
                                                      ↓
                                            Parse output → Filter → Return
```

#### 6.3 Performance Considerations

- Cache states for short duration (1-2 seconds) to avoid excessive queries
- Implement pagination on backend for large result sets
- Use streaming/chunked responses for large datasets
- Debounce filter inputs to avoid excessive API calls
- Consider WebSocket for real-time updates (future enhancement)

### 7. Security Considerations

- Require authentication (handled by existing middleware)
- Validate filter inputs to prevent command injection
- Sanitize IP addresses and ports
- Rate limit state queries to prevent DoS
- Log state queries for audit trail

### 8. Error Handling

- Handle missing conntrack tool gracefully
- Show user-friendly error messages
- Fallback to alternative methods if primary fails
- Display connection errors clearly

## File Structure

```
src/Monolith.FireWall.WebUI/
├── Pages/
│   └── Status/
│       └── States.cshtml (NEW)
├── wwwroot/
│   ├── js/
│   │   └── pages/
│   │       └── status.js (MODIFY - add renderStates method)
│   └── css/
│       └── status.css (CREATE/MODIFY - add states styles)

src/Monolith.FireWall.Core/
├── Services/
│   └── Firewall/
│       └── FirewallStatesManager.cs (NEW)
├── Models/
│   └── FirewallModels.cs (MODIFY - add state models)
└── Transport/
    └── Handlers/
        └── FirewallHandler.cs (MODIFY - add states handler)

src/Monolith.FireWall.WebUI/
└── Program.cs (MODIFY - add /api/firewall/states endpoint)
```

## Implementation Steps

### Phase 1: Backend Foundation
1. Create `FirewallStatesManager` service
2. Implement conntrack querying
3. Add state models to `FirewallModels.cs`
4. Add handler in `FirewallHandler.cs`
5. Register handler in `UnixSocketListener.cs`

### Phase 2: API Layer
1. Add `/api/firewall/states` endpoint in `Program.cs`
2. Implement query parameter parsing
3. Add error handling

### Phase 3: Frontend - Basic Display
1. Create `States.cshtml` page
2. Add `renderStates()` method to `status.js`
3. Implement basic table display
4. Add refresh button functionality

### Phase 4: Frontend - Filtering
1. Create filter panel UI
2. Implement filter logic
3. Connect filters to API
4. Add clear filters functionality

### Phase 5: Frontend - Enhancements
1. Add sorting functionality
2. Implement pagination
3. Add auto-refresh
4. Add connection kill functionality
5. Style improvements

### Phase 6: Polish & Testing
1. Add loading states
2. Error message handling
3. Performance optimization
4. Responsive design testing
5. Cross-browser testing

## API Specification

### GET /api/firewall/states

**Query Parameters:**
- `protocol` (string, optional): Filter by protocol (tcp, udp, icmp)
- `sourceIp` (string, optional): Filter by source IP
- `destIp` (string, optional): Filter by destination IP
- `sourcePort` (string, optional): Filter by source port
- `destPort` (string, optional): Filter by destination port
- `state` (string, optional): Filter by connection state (comma-separated)
- `interface` (string, optional): Filter by interface name
- `direction` (string, optional): Filter by direction (in, out)
- `search` (string, optional): General text search
- `minAge` (int, optional): Minimum connection age in seconds
- `page` (int, optional): Page number (default: 1)
- `pageSize` (int, optional): Items per page (default: 50)

**Response:**
```json
{
  "Success": true,
  "Data": {
    "states": [
      {
        "id": "1234567890",
        "protocol": "tcp",
        "sourceIp": "192.168.1.100",
        "sourcePort": 54321,
        "destIp": "8.8.8.8",
        "destPort": 53,
        "state": "ESTABLISHED",
        "interface": "eth0",
        "direction": "out",
        "age": 45,
        "packetsIn": 120,
        "packetsOut": 115,
        "bytesIn": 15360,
        "bytesOut": 14720
      }
    ],
    "total": 1250,
    "page": 1,
    "pageSize": 50,
    "totalPages": 25
  },
  "Error": null
}
```

### POST /api/firewall/states/kill

**Request Body:**
```json
{
  "id": "1234567890"
}
```

**Response:**
```json
{
  "Success": true,
  "Data": { "killed": true },
  "Error": null
}
```

## Enhancements Over pfSense

1. **Better Filtering**: More filter options with better UI
2. **Real-time Updates**: Auto-refresh with configurable intervals
3. **Better Performance**: Virtual scrolling, pagination, caching
4. **Modern UI**: Clean, responsive design with better UX
5. **Statistics**: Additional statistics panel
6. **Search**: General text search across all fields
7. **Export**: Ability to export filtered results (future)
8. **Details View**: Expandable rows with detailed connection info

## Dependencies

- `conntrack` command-line tool (conntrack-tools package)
- Or direct access to `/proc/net/nf_conntrack`
- Platform command execution capabilities (already available)

## Testing Checklist

- [ ] Load states page successfully
- [ ] Display connection states correctly
- [ ] Filter by protocol works
- [ ] Filter by IP address works
- [ ] Filter by port works
- [ ] Filter by state works
- [ ] Filter by interface works
- [ ] Search functionality works
- [ ] Refresh button works
- [ ] Auto-refresh works
- [ ] Pagination works
- [ ] Sorting works
- [ ] Kill connection works
- [ ] Error handling works (missing conntrack)
- [ ] Performance with large datasets
- [ ] Responsive design on mobile
- [ ] Security (input validation)

## Future Enhancements

1. WebSocket for real-time updates
2. Export to CSV/JSON
3. Connection history/logging
4. Bandwidth graphs per connection
5. Connection alerts/notifications
6. Bulk kill operations
7. Save filter presets
8. Connection details modal with packet capture info
