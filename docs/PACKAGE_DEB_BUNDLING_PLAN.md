# Package DEB Bundling Plan

## Overview
This plan outlines how to modify the MonolithFireWall package system to bundle `.deb` packages directly into `.mfwpkg` files, enabling offline installation and self-contained packages.

## Current State

### Package Structure
```
monolith-network.mfwpkg (ZIP archive)
├── manifest.json          # Contains AptDependencies: ["isc-dhcp-server", "bind9"]
├── backend/
│   └── Monolith.Network.dll
└── wwwroot/
    ├── css/
    └── js/
```

### Current Limitations
1. **AptDependencies** in `manifest.json` are declared but **NOT automatically installed**
2. Packages require internet connection to install apt dependencies
3. No mechanism to bundle deb packages with monolith packages
4. Dependency resolution happens at install time (if implemented)

### Current Installation Flow
1. Download `.mfwpkg` from URL
2. Extract to staging directory
3. Copy to `/var/lib/monolith-firewall/packages/{packageId}/`
4. Load package and modules
5. **Apt dependencies are NOT installed automatically**

## Goals

1. **Bundle deb packages** directly in `.mfwpkg` files
2. **Install bundled debs** during package installation
3. **Resolve and bundle dependencies** at package build time
4. **Support offline installation** (no internet required)
5. **Maintain backward compatibility** with existing packages
6. **Clean up build scripts** - remove old DHCP/DNS apt installation code
7. **Make packages fully standalone** - each package includes all its dependencies
8. **Keep ISO integration** - all 3 packages still included in ISO, but as standalone packages

## Proposed Package Structure

### New Structure
```
monolith-network.mfwpkg (ZIP archive)
├── manifest.json
│   └── Contains:
│       - AptDependencies: ["isc-dhcp-server", "bind9"] (for reference)
│       - BundledDebs: ["isc-dhcp-server_4.4.1-2.3_amd64.deb", ...] (NEW)
│       - DebDependencies: ["libisc110", "libdns110", ...] (NEW - auto-resolved)
├── debs/                    # NEW DIRECTORY
│   ├── isc-dhcp-server_4.4.1-2.3_amd64.deb
│   ├── isc-dhcp-common_4.4.1-2.3_amd64.deb
│   ├── bind9_9.18.1-1ubuntu1.3_amd64.deb
│   ├── bind9utils_9.18.1-1ubuntu1.3_amd64.deb
│   ├── libisc110_1:9.18.1-1ubuntu1.3_amd64.deb
│   ├── libdns110_1:9.18.1-1ubuntu1.3_amd64.deb
│   └── ... (all dependencies)
├── backend/
│   └── Monolith.Network.dll
└── wwwroot/
    ├── css/
    └── js/
```

## Implementation Plan

### Phase 1: Manifest Schema Updates

#### 1.1 Update PackageManifest Model
**File**: `src/Monolith.FireWall.Common/Models/PackageModels.cs`

```csharp
public record PackageManifest(
    string Id,
    string Name,
    string Version,
    string Description,
    string Author,
    string? Homepage = null,
    string? License = null,
    string[]? Dependencies = null,
    string[]? AptDependencies = null,           // Keep for backward compatibility
    BundledDebInfo[]? BundledDebs = null,        // NEW: Bundled deb packages
    string? MinCoreVersion = null,
    string? MaxCoreVersion = null,
    bool RequiresRestart = false,
    FirewallIntentDefinition[]? FirewallIntents = null
);

// NEW: Information about bundled deb packages
public record BundledDebInfo(
    string FileName,           // e.g., "isc-dhcp-server_4.4.1-2.3_amd64.deb"
    string PackageName,         // e.g., "isc-dhcp-server"
    string Version,            // e.g., "4.4.1-2.3"
    string Architecture,       // e.g., "amd64"
    string[]? Dependencies = null  // Runtime dependencies (for verification)
);
```

