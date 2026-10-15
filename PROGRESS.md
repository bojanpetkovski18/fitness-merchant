# Progress

Estimates are rough focused-work hours for agent and owner together, including review. Status values: `todo`, `in progress`, `in review`, `done`, `blocked`.

| # | Milestone | Branch | Estimate | Status | Verified on | Notes |
|---|---|---|---|---|---|---|
| P0 | Project setup (spec, rules, plans, CI) | `#0000-project-setup` | 2 h | done | 2026-10-13 | PR #1 merged; CI green (secret scan, repo lint) |
| M01 | Skeleton, CI, app shell | `#0001-solution-skeleton-ci-and-app-shell` | 6 h | done | 2026-10-13 | PR #2 merged; CI green (4 jobs). 7 API + 12 web tests; 78/78 browser checks |
| M02 | Data model and seed data | `#0002-catalog-schema-seed-data-and-artwork` | 6 h | done | 2026-10-13 | PR #3 merged; CI green (API job 47 s incl. Testcontainers). 57 tests (36 unit, 21 integration with Testcontainers); seeded dev DB: 8 categories, 6 brands, 62 products, 68 images |
| M03 | Authentication | `#0003-cookie-auth-identity-roles-and-csrf` | 6 h | done | 2026-10-13 | PR #4 merged; CI green. 72 API tests (36 unit, 36 integration) + 41 web tests; 42/42 browser checks; curl XSRF checks |
| M04 | Catalog (API + storefront) | `#0004-catalog-search-filters-and-storefront-pages` | 8 h | done | 2026-10-14 | PR #5 merged; CI green. 151 API tests (incl. oracle-checked filters/sorts) + 56 web tests; 41/41 browser checks |
| M05 | Cart (server + guest merge) | `#0005-server-cart-with-guest-cart-merge` | 6 h | done | 2026-10-14 | PR #6 merged; CI green. 178 API tests + 70 web tests; 32/32 browser checks (guest → sign-in merge → sign-out, SQL stock change, keyboard, axe) |
| M06 | Checkout and orders (fake gateway) | `#0006-checkout-orders-and-fake-payment-gateway` | 8 h | done | 2026-10-14 | PR #7 merged; CI green. 250 API tests + 75 web tests; 24/24 browser checks with both test cards, DB rows inspected, log grep clean |
| M07 | Stripe Checkout (test mode) | `#0007-stripe-checkout-test-mode-and-webhooks` | 6 h | blocked | none | Waiting for the owner's Stripe test key and Stripe CLI; M08 done first (no dependency between them) |
| M08 | My account | `#0008-account-profile-password-and-order-history` | 4 h | done | 2026-10-14 | PR #8 merged; CI green. 263 API tests (incl. stale-cookie regression shown failing first) + 87 web tests; 64/65 browser checks (the 1 is the K6 console line for the deliberate wrong-password 400), incl. two-browser session invalidation |
| M09 | Admin catalog and image upload | `#0009-admin-catalog-crud-and-image-upload` | 8 h | done | 2026-10-15 | PR #9 merged; CI green (first Linux run with SkiaSharp). 322 API tests (incl. route-table 401/403 and upload tests; 2 mutation checks) + 92 web tests (incl. table-focus regression shown failing first); 68/68 browser checks with real PNG/JPEG/WebP and a renamed .txt |
| M10 | Admin orders, dashboard, CSV | `#0010-admin-orders-dashboard-and-csv-export` | 8 h | in review | 2026-10-15 | 348 API tests (incl. 25-pair transition table, SQL cross-checked dashboard, CSV neutralization mutation-checked) + 98 web tests; 48/48 browser checks (every status, restock, CSV parse, dashboard = psql) + M09 rerun 68/68 |
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
| .NET test suite after M05 (178 tests) | 17.0–17.5 s (3 runs) | `dotnet test --solution FitnessMerchant.slnx --no-build` (MTP summary) | M1 | 2026-10-14 |
| Web initial bundle after M05 (adds cart store, toasts, CDK dialog) | 450.64 kB raw / 117.74 kB estimated transfer | `npm run build` output (CLI estimate) | M1 | 2026-10-14 |
| .NET test suite after M06 (250 tests) | 23.4–27.7 s (3 runs), 32.4 s once | `dotnet test --solution FitnessMerchant.slnx --no-build` (MTP summary) | M1 | 2026-10-14 |
| Concurrency: last-unit race | 15 runs of the 6-buyer HTTP race + deterministic two-transaction test: never oversold | 3 full suite runs | M1 | 2026-10-14 |
| Web initial bundle after M06 | 462.04 kB raw / 120.63 kB estimated transfer | `npm run build` output (CLI estimate) | M1 | 2026-10-14 |
| .NET test suite after M08 (263 tests; security stamp now checked per request) | 24.5–26.4 s (2 runs) | `dotnet test --solution FitnessMerchant.slnx --no-build` (MTP summary) | M1 | 2026-10-14 |
| Web initial bundle after M08 (account pages are lazy) | 466.56 kB raw / 121.89 kB estimated transfer | `npx ng build` output (CLI estimate) | M1 | 2026-10-14 |
| .NET test suite after M09 (322 tests; each admin test registers and signs in its own admin) | 42.2–44.6 s (2 runs) | `dotnet test --solution FitnessMerchant.slnx --no-build` (MTP summary) | M1 | 2026-10-15 |
| Web initial bundle after M09 (admin pages are lazy: 14–21 kB chunks) | 470.52 kB raw / 122.42 kB estimated transfer | `npx ng build` output (CLI estimate) | M1 | 2026-10-15 |
| .NET test suite after M10 (348 tests) | 49.1–50.6 s (2 runs) | `dotnet test --solution FitnessMerchant.slnx --no-build` (MTP summary) | M1 | 2026-10-15 |
| Web initial bundle after M10 (dashboard 10.9 kB and orders 15.9 kB lazy chunks) | 471.83 kB raw / 122.64 kB estimated transfer | `npx ng build` output (CLI estimate) | M1 | 2026-10-15 |
| Dashboard vs SQL, last 90 days of the dev DB | Dashboard €10,926.25 / 41 orders / €266.49 avg; psql `sum(total), count(*), round(avg,2)` over Paid+Shipped+Delivered in the same UTC range: 10926.25 / 41 / 266.49 | Browser tiles vs psql, same run | M1 | 2026-10-15 |
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
| K6 | A failed sign-in (401), and since M08 a wrong current password on the Password tab (400), shows one "Failed to load resource" entry in the browser console. Browsers log every 4xx; these are the correct statuses. | M03, M08 | Accepted; the console-clean checks exclude this deliberate case. |
| K7 | Intermittent local stalls (a 16-minute test run, a 5-minute build, and in M04 a run where every Testcontainers test failed at 0 ms). In M04 Docker Desktop was caught restarting (all containers "Up seconds") during the failed run, so the stalls are the local Docker daemon, not the code; reruns took 12–18 s. | M02–M04 | Nothing to change in the repo; CI runs have all been normal (API job 47–54 s). |
| K10 | Docker Desktop stalls also hit the dev API: one start failed with a Postgres connection timeout on `localhost:5433` while the container was healthy; a retry minutes later worked. At the same time another local project's container (`numera-app`) was crash-looping (exit 139), which may load Docker. | M06 | Local environment only (see K7). |
| K11 | Browser checks leave test data in the local dev database: archived "Test Lifting Straps …" products (ordered, so they cannot be deleted), their test orders (one forced back to "awaiting payment" by SQL, so its history shows Paid twice) and uploaded test images. Dev only; tests use their own databases. | M06–M10 | Cleared by the demo reset (M12). Note: the CLAUDE.md re-seed command dates from M02, before orders referenced products; it probably needs `CASCADE` now. Not run (destructive); to be fixed and verified with the demo reset. |
| K8 | The Brand filter lists all six brands in both departments, so picking a gear brand on Supplements gives an empty result (with a clear empty state). | M04 | Make brand facets department-aware (brands with products in the department) in M12 polish, or earlier if it bothers users. |
| K9 | "Top rated" is hidden on Home until reviews exist; the rating sort orders by name while every rating is 0. | M04 | Becomes meaningful with reviews (M11). |
| K2 | npm 11 skipped install scripts for `lmdb` and `msgpackr-extract` (optional native helpers for Angular's build cache). Build, tests and dev server work without them. | M01 | Leave as is unless builds become slow; approving scripts is a supply-chain decision for the owner. |

## Next step
The owner reviews and merges the M10 PR (`#0010-admin-orders-dashboard-and-csv-export`). M07 (Stripe Checkout in test mode) is still blocked on the owner's Stripe **test** secret key (`sk_test_…`, kept in the local `.env` only) and the Stripe CLI for local webhooks. Without them, M11 (verified-buyer reviews and ratings) can go next.
