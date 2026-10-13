# M06: Checkout and orders (fake gateway)

Branch: `#0006-checkout-orders-and-fake-payment-gateway`

## Goal
A customer turns their cart into an order in a single atomic transaction, pays with the fake gateway, and sees a confirmation and their order history. Overselling is impossible.

## Context
SPEC F8–F10, sections 5, 7 and 10 (criteria 1–4); D010, D012. v1 lesson: checkout ignored the cart, and orders were not linked to users.

## Scope
1. Order, OrderItem and OrderStatusChange tables (migration); order-number sequence (`FM-000001`).
2. `IPaymentGateway` with a `FakePaymentGateway`:
   - card `4242 4242 4242 4242` succeeds; `4000 0000 0000 0002` is declined;
   - any expiry in the future and any CVC are accepted;
   - card data is never stored or logged; only the last 4 digits and the brand are kept on the order.

   Select the gateway via `Payments__Provider=Fake`.
3. `POST /api/checkout` with the shipping address and payment input. The handler:
   1. loads the caller's cart;
   2. re-validates stock and prices;
   3. in one transaction: creates the order with price snapshots, decrements stock (optimistic concurrency via `xmin`, retried once), creates a `PendingPayment` status row, and clears the cart;
   4. calls the gateway: on success, sets `Paid` and records `PaidAt`; on decline, cancels the order and restocks in a transaction. The decline returns a problem detail; the cart is restored so it is not lost.

   An idempotency key header prevents double submission.
4. Order state machine in the Domain, with allowed transitions only (SPEC F10). Customer cancel is allowed while `PendingPayment` or `Paid`, and restocks.
5. Customer endpoints: list own orders (paged), detail by number, cancel. Another user's order returns 404.
6. UI:
   - checkout stepper (CDK stepper) with address form (server validation errors mapped to fields), payment step (test-card hint shown), review step and Place order (button disabled while submitting);
   - confirmation page;
   - minimal "My orders" list and detail with a status timeline. Full account area comes in M08.
7. Seed (idempotent, deterministic):
   - about 10 fictional customer accounts;
   - about 40 historical orders spread over the last 90 days in mixed statuses, with matching status-change rows;
   - dates relative to the seed time, so the dashboard always has recent data.

## Out of scope
Stripe (M07), admin order processing (M10), emails.

## Tests required
- Integration:
  - happy path (order Paid, stock decremented, cart empty, snapshots correct);
  - declined card (order Cancelled, stock restored, cart restored);
  - empty cart returns 400;
  - price changed between cart and checkout uses the current price, and the review step shows it;
  - **concurrency**: two parallel checkouts for the last unit; exactly one succeeds;
  - idempotency key replay returns the same order;
  - cancel restocks; cannot cancel a Shipped order;
  - another user's order returns 404.
- Unit: state-machine transitions table; totals.
- Web: checkout form validation and step navigation; submit button disables while submitting.

## Acceptance criteria
- SPEC acceptance criteria 1–4 pass (criterion 1 manually in the browser, the others via tests).
- The checkout is usable keyboard-only at 3 widths; errors are announced; console clean.
- Logs contain no card numbers. Show a grep of the logs from the manual run.

## Verification required
Run the tests, including the concurrency test several times (report how many runs). Do a manual browser checkout with both test cards, then inspect the DB rows.
