# Progress

Estimates are rough focused-work hours for agent and owner together, including review. Status values: `todo`, `in progress`, `in review`, `done`, `blocked`.

| # | Milestone | Branch | Estimate | Status | Verified on | Notes |
|---|---|---|---|---|---|---|
| P0 | Project setup (spec, rules, plans) | `#0000-project-setup` | 2 h | in review | none | Docs only; nothing to build yet |
| M01 | Skeleton, CI, app shell | `#0001-skeleton` | 6 h | todo | none | |
| M02 | Data model and seed data | `#0002-data-model` | 6 h | todo | none | Includes the original SVG artwork |
| M03 | Authentication | `#0003-auth` | 6 h | todo | none | |
| M04 | Catalog (API + storefront) | `#0004-catalog` | 8 h | todo | none | |
| M05 | Cart (server + guest merge) | `#0005-cart` | 6 h | todo | none | |
| M06 | Checkout and orders (fake gateway) | `#0006-checkout` | 8 h | todo | none | |
| M07 | Stripe Checkout (test mode) | `#0007-stripe` | 6 h | todo | none | Needs the owner's Stripe test keys |
| M08 | My account | `#0008-account` | 4 h | todo | none | |
| M09 | Admin catalog and image upload | `#0009-admin-catalog` | 8 h | todo | none | |
| M10 | Admin orders, dashboard, CSV | `#0010-admin-orders` | 8 h | todo | none | |
| M11 | Reviews and ratings | `#0011-reviews` | 4 h | todo | none | |
| M12 | Polish | `#0012-polish` | 8 h | todo | none | Review the known issues here |
| M13 | E2E, Docker, Compose | `#0013-e2e-docker` | 6 h | todo | none | |
| M14 | Deploy, README, portfolio | `#0014-deploy-docs` | 6 h | todo | none | |

## Measured results
Every number cited in the README or docs must appear here, with its method, machine and date.

| Metric | Value | Method | Machine | Date |
|---|---|---|---|---|
| none yet | | | | |

Machine for local measurements: _to be recorded at first measurement (CPU, RAM, OS)._

## Known issues
Deferred by the owner. Re-check them when relevant, and in M12.

_None yet._

## Next step
The owner adds the GitHub remote; the agent pushes `#0000-project-setup`; the owner opens and merges the PR. Then M01 starts.