#### 1.2 Example manifest.json
```json
{
  "id": "monolith-network",
  "name": "Monolith Network",
  "version": "1.0.0",
  "description": "Network management (DHCP, DNS)",
  "author": "MonolithFireWall",
  "aptDependencies": ["isc-dhcp-server", "bind9"],
  "bundledDebs": [
    {
      "fileName": "isc-dhcp-server_4.4.1-2.3_amd64.deb",
      "packageName": "isc-dhcp-server",
      "version": "4.4.1-2.3",
      "architecture": "amd64",
      "dependencies": ["isc-dhcp-common", "libc6"]
    },
    {
      "fileName": "bind9_9.18.1-1ubuntu1.3_amd64.deb",
      "packageName": "bind9",
      "version": "9.18.1-1ubuntu1.3",
      "architecture": "amd64",
      "dependencies": ["bind9utils", "libisc110", "libdns110"]
    }
  ]
}
```

### Phase 2: Package Installer Updates

#### 2.1 Add Deb Installation Logic
**File**: `src/Monolith.FireWall.Core/Services/PackageInstaller.cs`

**New Method**:
```csharp
private async Task<PackageInstallResult> InstallBundledDebsAsync(
    string packageDir,
    PackageManifest manifest,
    CancellationToken cancellationToken)
{
    if (manifest.BundledDebs == null || manifest.BundledDebs.Length == 0)
    {
        _logger.LogInformation("No bundled deb packages to install");
        return PackageInstallResult.Ok(manifest, false, false);
    }

    var debsDir = Path.Combine(packageDir, "debs");
    if (!Directory.Exists(debsDir))
    {
        _logger.LogWarning("Bundled debs directory not found, skipping deb installation");
        return PackageInstallResult.Ok(manifest, false, false);
    }

    var debFiles = manifest.BundledDebs.Select(b => Path.Combine(debsDir, b.FileName)).ToList();
    
    // Verify all deb files exist
    foreach (var debFile in debFiles)
    {
        if (!File.Exists(debFile))
        {
            return PackageInstallResult.Fail($"Bundled deb file not found: {debFile}");
        }
    }

    try
    {
        // Install deb packages using dpkg
        // Use --force-depends if needed, but prefer proper dependency resolution
        var debList = string.Join(" ", debFiles.Select(f => $"\"{f}\""));
        
        var cmd = new PlatformCommand
        {
            FileName = "dpkg",
            Arguments = $"-i {debList}",
            TimeoutMs = 300_000, // 5 minutes
            UseSudo = true
        };

        _logger.LogInformation($"Installing {debFiles.Count} bundled deb packages...");
        var result = await _commandRunner.RunAsync(cmd, cancellationToken);

        if (result.ExitCode != 0)
        {
            // Try to fix dependencies
            _logger.LogInformation("Attempting to fix broken dependencies...");
            var fixCmd = new PlatformCommand
            {
                FileName = "apt-get",
                Arguments = "install -f -y",
                TimeoutMs = 300_000,
                UseSudo = true
            };
            
            var fixResult = await _commandRunner.RunAsync(fixCmd, cancellationToken);
            if (fixResult.ExitCode != 0)
            {
                return PackageInstallResult.Fail(
                    $"Failed to install bundled deb packages. dpkg exit: {result.ExitCode}, " +
                    $"apt-get fix exit: {fixResult.ExitCode}. " +
                    $"Error: {result.StdErr ?? fixResult.StdErr ?? "Unknown error"}");
            }
        }

        _logger.LogInformation("Successfully installed bundled deb packages");
        await _loggingManager.LogMonolithAsync(
            "Package",
            "info",
            "PackageInstaller",
            $"Installed {debFiles.Count} bundled deb packages for {manifest.Id}",
            null,
            null,
            new Dictionary<string, object>
            {
                ["packageId"] = manifest.Id,
                ["debCount"] = debFiles.Count,
                ["debPackages"] = string.Join(", ", manifest.BundledDebs.Select(b => b.PackageName))
            });

        return PackageInstallResult.Ok(manifest, false, false);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to install bundled deb packages");
        return PackageInstallResult.Fail($"Failed to install bundled deb packages: {ex.Message}");
    }
}
```

