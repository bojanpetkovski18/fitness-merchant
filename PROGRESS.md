# Progress

Estimates are rough focused-work hours for agent and owner together, including review. Status values: `todo`, `in progress`, `in review`, `done`, `blocked`.

| # | Milestone | Branch | Estimate | Status | Verified on | Notes |
|---|---|---|---|---|---|---|
| P0 | Project setup (spec, rules, plans, CI) | `#0000-project-setup` | 2 h | done | 2026-10-13 | PR #1 merged; CI green (secret scan, repo lint) |
| M01 | Skeleton, CI, app shell | `#0001-solution-skeleton-ci-and-app-shell` | 6 h | done | 2026-10-13 | PR #2 merged; CI green (4 jobs). 7 API + 12 web tests; 78/78 browser checks |
| M02 | Data model and seed data | `#0002-catalog-schema-seed-data-and-artwork` | 6 h | done | 2026-10-13 | PR #3 merged; CI green (API job 47 s incl. Testcontainers). 57 tests (36 unit, 21 integration with Testcontainers); seeded dev DB: 8 categories, 6 brands, 62 products, 68 images |
| M03 | Authentication | `#0003-cookie-auth-identity-roles-and-csrf` | 6 h | done | 2026-10-13 | PR #4 merged; CI green. 72 API tests (36 unit, 36 integration) + 41 web tests; 42/42 browser checks; curl XSRF checks |
| M04 | Catalog (API + storefront) | `#0004-catalog-search-filters-and-storefront-pages` | 8 h | done | 2026-10-14 | PR #5 merged; CI green. 151 API tests (incl. oracle-checked filters/sorts) + 56 web tests; 41/41 browser checks |
| M05 | Cart (server + guest merge) | `#0005-server-cart-with-guest-cart-merge` | 6 h | in progress | none | |
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
| .NET test suite, full run (57 tests incl. Testcontainers) | about 12 s (11.2 s and 12.2 s in two runs) | `dotnet test --solution FitnessMerchant.slnx --no-build`; `time` and the MTP summary | M1 | 2026-10-13 |
| .NET test suite after M03 (72 tests) | 17.3 s | `dotnet test --solution FitnessMerchant.slnx --no-build` (MTP summary) | M1 | 2026-10-13 |
| Web initial bundle after M03 (shell + auth: HttpClient, forms, CDK menu/overlay) | 392.45 kB raw / 101.00 kB estimated transfer | `npm run build` output (CLI estimate) | M1 | 2026-10-13 |
| .NET test suite after M04 (151 tests) | 18.3 s | `dotnet test --solution FitnessMerchant.slnx --no-build` (MTP summary) | M1 | 2026-10-14 |
| Web initial bundle after M04 (catalog pages are lazy) | 440.58 kB raw / 113.61 kB estimated transfer | `npm run build` output (CLI estimate) | M1 | 2026-10-14 |
| Filtered product list query (dev DB, 62 products) | 7 ms page query, 165 ms first count (cold) | EF Core `Executed DbCommand` log in Development, single request | M1 | 2026-10-13 |
| Seed artwork size | 68 images, 60 kB total | `pg_size_pretty(sum(size_bytes))` on the seeded dev DB | M1 | 2026-10-13 |
| Web production build, initial bundle (scaffold + shell only) | 274.85 kB raw / 74.75 kB estimated transfer | `npm run build` output (Angular CLI estimate, not a network measurement) | M1 | 2026-10-13 |

Machines:
- **M1:** AMD Ryzen 7 8840HS (16 logical CPUs), 14 GB RAM, Windows 11 Pro 10.0.26200, Node 24.20.0, .NET SDK 10.0.401.

## Known issues
Deferred by the owner. Re-check them when relevant, and in M12.

| # | Issue | Found | Plan |
|---|---|---|---|
| K1 | **Production CSP vs Angular's critical-CSS inlining.** The production `index.html` contains an inline `<script>` (switches the stylesheet from `media="print"` to `all`) and an inline `<style>`. The API's CSP (`script-src 'self'; style-src 'self'`) will block both once the API serves the SPA, so the full stylesheet would never apply. Not visible on the dev server (no CSP). | M01, by inspecting `dist/web/browser/index.html` | Fix and verify in M13 when the API serves the build: Angular `security.autoCsp` (hashes) or `inlineCritical: false`, plus a nonce or hash strategy for component styles. |
| K3 | On the very first migration of an empty database, EF Core logs one `fail:` line (it queries `__EFMigrationsHistory` before creating it). Harmless; only on first startup. | M02 | Leave unless it confuses log alerting after deployment (M14). |
| K4 | One full test run took 16 min instead of about 12 s; not reproducible (3 later runs: 11–12 s, each test under 0.4 s except a deliberate 2 s timeout). Likely a Docker/Testcontainers stall on the dev machine. | M02 | Watch CI durations; the job has a 15-minute timeout. |
| K5 | The auth rate limiter partitions by `RemoteIpAddress`. Behind a reverse proxy every client would share the proxy's IP. | M03 | Configure forwarded headers (trusted proxy only) with deployment (M14). |
| K6 | A failed sign-in (401) shows one "Failed to load resource" entry in the browser console. Browsers log every 4xx; 401 is the correct status. | M03 | Accepted; the console-clean checks exclude this deliberate case. |
| K7 | Intermittent local stalls (a 16-minute test run, a 5-minute build, and in M04 a run where every Testcontainers test failed at 0 ms). In M04 Docker Desktop was caught restarting (all containers "Up seconds") during the failed run, so the stalls are the local Docker daemon, not the code; reruns took 12–18 s. | M02–M04 | Nothing to change in the repo; CI runs have all been normal (API job 47–54 s). |
| K8 | The Brand filter lists all six brands in both departments, so picking a gear brand on Supplements gives an empty result (with a clear empty state). | M04 | Make brand facets department-aware (brands with products in the department) in M12 polish, or earlier if it bothers users. |
| K9 | "Top rated" is hidden on Home until reviews exist; the rating sort orders by name while every rating is 0. | M04 | Becomes meaningful with reviews (M11). |
| K2 | npm 11 skipped install scripts for `lmdb` and `msgpackr-extract` (optional native helpers for Angular's build cache). Build, tests and dev server work without them. | M01 | Leave as is unless builds become slow; approving scripts is a supply-chain decision for the owner. |

## Next step
The owner reviews and merges the M04 PR (`#0004-catalog-search-filters-and-storefront-pages`). Then M05 (server cart with guest cart merge) starts on `#0005-server-cart-with-guest-cart-merge`.
