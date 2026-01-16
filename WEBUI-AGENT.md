# WebUI Agent Notes

Use this file as the quick start guidance for agents working on the WebUI rewrite.

## Dependencies
- Always run `./setup-dependencies.sh` whenever you pull new changes or need to refresh third-party repos. It clones/updates `tmp/CodeLogic3`, `tmp/CodeLogic3.Libs`, and `tmp/monolithfirewall-packages` and removes `../libs`.
- The WebUI build expects packages under `tmp/monolithfirewall-packages/`; do not relocate or delete that folder manually.
- `tmp/` is gitignored, so keep hand-written changes out of there.

## Build & Testing
- Build scripts live under `build-scripts/` and often assume you have up-to-date dependencies. Use `build-scripts/build-all-packages.sh` before testing package pages and `build-scripts/build-deb.sh` when packaging or restarting services.
- Login/setup pages are served from Razor Pages—do not treat them as part of the SPA.

## Goals for the WebUI rewrite
- Keep the login flow outside the SPA and ensure the SPA loads only after successful authentication.
- Replace hash-based routing with History API / popstate navigation.
- Simplify menus/routes by relying on a manifest (`/api/cms/manifest`) and dedicated endpoints for pages/assets.
- Make package modules load via `/tmp/packages` assets and surface friendly error messages when a module or page is missing.

## Troubleshooting
- Inspect `setup-dependencies.sh` to understand how external repos are structured before making changes.
- When packages fail to load, run `build-scripts/build-all-packages.sh` and check `tmp/monolithfirewall-packages/` for installed modules.