**Update InstallAsync method**:
```csharp
public async Task<PackageInstallResult> InstallAsync(...)
{
    // ... existing extraction and manifest reading code ...
    
    Directory.CreateDirectory(targetDir);
    CopyDirectory(stagingDir, targetDir);

    // NEW: Install bundled deb packages BEFORE setting package state
    var debInstallResult = await InstallBundledDebsAsync(targetDir, manifest, cancellationToken);
    if (!debInstallResult.Success)
    {
        // Clean up on failure
        try { Directory.Delete(targetDir, recursive: true); } catch { }
        return debInstallResult;
    }

    await _stateStore.SetPackageInstalledAsync(manifest.Id, manifest.Version, "local", log: false);
    
    // ... rest of installation ...
}
```

#### 2.2 Add Deb Uninstallation Logic (Optional)
```csharp
private async Task UninstallBundledDebsAsync(
    PackageManifest manifest,
    CancellationToken cancellationToken)
{
    if (manifest.BundledDebs == null || manifest.BundledDebs.Length == 0)
    {
        return;
    }

    var packageNames = manifest.BundledDebs.Select(b => b.PackageName).ToList();
    
    try
    {
        var cmd = new PlatformCommand
        {
            FileName = "apt-get",
            Arguments = $"remove -y {string.Join(" ", packageNames)}",
            TimeoutMs = 300_000,
            UseSudo = true
        };

        await _commandRunner.RunAsync(cmd, cancellationToken);
        _logger.LogInformation($"Uninstalled deb packages: {string.Join(", ", packageNames)}");
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, $"Failed to uninstall some deb packages (may not be critical)");
        // Don't fail package removal if deb uninstall fails
    }
}
```

### Phase 3: Package Build Tool Updates

#### 3.1 Create Deb Bundling Script
**File**: `build-scripts/bundle-debs.sh` (NEW)

```bash
#!/bin/bash
# Bundle deb packages and dependencies for a monolith package

set -e

PACKAGE_ID="$1"
PACKAGE_DIR="$2"
OUTPUT_DEBS_DIR="$3"

if [ -z "$PACKAGE_ID" ] || [ -z "$PACKAGE_DIR" ] || [ -z "$OUTPUT_DEBS_DIR" ]; then
    echo "Usage: $0 <package-id> <package-source-dir> <output-debs-dir>"
    exit 1
fi

MANIFEST_FILE="$PACKAGE_DIR/manifest.json"
if [ ! -f "$MANIFEST_FILE" ]; then
    echo "Error: manifest.json not found in $PACKAGE_DIR"
    exit 1
fi

# Read aptDependencies from manifest.json
APT_DEPS=$(jq -r '.aptDependencies // [] | .[]' "$MANIFEST_FILE" 2>/dev/null || echo "")

if [ -z "$APT_DEPS" ]; then
    echo "No aptDependencies found in manifest.json"
    exit 0
fi

echo "Bundling deb packages for $PACKAGE_ID..."
echo "Dependencies: $APT_DEPS"

# Create output directory
mkdir -p "$OUTPUT_DEBS_DIR"

# Download packages and all dependencies
# Use apt-get download to get .deb files
apt-get update -qq
apt-get download $(echo "$APT_DEPS" | tr '\n' ' ')

# Download all dependencies recursively
apt-get download $(apt-cache depends --recurse --no-recommends --no-suggests \
    --no-conflicts --no-breaks --no-replaces --no-enhances \
    $(echo "$APT_DEPS" | tr '\n' ' ') | \
    grep "^\w" | sort -u)

# Move all .deb files to output directory
mv *.deb "$OUTPUT_DEBS_DIR/" 2>/dev/null || true

# Generate BundledDebInfo entries
echo "Generating bundledDebs manifest entries..."
# This would parse .deb files and extract metadata
# Implementation would use dpkg-deb -I or similar

echo "Bundled $(ls -1 "$OUTPUT_DEBS_DIR"/*.deb 2>/dev/null | wc -l) deb packages"
```

