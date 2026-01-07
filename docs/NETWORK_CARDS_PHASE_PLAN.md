# Network Cards Page - Phase Implementation Plan

## Phase 1: Backend Foundation ⭐ (IN PROGRESS)
**Goal:** Set up backend infrastructure for network card detection and configuration

### Tasks:
1. ✅ Add `ethtool` and `pciutils` to `debian/control`
2. ⏳ Create data models in `NetworkModels.cs`
3. ⏳ Implement `NetworkCardService` with PCI detection
4. ⏳ Implement ethtool parsing in `NetworkCardService`
5. ⏳ Create `NetworkCardHandler` for Core API
6. ⏳ Create `NetworkCardController` for WebUI API
7. ⏳ Register handler in `UnixSocketListener`

**Estimated Time:** 2-3 hours

---

## Phase 2: Frontend Basic Display
**Goal:** Display network cards in the UI with basic information

### Tasks:
1. Create `network-cards.js` module
2. Add "Network Cards" tab to Advanced Settings page
3. Implement card listing API calls
4. Render basic card information (interface, PCI info, driver)
5. Display link status and basic stats
6. Test with various NIC types

**Estimated Time:** 1-2 hours

---

## Phase 3: Speed/Duplex Configuration
**Goal:** Allow users to configure speed and duplex settings

### Tasks:
1. Add speed/duplex UI controls (dropdowns, autoneg toggle)
2. Implement speed/duplex API endpoint
3. Add validation for speed/duplex values
4. Implement apply functionality
5. Add user feedback (toasts, loading states)
6. Test speed/duplex changes

**Estimated Time:** 1 hour

---

## Phase 4: Offload Configuration
**Goal:** Allow users to configure offload features

### Tasks:
1. Parse and display all offload features from ethtool
2. Create toggle switches for each offload
3. Group offloads by category (segmentation, checksum, VLAN, etc.)
4. Implement offload API endpoint
5. Add apply functionality for offloads
6. Test with different NIC types

**Estimated Time:** 2 hours

---

## Phase 5: Ring Buffer Configuration
**Goal:** Allow users to configure TX/RX ring buffers

### Tasks:
1. Parse ring buffer information from ethtool
2. Display current and maximum values
3. Create input fields for buffer configuration
4. Implement buffer API endpoint
5. Add validation (min/max values)
6. Test buffer changes

**Estimated Time:** 1 hour

---

## Phase 6: Apply & Revert Functionality
**Goal:** Implement apply all and revert to defaults

### Tasks:
1. Implement "Apply All Changes" button
2. Batch apply all settings for a card
3. Implement "Revert to Defaults" functionality
4. Add confirmation dialogs
5. Add comprehensive error handling
6. Test apply and revert operations

**Estimated Time:** 1-2 hours

---

## Phase 7: Testing & Polish
**Goal:** Comprehensive testing and UI improvements

### Tasks:
1. Test with Intel NICs
2. Test with Realtek NICs
3. Test with Broadcom NICs
4. Test edge cases (no link, unsupported features)
5. Add tooltips and help text
6. Improve error messages
7. Add loading states and animations
8. Responsive design improvements

**Estimated Time:** 2 hours

---

## Total Estimated Time: 10-13 hours

## Current Status: Phase 1 - Backend Foundation
