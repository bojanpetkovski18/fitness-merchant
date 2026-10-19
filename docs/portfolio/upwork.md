# Portfolio kit: Upwork

Text and media for an Upwork portfolio item. Every number comes from [PROGRESS.md → Measured results](../../PROGRESS.md#measured-results). No testimonials and no business results: this is a portfolio project with invented data.

**Field limits (checked 2026-10-19).** Upwork's help page could not be read automatically (HTTP 403), and secondary sources disagree. Upwork's article is quoted as title 70, role 100, description 600 characters, with up to 5 skills per item; some guides report 40/300 or 50/500. Each field below therefore comes in a **short** version (title ≤ 40, description ≤ 300) and a **full** version (title ≤ 70, role ≤ 100, description ≤ 600). Use whichever fits the form you see. The character counts were measured with a script.

## Title

- **Short (34):** Full-Stack E-Commerce Store (.NET)
- **Full (62):** Full-Stack E-Commerce Store: ASP.NET Core, Angular, PostgreSQL

## Role (full: ≤ 100)

- **(87):** Full-stack developer: specification, API, Angular UI, database, tests, Docker and CI/CD

## Description

**Short (≤ 300):**

> A secure online store for fitness products: catalog search and filters, guest cart, checkout with stock locking, order tracking, reviews and an admin dashboard. ASP.NET Core 10, Angular 22, PostgreSQL. 496 automated tests, Docker, CI, WCAG-checked UI.

**Full (≤ 600):**

> A rebuild of my university thesis as a production-quality online store for fitness supplements and gear. Customers search and filter the catalog, keep a cart as guests, check out with idempotent payments and stock that can't be oversold, track orders and leave reviews. Admins manage products and images, move orders through their statuses, export CSV and read a sales dashboard. Built with ASP.NET Core 10, Angular 22 and PostgreSQL, with cookie auth, CSRF protection and a strict CSP. 496 automated tests (unit, integration on real PostgreSQL, component, end-to-end), Docker and GitHub Actions CI.

## Skills (5)

1. ASP.NET Core
2. Angular
3. PostgreSQL
4. Docker
5. Web Application Security

Alternatives if one is not offered: .NET, TypeScript, Entity Framework Core, GitHub Actions, E-commerce Website.

## Problem and solution (for the item's longer text, a cover letter or a call)

- **Problem:** the 2024 thesis version worked as a demo but was unsafe and untested:
  - anyone could register as an admin;
  - carts and payments were reachable by ID (IDOR);
  - money was a `double`;
  - secrets were committed;
  - every query loaded whole tables;
  - checkout ignored stock;
  - there were no tests, CI or deployment.
- **Solution:** a rebuild from a written specification, with one milestone at a time, each merged through a reviewed pull request with green CI:
  - roles are never client-controlled, and "my" resources come from the signed-in user;
  - money is `numeric(12,2)`;
  - secrets come from the environment only;
  - filtering, paging and aggregation run in SQL;
  - checkout re-prices the cart, takes stock with one conditional `UPDATE` (the last unit cannot be sold twice) and charges outside the transaction, compensating on a decline.

## Key decisions (one line each)

- One deployable: the API serves the Angular build, so there is one origin, cookie auth and one CSP.
- Cookie auth with antiforgery instead of tokens in JavaScript; the security stamp is checked on every request.
- Integration tests run against real PostgreSQL in containers, not an in-memory fake.
- Images are re-encoded to WebP and metadata is stripped; the format comes from the magic bytes.
- No public hosting: the demo runs from one command, or in GitHub Codespaces.

## Screenshots: order and captions

Files are in [`docs/media`](../media).

1. `journey.gif`: "Guest search to a placed order, then the admin view."
2. `home-light.png`: "Storefront home: hot deals, top rated, new arrivals."
3. `listing-light.png`: "Search and filters run in SQL, with paging and sorting from fixed lists."
4. `product-dark.png`: "Product page in dark mode."
5. `checkout-light.png`: "Checkout with server-side re-pricing and an idempotency key."
6. `admin-dashboard-light.png`: "Admin sales dashboard; totals cross-checked against SQL."
7. `admin-orders-dark.png`: "Order management with allowed status transitions only."
8. `phone-listing-light.png`: "Responsive down to 320 px; keyboard and screen-reader checked."

## Measured facts you can quote

From PROGRESS → Measured results (machine M1, dates there):
- **Tests:** 383 API tests (unit, plus integration on PostgreSQL via Testcontainers) and 113 web component tests, 496 in total, plus the end-to-end journey.
- **Accessibility:** 30 screens audited with 0 axe findings in both themes; no horizontal overflow from 320 to 1280 px.
- **Lighthouse (mobile):** Home 94–96 performance, Listing 92–93; accessibility 100.
- **Catalog API under load:** p95 6.87 ms with 10 users for 60 s (k6), on a local Release build.
- **Production image:** 272 MB, non-root.

## Services this project demonstrates

- Building full-stack web applications with ASP.NET Core and Angular.
- Designing secure REST APIs: authentication, roles, CSRF, rate limiting, input validation.
- PostgreSQL schema design, EF Core migrations and safe concurrency (stock, idempotency).
- Automated testing at every level, and CI with GitHub Actions.
- Dockerizing an application for one-command setup.
- Accessibility (WCAG 2.2 AA checks) and performance work on an existing UI.
- Rebuilding or hardening a legacy project.
