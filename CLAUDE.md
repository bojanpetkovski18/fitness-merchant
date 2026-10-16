# CLAUDE.md: rules for AI agents working on this repository

## Project summary
Fitness Merchant is an online store for fitness supplements and training gear. It has a storefront, cart, checkout and order tracking for customers, plus an admin area for catalog, orders and the sales dashboard. It is an independent portfolio project built with AI assistance: a rebuild of the author's 2024 thesis project. Specification: `docs/SPEC.md`. Decisions: `DECISIONS.md`. Status: `PROGRESS.md`. Milestone briefs: `prompts/`.

The human owner is a professional full-stack developer who supervises, reviews and merges every PR. Explain important decisions briefly; do not explain basics.

## Stack
- **Backend:** ASP.NET Core on .NET 10 LTS (Minimal APIs), EF Core with Npgsql, PostgreSQL, ASP.NET Core Identity (cookie auth).
- **Frontend:** Angular 22, standalone components, zoneless, Signals, Tailwind CSS v4 and Angular CDK.
- **Tests:**
  - xUnit v3 unit tests;
  - integration tests with `WebApplicationFactory` and Testcontainers PostgreSQL;
  - Angular component tests;
  - one Playwright E2E flow.
- **Infra:** Docker Compose for local dev; one production image (the API serves the SPA); GitHub Actions CI; host chosen at M14.

Exact versions are pinned in the project files; see `DECISIONS.md` for the reasons.

## Target repository layout
```
src/
  api/FitnessMerchant.Api/
    Features/<Feature>/     endpoints + one handler per command/query + request/response records
    Domain/                 entities, enums, domain rules (no EF or HTTP types)
    Infrastructure/         DbContext, configurations, migrations, seed, payments, images
    Common/                 problem details, security headers, auth setup, paging, clock
  web/                      Angular app
    src/app/core/           session + cart stores, http interceptors, guards, layout
    src/app/shared/         presentational UI components, pipes
    src/app/features/<feature>/
tests/
  FitnessMerchant.Api.UnitTests/
  FitnessMerchant.Api.IntegrationTests/
  e2e/                      Playwright
docs/                       SPEC.md, ARCHITECTURE.md, portfolio/
prompts/                    session starter + milestone briefs
.github/workflows/
Dockerfile, docker-compose.yml, FitnessMerchant.slnx
```

## Commands
Fill this in only after a command has actually been run and verified. Format: command, then what it does, then the date verified.

All verified 2026-10-13 on Windows 11 (Git Bash), .NET SDK 10.0.401, Node 24.20.0, Docker 29.8.2. Run from the repo root unless a directory is given.

**One-time setup**
- `cp .env.example .env`: local settings; `.env` is git-ignored. Its `DEMO_*` values create the local demo admin and customer on API start (passwords need 10+ characters with upper, lower, digit and symbol).
- `cd src/web && npm ci`: installs the exact locked web dependencies.
- `dotnet tool restore`: installs the pinned `dotnet-ef` (from `dotnet-tools.json`). Verified 2026-10-13 (M02).

**Database (dev)**
- `docker compose -f docker-compose.dev.yml up -d`: starts PostgreSQL 18.6 on `127.0.0.1:5433` (healthcheck included).
- `docker compose -f docker-compose.dev.yml ps`: shows status; wait for `(healthy)`.
- `docker compose -f docker-compose.dev.yml down`: stops it and keeps the data volume.

