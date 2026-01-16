# WebUI Agent

## Step 1: Environment Primer
- Always run `./setup-dependencies.sh` after pulling new commits. The script clones/updates `tmp/CodeLogic3`, `tmp/CodeLogic3.Libs`, and `tmp/monolithfirewall-packages`, removes the obsolete `../libs`, and ensures expected projects/assets exist (e.g., `tmp/CodeLogic3/src/CodeLogic.csproj`).
- `tmp/` is gitignored, so treat it as a cache of third-party sources; do not commit changes from inside it. If a repo in `tmp/` exists but is not a git repo, `setup-dependencies.sh` will delete and reclone it.
- Build scripts look for packages in `tmp/monolithfirewall-packages/`, so keep that tree aligned with the script output.

## Step 2: Routing + Manifest Strategy
- The SPA shell will fetch `/api/cms/manifest` (with `menu`, `routes`, default/login IDs) and render the global navigation/menu from the manifest.
- Replace hash-based fragments with History API navigation (`pushState`/`popstate`) to keep clean URLs. Maintain a standalone login page that is *not* part of the SPA shell.
- Introduce `/api/cms/page/:package/:module/:page` that returns `{ html, js, css }` references (with optional metadata) so one call yields the content needed for a page.
- Keep a manifest-driven asset loader so each route fetches its specific JS/CSS on demand; ensure duplicate tab navigation is handled (render fresh or reuse DOM).

## Step 3: Package Integration Plan
- Every package under `tmp/packages/` (the external `monolithfirewall-packages` repo) must expose a manifest entry aligned with the new routing schema (including `package`, `module`, `page`, assets).
- `build-scripts/build-all-packages.sh` should be the canonical way to rebuild packages; document how this script ties into `/tmp/packages` and how to install builds (e.g., `build-scripts/build-deb.sh` for deploying services).
- The SPA router will resolve package modules by requesting `/api/cms/page/:package/:module/:page`, so each package needs matching HTTP endpoints and asset URLs (JS/CSS) that the router can load dynamically.

## Step 4: Implementation Guidance
- When implementing the rewrite, keep the SPA shell lean: one entry template that swaps in page HTML, wires manifest-based routes, and loads assets scoped per page.
- Handle menu/tab states in sync with `popstate`: push the current route, record active tabs, and avoid rerendering a tab if the target is already active.
- After coding changes, rerun `setup-dependencies.sh` (if deps change) and use `build-scripts/build-all-packages.sh` followed by `build-scripts/build-deb.sh` to refresh the runtime package blobs.

Refer back to this file whenever new developers or agents start working in this area.
