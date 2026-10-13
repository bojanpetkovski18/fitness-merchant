# M02: Data model and seed data

Branch: `#0002-catalog-schema-seed-data-and-artwork`

## Goal
The catalog schema in PostgreSQL via EF Core migrations, with deterministic fictional seed data and original SVG artwork, plus an integration-test base using Testcontainers.

## Context
SPEC sections 5 and 8; D002, D004, D009, D012. Tables that need users (Cart, Order, Review) are created in the milestones that use them, with the shape already fixed in SPEC section 8.

## Scope
1. Domain entities and EF configurations:
   - Category (with Department), Brand, Product, Image;
   - money `numeric(12,2)`, slugs unique, SKU unique, `xmin` concurrency token on Product;
   - indexes from SPEC section 8, plus the `pg_trgm` extension and a trigram index on `Product.Name`.
2. Initial migration. Apply migrations on startup when `Database__MigrateOnStartup=true` (dev and containers); otherwise use the CLI.
3. Health: `/health/live` for liveness; `/health` returns 200 only when the DB is reachable.
4. Seeder (idempotent, deterministic, runs after migration when `Seed__Enabled=true`):
   - 2 departments and 8 categories;
   - 6 invented brands, each with a description, accent colour and SVG wordmark logo;
   - about 60 products with varied prices, discounts, stock (including some out of stock and some low stock) and `CreatedAt` dates.

   Product artwork: a small set of original SVG templates (tub, bottle, shaker, dumbbell, kettlebell, plate, band, mat, apparel, cardio machine) tinted per brand, stored as Images.
5. Testcontainers fixture: one Postgres container per test run, migrations applied, and a reset between tests (Respawn or transaction rollback; choose and log).
6. Seed content lives in code or JSON under `Infrastructure/Seed`, with names clearly invented. No real brands.

## Out of scope
Users and auth (M03), catalog endpoints (M04), orders (M06).

## Tests required
- Integration:
  - migrations apply on an empty DB;
  - the seeder is idempotent (running it twice leaves the same row counts);
  - unique constraints are enforced (slug, SKU);
  - money round-trips exactly;
  - `/health` reports unhealthy when the DB is unreachable.
- Unit: sale-price calculation and rounding (SPEC section 5) with edge cases (0%, 90%, half-cent values).

## Acceptance criteria
- No pending model changes (`dotnet ef migrations has-pending-model-changes`); the migration SQL script is reviewed.
- A fresh `docker compose -f docker-compose.dev.yml up` followed by an API run produces a seeded DB; row counts are reported in the milestone report.
- Seed artwork renders when opened directly in the browser via a minimal image endpoint (M04 hardens it with caching headers).
- A scan of the seed data finds no real brand or product names.

## Verification required
Run the migrations and seed against the Compose DB, query row counts with `psql`, run all tests, and open a few seed images in the browser.
