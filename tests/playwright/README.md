# LON Playwright E2E

Phase 17 §E15 — pragmatic hybrid happy-path. Uses the API for setup
(login, master-data, ClientOrder create) and the UI for the user-facing
hub flow (navigation, AI helper drawer, audit tab, Razdolzuvanje view).

## Install

```bash
cd tests/playwright
npm ci
npx playwright install --with-deps chromium
```

## Run

Against local dev (frontend at http://localhost:3000, API at http://localhost:3000/api):

```bash
BASE_URL=http://localhost:3000 npx playwright test
```

Against VPS:

```bash
BASE_URL=https://elon.elbosoft.click \
  API_URL=https://elon.elbosoft.click/api \
  npx playwright test
```

## Headed mode (watch the browser drive)

```bash
npx playwright test --headed
```

## UI mode (interactive)

```bash
npx playwright test --ui
```

## What the happy-path covers

1. **Auth** — API login, JWT stashed in localStorage, navigation to a
   protected route.
2. **ClientOrder hub** (§E1–§E9) — render header (OrderNumber chip),
   action launcher, ≥6 tabs.
3. **AI helper drawer** (§E10) — FAB visible, drawer opens, the
   `hub.draft.no-fgs` recommendation appears for a fresh Draft order.
4. **Audit tab** (§E13) — Create entry shows up for the just-created CO.
5. **Razdolzuvanje view** (§E9) — page renders even when totals are zero.
6. **FxRate endpoint** (§E16) — seeded EUR/MKD effective rate is > 0.

The full v1 acceptance loop (IM → Receive → BOM → PO → Podelba →
MaterialIssue → ProductionReceipt → EX → Razdolzuvanje) lives in the
xUnit integration suite (`tests/LON.IntegrationTests`, 200+ [Fact]s)
where it's faster + less flaky than UI drives. This Playwright spec
proves the live system surfaces work end-to-end.

## CI

GitHub Action workflow (post-v1; see VERIFICATION.md §J6 for template):

```yaml
- run: cd tests/playwright && npm ci && npx playwright install --with-deps chromium
- run: cd tests/playwright && npx playwright test
- if: failure()
  uses: actions/upload-artifact@v4
  with:
    name: playwright-report
    path: tests/playwright/playwright-report/
```
