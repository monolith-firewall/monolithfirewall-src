# Deployment Guide

## Building and Deploying WebUI

### Quick Deploy (Development)

```bash
# 1. Build the WebUI project
cd /home/mlf/project/monolithfirewall-src
dotnet build src/Monolith.FireWall.WebUI/Monolith.FireWall.WebUI.csproj

# 2. Publish to temporary directory
dotnet publish src/Monolith.FireWall.WebUI/Monolith.FireWall.WebUI.csproj -c Debug -o /tmp/webui-publish

# 3. Stop the service
sudo systemctl stop monolith-firewall-webui

# 4. Copy files to installation directory
sudo cp -v /tmp/webui-publish/Monolith.FireWall.WebUI* /opt/monolith-firewall/webui/
sudo cp -v /tmp/webui-publish/*.dll /opt/monolith-firewall/webui/

# 5. Update service file to use dotnet (if needed)
# The service file should have: ExecStart=/usr/bin/dotnet /opt/monolith-firewall/webui/Monolith.FireWall.WebUI.dll
sudo systemctl daemon-reload

# 6. Start the service
sudo systemctl start monolith-firewall-webui

# 7. Check status
systemctl status monolith-firewall-webui
sudo journalctl -u monolith-firewall-webui -f
```

### Service File Configuration

The WebUI service file is located at:
- **Source**: `/home/mlf/project/monolithfirewall-src/debian/systemd/monolith-firewall-webui.service`
- **Installed**: `/usr/lib/systemd/system/monolith-firewall-webui.service`

**Important**: The service file must use `dotnet` to run the DLL:
```ini
ExecStart=/usr/bin/dotnet /opt/monolith-firewall/webui/Monolith.FireWall.WebUI.dll
```

Not:
```ini
ExecStart=/opt/monolith-firewall/webui/Monolith.FireWall.WebUI  # This won't work for framework-dependent builds
```

### Installation Directory

WebUI is installed to: `/opt/monolith-firewall/webui/`

Required files:
- `Monolith.FireWall.WebUI.dll` - Main application DLL
- `Monolith.FireWall.WebUI.deps.json` - Dependency manifest
- `Monolith.FireWall.WebUI.runtimeconfig.json` - Runtime configuration
- `*.dll` - All dependency DLLs (Monolith.*, CL.*, CodeLogic.*, etc.)

### Verification

After deployment, verify:

1. **Service is running**:
   ```bash
   systemctl status monolith-firewall-webui
   ```

2. **Assemblies are registered**:
   ```bash
   sudo journalctl -u monolith-firewall-webui | grep "Registered Views assembly"
   ```
   Should show:
   ```
   Registered Views assembly: Monolith.Network, Version=...
   Registered Views assembly: Monolith.Diagnostics, Version=...
   Registered Views assembly: Monolith.Vpn, Version=...
   ```

3. **Package pages work**:
   ```bash
   curl http://localhost/api/cms/page?route=/p/monolith-diagnostics/diagnostics/config
   ```

### Common Issues

**Issue**: Service fails with "Address already in use"
- **Solution**: Kill any existing WebUI processes:
  ```bash
  sudo pkill -f "Monolith.FireWall.WebUI"
  sudo systemctl start monolith-firewall-webui
  ```

**Issue**: Service fails with ".NET not found"
- **Solution**: Update service file to use `dotnet`:
  ```bash
  sudo sed -i 's|ExecStart=/opt/monolith-firewall/webui/Monolith.FireWall.WebUI|ExecStart=/usr/bin/dotnet /opt/monolith-firewall/webui/Monolith.FireWall.WebUI.dll|' /usr/lib/systemd/system/monolith-firewall-webui.service
  sudo systemctl daemon-reload
  ```

**Issue**: Package assemblies not registered
- **Check**: API response format (should use "Success"/"Data" not "success"/"data")
- **Check**: Logs for "PackageViewsRegistry" messages
- **Verify**: DLLs exist at paths returned by Core API
