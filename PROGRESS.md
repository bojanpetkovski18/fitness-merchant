# Progress

Estimates are rough focused-work hours for agent and owner together, including review. Status values: `todo`, `in progress`, `in review`, `done`, `blocked`.

| # | Milestone | Branch | Estimate | Status | Verified on | Notes |
|---|---|---|---|---|---|---|
| P0 | Project setup (spec, rules, plans, CI) | `#0000-project-setup` | 2 h | done | 2026-10-13 | PR #1 merged; CI green (secret scan, repo lint) |
| M01 | Skeleton, CI, app shell | `#0001-solution-skeleton-ci-and-app-shell` | 6 h | in review | 2026-10-13 | 7 API + 12 web tests; 78/78 browser checks (3 widths × 2 themes, keyboard, axe, console) |
| M02 | Data model and seed data | `#0002-catalog-schema-seed-data-and-artwork` | 6 h | todo | none | Includes the original SVG artwork |
| M03 | Authentication | `#0003-cookie-auth-identity-roles-and-csrf` | 6 h | todo | none | |
| M04 | Catalog (API + storefront) | `#0004-catalog-search-filters-and-storefront-pages` | 8 h | todo | none | |
| M05 | Cart (server + guest merge) | `#0005-server-cart-with-guest-cart-merge` | 6 h | todo | none | |
| M06 | Checkout and orders (fake gateway) | `#0006-checkout-orders-and-fake-payment-gateway` | 8 h | todo | none | |
| M07 | Stripe Checkout (test mode) | `#0007-stripe-checkout-test-mode-and-webhooks` | 6 h | todo | none | Needs the owner's Stripe test keys |
| M08 | My account | `#0008-account-profile-password-and-order-history` | 4 h | todo | none | |
| M09 | Admin catalog and image upload | `#0009-admin-catalog-crud-and-image-upload` | 8 h | todo | none | |
| M10 | Admin orders, dashboard, CSV | `#0010-admin-orders-dashboard-and-csv-export` | 8 h | todo | none | |
| M11 | Reviews and ratings | `#0011-verified-buyer-reviews-and-ratings` | 4 h | todo | none | |
| M12 | Polish | `#0012-polish-states-responsive-a11y-perf-demo-reset` | 8 h | todo | none | Review the known issues here |
| M13 | E2E, Docker, Compose | `#0013-e2e-test-production-docker-and-compose` | 6 h | todo | none | |
| M14 | Deploy, README, portfolio | `#0014-deployment-readme-architecture-portfolio` | 6 h | todo | none | |

## Measured results
Every number cited in the README or docs must appear here, with its method, machine and date.

| Metric | Value | Method | Machine | Date |
|---|---|---|---|---|
| Web production build, initial bundle (scaffold + shell only) | 274.85 kB raw / 74.75 kB estimated transfer | `npm run build` output (Angular CLI estimate, not a network measurement) | M1 | 2026-10-13 |

Machines:
- **M1:** AMD Ryzen 7 8840HS (16 logical CPUs), 14 GB RAM, Windows 11 Pro 10.0.26200, Node 24.20.0, .NET SDK 10.0.401.

## Known issues
Deferred by the owner. Re-check them when relevant, and in M12.

| # | Issue | Found | Plan |
|---|---|---|---|
| K1 | **Production CSP vs Angular's critical-CSS inlining.** The production `index.html` contains an inline `<script>` (switches the stylesheet from `media="print"` to `all`) and an inline `<style>`. The API's CSP (`script-src 'self'; style-src 'self'`) will block both once the API serves the SPA, so the full stylesheet would never apply. Not visible on the dev server (no CSP). | M01, by inspecting `dist/web/browser/index.html` | Fix and verify in M13 when the API serves the build: Angular `security.autoCsp` (hashes) or `inlineCritical: false`, plus a nonce or hash strategy for component styles. |
| K2 | npm 11 skipped install scripts for `lmdb` and `msgpackr-extract` (optional native helpers for Angular's build cache). Build, tests and dev server work without them. | M01 | Leave as is unless builds become slow; approving scripts is a supply-chain decision for the owner. |

## Next step
The owner reviews and merges the M01 PR (`#0001-solution-skeleton-ci-and-app-shell`). Then M02 (catalog schema, seed data and artwork) starts on `#0002-catalog-schema-seed-data-and-artwork`.
