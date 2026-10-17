# Fitness Merchant

> Supplements and training gear in one modern storefront.

**Status: early draft.** This README is a placeholder; the full version (screenshots, live demo, how to run it, architecture, decisions) comes in the final milestone.

Fitness Merchant is a full-stack e-commerce demo:
- a storefront with search and filters, a cart, checkout and order tracking;
- an admin area for the catalog, order processing and sales metrics.

It is a ground-up rebuild of my 2024 university thesis project, aiming for production-quality code, security and tests.

- **Stack (planned):** ASP.NET Core (.NET 10), EF Core, PostgreSQL, Angular 22, Tailwind CSS, Docker, GitHub Actions.
- **Data:** entirely fictional. The store, brands, products, people and orders are invented, and all artwork is original.
- **AI assistance:** this is an independent portfolio project built with AI assistance (Claude Code) under my direction and review. I wrote the specification and made the decisions, and I review and merge every change.

See [`docs/SPEC.md`](docs/SPEC.md) for the specification and [`PROGRESS.md`](PROGRESS.md) for the current status.

## Run locally

You need Git and Docker with Compose v2 (Docker Desktop includes both).

```sh
git clone https://github.com/bojanpetkovski18/fitness-merchant.git
cd fitness-merchant
cp .env.example .env
docker compose up --build
```

On Windows, run `git config --global core.longpaths true` first, or clone into a short path such as `C:\src`: some migration file paths are long, and a deep folder pushes them past Windows' 260-character limit.

The first build takes a few minutes. When the log says the app is listening, open <http://localhost:8080>. The catalog is seeded on the first start.

- **Demo accounts:** the `DEMO_ADMIN_*` and `DEMO_CUSTOMER_*` values in `.env` (admin: `admin@fitness-merchant.example` / `Change-Me-Admin-1`). Change them before you show the app to anyone.
- **Payments are fake:** card `4242 4242 4242 4242` succeeds and `4000 0000 0000 0002` is declined. Use any future expiry and any CVC. Nothing is charged.
- **Stop:** `Ctrl+C`, or `docker compose down`. Data stays in a Docker volume; `docker compose down -v` deletes it.
- **Another port:** set `APP_PORT` in `.env` if 8080 is taken.

This setup is for your own machine only: it serves plain http and turns off the HTTPS-only cookie rule (`Auth__RequireHttps=false` in `docker-compose.yml`). Never use it that way on a public server.

### End-to-end test

With the stack running:

```sh
cd tests/e2e
npm ci
npx playwright install chromium
npx playwright test
```

The test signs in as the demo admin from the same `.env`.