**API** (`src/api/FitnessMerchant.Api`)
- `dotnet build FitnessMerchant.slnx`: builds everything; warnings are errors.
- `dotnet test --solution FitnessMerchant.slnx`: unit and integration tests (Microsoft.Testing.Platform, opted in via `global.json`). Integration tests start their own PostgreSQL container through Testcontainers, so Docker must be running; the dev database is not used. About 40 s on M1 after M12 (375 tests); the time varies with the local Docker daemon.
- `dotnet test --project tests/FitnessMerchant.Api.IntegrationTests --output Detailed`: one project with per-test timings.
- `dotnet test --project tests/FitnessMerchant.Api.IntegrationTests -- --filter-class "*AccountTests"`: one test class (`--filter-method "*name*"` for one test; combining both filters matches nothing).
- `dotnet run --project src/api/FitnessMerchant.Api`: runs the API on `http://localhost:5080`. In Development it reads the repo-root `.env`, applies migrations (`Database__MigrateOnStartup`) and seeds an empty catalog (`Seed__Enabled`). API docs are at `/scalar` and `/openapi/v1.json`. Health: `/health` (includes the DB) and `/health/live`.
- `dotnet ef migrations add <Name> --project src/api/FitnessMerchant.Api --output-dir Infrastructure/Data/Migrations`: adds a migration. Review it with `dotnet ef migrations script --project src/api/FitnessMerchant.Api` (build first; `--no-build` uses the last build). A forgotten migration fails the test suite (`Model_matches_the_migrations`; EF also refuses `MigrateAsync`).
- Reset the dev data to the seed (verified 2026-10-16, M12): start the API with `DEMO_MODE=true` (environment variable or `.env`), sign in as the demo admin and use Admin → Dashboard → "Reset demo data now" (or `POST /api/admin/demo/reset`). This removes everything created since seeding, keeps the demo accounts and re-runs the seeders. It replaces the M02 truncate command, which predates orders (which reference products) and was not re-verified.
- `Spa__RootPath="$(pwd)/src/web/dist/web/browser" dotnet run --project src/api/FitnessMerchant.Api`: the API also serves the production SPA build (run `npm run build` first) on `http://localhost:5080`, with the strict CSP. Use it to test production-only behaviour. Verified 2026-10-16 (M12).

**Web** (`src/web`)
- `npm start`: dev server on `http://localhost:4200`, proxying `/api` and `/health` to the API.
- `npm run lint`: ESLint, including Angular template accessibility rules.
- `npm run format:check` / `npm run format`: Prettier check / fix.
- `npm run test:ci`: Vitest component tests, single run. `npm test` runs in watch mode. `npx ng test --watch=false --include src/app/shared/ui/confirm-dialog.spec.ts` runs one spec.
- `npm run build`: production build to `src/web/dist/web`, then `scripts/precompress.mjs` writes `.br`/`.gz` copies that the API serves (M12, verified 2026-10-16).

**Performance** (verified 2026-10-16, M12)
- `k6 run tools/load/catalog-list.js` (`-e BASE_URL=… -e VUS=… -e DURATION=…`): load test of the catalog list API with a p95 < 200 ms threshold. k6 is not a project dependency: download a pinned, checksum-verified release (v2.3.0 was used). Measure against a Release build (`dotnet run -c Release --no-launch-profile`) so console logging does not skew the numbers.

**CI**
- `.github/workflows/ci.yml` runs the same commands, plus a gitleaks history scan, actionlint and editorconfig-checker.

## Working rules
- **One milestone at a time.**
  - Read the milestone brief in `prompts/`, then `PROGRESS.md` and the relevant parts of `docs/SPEC.md`.
  - Implement only that scope: no extra features, no unrelated refactors, no premature abstraction.
- **Inspect before changing; preserve existing work.**
  - Look at files and `git status` before editing.
  - Ask before deleting files or running destructive commands (`git reset --hard`, force push, dropping databases or volumes).
- **Ask when blocked.**
  - If an ambiguity changes the design, ask one clear question.
  - Otherwise choose the simplest reasonable option, state the assumption, and log it in `DECISIONS.md`.
- **Tests are part of the feature.**
  - Never delete, skip or weaken a test to get green.
  - For a bug: write the regression test first, show it failing, then fix.
- **Verify for real.**
  - Run the build and tests after each step and read the output.
  - For UI work, run the app and use it in a real browser at 375, 768 and 1280 px; check the console and keyboard navigation.
  - Before deployment, test the production build too: CSP and other production-only behaviour differ from the dev server.
- **Never claim what was not verified** (compiles, tests pass, endpoint or page works, performance, security). Say plainly what could not be run.
- **Measure before stating numbers.** Record the method, machine and date in `PROGRESS.md` → Measured results. Numbers in the README or docs must come from there.
- **Time-sensitive facts** (versions, hosting limits, prices): research current sources and record what was found and when. New dependencies are pinned to a version that is at least two weeks old.
- **Known issues** the owner chooses to defer go to `PROGRESS.md` → Known issues. Remind the owner when they become relevant, and in M12.

