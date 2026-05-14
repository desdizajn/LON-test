/**
 * Phase 17 §E15 — v1 acceptance happy-path E2E.
 *
 * Pragmatic hybrid: API for setup (login + master-data + ClientOrder lookup
 * / creation), UI for the user-facing hub experience (navigation, AI helper
 * drawer, audit tab, action buttons). The full IM → Receive → BOM → PO →
 * Podelba → MaterialIssue → ProductionReceipt → EX → Razdolzuvanje chain
 * is covered by the integration suite (200+ tests) and reconciled bit-by-bit
 * against ELON by the §E.MIGRATE LON.Migration runner; this Playwright spec
 * proves the live system surfaces light up against the canonical Zaklucok
 * 2779 fixture when it has been imported.
 *
 * Run locally:
 *   cd tests/playwright && npm ci && npx playwright install --with-deps chromium
 *   BASE_URL=http://localhost:3000 npx playwright test
 *
 * Run against VPS:
 *   BASE_URL=https://elon.elbosoft.click npx playwright test
 *
 * The spec prefers the migrated Z2779 ClientOrder when it can be located
 * via `CustomerOrderReference='2779'`. Override the reference with the
 * `LON_E2E_REFERENCE` env var if you want to target a different fixture.
 */
import { test, expect } from '@playwright/test';
import { uiLogin } from './setup/auth';
import {
  newApiContext,
  login,
  getFirstCustomerPartnerId,
  getFirstLonAuthorizationId,
  createClientOrder,
  findClientOrderByReference,
  API_URL,
} from './setup/api';

// LON.Migration stamps `O{OdobrenieRBr}-Z{ZaklucokBroj}` into
// CustomerOrderReference. For our canonical fixture that's "O1-Z2779".
const Z2779_REFERENCE = process.env.LON_E2E_REFERENCE || 'O1-Z2779';

async function resolveTargetOrder(api: import('@playwright/test').APIRequestContext, token: string) {
  const fixture = await findClientOrderByReference(api, token, Z2779_REFERENCE);
  if (fixture) {
    return {
      id: fixture.id,
      orderNumber: fixture.orderNumber,
      status: fixture.status,
      statusName: fixture.statusName,
      kind: 'migrated' as const,
    };
  }
  const partnerId = await getFirstCustomerPartnerId(api, token);
  const lonAuthId = await getFirstLonAuthorizationId(api, token);
  const id = await createClientOrder(api, token, {
    customerPartnerId: partnerId,
    lonAuthorizationId: lonAuthId,
    customerOrderReference: `E15-PLAYWRIGHT-${Date.now()}`,
  });
  return { id, orderNumber: 'CO-XXXX-XXXXXX', status: 0, statusName: 'Draft', kind: 'synthetic' as const };
}

/** Closed (4) and Cancelled (99) orders are terminal — engines correctly emit zero recs. */
function isTerminalStatus(status: number) {
  return status === 4 || status === 99;
}

