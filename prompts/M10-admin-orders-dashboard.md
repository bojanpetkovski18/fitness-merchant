# M10: Admin orders, dashboard and CSV export

Branch: `#0010-admin-orders`

## Goal
Admins process orders through the allowed status transitions, see sales metrics for a date range, and export orders to a safe CSV.

## Context
SPEC F14–F16, section 10 (criteria 6–7). Aggregations run in SQL. The historical orders seeded in M06 give the dashboard data.

## Scope
1. Admin order endpoints:
   - paged list filtered by status, date range and search (order number, customer email);
   - detail with items and status history;
   - `POST .../status` `{ toStatus, note? }` validated by the domain state machine; records `ChangedByUserId`; cancel restocks.
2. `GET /api/admin/dashboard?from&to` (max 366 days). All values are computed by grouped SQL:
   - revenue and order count (Paid, Shipped and Delivered only);
   - average order value;
   - revenue per day, with zero-filled days;
   - top 5 products by quantity and revenue;
   - low-stock products (≤5).
3. `GET /api/admin/orders/export?from&to` streams CSV (UTF-8 with BOM for Excel).
   - Cells are escaped (RFC 4180) and protected against formula injection (prefix `'` to values starting with `=`, `+`, `-`, `@`, tab or carriage return).
   - Row limit of 10,000, otherwise 400; rate-limited.
4. Admin UI:
   - orders table with filters, all in the URL;
   - order detail with status actions limited to allowed transitions, a confirm dialog with a note, and the timeline;
   - dashboard: date-range presets (7, 30 or 90 days, custom), KPI tiles, revenue chart (a lightweight chart library chosen and logged, or hand-rolled SVG) with a data-table alternative, top products and low stock;
   - export button.

## Out of scope
Refunds, shipping labels, emails, customer management.

## Tests required
- Integration:
  - allowed and forbidden transitions (table-driven);
  - cancel restocks;
  - dashboard numbers equal a hand-written SQL check on a known dataset;
  - zero-filled days;
  - the date range is validated;
  - the CSV escapes commas, quotes and newlines;
  - formula cells are neutralized (`=HYPERLINK(...)`, `+1`, `-1`, `@SUM`);
  - the row limit is enforced.
- Web: the status action menu shows only allowed transitions; dashboard states.

## Acceptance criteria
- SPEC criteria 6 and 7 pass.
- The CSV opens cleanly in a spreadsheet app. Check it manually; if no spreadsheet app is available, say so and show the raw file.
- The chart is keyboard- and screen-reader-accessible via the table alternative.
- Manual pass at 3 widths; console clean.

## Verification required
Run the tests; process an order through every status in the browser; export and open the CSV; compare the dashboard totals with a `psql` query (paste both).
