# M13: End-to-end test, production Docker and Compose

Branch: `#0013-e2e-test-production-docker-and-compose`

## Goal
One production image and a `docker-compose.yml` that runs the whole stack with one command from a fresh clone. A Playwright flow runs in CI against that image.

## Context
D008, D011; SPEC acceptance criterion 9. Re-check the base image tags and the Playwright version at the time (pin versions at least two weeks old; record them).

## Scope
1. Multi-stage `Dockerfile`:
   1. Node stage builds Angular (production).
   2. SDK stage publishes the API.
   3. The runtime stage uses the ASP.NET runtime image (chiseled or non-root variant), runs as non-root, and copies the SPA into `wwwroot`.

   It also needs a `HEALTHCHECK`, a `.dockerignore`, and no secrets in layers. Report the image size.
2. SPA hosting in the API: static files with cache headers (hashed assets immutable, `index.html` no-cache), a fallback to `index.html` for client routes but not for `/api/*`, and the CSP verified to work with the production build.
3. `docker-compose.yml`:
   - `db` (Postgres, volume, healthcheck) and `app` (depends on db healthy);
   - environment from `.env`, with migrations and seed on start;
   - `docker compose up --build` is the one command.
4. Playwright (`tests/e2e`), with a single journey, plus an axe check on each visited page:
   1. guest searches;
   2. filters;
   3. adds to cart;
   4. registers;
   5. sees the merged cart;
   6. checks out with the fake card;
   7. sees the confirmation and the order in history;
   8. admin signs in and marks the order Shipped;
   9. customer sees Shipped.
5. CI job: build the image, start Compose, wait for health, run Playwright, upload the trace and report on failure.
6. Fresh-clone test: clone into a temporary directory and follow the README "Run locally" steps exactly; fix the docs or setup until it works.

## Out of scope
Deployment (M14).

## Tests required
The Playwright journey above, stable across 3 consecutive local runs (report them).

## Acceptance criteria
- `docker compose up --build` from a fresh clone gives a working app at the documented URL.
- The container runs as non-root (`docker exec app id`).
- Security headers are present on the production responses (`curl -I`).
- The CI E2E job is green on the pushed branch (or state that it could not be checked).

## Verification required
Fresh clone in a temporary directory, Compose up, use the app in a browser, run Playwright locally 3 times, `curl -I` for headers, report the image size.
