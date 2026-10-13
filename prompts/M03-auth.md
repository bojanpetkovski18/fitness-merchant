# M03: Authentication

Branch: `#0003-auth`

## Goal
Secure cookie-based registration, sign-in and sign-out with roles, antiforgery and rate limits, plus the Angular session store, guards and auth pages.

## Context
SPEC sections 2, 5 (password rules), 7 and 9; D005, D008. v1 lessons: no client-controlled role, no committed secrets.

## Scope
1. ASP.NET Core Identity with Guid keys:
   - User gets `DisplayName` and `CreatedAt`;
   - roles Admin and Customer; password and lockout rules from SPEC section 5;
   - migration.
2. Cookie auth:
   - `HttpOnly`, `Secure` (relaxed only in Development over http), `SameSite=Strict`, sliding expiry of 7 days;
   - API returns 401/403 problem details instead of redirects.
3. Antiforgery:
   - an endpoint or middleware issues the `XSRF-TOKEN` cookie;
   - unsafe methods on cookie-authenticated endpoints require `X-XSRF-TOKEN`;
   - Angular `withXsrfConfiguration` is set up.
4. Endpoints:
   - `POST /api/auth/register` (always Customer; signs in on success);
   - `POST /api/auth/sign-in`;
   - `POST /api/auth/sign-out`;
   - `GET /api/auth/me`.

   Generic error message for bad credentials. Rate limiting is per IP and per account on sign-in and register; record the limits in DECISIONS.
5. Authorization: fallback policy = authenticated user; public endpoints are explicitly `AllowAnonymous`; an `Admin` policy.
6. Demo users seeded from `DEMO_ADMIN_EMAIL`, `DEMO_ADMIN_PASSWORD`, `DEMO_CUSTOMER_EMAIL` and `DEMO_CUSTOMER_PASSWORD`. Skip with a warning log if they are not set.
7. Angular:
   - `SessionStore` (signal-based: `user`, `isSignedIn`, `isAdmin`), loaded on startup via `/me`;
   - sign-in and register pages: labelled fields, inline and server errors, show/hide password, return URL;
   - `authGuard` and `adminGuard`;
   - header shows the user menu or sign-in;
   - 401 interceptor clears the session.

## Out of scope
Profile and password change (M08), password reset by email (later), cart merge on sign-in (M05).

## Tests required
- Integration:
  - register → me → sign-out → me returns 401;
  - a role sent in the register body is ignored;
  - wrong password gives a generic 401;
  - lockout after 5 failures;
  - rate limit returns 429;
  - an unsafe request without an antiforgery token is rejected;
  - a non-admin calling an admin-only test endpoint gets 403;
  - the fallback policy protects an unannotated endpoint.
- Web: sign-in form validation and error display; guards redirect with the return URL.

## Acceptance criteria
- All of the tests above pass.
- In the browser: register, sign out, sign in as the demo admin; the session survives a reload; the cookie flags are correct (check DevTools → Application); no token is in localStorage.
- The forms are keyboard-only usable, errors are announced (`aria-describedby` / live region), and the layout works at the 3 widths.
- No secrets in the repo; `.env.example` updated with placeholders.

## Verification required
Run the tests; do a manual browser pass including the DevTools cookie check; `curl` sign-in without an XSRF token to show the rejection.
