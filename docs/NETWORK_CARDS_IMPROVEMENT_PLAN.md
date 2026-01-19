# Network Cards Improvement Plan

## Goals
1. Remove "Network Cards" tab from Advanced Settings (duplicate of Interfaces > Network Cards)
2. Improve network card information display:
   - Show proper driver version
   - Display NIC brand, model, and type clearly
   - Show min/max values for all configurable parameters
   - Improve naming and organization

## Current State Analysis

### Where Network Cards Appears
- ✅ **Interfaces > Network Cards** (`/interfaces/network-cards`) - KEEP THIS
- ❌ **Advanced Settings > Network Cards** tab - REMOVE THIS

### Current Data Retrieved
From `NetworkCardService.cs`:
- Driver name (from `ethtool` output: `driver:` field)
- Driver version (from `ethtool` output: `version:` field) - **Currently parsed but may not be displayed properly**
- PCI Vendor/Device (from `lspci`)
- Speed, Duplex, Auto-negotiation
- Offloads (from `ethtool -k`)
- Ring buffers (from `ethtool -g`) - **Min/Max values are parsed but may not be displayed**

### Issues to Fix
1. **Driver version**: Parsed but may not be prominently displayed
2. **NIC naming**: Currently shows "Vendor Device" but could be more descriptive
3. **Min/Max values**: Buffer min/max are parsed but may not be shown in UI
4. **Speed limits**: Supported speeds are available but min/max not explicitly shown
5. **Duplicate location**: Network Cards in Advanced Settings should be removed

## Implementation Plan

### Phase 1: Remove Network Cards from Advanced Settings

**Files to modify:**
1. `src/Monolith.FireWall.WebUI/wwwroot/js/pages/advanced-settings.js`
   - Remove Network Cards tab button (line ~57)
   - Remove Network Cards tab pane (line ~131)
   - Remove Network Cards event handlers (lines ~173-187)
   - Remove `loadNetworkCardsScript()` function (line ~15)

### Phase 2: Enhance Network Card Information Display

#### 2.1 Improve Driver Version Display
**File:** `src/Monolith.FireWall.WebUI/wwwroot/js/pages/network-cards.js`

**Changes:**
- Ensure driver version is prominently displayed in card header
- Format: "Driver: {driver} v{version}" or "Driver: {driver} ({version})"
- Show firmware version if available

#### 2.2 Improve NIC Brand/Model Display
**File:** `src/Monolith.FireWall.WebUI/wwwroot/js/pages/network-cards.js`

**Current:** Shows "Vendor Device (PCI Slot)"
**Improvement:**
- Better formatting: "Brand Model" or "Vendor - Model"
- Add PCI slot as secondary info
- Show bus info (e.g., "pci@0000:01:00.0")
- Make it more readable

#### 2.3 Add Min/Max Values for Buffers
**Files:**
- `src/Monolith.FireWall.Core/Services/NetworkCardService.cs` - Already parses min/max
- `src/Monolith.FireWall.Platform/Models/NetworkModels.cs` - Check if min/max fields exist
- `src/Monolith.FireWall.WebUI/wwwroot/js/pages/network-cards.js` - Display min/max in UI

**Changes:**
- Ensure buffer min/max values are included in API response
- Display min/max next to current values in buffer form
- Add validation in UI to prevent values outside min/max range
- Format: "Current: X (Min: Y, Max: Z)"

#### 2.4 Add Speed Limits Display
**Files:**
- `src/Monolith.FireWall.Core/Services/NetworkCardService.cs` - Parse supported speeds
- `src/Monolith.FireWall.WebUI/wwwroot/js/pages/network-cards.js` - Show available speeds

**Changes:**
- Parse supported link modes to extract available speeds
- Display available speeds in speed selection dropdown
- Show current speed vs. supported speeds
- Format: "Current: 1000Mb/s (Supported: 10/100/1000/10000 Mb/s)"

#### 2.5 Enhance Card Header Information
**File:** `src/Monolith.FireWall.WebUI/wwwroot/js/pages/network-cards.js`

**Current header shows:**
- Interface name
- Link status
- Vendor Device (PCI Slot)

**Improved header should show:**
- Interface name (prominent)
- Link status badge
- Driver: {driver} v{version}
- NIC: {Brand} {Model}
- PCI: {slot} / Bus: {bus-info}
- MAC: {mac-address}

### Phase 3: Data Model Enhancements

#### 3.1 Check NetworkCardBuffers Model
**File:** `src/Monolith.FireWall.Platform/Models/NetworkModels.cs`

**Verify fields exist:**
- `RxMini`, `RxMiniMax`
- `Rx`, `RxMax`
- `RxJumbo`, `RxJumboMax`
- `Tx`, `TxMax`

**If missing, add:**
- Min values for each buffer type
- Max values for each buffer type

#### 3.2 Add Supported Speeds to NetworkCardInfo
**File:** `src/Monolith.FireWall.Platform/Models/NetworkModels.cs`

**Add:**
- `List<string> SupportedSpeeds` - Extracted from SupportedLinkModes
- `List<string> AdvertisedSpeeds` - Extracted from AdvertisedLinkModes

### Phase 4: Backend Enhancements

#### 4.1 Improve Driver Version Parsing
**File:** `src/Monolith.FireWall.Core/Services/NetworkCardService.cs`

**Current:** Parses `version:` field from `ethtool` output
**Enhancement:**
- Also try `ethtool -i {interface}` for more detailed driver info
- Parse driver version more reliably
- Handle cases where version might be in different formats

#### 4.2 Extract Supported Speeds
**File:** `src/Monolith.FireWall.Core/Services/NetworkCardService.cs`