#### 3.2 Update Package Build Process
**File**: `build-scripts/package-mfwpkg.sh`

Add deb bundling step:
```bash
# After building the package, before creating .mfwpkg

# Bundle deb packages if aptDependencies exist
if [ -f "$PACKAGE_DIR/manifest.json" ] && jq -e '.aptDependencies // [] | length > 0' "$PACKAGE_DIR/manifest.json" >/dev/null 2>&1; then
    echo "Bundling deb packages..."
    DEBS_DIR="$PACKAGE_DIR/debs"
    mkdir -p "$DEBS_DIR"
    
    ./build-scripts/bundle-debs.sh "$PACKAGE_ID" "$PACKAGE_DIR" "$DEBS_DIR"
    
    # Update manifest.json with BundledDebInfo
    # (This would be done by a separate script that parses .deb files)
    ./build-scripts/update-manifest-debs.sh "$PACKAGE_DIR"
fi

# Create .mfwpkg (ZIP) including debs/ directory
cd "$PACKAGE_DIR"
zip -r "$OUTPUT_FILE" . -x "*.git*" -x "*.csproj.user" -x "bin/*" -x "obj/*"
```

#### 3.3 Create Manifest Update Script
**File**: `build-scripts/update-manifest-debs.sh` (NEW)

```bash
#!/bin/bash
# Update manifest.json with BundledDebInfo from .deb files

PACKAGE_DIR="$1"
DEBS_DIR="$PACKAGE_DIR/debs"
MANIFEST_FILE="$PACKAGE_DIR/manifest.json"

if [ ! -d "$DEBS_DIR" ]; then
    exit 0
fi

# Parse each .deb file and generate BundledDebInfo
BUNDLED_DEBS_JSON="[]"

for deb_file in "$DEBS_DIR"/*.deb; do
    if [ ! -f "$deb_file" ]; then
        continue
    fi
    
    DEB_NAME=$(basename "$deb_file")
    
    # Extract package info using dpkg-deb
    PACKAGE_NAME=$(dpkg-deb -f "$deb_file" Package 2>/dev/null || echo "")
    VERSION=$(dpkg-deb -f "$deb_file" Version 2>/dev/null || echo "")
    ARCH=$(dpkg-deb -f "$deb_file" Architecture 2>/dev/null || echo "")
    DEPS=$(dpkg-deb -f "$deb_file" Depends 2>/dev/null | sed 's/, /\n/g' | sed 's/|.*//' | sed 's/ (.*)//' | jq -R . | jq -s . || echo "[]")
    
    if [ -n "$PACKAGE_NAME" ]; then
        DEB_JSON=$(jq -n \
            --arg file "$DEB_NAME" \
            --arg pkg "$PACKAGE_NAME" \
            --arg ver "$VERSION" \
            --arg arch "$ARCH" \
            --argjson deps "$DEPS" \
            '{fileName: $file, packageName: $pkg, version: $ver, architecture: $arch, dependencies: $deps}')
        
        BUNDLED_DEBS_JSON=$(echo "$BUNDLED_DEBS_JSON" | jq --argjson deb "$DEB_JSON" '. + [$deb]')
    fi
done

# Update manifest.json
if [ -f "$MANIFEST_FILE" ]; then
    jq --argjson bundledDebs "$BUNDLED_DEBS_JSON" '.bundledDebs = $bundledDebs' "$MANIFEST_FILE" > "$MANIFEST_FILE.tmp"
    mv "$MANIFEST_FILE.tmp" "$MANIFEST_FILE"
    echo "Updated manifest.json with $(echo "$BUNDLED_DEBS_JSON" | jq 'length') bundled deb packages"
fi
```