## Git
- Start each milestone from a freshly pulled `dev`, on a new branch `#00nn-descriptive-name`: the milestone number plus 3–7 kebab-case words saying what it delivers (e.g. `#0004-catalog-search-filters-and-storefront-pages`). The planned names are in `PROGRESS.md`.
- Commit in small steps; each commit builds and passes its tests. Subjects are short and imperative ("Add product search endpoint"), with one logical change per commit.
- Push the branch when done. The owner opens and merges the PR. Never push to `dev` or `main` unless told to. Never force-push shared branches.
- End commit messages with the co-author trailer given by the environment, if any.

## Code standards
- **Strict typing everywhere.**
  - C#: nullable reference types enabled, with `TreatWarningsAsErrors` in CI.
  - TypeScript: `strict`, `strictTemplates`, `noImplicitOverride`, `noPropertyAccessFromIndexSignature`.
  - No `any` without a comment explaining why.
- **Endpoints are thin.** They bind, authorize and call a handler; no business logic.
  - Light CQRS: one handler class per command or query (`PlaceOrderHandler`, `ListProductsQuery`), registered directly.
  - No mediator framework and no generic repository.
- **Database work belongs in the database.** Aggregations, filtering, sorting and paging run in SQL; never load whole tables. Use `AsNoTracking` and projections for reads.
- **Money** is `decimal` / `numeric(12,2)`, never floating point. Rounding rules are in SPEC section 5.
- **Validation and errors.**
  - Server-side validation is authoritative; client-side validation is a convenience.
  - Errors are RFC 7807 problem details with a `traceId`.
- **UI states and accessibility.**
  - Every screen has loading, empty, error and success states.
  - Everything interactive has an accessible name and keyboard support; visible focus; respect `prefers-reduced-motion`.
- **Logging and time.**
  - Structured logging. Never log secrets, passwords, cookies, tokens or full request bodies.
  - Time comes from an injected `TimeProvider` (testable); all timestamps are UTC.
- **Style.** Match the surrounding code: naming, comment density and idioms. Comments explain *why*, not *what*.

## Security rules
- **Secrets.**
  - No secrets in the repository. Configuration comes from environment variables.
  - `.env.example` contains placeholders only; `.env` is git-ignored.
- **Authentication and authorization.**
  - Every endpoint requires authentication except the explicitly public ones listed in SPEC section 7.
  - Roles are checked server-side.
  - "My" resources are resolved from the authenticated user, never from an ID in the request; another user's resource returns `404`.
- **Queries.** Parameterized only (EF Core LINQ, or `FromSql` with interpolation parameters). Sort columns and other identifiers come from fixed allow-lists, never from input.
- **Uploads and exports.**
  - Validate size, dimensions and real content type (magic bytes); treat file contents as untrusted; re-encode images.
  - CSV exports prefix cells starting with `=`, `+`, `-`, `@`, tab or carriage return with `'`.
- **Abuse protection.**
  - Rate-limit sign-in, register, checkout and upload.
  - Use antiforgery on cookie-authenticated unsafe requests.
- **Headers and containers.**
  - Security headers: strict CSP (no inline scripts), HSTS in production, `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy`, `frame-ancestors 'none'`.
  - Containers run as non-root.
- **Before the repository goes public,** scan the files and the full git history for secrets and personal data, and report the result.

## Data and legal rules
- **Fictional data only.**
  - The store, brands, products, users, addresses and orders are invented.
  - Emails use the reserved `example` domain (`*.example`).
  - Phone numbers are obviously fake.
- **No real brand names, logos, product photos or third-party videos.** Artwork is original to this repo (SVG).
- **No real personal data, and no employer code or business rules, ever.**
- **No code copied from the v1 thesis project;** it is a feature reference only.
- The README must state that the project was built with AI assistance.

## Milestone report format
At the end of every milestone, give exactly this:
```
## Milestone report: <name>
Status: complete | partial | blocked
Changed: <files/areas>
Verified (commands actually run and results):
- <command>: <result>
Not verified / could not run: <list or "none">
Acceptance criteria: <each criterion, pass/fail, how checked>
Assumptions and decisions logged: <list>
Known issues: <list or "none">
Suggested next step: <one line>
```
After every push, give a pull request description in the chat, not in a file, as raw Markdown inside a fenced code block so the owner can paste it into GitHub. It covers the whole branch, starts with a line naming its readers, and has these sections:
- What changed (grouped)
- Verified locally (a table of real results)
- Not verified
- Review notes
- Out of scope

Before the report, update `PROGRESS.md` and `DECISIONS.md`, plus this file's Commands section for any newly verified commands.
