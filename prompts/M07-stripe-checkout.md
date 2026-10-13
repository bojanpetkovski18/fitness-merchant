# M07: Stripe Checkout (test mode)

Branch: `#0007-stripe`

## Goal
With `Payments__Provider=Stripe`, checkout redirects to Stripe's hosted Checkout page in test mode. A signature-verified, idempotent webhook marks orders paid, and abandoned payments are expired and restocked.

## Context
SPEC F9, sections 5 (pending expiry) and 7; D010. Research the current Stripe .NET SDK version and the Checkout Session and webhook docs at the time; record the findings and date. The test keys belong to the owner; they are never committed.

## Scope
1. `StripePaymentGateway`:
   - creates a Checkout Session with line items from the order snapshot, the order ID in metadata, and success/cancel URLs;
   - checkout returns `{ orderNumber, redirectUrl }` and the UI redirects.
2. `POST /api/payments/stripe/webhook`:
   - anonymous, with no antiforgery, but verifies the `Stripe-Signature` header;
   - handles `checkout.session.completed` (→ Paid) and `checkout.session.expired` (→ Cancelled, restock);
   - idempotent via a processed-event table;
   - responds quickly; logs the event ID only.
3. Background expiry job: cancels and restocks orders in `PendingPayment` for longer than 30 minutes (safety net when webhooks are missed). Use `TimeProvider` so it is testable.
4. Return pages: success shows "confirming payment…", polling the order status with a timeout; cancel returns to the cart with the items restored.
5. Config: `STRIPE_SECRET_KEY` and `STRIPE_WEBHOOK_SECRET` (placeholders in `.env.example`); startup validation when the provider is Stripe. CSP updated for the redirect if needed; verify it.

## Out of scope
Refunds, Stripe Elements, saved cards, real (live) mode.

## Tests required
- Integration, with the Stripe HTTP layer stubbed:
  - webhook with a valid signature marks the order Paid;
  - an invalid signature returns 400;
  - a replayed event has no double effect;
  - an expired event restocks;
  - the expiry job cancels stale orders (using a fake `TimeProvider`).
- The fake gateway remains the default for all other tests and for E2E.

## Acceptance criteria
- A manual test-mode payment with the Stripe CLI forwarding webhooks locally works end to end; the order becomes Paid. Record the commands used in CLAUDE.md.
- Declined test card on Stripe's page → return to the cart; the order is cancelled after expiry or cancel.
- No keys in the repo, history or logs.

## Verification required
Run the tests; do the manual Stripe CLI run (if the owner's test keys are unavailable, say so and mark this not verified); grep the logs for key prefixes (`sk_`, `whsec_`).