### Phase 4: Backward Compatibility

#### 4.1 Handle Packages Without Bundled Debs
- If `BundledDebs` is null/empty, skip deb installation
- If `debs/` directory doesn't exist, skip deb installation
- Log warning but don't fail installation

#### 4.2 Fallback to AptDependencies (Optional)
If bundled debs fail to install, optionally fall back to `apt-get install`:
```csharp
if (manifest.BundledDebs == null || manifest.BundledDebs.Length == 0)
{
    // Fallback: Try installing from aptDependencies if available
    if (manifest.AptDependencies != null && manifest.AptDependencies.Length > 0)
    {
        _logger.LogInformation("No bundled debs, attempting to install from aptDependencies...");
        // Use apt-get install (requires internet)
    }
}
```

### Phase 5: Testing & Validation

#### 5.1 Test Cases
1. **Package with bundled debs**: Install and verify debs are installed
2. **Package without bundled debs**: Should install normally
3. **Offline installation**: Install package without internet
4. **Dependency resolution**: Verify all dependencies are bundled
5. **Version conflicts**: Handle cases where system has newer/older versions
6. **Uninstallation**: Verify debs are removed (optional)

#### 5.2 Validation Steps
```bash
# 1. Build package with bundled debs
./build-scripts/package-mfwpkg.sh monolith-network

# 2. Verify .mfwpkg contains debs/
unzip -l monolith-network.mfwpkg | grep debs/

# 3. Install on clean system (offline)
# Disconnect network
monolith-pkgmgr package install monolith-network.mfwpkg

# 4. Verify debs are installed
dpkg -l | grep -E "isc-dhcp-server|bind9"

# 5. Verify package works
# Test DHCP/DNS functionality
```

## Phase 6: Build Script Cleanup & Standalone Packages

### 6.0 Overview

**Goal**: Remove all old apt installation code for DHCP/DNS from build scripts. Make all packages fully standalone so they can be installed offline without any external apt dependencies.

**Key Principle**: Everything needed by a package should be bundled inside the `.mfwpkg` file. Build scripts should NOT install packages via apt - that's the package's responsibility.

### 6.1 Remove Old DHCP/DNS Apt Installation

#### 6.1.1 Clean up build-iso.sh
**File**: `build-scripts/build-iso.sh`

**Remove from REQUIRED_PACKAGES** (line 487):
```bash
# OLD (REMOVE):
REQUIRED_PACKAGES="... isc-dhcp-client ..."

# NEW (CLEANED):
REQUIRED_PACKAGES="openssh-server openssh-client nftables iproute2 systemd sqlite3 sudo procps iputils-ping bridge-utils vlan ifupdown2 tcpdump mtr traceroute iptables socat ethtool pciutils dbus-user-session"
```

**Rationale**: 
- `isc-dhcp-client` will be bundled in `monolith-network.mfwpkg`
- `bind9` and `isc-dhcp-server` will be bundled in `monolith-network.mfwpkg`
- All DHCP/DNS dependencies will be handled by the package itself

#### 6.1.2 Verify install-packages.sh
**File**: `iso-build/install-packages.sh`

**Current State**: Already clean - no explicit DHCP/DNS installation
- Only installs .NET runtime, openssh, and monolith-firewall
- Packages are installed from offline repo
- No changes needed

#### 6.1.3 Update preseed.cfg (if needed)
**File**: `iso-build/preseed.cfg`

**Verify**: Ensure it only copies and installs .mfwpkg files, no apt installs for DHCP/DNS

### 6.2 Package Standalone Requirements

#### 6.2.1 All Packages Must Be Self-Contained
Each of the 3 packages (`monolith-network`, `monolith-vpn`, `monolith-diagnostics`) must:

