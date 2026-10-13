# M14: Deployment, README, architecture and portfolio kit

Branch: `#0014-deploy-docs`

## Goal
A deployed public demo with documented limits and cost, a client-facing README, an accurate architecture document, a portfolio kit, and a clean security scan before the repository goes public.

## Context
D016 (hosting is decided here). SPEC acceptance criterion 10. Numbers used anywhere must come from PROGRESS.md → Measured results.

## Scope
1. **Hosting research** (current sources, dated):
   - compare 3 or more options for one container plus managed Postgres: free and low tiers, sleep/cold-start behaviour, DB size limits, egress, custom domain, region and monthly cost;
   - recommend one and ask the owner before creating any account or resource;
   - record the result as a decision in DECISIONS.md.
2. **Deploy:**
   - the production image from M13 with environment secrets set in the host (never in the repo);
   - `DEMO_MODE=true` with scheduled reset;
   - Stripe test keys if M07 was done;
   - HTTPS and HSTS verified;
   - a CI deploy job on merge to `main` (or a documented manual step if the host does not allow it).
3. **Smoke test the live demo:**
   - the full journey by hand at 3 widths;
   - security headers;
   - measure the cold-start time and record it.
4. **README** (client-facing):
   - what it does and why;
   - screenshots (taken from the demo, both themes);
   - live demo link and demo credentials;
   - run locally (copy-paste steps, tested from a fresh clone);
   - a guided tour;
   - architecture diagram (Mermaid);
   - the stack with reasons;
   - developer setup and tests;
   - decisions and challenges (link DECISIONS.md, plus the v1 → v2 lessons);
   - demo limits, cold starts and cost;
   - the AI-assistance note;
   - licence.
5. **`docs/ARCHITECTURE.md`:** matches the code as built, covering:
   - request flow;
   - auth and CSRF;
   - checkout transaction and concurrency;
   - payments;
   - images;
   - data model diagram;
   - deployment.
6. **`docs/portfolio/`**, for the target platform (Upwork; check its current field limits and record the date):
   - title and description variants within the limits;
   - role, problem and solution;
   - key decisions;
   - screenshot order with captions;
   - keywords;
   - the client services the project demonstrates.

   No testimonials, revenue claims or unmeasured numbers.
7. **Public-repo scan:**
   - scan the working tree and the full git history for secrets (gitleaks or trufflehog; pin a version) and for personal data (emails other than `*.example`, phone numbers, names);
   - report the findings and remediation; the owner makes the repo public, not the agent.

## Out of scope
Custom domain purchase (owner's choice), analytics.

## Tests required
CI green, including E2E. Smoke test results recorded.

## Acceptance criteria
- SPEC criterion 10 passes.
- The README steps work from a fresh clone (re-run them).
- Every number in the README and portfolio kit has a matching row in PROGRESS.md.
- The scan reports zero unresolved findings.

## Verification required
Live demo used in a real browser at 3 widths; `curl -I` against production; fresh-clone run; scan output summarized in the report.
