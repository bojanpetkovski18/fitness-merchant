# M04: Catalog (API + storefront)

Branch: `#0004-catalog-search-filters-and-storefront-pages`

## Goal
Shoppers can browse, search, filter, sort and page the catalog, view product details and brands, and see a real home page, all backed by efficient SQL.

## Context
SPEC features F1–F4, sections 6 and 7. Lists are paged; sort keys come from an allow-list; search uses `ILIKE` plus trigram similarity.

## Scope
1. Queries (one handler each):
   - departments with categories;
   - brands list and detail;
   - product list with q, department, category (multi), brand (multi), minPrice, maxPrice, inStock, sort (`newest|price-asc|price-desc|rating|discount`), page and pageSize (max 48);
   - product detail by slug;
   - related products (same category, then same brand, up to 4, excluding self).

   Only active products are returned. Projections and `AsNoTracking` are used throughout.
2. Harden the image endpoint: `Cache-Control: public, max-age=31536000, immutable` and ETag.
3. Response DTOs include price, discount, sale price (server-computed), stock status (`in-stock`, `low-stock` (≤5), `out-of-stock`), and rating average and count. Ratings are 0 until M11.
4. Angular pages:
   - Home: hero; Hot Deals, Top Rated and New Arrivals as product rails (each section has its own loading and error states); brand strip.
   - Department listing:
     - filter panel (a drawer on mobile, using CDK);
     - all state in the URL query params, so back/forward and shared links work;
     - debounced search; result count; empty state with "clear filters";
     - pagination with accessible labels.
   - Product detail: image, price block, stock badge, quantity selector (disabled "Add to cart" placeholder until M05), description and related products; 404 state.
   - Brands grid and brand detail with that brand's products.
   - Shared `ProductCard` (one accessible link, discount badge, sale price, rating stars with text alternative) and skeleton components.
5. Page titles and meta descriptions per route.

## Out of scope
Cart (M05), reviews list (M11), admin (M09).

## Tests required
- Integration:
  - each filter alone and combined;
  - each sort;
  - paging bounds (page beyond range returns empty items with the correct total);
  - invalid sort or pageSize returns 400 problem details;
  - inactive products are hidden;
  - search finds partial and misspelled names (trigram);
  - unknown slug returns 404.
- Unit: query-parameter parsing and validation.
- Web: `ProductCard` rendering (discount and out-of-stock variants); listing page reads and writes URL state; empty and error states render.

## Acceptance criteria
- In a browser, at 3 widths:
  - filtering, sorting and paging work and survive a reload and back/forward;
  - every section shows a skeleton while loading, an empty state and an error state (simulate the API being down);
  - keyboard-only pass works; console clean.
- Log the SQL for the list query in Development and confirm a single paged query plus count, with no N+1 (paste the SQL into the report).

## Verification required
Run the tests; do the manual browser pass described above, including stopping the API to see the error states; inspect the EF-generated SQL.
