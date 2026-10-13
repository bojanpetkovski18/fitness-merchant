# M11: Reviews and ratings

Branch: `#0011-verified-buyer-reviews-and-ratings`

## Goal
Verified buyers can review products. Product ratings are real and consistent, and admins can hide abusive reviews.

## Context
SPEC F11, section 5 (eligibility), section 7. Ratings shown since M04 are 0 until now.

## Scope
1. Review table (migration) with `unique(ProductId, UserId)`.
2. `POST /api/catalog/products/{slug}/reviews`:
   - rating 1–5, title 3–80 characters, body 10–2000 characters (plain text, rendered escaped);
   - eligibility check (SPEC section 5), otherwise 403 problem details;
   - one per user (409 on a duplicate);
   - in the same transaction, `Product.RatingAverage` and `RatingCount` are recalculated from SQL (not incremented in memory);
   - rate-limited.
3. `GET .../reviews?page`: visible reviews only, newest first, with the reviewer's display name (never their email).
4. `PUT /api/admin/reviews/{id}/visibility` recalculates the rating.
5. Seed reviews consistent with the seeded orders.
6. UI:
   - product page: rating summary with distribution bars (accessible text), paged review list with empty and error states, and a review form shown only when eligible (otherwise an explanation);
   - "Top rated" on Home and the rating sort now use real data;
   - admin: a reviews list with a hide/unhide toggle (simple page under admin).

## Out of scope
Editing your own review, helpful votes, review images.

## Tests required
- Integration:
  - ineligible user gets 403 (no order, or only a Cancelled order);
  - eligible user can post once, then gets 409;
  - the rating average and count are correct after posting and after hiding/unhiding;
  - HTML in the body is stored as text and returned unchanged (the client escapes it);
  - email is never exposed.
- Web: review form validation; eligibility states.

## Acceptance criteria
- Manual flow in the browser: buy a product as the demo customer, mark it Paid or Delivered as admin, review it, and see the rating update on the card and the detail page.
- A body containing `<script>` renders as text.
- Manual pass at 3 widths; keyboard; console clean.

## Verification required
Run the tests; do the manual flow; compare the rating in a `psql` query with the UI.
