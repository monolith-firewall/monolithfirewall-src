## Package manager dev test (feed → download → install → optional restart)

This is a quick end-to-end sanity check for the packages update flow.

### 1) Prepare a local feed + package file

- Put a `.mfwpkg` file and a feed JSON on a machine reachable by the firewall box.
- For a quick local test, you can use the example file `docs/dev-package-feed.json` (update the URL + SHA256).

### 2) Point WebUI at the feed

Edit WebUI config:

- `src/Monolith.FireWall.WebUI/appsettings.Development.json`
  - set `PackageUpdates:BaseUrl` to your local feed URL (without the `?version=...` part)
  - set `PackageUpdates:AllowInsecureHttp` to `true` if you’re using plain HTTP

Example:

```json
{
  "PackageUpdates": {
    "BaseUrl": "http://localhost:8081/packages.json",
    "AllowInsecureHttp": true
  }
}
```

### 3) Verify in WebUI

- Open **System → Packages**
- Click **Refresh** (or reload the page)
- Confirm the “Available” list shows your package and the **Install** button has a valid URL.
- Install, then check:
  - `/var/lib/monolith-firewall/packages-cache/` contains the downloaded `.mfwpkg`
  - `/opt/monolith-firewall/packages/{packageId}/manifest.json` exists after install

### 4) Verify restart behavior (only if required)

If the package’s `manifest.json` contains `"requiresRestart": true`:

- The Core will schedule a background restart of:
  - `monolith-firewall-core.service`
  - `monolith-firewall-webui.service`

Check journal:

- `journalctl -u monolith-firewall-core.service -n 200 --no-pager`
- `journalctl -u monolith-firewall-webui.service -n 200 --no-pager`

