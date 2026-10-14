# Fitness Merchant: Specification

Status: v0.2 (Phase 0), 2026-10-13. Phase 0 questions resolved: name kept, Stripe included (D010), training programs dropped (D017).

## 1. Purpose

Fitness Merchant is an online store for fitness supplements and training gear. Shoppers browse and filter a catalog, keep a cart, check out and track their orders. Staff manage the catalog, process orders and see sales at a glance.

It is a rebuild of the author's 2024 university thesis project (FitHub / Fitness Merchant: .NET 8, Angular 17, SQL Server). The rebuild aims to be a polished, modern and fully working portfolio piece. It shows the full product lifecycle: specification, secure API design, a modern Angular UI, tests at three levels, containerization, CI and a deployed demo.

- **Context:** an independent portfolio project built with AI assistance. The README says so.
- **Data:** fictional only. The store "Fitness Merchant" and all of its brands, products, users, addresses and orders are invented. The project uses no real brand names, logos, product photos or third-party videos, and no real personal data. The thesis code is a reference for features only; no code is copied from it.

## 2. Users and roles

| Role | Who | Can do |
|---|---|---|
| Guest | Anonymous visitor | Browse the catalog and brands; keep a guest cart in the browser; register; sign in |
| Customer | Registered shopper | Everything a guest can, plus a server-side cart, checkout, order history, cancelling an unshipped order, reviews of purchased products, editing their profile and password |
| Admin | Store staff | Everything a customer can, plus managing products, categories, brands and images; processing orders; the sales dashboard; CSV export; demo reset |

New users are always registered as Customer. Admins exist only through seeding or another admin, never through self-registration.

## 3. Lessons from v1 (what this rebuild must fix)

| v1 problem | v2 rule |
|---|---|
| Role taken from the registration body, so anyone could become Admin | Role is never client-controlled |
| Cart and payment IDs taken from the route without an ownership check (IDOR) | "My" resources come from the authenticated user; admin resources sit under `/api/admin` |
| Unauthenticated writes (likes, brand/category product links, user lookup) | All writes need auth; reads of personal data are owner- or admin-only |
| Money stored as `double` | `numeric(12,2)` / `decimal` |
| JWT signing key and connection string committed | Secrets come only from the environment |
| Every query loads whole tables into memory | Filtering, paging and aggregation run in SQL |
| Checkout ignored the cart; no stock check; orders not linked to users | Orders are built server-side from the cart in one transaction, with a stock check and price snapshots |
| No tests, no CI, no deployment | Unit, integration (real Postgres), component and E2E tests; CI; Docker; deployed demo |

## 4. Features

### 4.1 MVP

**Catalog**
- F1 Home page: hero, Hot Deals (highest discount), Top Rated, New Arrivals, brand strip.
- F2 Product listing for Supplements and Gear (each a department with categories). Includes:
  - text search over name and description;
  - filters for category, brand, price range and in-stock only;
  - sorting by newest, price ascending/descending, rating or discount;
  - pagination, with all filter state in the URL.
