# M01: Skeleton, CI and app shell

Branch: `#0001-skeleton`

## Goal
A building, testable, linted skeleton for API and web, a local Postgres via Docker Compose, CI that enforces quality gates, and an Angular app shell (layout, navigation, theming, routing) that later milestones fill in.

## Context
- SPEC sections 6 and 9; decisions D003–D008, D011, D014.
- Re-check current stable versions before pinning anything (rule: at least two weeks old):
  - .NET 10 SDK and runtime patch;
  - EF Core and Npgsql;
  - Angular (22.2.0 was the candidate on 2026-10-13);
  - Tailwind v4, the PostgreSQL image, xUnit v3, Testcontainers, Playwright.
- Check whether Vitest is the Angular default test runner and whether `@angular/aria` is stable. Record findings and dates in DECISIONS.md (update D006, D007, D011).

## Scope
1. `FitnessMerchant.slnx` and `Directory.Build.props`:
   - `Nullable` enabled and `TreatWarningsAsErrors` on;
   - analyzers enabled (`AnalysisLevel` latest-recommended);
   - Central Package Management (`Directory.Packages.props`).
2. `src/api/FitnessMerchant.Api` (Minimal APIs) with the folder layout from CLAUDE.md:
   - `GET /health` (liveness; DB readiness comes in M02);
   - problem-details setup with `traceId`;
   - JSON console logging;
   - security-headers middleware (CSP, nosniff, Referrer-Policy, Permissions-Policy, frame-ancestors);
   - OpenAPI plus Scalar in Development only;
   - configuration bound from environment variables with startup validation (fail fast on missing required settings).
3. `tests/FitnessMerchant.Api.UnitTests` and `tests/FitnessMerchant.Api.IntegrationTests` (`WebApplicationFactory`). Each has one real test, e.g. health returns 200 and security headers are present.
4. `src/web` Angular app:
   - standalone, zoneless, strict TS and templates;
   - ESLint (angular-eslint) and Prettier;
   - Tailwind v4 with design tokens as CSS variables for dark and light themes (D014);
   - theme toggle that respects `prefers-color-scheme` and remembers the choice;
   - app shell:
     - skip link;
     - header with logo wordmark, nav (Home, Supplements, Gear, Brands), cart icon placeholder and sign-in placeholder;
     - mobile menu below 768 px, using a CDK focus trap;
     - footer;
   - lazy routes with placeholder pages for every top-level route in SPEC section 6, plus a real 404 page;
   - dev proxy `/api` → API;
   - one component test (shell renders nav, theme toggle switches theme).
5. `docker-compose.dev.yml` with Postgres only, a named volume, healthcheck, and credentials from `.env`.
6. `.github/workflows/ci.yml` on push and PR:
   - API: restore, build with warnings as errors, unit and integration tests;
   - web: `npm ci`, lint, format check, test, production build;
   - npm and NuGet caching.
7. Fill the CLAUDE.md "Commands" section with every command actually run and verified.

## Out of scope
DB schema, auth, real pages, the production Dockerfile (M13) and deployment.

## Tests required
- API: health 200, security headers present on responses, unknown route returns problem details.
- Web: shell renders; theme toggle; 404 route renders.

## Acceptance criteria
- `dotnet build` passes with warnings as errors; all tests pass locally and in CI. CI is verified on a pushed branch; if it cannot be checked from here, say so.
- `npm run lint`, the format check, `npm test` and `npm run build` pass.
- In a browser at 375, 768 and 1280 px:
  - the shell has no horizontal scroll;
  - the mobile menu opens and closes with the keyboard and Escape, and focus returns to the toggle;
  - the theme toggle works and persists;
  - the console is clean.
- The skip link works; focus is visible in both themes.

## Verification required
Run every command and read the output. Open the app in a real browser at the three widths, use only the keyboard for one pass, and check the console. Report the exact versions pinned, with their release dates.