**Add method:**
- Parse `SupportedLinkModes` to extract speed values
- Convert link mode strings (e.g., "1000baseT/Full") to speed values (e.g., "1000")
- Return list of supported speeds

#### 4.3 Ensure Min/Max Buffer Values Are Returned
**File:** `src/Monolith.FireWall.Core/Services/NetworkCardService.cs`

**Verify:**
- `ParseBuffers()` method correctly extracts min/max values
- All buffer types have min/max values populated
- Values are included in API response

### Phase 5: UI Improvements

#### 5.1 Improve Card Display
**File:** `src/Monolith.FireWall.WebUI/wwwroot/js/pages/network-cards.js`

**Changes:**
- Reorganize card header with better information hierarchy
- Add driver version prominently
- Better formatting for NIC brand/model
- Show all relevant info in collapsible sections

#### 5.2 Add Min/Max Validation
**File:** `src/Monolith.FireWall.WebUI/wwwroot/js/pages/network-cards.js`

**Changes:**
- Add min/max validation for buffer inputs
- Show min/max values next to input fields
- Prevent submission of values outside range
- Add helpful tooltips showing valid ranges

#### 5.3 Improve Speed Selection
**File:** `src/Monolith.FireWall.WebUI/wwwroot/js/pages/network-cards.js`

**Changes:**
- Populate speed dropdown with only supported speeds
- Show current speed vs. supported speeds
- Add validation to prevent unsupported speed selection

## Implementation Steps

1. **Remove Network Cards from Advanced Settings**
   - Remove tab and related code from `advanced-settings.js`
   - Test that Interfaces > Network Cards still works

2. **Enhance Data Models**
   - Verify/update `NetworkCardBuffers` to include all min/max fields
   - Add supported speeds extraction to `NetworkCardInfo`

3. **Improve Backend Parsing**
   - Enhance driver version parsing
   - Extract supported speeds from link modes
   - Ensure all min/max values are parsed and returned

4. **Update UI Display**
   - Improve card header with better information
   - Add min/max display for buffers
   - Improve speed selection with supported speeds
   - Add validation for min/max ranges

5. **Testing**
   - Test with various NIC types
   - Verify driver versions display correctly
   - Verify min/max values are shown and validated
   - Test speed selection with different NICs

## Files to Modify

1. `src/Monolith.FireWall.WebUI/wwwroot/js/pages/advanced-settings.js` - Remove Network Cards tab
2. `src/Monolith.FireWall.Core/Services/NetworkCardService.cs` - Enhance parsing
3. `src/Monolith.FireWall.Platform/Models/NetworkModels.cs` - Verify/add fields
4. `src/Monolith.FireWall.WebUI/wwwroot/js/pages/network-cards.js` - Improve display
5. `src/Monolith.FireWall.WebUI/Features/NetworkCards/NetworkCardController.cs` - Verify API returns all data

## Expected Outcomes

- Network Cards removed from Advanced Settings (no duplication)
- Driver version clearly displayed
- NIC brand/model clearly identified
- Min/max values shown for all configurable parameters
- Better user experience with clearer information hierarchy
- Validation prevents invalid parameter values

## Implementation Status: ✅ COMPLETE

### Completed Features:

1. ✅ **Removed Network Cards from Advanced Settings**
   - Removed tab and all related code
   - Network Cards now only accessible via Interfaces > Network Cards

2. ✅ **Enhanced Data Models**
   - Added `NetworkCardCoalescing` with all coalescing parameters
   - Added `NetworkCardPause` for pause frame parameters
   - Added min values to `NetworkCardBuffers` (RxMiniMin, RxMin, RxJumboMin, TxMin)
   - Added `Locked` dictionaries to track locked parameters
   - Added `SupportedSpeeds` and `AdvertisedSpeeds` to `NetworkCardInfo`

3. ✅ **Backend Enhancements**
   - Added parsing for coalescing parameters (`ethtool -c`)
   - Added parsing for pause frame parameters (`ethtool -a`)
   - Enhanced buffer parsing to extract min values
   - Added locked parameter detection (checks for "[fixed]" in ethtool output)
   - Added speed extraction from link modes
   - Added `SetCoalescingAsync()` and `SetPauseAsync()` methods
   - All parameters now include locked state tracking

4. ✅ **UI Improvements**
   - Enhanced card header with driver version, NIC brand/model, PCI slot, bus info, MAC address
   - Shows supported speeds next to current speed
   - Buffer section shows min/max values with validation
   - Coalescing section with all 21+ parameters
   - Pause frame section with autoneg, rx, tx
   - All locked parameters are greyed out and disabled
   - Locked parameters show "Locked" badge
   - Validation prevents values outside min/max ranges
   - Reset functions skip locked parameters

5. ✅ **API Endpoints**
   - Added `/api/system/network-cards/{interface}/coalescing` POST endpoint
   - Added `/api/system/network-cards/{interface}/pause` POST endpoint
   - Added handlers for `network.cards.coalescing.set` and `network.cards.pause.set`

### Parameters Now Available:

**Buffers (with min/max):**
- RX Mini (min/max)
- RX (min/max)
- RX Jumbo (min/max)
- TX (min/max)

**Coalescing (21+ parameters):**
- Adaptive RX/TX
- RX/TX Usecs
- RX/TX Frames
- RX/TX Usecs IRQ
- RX/TX Frames IRQ
- Stats Block Usecs
- Pkt Rate Low/High
- RX/TX Usecs Low/High
- RX/TX Frames Low/High
- Sample Interval

**Pause Frames:**
- Auto-negotiation
- RX Pause
- TX Pause

**Offloads (30+ parameters):**
- All existing offloads with locked state detection

**All parameters:**
- Show current values
- Show min/max where applicable
- Grey out and disable locked parameters
- Display "Locked" badge for locked parameters
