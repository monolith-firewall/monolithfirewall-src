# ISO Build Requirements

## Prerequisites

1. **.NET 10.0 Runtime** - The monolith-firewall package requires .NET 10.0 runtime.
   - Download the .deb packages from: https://dotnet.microsoft.com/download/dotnet/10.0
   - You need: `dotnet-runtime-10.0.x.x.x-amd64.deb` and `aspnetcore-runtime-10.0.x.x.x-amd64.deb`
   - Place these .deb files in `build-output/monolith-debs/` before building the ISO
   - Or they will be downloaded during the build process

2. **Build Dependencies**
   ```bash
   sudo apt-get install -y p7zip-full xorriso syslinux-utils apt-utils wget
   ```

## Building the ISO

1. Build the Debian package first:
   ```bash
   ./build-scripts/build-deb.sh
   ```

2. (Optional) Download .NET runtime .deb files and place in `build-output/monolith-debs/`

3. Build all packages:
   ```bash
   ./build-scripts/build-all-packages.sh
   ```

4. Build the ISO:
   ```bash
   ./build-scripts/build-iso.sh [VERSION]
   ```

## Known Issues

- If installation hangs at package installation, it may be waiting for confirmation
- The updated preseed.cfg includes better non-interactive handling
- .NET runtime must be included in the ISO for offline installation