- F3 Product detail: image, brand, category, price with discount, stock status, quantity selector, add to cart, description, rating summary, reviews and related products.
- F4 Brands page and brand detail (brand description plus that brand's products).

**Accounts**
- F5 Register (email, display name, password), sign in, sign out, current-user endpoint. Cookie session.
- F6 My account: profile (display name), change password, order history.

**Cart and checkout**
- F7 Cart. Guests keep the cart in localStorage and it is merged into the server cart on sign-in. Customers have a server cart. Items have quantities (1–10 per line, never above stock). A header badge shows the item count. The cart shows subtotal, shipping and total, all computed server-side.
- F8 Checkout (signed-in only) in three steps:
  1. shipping address;
  2. payment;
  3. review and place order.

  On success it shows a confirmation page with the order number, and the order appears in history.
- F9 Payment sits behind a payment-gateway interface:
  - **Fake gateway** for local runs, tests and E2E: test card `4242…` succeeds, `4000…0002` is declined.
  - **Stripe Checkout in test mode** with a signature-verified webhook for the deployed demo.
- F10 Order lifecycle: `PendingPayment → Paid → Shipped → Delivered`. `PendingPayment` or `Paid` can move to `Cancelled`, which restocks the items. Customers can cancel their own order before it ships. Every status change is recorded as an audit row.

**Reviews**
- F11 Customers who bought a product (order Paid or later) can leave one review per product: 1–5 stars, a title and text. The product rating and review count update in the same transaction. Admins can hide a review.

**Admin**
- F13 Catalog management: CRUD for products, categories and brands. Products with orders are archived (inactive) instead of deleted. Image upload is validated (see section 8).
- F14 Order management:
  - list with status, date-range and search filters, and paging;
  - order detail with the status history;
  - status transitions allowed only by the state machine.
- F15 Dashboard for a date range: revenue, order count, average order value, revenue per day (chart), top 5 products and low-stock list. All aggregated in SQL.
- F16 CSV export of orders for a date range, protected against formula injection.
- F17 Demo reset: an admin action plus a scheduled job (deployed demo only). Both restore the seed data.

**Cross-cutting**
- F18 Light and dark themes (dark by default, carrying over v1's identity: near-black with an amber/gold accent), using the system preference with a manual toggle.
- F19 Static pages: About (states it is a portfolio project), Contact info with FAQ (no form submission in MVP), and a 404 page.

### 4.2 Later (not in MVP)
Wishlist, coupon codes, product variants (size or flavour), multiple images per product, contact form with an admin inbox, password reset by email, email notifications, guest checkout, multi-currency and i18n, a full-text search engine.

### 4.3 Out of scope
- Real payments (test mode only).
- Real shipping carriers, tax calculation (prices include VAT) and invoices.
- Sending real email.
- Native mobile apps and social login.
- Training programs and video content from v1 (dropped in Phase 0, D017). Feature numbers keep their original values, so F12 is unused.

## 5. Business rules (invented for the fictional store)

| Rule | Value |
|---|---|
| Currency | EUR, prices include VAT, 2 decimal places, rounding half away from zero |
| Discount | Integer percent 0–90; sale price = round(price × (100 − discount) / 100, 2) |
| Shipping | €4.90 flat; free when the subtotal is at least €60.00 |
| Cart line quantity | 1–10, and never above available stock |
| Stock | Decremented when an order is placed (reserved); restored on cancel or payment failure/expiry |
| Pending payment expiry | Orders still in `PendingPayment` after 30 minutes are cancelled and restocked (Stripe mode only) |
| Order number | `FM-` + 6-digit sequence (e.g. `FM-000123`) |
| Review eligibility | The user has an order with status Paid, Shipped or Delivered containing the product; one review per user per product |
| Password | Minimum 10 characters; ASP.NET Identity defaults otherwise; lockout after 5 failures for 5 minutes |

## 6. Screens

Every data-driven screen has **loading** (skeletons), **empty**, **error** (message plus retry) and **success** states. Every screen works at 375, 768 and 1280 px and is fully keyboard-operable.

| Screen | Route | Access | Notes |
|---|---|---|---|
| Home | `/` | public | Sections load independently; a failed section shows an inline error with retry |
| Department listing | `/supplements`, `/gear` | public | Filter panel becomes a drawer below 768 px; "no results" state offers to clear filters |
| Product detail | `/products/:slug` | public | 404 state for an unknown slug |
| Brands / brand detail | `/brands`, `/brands/:slug` | public | |
| Cart | `/cart` | public | Empty state links to shopping; stock-conflict warnings per line |
| Checkout | `/checkout` | customer | Three-step stepper; redirects to the cart if it is empty |
| Order confirmation | `/orders/:number/confirmation` | owner | |
| Sign in / Register | `/sign-in`, `/register` | guest | Return URL preserved |
| Account | `/account` (profile, password, orders) | customer | |
| Order detail | `/account/orders/:number` | owner | Status timeline; cancel button when allowed |
| Admin dashboard | `/admin` | admin | Date-range picker; chart has a table alternative |
| Admin products / categories / brands | `/admin/...` | admin | Server-side paged tables; create/edit forms; confirm before delete/archive |
| Admin orders / order detail | `/admin/orders`, `/admin/orders/:number` | admin | Allowed transitions only; CSV export |
| About, Contact, 404 | `/about`, `/contact`, `**` | public | |

## 7. API

REST over JSON. Errors are RFC 7807 `application/problem+json` with a `traceId`; validation errors are `400` with an `errors` map. Lists are paged as `{ items, page, pageSize, totalCount }` (`pageSize` at most 48). Sort keys are mapped from a fixed allow-list. The OpenAPI document and the Scalar UI are available in Development only.

| Method and path | Access | Purpose |
|---|---|---|
| `GET /health` | public | Liveness and DB readiness |
| `GET /api/catalog/departments` | public | Departments with their categories |
| `GET /api/catalog/brands`, `GET /api/catalog/brands/{slug}` | public | Brands |
| `GET /api/catalog/products?q&department&category&brand&minPrice&maxPrice&inStock&sort&page&pageSize` | public | Search and filter |
| `GET /api/catalog/products/{slug}` | public | Detail |
| `GET /api/catalog/products/{slug}/related` | public | Up to 4 related products |
| `GET /api/catalog/products/{slug}/reviews?page` | public | Visible reviews |
| `GET /api/images/{id}` | public | Image bytes with long cache headers (ID is content-addressed) |
| `POST /api/auth/register`, `POST /api/auth/sign-in`, `POST /api/auth/sign-out` | public (sign-out: user) | Rate-limited |
| `GET /api/auth/me` | public | `{ user }`, with `user: null` for guests (200 either way, so guest page loads log no errors); also refreshes the XSRF cookie |
| `POST /api/cart/quote` `{items}` | public, rate-limited | Price a guest cart (nothing persisted) |
| `GET /api/cart`; `PUT /api/cart/items/{productId}` `{quantity}`; `DELETE /api/cart/items/{productId}`; `DELETE /api/cart`; `POST /api/cart/merge` | customer | The caller's own cart only (no cart ID in any route). Every call returns the priced cart. `PUT` answers 404 for archived or unknown products and 409 (with `maxQuantity`) when out of stock or above stock. Lines carry `issue`: `unavailable`, `out-of-stock` or `exceeds-stock`. |
| `POST /api/checkout` | customer | Create an order from the cart; returns the order plus a payment action |
| `POST /api/payments/stripe/webhook` | public, signature-verified | Stripe events |
| `GET /api/orders?page`, `GET /api/orders/{number}`, `POST /api/orders/{number}/cancel` | customer (own) | |
| `PUT /api/account/profile`, `POST /api/account/password` | customer | |
| `POST /api/catalog/products/{slug}/reviews` | customer, eligible | |
| `GET/POST/PUT/DELETE /api/admin/products[/{id}]` | admin | Delete archives the product if it has orders |
| `GET/POST/PUT/DELETE /api/admin/categories`, `.../brands` | admin | |
| `POST /api/admin/images` | admin | Multipart upload |
| `GET /api/admin/orders?status&from&to&q&page`, `GET /api/admin/orders/{number}`, `POST /api/admin/orders/{number}/status` | admin | |
| `GET /api/admin/orders/export?from&to` | admin | `text/csv` |
| `GET /api/admin/dashboard?from&to` | admin | |
| `PUT /api/admin/reviews/{id}/visibility` | admin | |
| `POST /api/admin/demo/reset` | admin | Enabled only when `DEMO_MODE=true` |

CSRF: the session cookie is `HttpOnly; Secure; SameSite=Strict`. Every unsafe `/api` request, sign-in and register included, also needs `X-XSRF-TOKEN` matching the readable `XSRF-TOKEN` cookie; Angular's built-in XSRF support sends it.

- Tokens are bound to the user and are reissued on `/me`, sign-in, register and sign-out.
- Requests matching no endpoint fall under the authenticated fallback policy: guests get 401, signed-in users get 404, both as problem details.

## 8. Data model (PostgreSQL)

All IDs are UUID v7 (`Guid.CreateVersion7()`). Timestamps are `timestamptz` in UTC. Money is `numeric(12,2)`. Aggregates use `xmin` as the optimistic-concurrency token.

```
Department (enum: Supplements, Gear)
Category    Id, Department, Name, Slug [unique], Description?, SortOrder
Brand       Id, Name, Slug [unique], Description, LogoImageId?, AccentColor
Product     Id, Sku [unique], Name, Slug [unique], Description, CategoryId→Category, BrandId→Brand,
            Price, DiscountPercent, StockQuantity, IsActive, ImageId?, RatingAverage numeric(3,2), RatingCount,
            CreatedAt, UpdatedAt
            idx: (CategoryId), (BrandId), (IsActive, CreatedAt), trigram index on Name for search
Image       Id, Sha256 [unique], ContentType, Data bytea, SizeBytes, Width, Height, CreatedAt
User        ASP.NET Identity (Guid keys) + DisplayName, CreatedAt;  Roles: Admin, Customer
Cart        Id, UserId [unique], UpdatedAt
CartItem    CartId, ProductId [PK pair], Quantity, AddedAt
Order       Id, Number [unique], UserId, Status, Subtotal, ShippingCost, Total, Currency,
            ShipFullName, ShipLine1, ShipLine2?, ShipCity, ShipPostalCode, ShipCountry, ShipPhone, ContactEmail,
            PaymentProvider, PaymentReference?, PlacedAt, PaidAt?, ShippedAt?, DeliveredAt?, CancelledAt?
            idx: (UserId, PlacedAt desc), (Status, PlacedAt)
OrderItem   Id, OrderId, ProductId→Product (restrict), ProductName, Sku, UnitPrice, Quantity, LineTotal
OrderStatusChange  Id, OrderId, FromStatus?, ToStatus, ChangedByUserId?, ChangedAt, Note?
Review      Id, ProductId, UserId, Rating 1–5, Title, Body, IsHidden, CreatedAt;  unique(ProductId, UserId)
```

**Seed data** (deterministic, idempotent):
- 2 departments and 8 categories.
- 6 invented brands, each with an SVG wordmark logo.
- About 60 products, each with original SVG artwork made for this project.
- About 40 historical orders over the last 90 days, so the dashboard has data.
- Reviews.
- Demo users `admin@fitness-merchant.example` and `customer@fitness-merchant.example`, with passwords taken from the environment.

**Uploads:** PNG, JPEG or WebP only, max 1 MB, max 4000×4000 px. The content type is detected from magic bytes, not the header. Files are re-encoded server-side to WebP to strip metadata. Images are stored in the database, which avoids persistent disk on cheap hosts (D009). The original filename is never trusted.

## 9. Non-functional requirements

- **Security:**
  - Auth on every endpoint except the public ones listed above. Role checks run server-side.
  - Rate limiting on sign-in, register, checkout and upload.
  - Security headers: strict CSP with no `unsafe-inline` scripts, HSTS in production, `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy`, `frame-ancestors 'none'`.
  - Parameterized queries only.
  - Non-root containers.
  - Secrets only in the environment.
  - Secret and personal-data scan of the files and full history before the repo goes public.
- **Performance (targets, to be measured in M12 and recorded in PROGRESS.md):**
  - catalog list API p95 < 200 ms locally with the seeded dataset;
  - Lighthouse performance ≥ 90 on Home and Listing (mobile, production build);
  - initial JS bundle < 300 kB gzipped; routes lazy-loaded.
- **Accessibility:**
  - WCAG 2.2 AA target, and no serious or critical axe violations on any screen.
  - Visible focus, logical order and skip link.
  - Labelled form controls with associated error messages.
  - Charts have a data-table alternative.
  - Colour contrast is checked in both themes.
- **Responsive:** 375 / 768 / 1280 px verified on every screen; no horizontal scroll.
- **Reliability:**
  - Migrations apply on startup in containers.
  - Checkout is atomic: one transaction with stock re-checked and concurrency tokens.
  - Idempotent webhook handling.
- **Observability:** structured JSON logs with trace IDs, never logging secrets, passwords, cookies or full request bodies; the health endpoint reports DB readiness.
- **Browsers:** current and previous versions of Chrome, Edge, Firefox and Safari.
- **Quality gates:** nullable reference types and strict TypeScript; warnings are errors in CI; lint and format checks in CI.

## 10. Acceptance criteria (product level)

1. A guest can find a product by search plus filters, add it to the cart, register, see the guest cart merged, check out with the fake gateway, and see the order in history with status Paid.
2. A declined test card leaves the order unpaid, shows a clear error, and does not lose the cart.
3. Two customers buying the last unit at the same time: exactly one succeeds; the other sees a stock error (integration test).
4. A customer cannot read or change another user's cart or orders (integration tests return `404`), and cannot reach any `/api/admin` endpoint (`403`).
5. An admin can create a product with an uploaded image, see it in the storefront, archive it, and see it disappear from the storefront while existing orders still show it.
6. An admin can move an order through the allowed transitions only; cancelling restocks.
7. The dashboard totals match a SQL check over the seed data; the CSV export opens cleanly and neutralizes formula cells.
8. Every screen passes the 375/768/1280, keyboard, console-clean and axe checks.
9. `docker compose up` from a fresh clone runs the whole stack; the Playwright flow passes against it in CI.
10. The deployed demo works with the documented demo credentials; its limits, cold starts and cost are documented.

## 11. Delivery plan

| # | Milestone | Depends on |
|---|---|---|
| M01 | Skeleton, CI, app shell | none |
| M02 | Data model and seed data | M01 |
| M03 | Authentication | M02 |
| M04 | Catalog (API + storefront) | M02 |
| M05 | Cart (server + guest merge) | M03, M04 |
| M06 | Checkout and orders (fake gateway) | M05 |
| M07 | Stripe Checkout (test mode) | M06 |
| M08 | My account | M06 |
| M09 | Admin catalog and image upload | M04, M03 |
| M10 | Admin orders, dashboard, CSV export | M06 |
| M11 | Reviews and ratings | M06 |
| M12 | Polish: states, responsive, a11y, performance, demo reset, clean-up | all features |
| M13 | E2E test, production Docker, compose, CI E2E | M12 |
| M14 | Deployment, README, architecture, portfolio kit, public-repo scan | M13 |

Details for each milestone are in `prompts/Mnn-*.md`; status is tracked in `PROGRESS.md`.
