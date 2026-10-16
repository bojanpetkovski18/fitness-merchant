# M12 audit: states, responsive, accessibility, performance

Audited on 2026-10-16 on machine M1 (see `PROGRESS.md`), on the **production build served by the API**
(`npm run build`, `Spa__RootPath`), so the strict CSP and production-only behaviour were in force. Browser
checks used Playwright 1.63.0 (Chromium) with `@axe-core/playwright`. The audit scripts are throwaway and
not in the repository; the results are recorded here and in `PROGRESS.md` → Measured results.

## States

Every data-driven screen and its loading, empty, error and success states. "Form" means the screen only
submits data: its error state is the field errors and an alert, and its success state is the confirmation.

| Screen | Loading | Empty | Error | Success |
|---|---|---|---|---|
| Home (`/`) | Skeleton per rail | "Nothing here right now" per rail; "No brands yet" (fixed in M12) | Per-section error with retry | Rails, brands |
| Listing (`/supplements`, `/gear`) | Skeleton grid | "No results" with "Clear filters" | Error with retry | Grid, pagination |
| Product (`/products/:slug`) | Status text | Not-found state (unknown slug) | Error with retry | Detail, related, reviews |
| Reviews (on the product page) | Status text | "No reviews yet" / "Be the first" | Error with retry | Summary, list, form |
| Brands / brand (`/brands`, `/brands/:slug`) | Skeletons | "No brands" / not found | Error with retry | Cards, products |
| Cart (`/cart`) | Status text | "Your cart is empty" with shop link | Error with retry | Lines, summary |
| Checkout (`/checkout`) | "Loading your cart" (added in M12) | Empty cart with shop link | Cart error with retry (added in M12); step and payment alerts | Confirmation page |
| Order confirmation | Status text | Not found | Error with retry | Confirmation |
| Sign in / register | Form | n/a | Field errors, alert | Redirect |
| Account: profile, password | Form | n/a | Field errors, alert | Status message |
| Account: orders, order | Status text | "No orders yet" / not found | Error with retry | List / detail, cancel |
| Admin: dashboard | Status text | "No sales in this range" | Error with retry; invalid range explained | Tiles, chart, table |
| Admin: orders, order | Status text | "No orders match" / not found | Error with retry; filter errors | Table / detail, status actions |
| Admin: products, product form | Status text | "No products match" / not found | Error with retry; field errors; 409 reload | Table / saved |
| Admin: categories, brands (+ forms) | Status text | "No categories/brands yet" / not found | Error with retry; field errors | List / saved |
| Admin: reviews | Status text | "No reviews here" | Error with retry | List, hide/show |
| About, Contact, 404 | Static | n/a | n/a | Page |

Two gaps were found and fixed with regression tests shown failing first: checkout rendered its steps over a
cart that was still loading or had failed, and Home showed only a heading for an empty rail.

## Accessibility and responsive

30 screens (guest, customer and admin), each checked:

- axe (WCAG 2.0/2.1/2.2 A and AA rules) in the dark and light themes at 375 and 1280 px: **0 findings of any impact**.
- Horizontal overflow at 320, 375, 640 (1280 px at 200 % zoom), 768 and 1280 px: **none**.
- Landmarks: one `main`, a banner and a contentinfo, every `nav` labelled; exactly one `h1` and no skipped heading levels.
- Keyboard: Tab through the first 25 stops; every stop shows a focus indicator.
- No CSP violations and no console errors.
- `prefers-reduced-motion: reduce`: animations and transitions shortened to 0.01 ms.

Found and fixed during the audit: the header overflowed by 47 px at 320 px; stretched card links had only a
1 px border change on focus; date inputs showed no focus ring; the light-theme success colour was 4.21:1 on its
own tint. Earlier in M12: K1 (production CSP), K12 (buttons losing focus while busy).

## Performance

| Measure | Target (SPEC section 9) | Result |
|---|---|---|
| Catalog list API p95, 10 users, 60 s, k6 (`tools/load/catalog-list.js`) | < 200 ms | 6.87 ms (1,802 requests/s, 0 errors) |
| Lighthouse performance, Home, mobile | ≥ 90 | 94, 95, 96 |
| Lighthouse performance, Listing, mobile | ≥ 90 | 92, 93, 93 |
| Initial JavaScript, gzipped/Brotli | < 300 kB | 123 kB (CLI estimate); 125 kB transferred on Home |

Before the M12 fixes Lighthouse scored 61–76: the bundles were sent uncompressed and the footer moved when the
page content arrived (CLS 0.249). The build now writes Brotli and gzip copies that the API serves, and `<main>`
is at least one screen tall (CLS 0).

The Lighthouse runs used the Production environment over plain HTTP, so the session request answered 500
(the antiforgery cookie is HTTPS-only) and the pages rendered as a guest; see K13.

## Dependencies

- `npm audit`: 0 vulnerabilities (all, and production only).
- `dotnet list package --vulnerable --include-transitive`: no vulnerable packages in any project.
- No `TODO`, `FIXME` or `HACK` markers in the source.
