# M08: My account

Branch: `#0008-account-profile-password-and-order-history`

## Goal
A complete account area: profile, password change, order history and order detail with cancel.

## Context
SPEC F6, screens `/account` and `/account/orders/:number`. Builds on the minimal order pages from M06.

## Scope
1. `PUT /api/account/profile` (display name, 2–50 characters, trimmed).
2. `POST /api/account/password` (current + new password): rate-limited; on success, refresh the security stamp so other sessions are signed out and the current session is re-issued.
3. Account layout with tabs or side navigation, accessible as navigation landmarks:
   - Profile;
   - Password;
   - Orders: paged, with status badges, empty state and links to detail.
4. Order detail polish:
   - status timeline from `OrderStatusChange`;
   - items with snapshot prices and links to products (archived products shown as text);
   - address and totals;
   - cancel button with a confirm dialog (CDK dialog, focus-managed) when allowed.

## Out of scope
Address book, account deletion, email change, password reset by email.

## Tests required
- Integration:
  - profile validation;
  - password change with the wrong current password is rejected;
  - after a password change, an old session cookie is rejected;
  - rate limit on the password change.
- Web: profile and password forms (validation, server errors); confirm dialog focus behaviour.

## Acceptance criteria
- The manual pass at 3 widths covers every tab's loading, empty, error and success states.
- Keyboard: tabs and the dialog are fully operable, and Escape closes the dialog with focus restored.
- Console clean.

## Verification required
Run the tests; do the manual browser pass. To check the session invalidation, sign in from two browsers (or a normal and a private window), change the password in one, and confirm the other is signed out.
