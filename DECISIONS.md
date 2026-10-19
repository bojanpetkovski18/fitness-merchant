# Decisions

Numbered, never renumbered. Superseded decisions stay with a note. "Pending" means the decision is proposed and gets confirmed (or changed) at the milestone named.

---

## D001: Rebuild from scratch; v1 is a feature reference only
- **Date:** 2026-10-13
- **Context:** The 2024 thesis version (.NET 8, Angular 17, SQL Server) works as a demo but has structural and security problems (SPEC section 3): client-controlled roles, IDOR on carts and payments, `double` money, committed secrets, whole tables loaded into memory, and no tests.
- **Decision:** New repository and new code. v1 is used only to list features and screens; no code is copied.
- **Why:** Fixing v1 in place would touch almost every file; a clean design is cheaper and gives a cleaner history for the portfolio.
- **Alternatives:** Incremental refactor of v1 (keeps its layered/repository structure and migrations history, which we do not want).
- **Trade-offs:** More up-front work; the v1 seed content (real brands and photos) cannot be reused anyway (D002).

## D002: Fictional data and original artwork only
- **Date:** 2026-10-13
- **Context:** v1 used real brands (Optimum Nutrition, Nike, Rogue Fitness, Under Armour, Life Fitness), their logos, product photos and YouTube videos. A public portfolio repo and demo must not use trademarks or copyrighted media without permission.
- **Decision:**
  - Invented brands with SVG wordmark logos.
  - Product images drawn as original SVG artwork for this repo.
  - Demo users on the reserved `.example` domain.
- **Why:** Legal safety and the project's data rules.
- **Alternatives:** Stock photo services, which add licence tracking and attribution and still look generic.
- **Trade-offs:** Illustrations look less "real" than photos; mitigated with a consistent art style.

## D003: Backend: ASP.NET Core on .NET 10 LTS, Minimal APIs, one project with feature folders
- **Date:** 2026-10-13
- **Context:** v1 had three projects (Api, Entities, Services) with a generic repository and an interface per CRUD verb.
- **Decision:**
  - One `FitnessMerchant.Api` project with three areas:
    - `Features/<Feature>/`: endpoints plus one handler class per command or query;
    - `Domain/`: entities and rules;
    - `Infrastructure/`: EF Core, payments, images.
  - Endpoints only bind, authorize and call a handler.
  - Handlers use `DbContext` directly; there is no repository layer.
  - No MediatR, AutoMapper or FluentValidation; built-in .NET 10 validation is used instead.
- **Why:** Light CQRS without framework overhead. EF Core already is a unit of work and repository. Fewer projects mean faster builds and simpler navigation.
- **Alternatives:** Clean Architecture with 4+ projects (more ceremony than the domain needs); controllers (more boilerplate).
- **Trade-offs:** Layer boundaries are enforced by convention and review, not by project references.
- **Verified:** SDK 10.0.401 installed locally (2026-10-13). Package versions to be pinned at M01.

## D004: PostgreSQL instead of SQL Server
- **Date:** 2026-10-13
- **Decision:** PostgreSQL (version pinned at M01) with EF Core and Npgsql.
- **Why:**
  - Free managed tiers are common.
  - Small official Docker image.
  - `pg_trgm` gives fuzzy product search.
  - `xmin` works as a free concurrency token.
- **Alternatives:** SQL Server (v1; heavier container, fewer free hosted options), SQLite (no realistic concurrency story).
- **Trade-offs:** v1 SQL Server migrations are not reusable; they would not be reused anyway (D001).

## D005: Cookie session auth with ASP.NET Core Identity, not JWT in localStorage
- **Date:** 2026-10-13
- **Decision:**
  - Identity (Guid keys) with an `HttpOnly; Secure; SameSite=Strict` auth cookie.
  - Antiforgery token sent as `X-XSRF-TOKEN`, using Angular's built-in XSRF support.
  - Custom thin endpoints (register, sign-in, sign-out, me) rather than `MapIdentityApi`, to control the response shape and rate limits.
- **Why:**
  - The SPA and API share an origin (D008), so cookies are the simplest secure option.
  - Tokens are not readable by JavaScript.
  - Sign-out really ends the session.
- **Alternatives:** JWT bearer in localStorage (v1; XSS can steal the token, and logout is cosmetic); BFF with OIDC (overkill).
- **Trade-offs:** Needs CSRF protection (done with antiforgery plus SameSite); the API is not convenient for third-party clients (not a requirement).

## D006: Frontend: Angular 22, standalone, zoneless, Signals; signal services for shared state
- **Date:** 2026-10-13
- **Context:**
  - npm on 2026-10-13: `@angular/core` latest is 22.2.2.
  - 22.2.0 was published 2026-09-23 and is the newest release at least two weeks old.
- **Decision:**
  - Angular 22.2.0 (re-check at M01), standalone components, zoneless change detection, Signals, lazy routes, strict TypeScript and strict templates.
  - Cross-screen state (session, cart) lives in signal-based injectable stores; screen state stays in components.
  - No NgRx unless a need appears.
- **Why:** The modern Angular defaults are less code, with no extra dependency for two small shared stores.
- **Alternatives:** NgRx SignalStore (a fine fit, but one more dependency to justify).
- **Trade-offs:** No devtools time travel.
- **Verified at M01 (2026-10-13):** `ng new` with CLI 22.2.0 (zoneless, Vitest, Tailwind). Exact pins: Angular 22.2.0, TypeScript 6.0.3 (the major Angular 22 installs), RxJS 7.8.2. The scaffold's `^` ranges resolved to 22.2.2 (5 days old), so versions are now exact. The generated tsconfig did **not** enable `strict`, so `strict`, `noUncheckedIndexedAccess`, `strictTemplates`, `strictStandalone` and extended diagnostics as errors are now set explicitly.