1. **Bundle ALL required .deb packages** in `debs/` directory
2. **Include ALL dependencies** (no external apt installs needed)
3. **Work offline** (no internet required for installation)
4. **Be installable independently** (can install just one package)

#### 6.2.2 Package-Specific Requirements

**monolith-network**:
- Bundle: `isc-dhcp-server`, `isc-dhcp-common`, `bind9`, `bind9utils`
- Bundle: All library dependencies (libisc110, libdns110, etc.)
- Bundle: Any other DHCP/DNS related packages

**monolith-vpn**:
- Bundle: VPN-related packages (strongswan, openvpn, wireguard-tools, etc.)
- Bundle: All dependencies

**monolith-diagnostics**:
- Bundle: Diagnostic tools and their dependencies
- (May have fewer debs than network/vpn)

### 6.3 ISO Integration (No Changes Needed)

The ISO build process already handles packages correctly:

1. **Build Script** (`build-iso.sh`):
   - Builds all packages via `build-all-packages.sh`
   - Copies `.mfwpkg` files to `monolith-packages/` on ISO
   - No changes needed - packages are already included

2. **Preseed** (`iso-build/preseed.cfg`):
   - Copies `.mfwpkg` files from ISO to `/var/lib/monolith-firewall/packages/`
   - Installs packages via `monolith-pkgmgr` or API
   - No changes needed

3. **Package Installation**:
   - Packages install their own bundled debs during installation
   - No separate apt install step needed
   - Fully offline

### 6.4 Build Script Updates

#### 6.4.1 Update build-all-packages.sh
**File**: `build-scripts/build-all-packages.sh`

**Ensure**: Each package build includes deb bundling step:
```bash
# For each package:
./build-scripts/package-mfwpkg.sh <package-id>
# This should automatically:
# 1. Build the package
# 2. Bundle debs (if aptDependencies exist)
# 3. Update manifest.json with BundledDebInfo
# 4. Create .mfwpkg with debs/ directory
```

#### 6.4.2 Update package-mfwpkg.sh
**File**: `build-scripts/package-mfwpkg.sh`

**Add deb bundling step** (before creating .mfwpkg):
```bash
# After building package, before creating .mfwpkg:

# Bundle deb packages if aptDependencies exist
if [ -f "$PACKAGE_DIR/manifest.json" ] && \
   jq -e '.aptDependencies // [] | length > 0' "$PACKAGE_DIR/manifest.json" >/dev/null 2>&1; then
    echo "Bundling deb packages for $PACKAGE_ID..."
    
    DEBS_DIR="$PACKAGE_DIR/debs"
    mkdir -p "$DEBS_DIR"
    
    # Download and bundle all debs
    ./build-scripts/bundle-debs.sh "$PACKAGE_ID" "$PACKAGE_DIR" "$DEBS_DIR"
    
    # Update manifest.json with BundledDebInfo
    ./build-scripts/update-manifest-debs.sh "$PACKAGE_DIR"
fi

# Create .mfwpkg (ZIP) - now includes debs/ directory
cd "$PACKAGE_DIR"
zip -r "$OUTPUT_FILE" . -x "*.git*" -x "*.csproj.user" -x "bin/*" -x "obj/*"
```

## Implementation Order

1. **Phase 1**: Update manifest schema and models
2. **Phase 2**: Update PackageInstaller to install bundled debs
3. **Phase 3**: Create build scripts for deb bundling
4. **Phase 4**: Clean up build scripts (remove old DHCP/DNS apt installs)
5. **Phase 5**: Test with monolith-network package (offline installation)
6. **Phase 6**: Update other packages (monolith-vpn, monolith-diagnostics)
7. **Phase 7**: Verify ISO build includes all 3 standalone packages

## Benefits

