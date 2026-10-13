# M05: Cart (server cart + guest cart merge)

Branch: `#0005-cart`

## Goal
A reliable cart: guests keep it in the browser, customers on the server, and the two merge on sign-in. Totals and stock rules are always enforced server-side.

## Context
SPEC F7, sections 5 and 7; D015. v1 lesson: the cart ID came from the route (IDOR) and line quantities were not persisted.

## Scope
1. Cart and CartItem tables (migration).
2. Handlers and endpoints for the caller's own cart only: get, set line quantity, remove line, clear, and merge `{ items: [{productId, quantity}] }`.
   - Rules: quantity 1–10, capped at stock; inactive or out-of-stock products are rejected with a problem detail.
   - `GET /api/cart` returns lines with current prices, line totals, subtotal, shipping and total (SPEC section 5), plus per-line warnings when stock fell below the cart quantity or a product became inactive.
3. Public `POST /api/cart/quote` (rate-limited) prices a guest cart without persisting it, so guests see real totals. Add it to SPEC section 7.
4. Angular `CartStore` (signals):
   - one interface over two backends: localStorage for guests, the API for customers;
   - merges on sign-in and switches back to the guest backend on sign-out;
   - optimistic quantity updates with rollback on error.
5. UI:
   - add to cart on product cards and the detail page, with a toast and an `aria-live` announcement;
   - header badge with the item count;
   - cart page with quantity steppers, remove, clear (with confirm), order summary, warnings, empty state, and "Proceed to checkout" (redirects guests to sign in, then back).

## Out of scope
Checkout and orders (M06), wishlist (later).

## Tests required
- Integration:
  - add, update and remove;
  - quantity bounds and stock cap;
  - an inactive product is rejected;
  - merge sums quantities with the caps;
  - user A cannot see or change user B's cart (each user only ever reaches their own);
  - shipping threshold at €59.99, €60.00 and €60.01;
  - an unauthenticated cart call returns 401.
- Unit: totals calculator.
- Web: `CartStore` guest/server switching and merge-on-sign-in; optimistic rollback.

## Acceptance criteria
- Manual browser flow at 3 widths:
  - as a guest, add items, reload (cart persists), sign in (merged, guest storage cleared), sign out (empty guest cart);
  - quantity changes update totals; the badge count is correct;
  - keyboard-only pass; screen-reader announcement on add (check the live region in DevTools).
- Stock warning shown after an admin-side stock change (simulate via SQL).

## Verification required
Run the tests; do the manual flow above; check the localStorage contents (IDs and quantities only, no prices).
