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

## D007: UI: Tailwind CSS v4 plus Angular CDK, not Material or Bootstrap (pending, confirmed at M01)
- **Date:** 2026-10-13
- **Decision:** Tailwind CSS v4 with design tokens as CSS variables (theme switching); Angular CDK for overlays, focus trap, a11y utilities and stepper behaviour. If `@angular/aria` is stable at M01, use it for headless accessible widgets.
- **Why:** v1 combined Material and Bootstrap and looked generic. Tailwind gives a distinctive, consistent look with a small CSS output, and CDK covers the hard accessibility parts.
- **Alternatives:** Angular Material 3 (fastest accessible widgets, but a recognizably "Material" look); PrimeNG (heavy).
- **Trade-offs:** We own the visual components, so accessibility must be tested (axe and keyboard checks every milestone).

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

## D012: Money and currency
- **Date:** 2026-10-13
- **Decision:** EUR only, prices include VAT, `numeric(12,2)` in the DB and `decimal` in C#. Rounding is `MidpointRounding.AwayFromZero` at line level. The API sends amounts as JSON numbers with two decimals. Angular formats them with `CurrencyPipe` and never does money arithmetic beyond display.
- **Why:** Exact decimals; the server is the single source of truth for totals.

## D013: Branches and milestone numbering
- **Date:** 2026-10-13
- **Decision:**
  - The branch for milestone `Mnn` is `#00nn-short-name` (e.g. `#0004-catalog`); Phase 0 is `#0000-project-setup`.
  - PRs target `dev`.

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