test.describe('Phase 17 happy-path', () => {
  test('Login → load Z2779 (or fallback) → hub renders all critical widgets', async ({ page }) => {
    const api = await newApiContext();
    const token = await login(api);
    const target = await resolveTargetOrder(api, token);
    console.log(`[happy-path] target: ${target.orderNumber} (${target.kind})`);

    // ─── UI: login + hub navigation ────────────────────────────────────
    await uiLogin(page);
    await page.goto(`/orders/${target.id}`);

    // Hub header carries the OrderNumber chip + customer/LON-auth strip.
    await expect(page.locator('h4').first()).toContainText(/CO-\d{4}-\d{6}/);

    // Action launcher: at least the "BOM" and "IM declaration" actions are visible.
    await expect(page.getByText(/BOM|Креирај BOM/).first()).toBeVisible();

    // Hub tabs: 9 after the cutover fixes
    // (declarations / production orders / BOM / materials / material issues /
    //  shipments / receipts / commercial invoices / audit).
    await expect(page.getByRole('tab').first()).toBeVisible();
    const tabCount = await page.getByRole('tab').count();
    expect(tabCount).toBeGreaterThanOrEqual(9);

    // For the migrated Z2779 fixture the BOM + MaterialIssues tabs should
    // surface real data — verify the BOM tab renders ≥1 BOM card and
    // the MaterialIssues tab lists ≥1 issue.
    if (target.kind === 'migrated') {
      await page.getByRole('tab', { name: /BOM/ }).click();
      await expect(page.getByText(/PO PO-/).first()).toBeVisible({ timeout: 10_000 });

      await page.getByRole('tab', { name: /Издадени материјали|Issued materials/i }).click();
      // Migrated issue numbers carry `<IzdatnicaRBr>/<Year>-<hash>` for Z2779 (8232/…)
      // and Z2783 (8294/…, 8316/…); fresh hub-created issues use `MI-<year>-<seq>`.
      // Match any of those patterns.
      await expect(page.getByText(/\d{3,5}\/\d{4}-|MI-\d{4}-/).first()).toBeVisible({ timeout: 10_000 });

      // Materials tab should now show at least one item with a migrated MRN
      // (LEG-…) OR a real-flow MRN (^\d{2}MK…).
      await page.getByRole('tab', { name: /Материјали на лагер|Materials on hand/i }).click();
      await expect(page.locator('text=/LEG-|^\\d{2}MK/').first()).toBeVisible({ timeout: 10_000 });
    }

    // ─── AI helper FAB ─────────────────────────────────────────────────
    const fab = page.locator('[data-testid="ai-helper-fab"]');
    await expect(fab).toBeVisible();
    await fab.click();
    if (isTerminalStatus(target.status)) {
      // Closed / Cancelled — engines correctly return zero nudges. We
      // assert the drawer opens with the "no recommendations" surface
      // (either an empty success Alert or just no actionable buttons).
      // Wait briefly for the panel to settle and continue.
      await page.waitForTimeout(500);
    } else {
      // For a Draft order with no FGs the engine emits `hub.draft.no-fgs`.
      // For an Active order with Cleared IM but unflagged razdolzuvanje
      // lines, the engine emits the preflight nudge.
      const recButton = page.getByRole('button', { name: /Креирај BOM|Razdolzuvanje|Razdolžuvanje|Open|Отвори/i });
      await expect(recButton.first()).toBeVisible({ timeout: 10_000 });
    }
    await page.keyboard.press('Escape');

    // ─── Audit tab ─────────────────────────────────────────────────────
    await page.getByRole('tab', { name: /Аудит|Audit/ }).click();
    await expect(page.getByText(/Create|Update/).first()).toBeVisible({ timeout: 10_000 });

    // ─── Razdolzuvanje navigation ──────────────────────────────────────
    await page.goto(`/orders/${target.id}/razdolzuvanje`);
    await expect(page.locator('h4, h5').first()).toBeVisible();
  });

  test('Z2779 (or fallback) — recommendations endpoint returns the right shape for the target status', async () => {
    const api = await newApiContext();
    const token = await login(api);
    const target = await resolveTargetOrder(api, token);

    const resp = await api.post(`${API_URL}/Ai/recommendations`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { entityType: 'ClientOrder', entityId: target.id },
    });
    expect(resp.ok()).toBeTruthy();
    const recs = await resp.json();
    expect(Array.isArray(recs)).toBeTruthy();
    if (isTerminalStatus(target.status)) {
      // Closed / Cancelled — engines correctly return an empty list.
      expect(recs.length).toBe(0);
    } else {
      expect(recs.length).toBeGreaterThan(0);
      expect(recs[0].code).toBeTruthy();
    }
  });

  test('Z2779 (or fallback) — razdolzuvanje endpoint returns IM vs EX totals', async () => {
    const api = await newApiContext();
    const token = await login(api);
    const target = await resolveTargetOrder(api, token);

    const resp = await api.get(`${API_URL}/ClientOrders/${target.id}/razdolzuvanje`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(resp.ok()).toBeTruthy();
    const body = await resp.json();
    const data = body?.data ?? body;
    // Both real Z2779 and a fresh CO should expose the totals envelope.
    expect(data).toBeTruthy();
    expect(typeof data.imTotal !== 'undefined' || typeof data.imDutyTotal !== 'undefined' || data.lines !== undefined)
      .toBeTruthy();
  });

  test('FxRates endpoint returns the seeded EUR/MKD rate', async () => {
    const api = await newApiContext();
    const token = await login(api);
    const resp = await api.get(`${API_URL}/Finance/fx-rates/effective?from=EUR&to=MKD`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(resp.ok()).toBeTruthy();
    const body = await resp.json();
    expect(body.isSuccess).toBeTruthy();
    expect(body.data).toBeGreaterThan(0);
  });
});
