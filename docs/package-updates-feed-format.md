# Package updates feed (Monolith packages)

This documents the JSON format consumed by `Monolith.FireWall.WebUI` at `GET /api/packages/available`.

For now this is **packages-only**. (OS updates can be added later.)

## Endpoint shape

The WebUI calls:

- `GET {BaseUrl}?version={coreVersion}`

Where `BaseUrl` is `PackageUpdates:BaseUrl` (see `src/Monolith.FireWall.WebUI/appsettings.json`).

The response can be either:

- `{ "packages": [ ... ] }`, or
- `{ "data": [ ... ] }`, or
- `[ ... ]`

## Package entry shape

Each entry should be an object with these fields:

```json
{
  "id": "monolith-network",
  "name": "Monolith Network",
  "version": "1.2.3",
  "description": "DHCP/DNS management",
  "downloadUrl": "https://updates.example.com/files/monolith-network-1.2.3.mfwpkg",
  "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",

  "author": "Monolith",
  "homepage": "https://monolithfirewall.com",
  "releaseNotes": "Bug fixes and improvements",

  "minCoreVersion": "1.0.0",
  "maxCoreVersion": null,
  "requiresRestart": false
}
```

### Notes

- `downloadUrl` **should be HTTPS**. For development only, HTTP is allowed if `PackageUpdates:AllowInsecureHttp` is `true` (or if the host is localhost).
- If `sha256` is provided, the WebUI will verify the downloaded `.mfwpkg` before asking Core to install it.
- The `.mfwpkg` file is downloaded by the WebUI to `/var/lib/monolith-firewall/packages-cache/` and then installed by Core from that local path.

## Field aliases (accepted)

The WebUI parser accepts a few common aliases:

- `id`: also `packageId`, `package_id`
- `downloadUrl`: also `download_url`, `url`
- `sha256`: also `sha_256`, `checksum`, `hash`

