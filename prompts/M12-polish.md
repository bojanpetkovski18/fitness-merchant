# M12: Polish: states, responsive, accessibility, performance, demo reset

Branch: `#0012-polish-states-responsive-a11y-perf-demo-reset`

## Goal
Every screen is finished: consistent states, responsive at every width, accessible, fast. A demo reset keeps the public demo clean. All deferred known issues are reviewed with the owner.

## Context
SPEC sections 6 and 9 (targets), F17–F19. Start by listing every item in `PROGRESS.md` → Known issues and asking the owner which to fix now.

## Scope
1. **States audit:** a table of every screen × loading, empty, error and success; fix the gaps. Consistent skeletons, empty-state illustrations (original SVG), and error components with retry.
2. **Responsive audit:** every screen at 375, 768 and 1280 px, plus 320 px and 200% zoom spot checks; fix any overflow.
3. **Accessibility audit:**
   - axe (`@axe-core/playwright` or the browser extension) on every screen in both themes; fix all serious and critical issues;
   - keyboard pass;
   - contrast check of the tokens;
   - `prefers-reduced-motion` respected;
   - landmarks and headings order.
4. **Performance:**
   - measure the catalog list API p95 (a k6 or bombardier script committed in `tools/`) and Lighthouse on Home and Listing (mobile, production build);
   - measure the initial bundle size;
   - fix what misses the SPEC section 9 targets;
   - record everything in PROGRESS.md → Measured results, with method and machine.
5. **Demo reset** (F17):
   - `POST /api/admin/demo/reset`, enabled only when `DEMO_MODE=true`: in one transaction, truncate the user-generated data and re-run the seed;
   - scheduled reset every N hours (configurable);
   - a banner "Demo: data resets every N hours" when demo mode is on;
   - demo mode also blocks deleting or demoting the demo admin.
6. **Static pages:** About (portfolio project, AI-assistance note, link to the repo), Contact info and FAQ (CDK accordion).
7. **Clean-up:** dead code, TODOs resolved or logged, consistent naming, `npm audit` and `dotnet list package --vulnerable` reviewed (report the results).

## Out of scope
New features.

## Tests required
- Integration: demo reset restores the seed counts and is forbidden when demo mode is off.
- Any regression tests for bugs found in the audits (each shown failing first).

## Acceptance criteria
- The states table is complete with no gaps.
- axe reports zero serious or critical violations on every screen (attach a summary).
- Measured numbers are recorded; any target missed is listed with a reason and the owner's decision.
- Each known issue has been fixed or explicitly re-deferred by the owner.

## Verification required
All audits are done in a real browser on the production build (`ng build` served by the API) as well as the dev server, because CSP differences only show up in production.