1. **Offline Installation**: Packages can be installed without internet
2. **Version Control**: Exact versions are bundled, no surprises
3. **Self-Contained**: All dependencies included
4. **Reproducible**: Same package works the same way everywhere
5. **ISO Integration**: Perfect for ISO-based installations

## Considerations

1. **Package Size**: Bundling debs increases .mfwpkg file size significantly
   - `monolith-network`: ~50-100MB (DHCP + DNS + deps)
   - `monolith-vpn`: ~20-50MB (VPN packages + deps)
   - `monolith-diagnostics`: ~10-20MB (diagnostic tools)
   - Total ISO size will increase, but enables offline installation

2. **Version Pinning**: Bundled versions may conflict with system packages
   - Solution: Check if package is already installed before installing bundled deb
   - Or: Use `--force-depends` if version conflict is acceptable

3. **Architecture**: Need to bundle debs for correct architecture (amd64, arm64, etc.)
   - Build scripts should detect architecture
   - Bundle architecture-specific debs
   - Fail gracefully if architecture mismatch

4. **Distribution**: Need to match Debian/Ubuntu version for compatibility
   - Currently targeting Debian 13 (Trixie)
   - Build scripts should verify Debian version
   - Bundle debs compatible with target distribution

5. **Updates**: System package updates may conflict with bundled versions
   - Consider: Mark bundled packages as "held" (`apt-mark hold`)
   - Or: Allow system updates to override bundled versions
   - Document behavior in package README

6. **Build Script Cleanup**: Remove all old apt installation code
   - No more `apt-get install isc-dhcp-server` in build scripts
   - No more `apt-get install bind9` in build scripts
   - Everything handled by package bundling

7. **ISO Standalone Packages**: All 3 packages must work independently
   - Can install just `monolith-network` without others
   - Can install just `monolith-vpn` without others
   - Each package includes its own dependencies

## Build Script Cleanup Checklist

### Files to Update

- [ ] `build-scripts/build-iso.sh`
  - [ ] Remove `isc-dhcp-client` from REQUIRED_PACKAGES (line 487)
  - [ ] Verify no other DHCP/DNS packages in REQUIRED_PACKAGES
  - [ ] Document that packages handle their own debs

- [ ] `build-scripts/build-all-packages.sh`
  - [ ] Verify it calls deb bundling for each package
  - [ ] Ensure packages are built with debs included

- [ ] `build-scripts/package-mfwpkg.sh`
  - [ ] Add deb bundling step before creating .mfwpkg
  - [ ] Ensure debs/ directory is included in ZIP

- [ ] `iso-build/install-packages.sh`
  - [ ] Verify no DHCP/DNS apt installs (already clean)
  - [ ] Document that packages install their own debs

- [ ] `iso-build/preseed.cfg`
  - [ ] Verify only installs .mfwpkg files
  - [ ] No apt installs for DHCP/DNS

### Verification Steps

1. **Build packages**:
   ```bash
   ./build-scripts/build-all-packages.sh
   # Verify each .mfwpkg contains debs/ directory
   unzip -l build-output/packages/monolith-network.mfwpkg | grep debs/
   ```

2. **Build ISO**:
   ```bash
   ./build-scripts/build-iso.sh
   # Verify ISO contains all 3 .mfwpkg files
   # Verify ISO does NOT include separate DHCP/DNS debs
   ```

3. **Test offline installation**:
   ```bash
   # Install ISO on VM without network
   # Verify packages install successfully
   # Verify DHCP/DNS work without internet
   ```

## Future Enhancements

1. **Architecture Detection**: Auto-detect system architecture and bundle correct debs
2. **Distribution Detection**: Match Debian/Ubuntu version
3. **Version Checking**: Check if system has newer versions before installing bundled debs
4. **Selective Installation**: Allow user to choose bundled vs system packages
5. **Deb Repository**: Create internal apt repository for bundled debs
6. **Package Size Optimization**: Use compression or delta updates for large packages
7. **Dependency Caching**: Share common dependencies between packages to reduce size
