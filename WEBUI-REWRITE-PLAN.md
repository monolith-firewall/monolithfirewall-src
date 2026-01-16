# WebUI Rewrite Plan

## Goal
Rebuild the WebUI around a manifest-driven SPA shell that runs after login, uses clean History API routing, and loads each page's HTML/JS/CSS from dedicated `/api/cms/page/...` endpoints. Package modules must register routes/assets in the same manifest so that both internal and external pages render consistently.

## Step 1 – Foundation & Environment
1. Documented in `AGENT.md`: how `setup-dependencies.sh` works, where `tmp/` lives, and how build scripts expect the external repositories.
2. Ensure `build-scripts/` remains the single source of truth for packaging (`build-all-packages.sh`, `build-deb.sh`).
3. Confirm the SPA is only activated after the login step; the login page stays separate because it should not rely on SPA assets.

## Step 2 – Manifest, Router, and Asset Strategy
1. `/api/cms/manifest` returns the complete menu, route definitions (`id`, `path`, `meta.module`, `requiresAuth`, asset references), plus default/login IDs.
2. SPA shell loads the manifest once and builds the global navigation. Each menu entry maps to a route ID.
3. Routing uses the History API: `router.push` performs `history.pushState`, `router.init` listens to `popstate`, and `router.navigate` fetches page content/asset definitions.
4. Pages load via `/api/cms/page/:package/:module/:page` (or `/:path` in the monolith core) returning `{ html, js: [...], css: [...], meta }`. The SPA renders the HTML, injects CSS `<link>` entries (with cache-busting), and loads JS modules sequentially.
5. Tab handling: each tab action resolves to a route update; the tab container cleans existing tab content before injecting new markup to prevent duplicates.

## Step 3 – Package Support
1. External packages (e.g., `monolith-diagnostics`, `monolith-network`, `monolith-vpn`) expose manifests that align with the core manifest structure (`package`, `module`, pages, assets).
2. `build-scripts/build-all-packages.sh` rebuilds their assets under `tmp/monolithfirewall-packages`. The router fetches JS/CSS from the package asset root (maybe `/pkg-assets/:package/...`).
3. For missing packages/pages, the SPA shows friendly errors (e.g., "Package page not found: ...") instead of raw exceptions.
4. Make sure packages define metadata (tabs, additional status) to avoid duplicates when multiple tabs reference the same DOM.

## Step 4 – Implementation & Integration
1. Bootstrapping: a lightweight entrypoint script loads once (after login) to fetch the manifest, render the menu, and start the router.
2. Each route update fetches the required HTML/JS/CSS, unloads previous scripts (if necessary), and updates page title/breadcrumbs.
3. Keep dependencies minimal: reuse existing utilities (`monolith.api.js`, `cms-client.js`) but update them to support the manifest/endpoint changes.
4. After implementing the new router, rebuild packages via `build-scripts/build-all-packages.sh`, then deploy with `build-scripts/build-deb.sh` to restart services and verify `/api/cms/page/*` endpoints provide the expected payloads.

## Verification
- Use the manifest menu to navigate to every internal module route and each package module route to ensure HTML/JS/CSS loads exactly once.
- Verify tab interactions (primary/secondary) no longer duplicate content and update the active state correctly.
- Confirm login remains separate, and the SPA only runs when authenticated.

## Notes
- All new endpoints should provide JSON with asset lists, so future package integrations can declare their files without editing the SPA.
- Keep track of `tmp/monolithfirewall-packages` as the canonical package source; don't edit it manually outside `build-scripts`.
