# M09: Admin catalog and image upload

Branch: `#0009-admin-catalog-crud-and-image-upload`

## Goal
Admins manage products, categories and brands, including validated image uploads, through a clean admin area.

## Context
SPEC F13, section 8 (upload rules), screens `/admin/...`; D009. All endpoints are under `/api/admin` and require the Admin policy.

## Scope
1. Admin endpoints:
   - **Products:** paged list with search, filter by category, brand and active state, and an allow-listed sort; get, create, update and delete.
     - Delete archives the product (`IsActive=false`) when it has order items; otherwise it is a hard delete. The response says which.
     - Optimistic concurrency via `xmin`: a stale update returns 409 problem details.
   - **Categories and brands:** CRUD. Delete is blocked with 409 when products reference them.
   - **Images:** `POST /api/admin/images` (multipart):
     - limits: 1 MB, ≤ 4000×4000 px;
     - format: PNG, JPEG or WebP, detected by magic bytes;
     - decoded and re-encoded to WebP to strip metadata, with a library chosen and logged (licence checked);
     - deduplicated by SHA-256;
     - rate-limited.
2. Slugs are generated from names (unique, with a suffix on collision) and editable.
3. Admin UI:
   - admin layout with side navigation;
   - products table: server-side paging and sorting, search, filters, and loading, empty and error states;
   - product form with all fields, server validation mapped to fields, an image picker (upload with progress and preview, or choose an existing image), and a conflict message on 409 offering a reload;
   - categories and brands forms;
   - delete and archive confirmation dialogs.
4. The admin nav entry is visible only to admins; `adminGuard` is on all admin routes.

## Out of scope
Orders admin and dashboard (M10), bulk import.

## Tests required
- Integration:
  - Customer gets 403 and anonymous gets 401 on every admin endpoint (one parameterized test over the route table);
  - create/update validation;
  - archive vs. hard delete;
  - 409 on a stale update;
  - category delete blocked when in use.
- Uploads:
  - oversize file;
  - wrong magic bytes with a spoofed content type;
  - image too large in dimensions;
  - a valid PNG becomes stored as WebP;
  - the duplicate upload returns the same ID;
  - EXIF metadata is removed.
- Web: product form validation and 409 handling.

## Acceptance criteria
- SPEC acceptance criterion 5 passes (manual browser check).
- The admin tables are usable at 768 and 1280 px; at 375 px they degrade to a stacked-card layout, stay readable, and have no horizontal page scroll.
- Keyboard-only create/edit/delete flow; console clean.

## Verification required
Run the tests; upload real PNG, JPEG and WebP files plus a renamed `.txt` file in the browser; confirm the storefront reflects the changes.