## D007: UI: Tailwind CSS v4 plus Angular CDK, not Material or Bootstrap
- **Date:** 2026-10-13
- **Decision:** Tailwind CSS v4 with design tokens as CSS variables (theme switching); Angular CDK for overlays, focus trap, a11y utilities and stepper behaviour. If `@angular/aria` is stable at M01, use it for headless accessible widgets.
- **Why:** v1 combined Material and Bootstrap and looked generic. Tailwind gives a distinctive, consistent look with a small CSS output, and CDK covers the hard accessibility parts.
- **Alternatives:** Angular Material 3 (fastest accessible widgets, but a recognizably "Material" look); PrimeNG (heavy).
- **Trade-offs:** We own the visual components, so accessibility must be tested (axe and keyboard checks every milestone).
- **Confirmed at M01 (2026-10-13):**
  - Tailwind 4.3.3 via PostCSS (the CLI's `--style tailwind` setup); `@angular/cdk` 22.2.0.
  - Theme tokens are CSS variables mapped into Tailwind with `@theme inline`, so utilities such as `bg-surface` follow the active theme.
  - `@angular/aria` 22.2.0 exists but is not adopted yet. M01 only needed the CDK focus trap; re-evaluate it in M04 for the filter widgets.

## D008: One deployable: the API serves the built SPA (same origin)
- **Date:** 2026-10-13
- **Decision:**
  - Production: one container. ASP.NET Core serves the Angular build from `wwwroot` with a fallback to `index.html` for client routes, plus `/api/*`.
  - Development: the Angular dev server proxies `/api` to the API.
- **Why:** No CORS, cookie auth works simply (D005), one service to host (cheaper, D016), and one CSP to manage.
- **Alternatives:** Separate static hosting plus an API (CORS, cross-site cookies, two deploys).
- **Trade-offs:** Frontend and backend deploy together; that is fine for this project.

## D009: Uploaded images stored in PostgreSQL
- **Date:** 2026-10-13
- **Decision:** Images are stored as `bytea`, content-addressed by SHA-256, and served from `/api/images/{id}` with immutable cache headers. Uploads are validated by magic bytes, limited to 1 MB and re-encoded to WebP.
- **Why:** Free and cheap hosts often have no persistent disk. The volume is small (fewer than 200 images); demo reset becomes trivial.
- **Alternatives:** Object storage (S3/R2: another account and secret); local disk volume (lost on redeploy on many hosts).
- **Trade-offs:** DB size grows with images; acceptable at this scale. Revisit if the image count grows.
- **M02 note (2026-10-13):** seed artwork is SVG generated in code (`image/svg+xml`), not WebP. Only plain shapes and text are allowed; unit tests reject scripts, styles, `foreignObject`, `href` and `on*` attributes. Uploads (M09) stay PNG/JPEG/WebP and are re-encoded to WebP. The whole seed set is 68 images, 60 kB.

## D010: Payment gateway behind an interface: Fake plus Stripe Checkout test mode
- **Date:** 2026-10-13
- **Decision:**
  - An `IPaymentGateway` interface with two implementations, selected by configuration:
    - Fake: deterministic test cards; used locally, in tests and in E2E.
    - Stripe Checkout in test mode: hosted payment page plus a signature-verified, idempotent webhook.
- **Why:** The demo then shows a real payment integration, while tests and E2E stay deterministic and offline. Two real implementations justify the interface.
- **Alternatives:** Fake only (simpler, less convincing); Stripe only (flaky E2E, requires keys to run locally).
- **Trade-offs:** Webhook handling and pending-order expiry add work (one milestone, M07).
- **Confirmed:** by the owner on 2026-10-13. The owner provides the Stripe test keys at M07.

## D011: Test stack
- **Date:** 2026-10-13
- **Decision:**
  - xUnit v3 for unit tests.
  - Integration tests with `WebApplicationFactory` and Testcontainers PostgreSQL (real database, real migrations).
  - Angular component and service tests with Angular's default runner (Vitest; to confirm at M01).
  - One Playwright E2E flow against the production Docker image.
- **Why:** Tests against a real Postgres catch query, constraint and concurrency bugs that an in-memory provider hides.
- **Trade-offs:** Integration tests need Docker (available locally; available on GitHub-hosted Ubuntu runners).
- **Verified at M01 (2026-10-13):**
  - xUnit v3 4.0.1 runs on Microsoft.Testing.Platform. The .NET 10 SDK refuses the VSTest path for it, so `global.json` opts `dotnet test` into MTP, and `Microsoft.NET.Test.Sdk` / `xunit.runner.visualstudio` are not used.
  - Vitest 5.0.2 with jsdom 30.1.1 is the CLI's default runner (`--test-runner` default `vitest`).
  - Testcontainers.PostgreSql 4.15.0 (published 2026-09-06; 4.16.0 was too new), image `postgres:18.6-alpine` like dev.
- **Test isolation (M02):**
  - One container per run (xUnit v3 assembly fixture). A template database is migrated and seeded once; each test gets its own clone via `CREATE DATABASE … TEMPLATE`.
  - No reset library (Respawn) and no shared state between tests, so tests can run in parallel. The full suite takes about 12 s on M1.

## D012: Money and currency
- **Date:** 2026-10-13
- **Decision:** EUR only, prices include VAT, `numeric(12,2)` in the DB and `decimal` in C#. Rounding is `MidpointRounding.AwayFromZero` at line level. The API sends amounts as JSON numbers with two decimals. Angular formats them with `CurrencyPipe` and never does money arithmetic beyond display.
- **Why:** Exact decimals; the server is the single source of truth for totals.

## D013: Branches and milestone numbering
- **Date:** 2026-10-13
- **Decision:**
  - The branch for milestone `Mnn` is `#00nn-descriptive-name`: the number plus 3–7 kebab-case words describing what it delivers (e.g. `#0004-catalog-search-filters-and-storefront-pages`). Phase 0 was `#0000-project-setup`.
  - The planned names are listed in `PROGRESS.md` and in each milestone brief.
  - PRs target `dev`, which is the GitHub default branch.
- **Changed 2026-10-13:** The owner asked for more descriptive names than the original short ones (`#0004-catalog`).

## D014: Visual identity: dark-first with an amber accent, light theme available
- **Date:** 2026-10-13
- **Decision:** Keep v1's recognisable identity (near-black surfaces, gold/amber accent, bold italic display headings) but modernise it: a token-based palette, a light theme, system-preference detection with a toggle, and AA contrast in both themes.
- **Why:** Continuity with the thesis version; a dark storefront also stands out in a portfolio.

## D015: Guest cart in the browser, merged on sign-in; checkout needs an account
- **Date:** 2026-10-13
- **Decision:** Guests keep a cart in localStorage (product ID and quantity only; prices are always re-read from the server). On sign-in the guest cart is merged into the server cart (quantities summed, capped at 10 and at stock), then cleared. Checkout requires sign-in.
- **Why:** Modern shopping UX without the complexity of guest orders and order lookup by email.
- **Alternatives:** Login required to add to cart (v1; poor UX); full guest checkout (out of scope).

## D016: Hosting chosen at M14 after current research
- **Date:** 2026-10-13
- **Decision:** Defer. Candidates to research then: one container host plus one managed Postgres with a free or low tier. The decision records limits, cold starts and monthly cost, with sources and dates.
- **Why:** Free tiers change often; research must be current at deploy time.

## D017: Training programs dropped
- **Date:** 2026-10-13
- **Context:** v1 had a "Programs" page linking YouTube videos with like and dislike counters. The Phase 0 proposal reworked it into original written training plans that users could save (one milestone).
- **Decision:** The owner dropped the feature. The project focuses on the store. Milestones after M11 moved up by one (Polish is now M12, E2E/Docker M13, Deploy/docs M14). SPEC feature number F12 is left unused.
- **Why:** A tighter scope and a store-centred portfolio story.
- **Trade-offs:** Less to set the project apart from a generic shop; this can be added later as its own milestone if wanted.

## D018: CI from Phase 0: secret scan and repo lint
- **Date:** 2026-10-13
- **Context:** The repository is public from day one, and the owner wants GitHub Actions visible from the start. There is no application code yet.
- **Decision:** `.github/workflows/ci.yml` runs on pushes to `dev`/`main`, on PRs into them, and on manual dispatch. It has two jobs:
  - **Secret scan:** gitleaks over the full git history.
  - **Repository lint:** actionlint on the workflows, plus editorconfig-checker.

  M01 adds the build, lint and test jobs to the same workflow.
- **Supply chain:**
  - `actions/checkout` is pinned to a commit SHA (v7.0.1, released 2026-07-20).
  - The tools are downloaded from their GitHub releases and verified against the published SHA-256:
    - gitleaks 8.30.1 (2026-03-21);
    - actionlint 1.7.12 (2026-03-30);
    - editorconfig-checker 4.0.2 (2026-09-19).

    All were the latest releases and at least two weeks old on 2026-10-13.
  - The workflow token is read-only (`permissions: contents: read`).
- **Why:** Catches leaked secrets and broken workflows before they reach `dev`. A plain binary avoids the gitleaks-action licence-key requirement and third-party action code.
- **False positive handled:** the Stripe-style placeholder in `.env.example` (commit 159459b) is listed in `.gitleaksignore` by fingerprint. The placeholders are now empty values. History was not rewritten because no real secret was involved.
- **Markdown indentation:** `.editorconfig` unsets indentation rules for `*.md`, because nested items under numbered lists use 3 spaces.
- **Trade-offs:** Version bumps are manual (update the version and checksum together). Dependabot can be added later for `actions/*`.

## D019: M01 toolchain and conventions
- **Date:** 2026-10-13
- **Versions** (all at least two weeks old on 2026-10-13; NuGet and npm registries queried that day):
  - **Repository and runtime:** .NET SDK 10.0.401 (`global.json`, `rollForward: latestFeature`), runtime 10.0.12, Central Package Management.
  - **API packages:** `Microsoft.AspNetCore.OpenApi` 10.0.12, `Scalar.AspNetCore` 2.17.11 (2.18.0 was too new).
  - **Test packages:** `Microsoft.AspNetCore.Mvc.Testing` 10.0.12, `xunit.v3` 4.0.1.
  - **Web tooling:** ESLint 10.11.0, `@eslint/js` 10.0.1, `typescript-eslint` 8.71.0, `angular-eslint` 22.5.0, Prettier 3.9.9, PostCSS 8.5.28.
  - **Database and CI:** PostgreSQL image `postgres:18.6-alpine` (published 2026-09-21); CI actions `setup-dotnet` v6.0.0, `setup-node` v7.0.0 (v7.1.0 too new), `cache` v6.1.0, all SHA-pinned.
- **Analyzers:**
  - `AnalysisLevel` latest-recommended, `EnforceCodeStyleInBuild` and `TreatWarningsAsErrors` apply locally too, not just in CI, so a green local build means a green CI build.
  - CA1707 (underscores in member names) is disabled for `tests/**` only, because test names read as sentences.
- **Dev database port 5433:** port 5432 was already taken on the dev machine by a local PostgreSQL. 5433 avoids the clash and is configurable through `POSTGRES_PORT`.
- **Mobile-menu focus:**
  - Focus is managed explicitly: on open, the first link is focused after render; on close, focus returns to the menu button. The CDK trap only contains Tab.
  - `cdkTrapFocusAutoCapture` was removed because, on destroy, it restored focus to whatever was focused when the menu opened. The browser pass caught this; the regression test is in `site-header.spec.ts`.
- **Browser verification until M13:** Playwright 1.63.0 with `@axe-core/playwright`, run from a scratch folder outside the repo. M13 adds the committed Playwright suite.
- **Deferred:** startup validation of required configuration starts in M02 with the first required setting (the connection string). M01 has no required settings.

## D020: Data layer and seed conventions (M02)
- **Date:** 2026-10-13
- **Naming:** `EFCore.NamingConventions` 10.0.1 maps everything to snake_case. Hand-written SQL (psql checks, dashboard queries in M10) then needs no quoted identifiers.
- **Integrity in the database too:**
  - Check constraints: price ≥ 0, discount 0–90, stock ≥ 0, rating 0–5, accent colour `#rrggbb`.
  - Unique slugs, SKUs and brand names.
  - `RESTRICT` on category and brand foreign keys; `SET NULL` when an image is removed.
  - `Product.Version` maps to PostgreSQL's `xmin` system column as the optimistic-concurrency token (no extra column).
- **Configuration:**
  - `ConnectionStrings:Database` is required at startup with a clear error message (the M01 deferral is done).
  - In Development only, the API reads the repository-root `.env` (the file Docker Compose uses); real environment variables win. This avoids keeping settings in two places (`.env` plus user-secrets).
- **Seeding:**
  - Runs at startup when `Seed__Enabled=true`, only into an **empty** catalog, in one transaction.
  - IDs are deterministic (RFC 9562 version 8 from SHA-256 of a key such as `product:<slug>`); timestamps are relative to the seed time, so "new arrivals" stays fresh.
  - The M12 demo reset empties the catalog and seeds again.
  - SKUs are `<brand prefix>-<0001>`.
  - Images are content-addressed. A unit test requires every product to have distinct artwork, because identical SVGs would otherwise silently share one image; the browser contact sheet exposed exactly that for three products.
- **Health:** `/health/live` checks only the process; `/health` includes `AddDbContextCheck` and returns 503 when the database is unreachable.
- **Analyzers and generated code:**
  - The Migrations folder is marked `generated_code = true` and `charset = utf-8-bom`, because EF writes migrations with block namespaces, constant arrays and a BOM.
  - Logging uses source-generated `[LoggerMessage]` methods (CA1848).
- **Trade-offs:**
  - Seeding never updates an existing catalog. Changing seed content means re-seeding (documented in CLAUDE.md Commands). Acceptable for a demo store with a reset.
  - SHA-256-derived IDs are not time-ordered like v7. Fine for about 70 seed rows; runtime rows use v7.

## D021: Authentication details (M03)
- **Date:** 2026-10-13
- **Identity:**
  - `AddIdentityCore` with roles, EF stores and SignInManager; Guid keys; snake_case tables (`users`, `roles`, `user_roles` and the rest).
  - Both roles are inserted by the migration with fixed IDs, so registration works with seeding off.
  - .NET 10's optional passkeys table is not mapped (schema version 3 not opted into).
- **Passwords and lockout:** SPEC section 5 (at least 10 characters plus Identity's character-class defaults); lockout after 5 failures for 5 minutes, also for new users.
- **Session cookie:**
  - `fm_session`: HttpOnly, SameSite=Strict, Secure everywhere except Development over http; 7-day sliding expiry; persistent.
  - Redirects are replaced by 401/403, which the status-code-pages middleware renders as problem details.
- **Authorization:**
  - The fallback policy is "authenticated user"; health, images, auth and the dev API explorer are explicitly anonymous.
  - The `Admin` policy requires the Admin role.
  - Side effect: an unknown `/api` path gives guests 401 (no route probing) and signed-in users 404.
- **CSRF (refines D005):**
  - `AntiforgeryValidationMiddleware` validates every unsafe `/api` request, including sign-in and register, which blocks login CSRF. The built-in antiforgery middleware only checks form posts.
  - Endpoints can opt out with `.DisableAntiforgery()`; the Stripe webhook in M07 will.
  - The readable `XSRF-TOKEN` cookie is reissued whenever the user changes. Verified: a token from before sign-in is rejected after it.
- **`/api/auth/me`** returns 200 `{ user: null }` for guests instead of 401, because browsers log every 4xx as a console error and guests load it on every page.
- **Generic failures and lockout:**
  - Wrong password, unknown email and locked-out account all return the same 401 "Invalid email or password."
  - For unknown emails a dummy password hash is computed, so response timing does not reveal which emails exist.
  - Registering an existing email does say so (a field error). That is unavoidable for usable sign-up and acceptable here.
- **Rate limiting:**
  - Sign-in and register are limited to 10 requests per 60 s per client IP (`RateLimiting__AuthPermitLimit` / `__AuthWindowSeconds`). The response is 429 problem details with `Retry-After`.
  - "Per account" protection is Identity lockout.
  - Behind a reverse proxy the client IP needs forwarded headers; tracked as K5 for deployment (M14).
- **Demo users:**
  - Created from `DEMO_ADMIN_*` / `DEMO_CUSTOMER_*` at startup when seeding is on; never from the repository.
  - Existing accounts are not changed; a missing setting skips that user with a warning; errors log Identity error codes, never the password.
- **Test seam:** `IEndpointMapper` implementations in DI are mapped at startup. Production registers none; tests add an admin-only and an unannotated probe endpoint to test the policies.
- **Frontend:**
  - `SessionStore` (signals) loads through `provideAppInitializer` and never throws, so the app still starts as a guest if the API is down.
  - Nothing auth-related goes into web storage (verified in the browser).
  - Return URLs are restricted to same-site paths (open-redirect guard).
  - Reactive Forms (stable) rather than experimental Signal Forms.
  - CDK Menu for the account menu, with its overlay CSS added to the build.
- **Analyzers:** CA1861 (constant array arguments) is disabled for tests, like CA1707.

## D022: Catalog API and storefront (M04)
- **Date:** 2026-10-14
- **List queries:**
  - One handler per query, read-only (`AsNoTracking`) projections.
  - Filters and sorting run on the entity and the projection comes last, because EF cannot translate filters over a constructor-built record. Found by a 500 in the smoke test.
  - Each list request runs exactly one `COUNT` and one paged `SELECT`, verified in the EF SQL log.
- **Sale price in SQL:** price filters and sorting use the sale price computed in SQL (`round(price * (100 - discount) / 100, 2)`); PostgreSQL rounds numeric half away from zero, like the domain rule. An integration test compares every API sale price with `Pricing.SalePrice`.
- **Validation:** every query parameter is parsed as a string and validated, returning 400 problem details that name the field. Sort keys come from an allow-list, and every sort ends with the ID so paging is stable.
- **Search:**
  - `ILIKE` on product and brand names with LIKE wildcards escaped, plus trigram `word_similarity` ≥ 0.45 for terms of 4+ characters, ranked by similarity.
  - Thresholds were measured on the seed data: "protien" 0.50, "creatin" 0.88; short terms such as "bar" were too noisy.
  - Trigram functions ship in the main Npgsql provider; the old Trigrams plugin package stopped at 5.0.
- **Stock status:** in-stock above 5, low-stock 1–5, out-of-stock 0. `maxOrderQuantity` is stock capped at 10 (SPEC section 5). Enums are serialized as kebab-case strings.
- **Images:** a strong ETag (the content hash) and `Cache-Control: public, max-age=31536000, immutable`; If-None-Match gets a 304. The header is set only for found images, after a first version would also have cached 404s.
- **Frontend data loading:**
  - `httpResource` (`@publicApi 22.0`, stable) for every section, so each has independent loading, error and success states.
  - **`value()` throws while a resource is in its error state**, so reads outside a `hasValue()` branch go through guarded computeds. A component test caught this; it would otherwise have crashed exactly the error states it should show.
- **Listing state:**
  - Kept entirely in the URL (a pure, unit-tested parse/serialize module). Malformed URL values fall back to defaults; the API still validates.
  - Search is debounced (350 ms) and uses `replaceUrl`, so typing adds no history entries (verified: 0 entries for 7 keystrokes).
  - Filter changes reset to page 1.
- **Layout and accessibility:**
  - Filters sit in a sidebar from 768 px; below that a CDK Dialog drawer with `ariaModal: true` (CDK no longer sets it by default), focus inside, Escape and focus restore.
  - Product cards have one link stretched over the card; screen readers get price, discount and rating as text.
  - The first 4 cards of a grid load eagerly with `fetchpriority=high` (Angular NG0913 flagged the lazy LCP image).
- **"Top rated" on Home** is hidden until products have reviews (M11), rather than ranking unrated products.
- **Focus management fix (amends D019):** the shell moves focus to `<main>` only when the URL *path* changes. Query-only navigations (filters, sort, paging) keep focus where the user is. Before the fix, ticking a filter sent keyboard users to the top of the content and broke the drawer's focus restore. Regression test in `app.spec.ts`.

## D023: Cart (M05)
- **Date:** 2026-10-14
- **Schema:**
  - One cart per user (unique `user_id`), created on the first write.
  - `cart_items` keyed by (cart, product); quantity 1–10 enforced by a check constraint.
  - Lines cascade with the cart, and with the product (only possible for never-ordered products, M09).
- **Ownership:** every cart route resolves the user from the session (`ClaimsPrincipal.GetUserId()`); there is no cart ID in any route. Tested: another user's remove and clear calls leave a cart untouched.
- **Concurrency:** writes are parameterized PostgreSQL `INSERT … ON CONFLICT` upserts (cart and line), so parallel adds cannot hit unique-key errors. Tested with 10 concurrent writes to one line.
- **Pricing:**
  - One `CartPricer` serves the stored cart and the guest quote: current sale prices, one product query per call.
  - Per-line `issue`: `unavailable` and `out-of-stock` lines are excluded from totals; `exceeds-stock` lines are counted at what is buyable.
  - Shipping follows `CartPricing` (domain, unit-tested at €59.99, €60.00 and €60.01).
  - The 10-per-line cap moved from the catalog feature into the domain.
- **Write rules:**
  - `PUT` rejects archived or unknown products (404) and out-of-stock or over-stock quantities (409 with `maxQuantity`). It does not silently cap, so the UI can say why.
  - **Merge is lenient** (sums, caps at min(10, stock), skips stale items), because guest data can be days old.
- **Guest quote:** `POST /api/cart/quote` is anonymous and rate-limited (120 per minute per IP, `RateLimiting__QuotePermitLimit`), and stores nothing. Guests see real server totals without the browser ever computing money (D012).
- **Frontend (refines D015):**
  - `CartStore` keeps one interface over two backends.
  - Guest localStorage holds only `{ productId, quantity }`; malformed entries are dropped on read.
  - Signed-in changes are optimistic (quantities only; money stays as last priced) and roll back on error. Failed guest changes roll back the stored items too.
  - Merging on sign-in and emptying on sign-out are driven by an effect on the session user.
- **Accessibility:**
  - Toasts use two live regions that always exist (polite and assertive); regions inserted later are often not announced, and `display: contents` can drop roles.
  - The cart link's name carries the count ("Cart, 3 items"); the badge is `aria-hidden`.
  - Steppers and remove buttons are named per product.
  - Emptying the cart asks in a CDK `alertdialog` that starts on Cancel.
  - Checkout is `aria-disabled` with a described reason while lines have issues.
- **Test infrastructure:** the integration suite outgrew PostgreSQL's 100 connections (one pool per test database). Test connection strings now prune idle connections after about 2 s with pools of at most 10, and the test container allows 300 connections. Three consecutive green runs of about 17 s.

## D024: Checkout, orders and payments (M06)
- **Date:** 2026-10-14
- **Order model:**
  - An `Order` aggregate guards its status with `OrderStateMachine` (SPEC F10); every transition appends an `order_status_changes` row, the audit trail.
  - Items snapshot product name, slug, SKU and the price paid.
  - Totals are tied together by check constraints (`total = subtotal + shipping_cost`, `line_total = unit_price * quantity`).
  - Orders and order items **RESTRICT** deletion of users and products (financial records).
  - Order numbers come from a PostgreSQL sequence (`FM-000001`).
- **Stock uses a conditional UPDATE instead of the brief's "xmin, retry once":**
  - `StockLedger.TryTakeAsync` runs `UPDATE … SET stock = stock - n WHERE … AND stock >= n`. Under READ COMMITTED a competing transaction blocks on the row lock and re-checks the condition, so the last unit cannot be sold twice. No retry loop is needed; the `stock >= 0` constraint is the backstop.
  - **Verification:** a test forces the interleaving with two real transactions (B is asserted to be blocked while A holds the row).
  - **Mutation check:** removing the guard makes that test fail (B then hits the check constraint, 23514).
  - An earlier HTTP-level race test still passed under the same mutation, because the requests did not overlap. It is kept as a 6-buyer smoke test but is not the proof.
- **Checkout flow:**
  - Validate; replay a known `Idempotency-Key` (unique per user).
  - Re-price the cart: an issue gives 409 `cart-changed`; a total different from `expectedTotal` gives 409 `price-changed` with the new total.
  - One transaction: order + stock + cart clear.
  - **Charge outside the transaction**, so no row locks are held during an external call.
  - A decline or gateway error runs a compensating transaction: cancel the order, return the stock, restore the cart lines.
- **Payments (D010):**
  - `IPaymentGateway` with `FakePaymentGateway` (4242… succeeds, 4000…0002 is declined, anything else is declined with a hint); `Payments__Provider` must be `Fake` until M07.
  - Card data is validated server-side (Luhn, future expiry, CVC) and never stored beyond brand and last 4.
  - `CardDetails` and `PaymentInput` override `ToString()` with masked output.
  - A test captures all API logs during declined and paid checkouts and asserts no card number appears; the manual run's log was grepped as well (0 matches in 1,164 lines).
- **Customer cancel:**
  - Allowed while `PendingPayment` or `Paid`; restocks.
  - `xmin` on orders turns a concurrent staff change into a clean 409.
  - The fake gateway takes no money, so there is no refund step. Real refunds are out of scope even with Stripe (M07).
- **Rate limit:** checkout is limited to 10 per minute per IP (`RateLimiting__CheckoutPermitLimit`); each attempt may hit the gateway.
- **Seed:**
  - 10 fictional customers with no password (cannot sign in), reused if they already exist.
  - 40 historical orders over 90 days, numbered in date order, with full audit trails. The newest 6 are still `Paid` (work for M10); big-ticket items have quantity 1.
  - Deterministic (fixed random seed); stock untouched.
- **Checkout UI:**
  - Three steps as plain sections with an `aria-current="step"` progress list and focus moved to each step heading, instead of a CDK stepper. Simpler, and announced more clearly.
  - Server field-error keys (`shipping.city`) map back to the right step.
  - One idempotency key per attempt.
  - The review step re-prices before showing "Place order · €total".
- **Header fix:** signed-in at 768 px the account button overflowed by 73 px (M03 checked 768 px only as a guest). The display name now shows from 1024 px; initials and the accessible name always do.

## D025: My account (M08)
- **Old sessions end on a password change.**
  - `ConfigureApplicationCookie` replaced `options.Events` (for the 401/403 API answers) and with it Identity's default `OnValidatePrincipal` hook. Old cookies therefore survived a security-stamp change. Found by a regression test written first and shown failing; the hook is now wired back explicitly.
  - `SecurityStampValidatorOptions.ValidationInterval` is zero: every authenticated request looks the user up by primary key and compares stamps. The default (30 minutes) would leave a stolen session alive that long after the owner changes the password. Cost: one indexed query per authenticated request, and the session cookie is re-issued on those responses.
  - Verified in tests (old cookie gets 401, `/me` answers guest) and in two real browser contexts.
- **Password change:** `UserManager.ChangePasswordAsync` (rotates the stamp), then `RefreshSignInAsync` so this browser stays signed in, and a new XSRF token. A wrong current password is a 400 on `currentPassword`; Identity's rules on `newPassword`. It shares the sign-in rate limit (`auth` policy): guessing the current password is the same attack as guessing it at sign-in.
- **Profile:** only the display name is editable (2 to 50 characters after trimming, checked again in the handler because `StringLength` sees the untrimmed value). Email changes would need confirmation mail, which the demo does not send.
- **Account area:** one layout with a `nav` landmark labelled "Account" (tabs on phones, a side list from `md`) around child routes: Profile, Password, Orders and an order's page. `aria-current="page"` on the exact section; on an order's page the Orders link gets `aria-current="true"` (inside the section, not the page itself).
- **Order items link by the current slug.** The detail and cancel responses look up the ordered products: still active gives the current slug (M09 lets admins rename slugs), archived gives `productAvailable: false` and the UI shows the name as text with "(no longer sold)". Names and prices always come from the order snapshot.
- **Confirm dialog:** gained an optional `dismissLabel`. Cancelling an order now offers "Keep order" / "Cancel order" instead of "Cancel" / "Cancel order".

## D026: Admin catalog and image upload (M09)
- **Image library: SkiaSharp 4.153.1 (MIT)**, published 2026-09-30, checked on nuget.org on 2026-10-15.
  - Alternatives: ImageSharp 4.1.3 is under the Six Labors Split License (Apache 2.0 only for open-source use, revenue under $1M, or as a transitive dependency; commercial otherwise). Magick.NET and NetVips bring large native stacks.
  - SkiaSharp's native library ships per platform: Windows comes with the main package; `SkiaSharp.NativeAssets.Linux.NoDependencies` covers CI (Ubuntu) and the container (M13) without system packages.
- **Upload pipeline** (`UploadedImageProcessor`):
  1. Magic bytes decide the format (PNG, JPEG, WebP). The file name and the declared content type are ignored.
  2. Dimensions come from the header before any pixels are decoded (≤ 4000×4000), so a small file that decodes into a huge bitmap is refused cheaply.
  3. The decoder must agree with the sniffed format (a GIF or a broken file is refused).
  4. The EXIF orientation is applied, then the pixels are re-encoded to WebP at quality 85. This drops EXIF, XMP and ICC metadata (camera, GPS).
  5. The SHA-256 of the stored WebP is the dedupe key (the encoder is deterministic; a unit test checks it). A duplicate answers 200 with the existing image; a concurrent duplicate hits the unique index and gets the same row.
  - Size: requests over 1 MB plus 16 kB of multipart framing answer 413 problem details; Kestrel's body limit is lowered for the endpoint too.
  - The form is read from `HttpRequest`, not bound as `IFormFile`. Binding would require the framework antiforgery middleware, and opting out with `DisableAntiforgery()` would also switch off ours, which honours the same metadata.
  - Rate limit `upload`: 20 per minute per IP (`RateLimiting__UploadPermitLimit`).
- **Admin routes** sit in one `/api/admin` group with the Admin policy, so a route added later cannot miss it. An integration test walks the endpoint data source and asserts 401 for guests and 403 for customers on every admin route (mutation-checked).
- **Products:**
  - Updates carry `version` (xmin). The handler sets it as the original value, so a stale form updates nothing and gets 409 `concurrency` (mutation-checked).
  - Delete hard-deletes a never-ordered product (its cart lines cascade) and archives an ordered one. If an order lands between the check and the delete, the restricting foreign key refuses and the handler archives instead.
  - SKUs are normalized to upper case.
  - Slugs: a given slug must already be slug-shaped and free. An empty slug is generated from the name with `-2`, `-3`… on collision, within the length limit. Clearing the slug keeps the stored one unless the name changed.
  - The list allows only fixed sort keys, filters by category, brand and state, searches name or SKU with escaped `ILIKE`, and pages at most 48.
- **Categories and brands:**
  - Returned whole, not paged: 8 and 6 seeded, and an admin adds a handful at most.
  - Last write wins; only products have a concurrency check.
  - Delete is 409 `in-use` with `productCount` while any product (archived included) references them. The UI says so up front instead of asking for a confirmation it cannot honour.
- **Admin UI:**
  - The navigation is labelled "Admin": tabs up to tablets, a side list from `lg`, so the products table fits at 768 px.
  - All table state lives in the URL. Column headers are buttons inside `th[aria-sort]`. Phones get stacked cards with a sort select.
  - While a new query loads, the previous rows stay on screen (`linkedSignal`). Otherwise the table vanished and took keyboard focus with it: found in the browser pass, with a regression test written first.
  - The product form maps server field errors to inputs. A 409 shows an alert whose "Reload latest version" button gets focus.
  - The image picker uploads with progress and a preview, or chooses from a paged library dialog.

## D027: Admin orders, dashboard and CSV export (M10)
- **Days are UTC.** `from` and `to` are inclusive `yyyy-MM-dd` dates turned into `[from 00:00Z, to+1 00:00Z)`. The store has no business time zone yet; a deployed demo in one market could switch to its zone in one place (`DateRange`). The dashboard defaults to the last 30 days, allows at most 366, and refuses an inverted range. The order list has no date filter until one is given.
- **What counts as revenue:** orders that are Paid, Shipped or Delivered, by the day they were *placed*, using the order total (shipping included). Pending and cancelled orders never count. AOV = revenue / orders, rounded half away from zero.
- **SQL does the maths:**
  - Totals and top products are grouped LINQ.
  - Revenue per day is one interpolated `SqlQuery` over `generate_series(from, to, '1 day')`, left-joined to the counted orders, so empty days come back as 0 instead of being missing. The dates are parameters; the statuses are fixed literals.
  - An integration test compares every number with independently written SQL over the seeded history. A second test uses a small known dataset on fixed future dates (exact totals, a 23:59 UTC edge, zero-filled days).
- **Top products:** one list of 5, ranked by units then revenue, showing both. Names come from the current product row.
- **Low stock:** active products with 5 or fewer units left, the 20 lowest.
- **Status changes:**
  - Only the domain state machine decides what is allowed. The admin detail response includes `allowedTransitions`, so the UI never offers a refused move.
  - A move that is no longer allowed (another admin was first) is 409 `invalid-transition` with the moves allowed now. A concurrent write is caught by xmin (409 `concurrent-change`).
  - Every change records the admin's user ID and an optional note (500 characters).
  - Admin cancel restocks in the same transaction, with reason `admin`.
  - Admins may also mark an awaiting-payment order as paid (allowed by the state machine; for payments confirmed outside the gateway).
- **CSV export:**
  - One row per order, streamed with a UTF-8 BOM and CRLF records.
  - RFC 4180 quoting; text cells starting with `= + - @`, tab or CR get a leading `'` (mutation-checked).
  - Numbers are written raw in the invariant culture, because they come from us, not from users.
  - Both dates are required. Over 10,000 rows (`Export__MaxRows`) is a 400 `too-many-rows`. Rate-limited to 10 per minute (`RateLimiting__ExportPermitLimit`).
  - The UI downloads it as a blob, so a refusal shows its problem title instead of saving an error file.
- **Chart: hand-rolled SVG, no library.** One bar per day in a 100-unit view box.
  - The SVG is `role="img"` with a summary label (days, highest day). The full numbers are in a "Revenue per day as a table" disclosure right below it, so keyboard and screen-reader users get the same data.
  - A chart library would be a new dependency (size not measured) for a single bar chart; the whole dashboard chunk is 10.9 kB.
- **Admin nav:** Dashboard is `/admin` itself (exact path match, query strings ignored). Below `lg` the five tabs wrap inside a `minmax(0, 1fr)` grid column, found at 375 px in the browser pass.

## D028: Reviews and ratings (M11)
- **Eligibility** (SPEC section 5): the user has an order that is Paid, Shipped or Delivered and contains the product. Otherwise the answer is 403 `not-eligible`. One review per user and product (unique index, 409 `already-reviewed`). Archived products cannot be reviewed (404).
- **The rating is computed, never adjusted:**
  - After every insert, hide or unhide, one `ExecuteUpdate` sets `RatingCount` and `RatingAverage` from the visible reviews (`round(avg(rating), 2)`), in the same transaction.
  - The write first locks the product row (`SELECT … FOR UPDATE`). Concurrent reviews of one product then run one after the other, and under READ COMMITTED each recalculation sees every committed review.
  - **Mutation check:** without the lock, five concurrent posts ended with a count of 1 in 5 of 5 runs; with it, 5 every time.
- **Text is plain:** title 3–80 and body 10–2000 characters after trimming, stored exactly as written. The SPA renders it through interpolation (escaped), so `<script>` shows as text (web test plus browser check). There is no server-side sanitizing to get wrong or to double-escape.
- **Privacy:** public responses carry the display name only. Integration tests assert no email address and no `email` field in the JSON. The admin list shows the email, to deal with abuse.
- **One public call** for the product page, `GET …/reviews`: the summary (with the per-star distribution), a page of 5 newest reviews, and `viewer` (`sign-in`, `not-purchased`, `already-reviewed`, `can-review`). The SPA refetches it when the session changes. Rate limit `review`: 10 posts per minute per IP.
- **Seed:** reviews only from the fictional seed customers, for products they bought in a paid order, about 65% of those purchases. Ratings lean positive (weights 1/2/5/14/22 for 1–5 stars), texts are invented and generic, dated 2–11 days after the order and never in the future. A test checks that every seeded review has a qualifying order and every product's rating matches SQL.
- **UI:**
  - The rating input is five radios in natural order (1 to 5; arrow keys go up), with stars filled up to the choice.
  - The distribution has visible bars plus text ("5 stars: 12 reviews, 60%").
  - Admin hide/show buttons stay enabled while saving (`aria-busy` plus a guard against double presses), because disabling them dropped keyboard focus to the page. Found in the browser pass; regression test shown failing on the old code. The same pattern elsewhere is K12.

## D029: Production hosting of the SPA, CSP, compression, HSTS and proxies (M12)
- **The API serves the built SPA** when `Spa__RootPath` points at `npm run build` output; otherwise (development) it serves no pages.
  - Hashed bundles are cached for a year as immutable; `index.html` is `no-cache`, so a deploy shows on the next load.
  - Deep links fall back to `index.html` through a named route constraint (`spa`): every path except `/api` and `/health`. Unknown API routes keep their 401/404 problem details. Missing files fall under the fallback policy (401 for guests), as SPEC section 7 says for every unmatched request.
- **K1, strict CSP unchanged** (`script-src 'self'; style-src 'self'`). Reproduced on the build served by the API: 3 violations per page and an unstyled site. Two causes:
  - Angular's critical-CSS inlining (an inline `<style>` plus an inline `onload` script) is now off in the production configuration.
  - The CDK `MediaMatcher` inserts an empty `<style>` in Blink and WebKit (used by the A11yModule's high-contrast detector). `CspSafeMediaMatcher` calls `matchMedia` directly.
  - Result: 0 violations on all 30 audited screens. No nonces were needed.
- **Compression, static files only:**
  - `npm run build` writes `.br` (quality 11) and `.gz` (level 9) copies (`scripts/precompress.mjs`); the API serves them when the browser accepts them, with `Vary: Accept-Encoding`.
  - Anything without a copy (`index.html`) is compressed on the fly.
  - Nothing under `/api` or `/health` is compressed: responses that mix user data and secrets over HTTPS invite BREACH-style attacks.
  - Measured: Brotli quality 11 costs about 300 ms per bundle (fine once at build time, not per request) and is about 25 % smaller than quality 1.
- **HSTS** (`max-age` one year) outside Development; ASP.NET sends it only on HTTPS and never for localhost.
- **K5, forwarded headers:** trusted only from `ReverseProxy__KnownProxies`/`KnownNetworks` (nearest hop, `ForwardLimit` 1). With nothing configured the headers are ignored, so a client cannot claim a fresh IP to reset its rate limits.
- **K3:** the EF migrations history table is created before migrating, so the first start on an empty database logs no error.
- **Cumulative layout shift:** `<main>` is at least one viewport tall, so the footer starts below the fold and cannot move into view when content arrives (Lighthouse CLS 0.249 → 0).

## D030: Demo mode (M12)
- **Switch:** `DEMO_MODE=true` (public deployment only). `GET /api/meta` tells the SPA, which shows a banner on every page.
- **Reset** (`POST /api/admin/demo/reset`, admin; 404 when demo mode is off; 2 per minute):
  - One transaction under a PostgreSQL advisory lock, so a manual and a scheduled reset, or two instances, never overlap.
  - Steps: truncate the shop data, delete every account except the two demo accounts, restart the order-number sequence, then run the four seeders again.
  - The seeders join the surrounding transaction (`JoinableTransaction`), so a failure rolls everything back.
- **Demo accounts are kept**, with their names and roles restored and any lockout cleared, so the admin who pressed the button stays signed in. Everyone else's session ends, because their account no longer exists.
- **Schedule:** a `BackgroundService` repeats the reset every `DEMO_RESET_INTERVAL_HOURS` (default 6, 0 = off), driven by `TimeProvider`. Failures are logged and the next tick tries again.
- **Guard:** there are no user-management endpoints, so nothing can delete or demote the demo admin. The real risk is a visitor changing the shared demo passwords and locking everyone out, so demo mode refuses password changes on the demo accounts (403 `demo-account`).

## D031: Accessibility and UX decisions from the M12 audit
- **Busy buttons** (K12): a `BusyButton` directive replaces `disabled` while a request runs. It sets `aria-disabled` and `aria-busy` and swallows clicks in the capture phase, which also stops Enter-key form submits. Limits such as out of stock still use `disabled`. Where the pressed button disappears (order status, cancel), focus moves to the section heading.
- **Focus rings:** stretched card links draw the standard 3 px ring on the card (`has-[a:focus-visible]`). Date inputs get the ring through `:focus-within`, because their focus sits in the date segments.
- **Colour:** the light-theme success colour is `#166534`, at least 4.5:1 also on its own 10 % tint.
- **Header:** the wordmark wraps onto two lines below 360 px instead of pushing the buttons off-screen.
- **K8, brand facets:** `/api/catalog/brands?department=` returns brands with active products in that department, counted there.
- **Static pages:** About (the portfolio project, invented data, AI assistance, repository link) and Contact/FAQ (CDK accordion, no personal contact data). The placeholder page they replaced was deleted as dead code, as the M12 brief asks.
- **K2:** the owner approved the `lmdb` and `msgpackr-extract` install scripts, pinned to exact versions in `package.json` `allowScripts`.

## D032: Production image, Compose stack and the E2E journey (M13)
- **One image, three stages,** each base pinned by tag and digest (recorded 2026-10-16):
  - `node:24.20.0-bookworm-slim` builds the SPA with `npm run build`, so the precompressed copies ship (D029);
  - `dotnet/sdk:10.0.401-noble` publishes the API for linux-x64, framework-dependent;
  - `dotnet/aspnet:10.0.12-noble-chiseled-extra` runs it. Chiseled means no shell and no package manager; "extra" adds ICU and time-zone data.
  - The image runs as the image's non-root `app` user (UID 1654). Size 272 MB.
  - No secrets in layers: `.dockerignore` excludes `.env`, and settings arrive as environment variables at run time.
- **Health check without a shell:** the image has no curl, so `dotnet FitnessMerchant.Api.dll --health-check` probes `/health/live` on the local port and exits 0 or 1 (`Features/Health/HealthProbe.cs`). It checks liveness only; `/health` still includes the database.
- **`Auth:RequireHttps`** (default: on outside Development): session and antiforgery cookies are HTTPS-only. Only the local Compose file turns it off, so the image works on `http://localhost`. A deployment keeps it on (K13 still applies there). Both ways are tested.
- **Data-protection keys in PostgreSQL** (`data_protection_keys`):
  - Inside a container the default key ring lives in the container's file system, so a restart (or a second instance) would invalidate every session and antiforgery token. A test shows a session surviving a restart and moving to another instance; checked by hand against a restarted container.
  - **Own `IXmlRepository`** (about 30 lines) instead of `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`. That package's 10.0.12 release pulls NuGet copies of `System.Security.Cryptography.Xml`/`Pkcs` with open advisories (GHSA-7hqp-7c5m-4vq6 and others, published 2026-10-13). The fix, 10.0.13, is too new to pin under the two-week rule. The interface already ships with ASP.NET Core.
  - **Keys are not encrypted at rest** (the framework logs "No XML encryptor configured" once per new key). Anyone who can read the database can read the keys, but such a person can already read every account. Encrypting them needs a certificate kept as a secret; decide in M14 with the host.
  - A fixed application name (`FitnessMerchant`) keeps the keys valid whatever the install path. The demo reset does not touch the table.
- **No `Server: Kestrel` header.** It only advertises the stack.
- **Compose:**
  - the project name is fixed; the database port is not published;
  - the app is bound to `127.0.0.1` (`APP_PORT`, default 8080);
  - migrations and seed run on start; demo users come from `.env`;
  - `docker compose up --build` is the one command.
- **E2E:**
  - one Playwright journey (SPEC criterion 9), with axe on each visited page;
  - `@playwright/test` 1.63.0 and `@axe-core/playwright` 4.13.0, the versions already used for the M12 audit;
  - Chromium only, one worker, no retries: a flaky journey should fail.
  - It reads the repo-root `.env` with Node's `process.loadEnvFile`, so it signs in as the same demo admin the stack seeds. Variables already set win.
  - It waits for the account menu, not the cart badge, to know a sign-in finished. The guest badge proves nothing, and navigating too early aborted the registration request.
- **First CI run (PR #13) found two bugs, both fixed on this branch:**
  - **Guest items lost on a reload during the sign-in merge.** The session loads before the cart store exists, so a page that is already signed in only loaded the server cart, and guest items an interrupted merge left in storage were never merged. Now a signed-in first load merges whatever is still stored. A retry after a merge that committed but whose response was lost would add the quantities twice (capped at min(10, stock), and visible in the cart before checkout). That is rarer and visible, while the old behaviour lost items silently. Server-side idempotency (a merge ID) was judged too heavy for this.
  - **Order-dependent web spec.** The Angular test builder runs Vitest with `isolate: false` by default, so spec files in one worker share module state. The CDK caches its media-query `<style>` node per module, and `site-header.spec` running first made `csp-safe-media-matcher.spec` see 0 styles instead of 1. It also failed the `dev` push after the M12 merge. `isolate: true` in `angular.json` costs about 1 s locally (4.5 s to 5.5 s). Reproduced with a one-worker Vitest config and fixed under it.
  - The journey now waits for the merge response, not the cart badge, before it navigates.
- **CI job `e2e`:**
  - builds and starts the stack from `.env.example`, waits for health and runs the journey;
  - on failure it prints the app log and uploads the report and trace (`actions/upload-artifact` v7.0.1 pinned by SHA; v7.0.2 was 9 days old).
- **Windows clones:** a deep clone path pushes the longest migration paths past 260 characters (found in the fresh-clone test), so the README tells Windows users to set `core.longpaths` or clone into a short path.

## D033: .NET 10.0.13 security update (K14)
- **Exception to the two-week rule,** decided by the owner on 2026-10-16: 10.0.13 was released 2026-10-13 (44 CVEs, including denial-of-service fixes in `System.Security.Cryptography.Xml`/`Pkcs`). A patch release that only fixes security issues is taken as soon as it is available.
- **Changed:** SDK 10.0.402 (`global.json`, `dotnet/sdk:10.0.402-noble`), runtime image `dotnet/aspnet:10.0.13-noble-chiseled-extra`, the five ASP.NET/EF Core packages and `dotnet-ef` to 10.0.13. Both images are pinned by digest again (sha256:0d99ebf… and sha256:3ed6558…, read 2026-10-16).
- **Unchanged:** Npgsql 10.0.3, EFCore.NamingConventions 10.0.1, SkiaSharp, Scalar and test packages, which have no newer release inside the rule or no advisory.
- **Own key repository stays** (D032): with 10.0.13 the EF Core package would no longer carry the advisories, but the 30-line repository works, is tested, and adds no dependency.
- **Verified:** 0 vulnerable NuGet packages; 378 tests; the image reports runtime 10.0.13; the E2E journey passes against it.
