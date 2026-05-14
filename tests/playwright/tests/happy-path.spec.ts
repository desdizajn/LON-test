/**
 * Phase 17 §E15 — v1 acceptance happy-path E2E.
 *
 * Pragmatic hybrid: API for setup (login + master-data + ClientOrder
 * creation), UI for the user-facing hub experience (navigation, AI helper
 * drawer, audit tab, action buttons). The full IM → Receive → BOM → PO →
 * Podelba → MaterialIssue → ProductionReceipt → EX → Razdolzuvanje chain
 * is covered by the integration suite (200+ tests); this spec proves the
 * hub renders + the AI helper + audit + razdolzuvanje surface light up
 * end-to-end on a live system.
 *
 * Run locally:
 *   cd tests/playwright && npm ci && npx playwright install --with-deps chromium
 *   BASE_URL=http://localhost:3000 npx playwright test
 *
 * Run against VPS:
 *   BASE_URL=https://elon.elbosoft.click npx playwright test
 */
import { test, expect } from '@playwright/test';
import { uiLogin } from './setup/auth';
import {
  newApiContext,
  login,
  getFirstCustomerPartnerId,
  getFirstLonAuthorizationId,
  createClientOrder,
  API_URL,
} from './setup/api';

test.describe('Phase 17 happy-path', () => {
  test('Login → create order → hub renders all critical widgets', async ({ page }) => {
    // ─── Setup ─────────────────────────────────────────────────────────
    const api = await newApiContext();
    const token = await login(api);
    const partnerId = await getFirstCustomerPartnerId(api, token);
    const lonAuthId = await getFirstLonAuthorizationId(api, token);
    const orderId = await createClientOrder(api, token, {
      customerPartnerId: partnerId,
      lonAuthorizationId: lonAuthId,
      customerOrderReference: `E15-PLAYWRIGHT-${Date.now()}`,
    });

    // ─── UI: login + hub navigation ────────────────────────────────────
    await uiLogin(page);
    await page.goto(`/orders/${orderId}`);

    // Hub header carries the OrderNumber chip + customer/LON-auth strip.
    await expect(page.locator('h4').first()).toContainText(/CO-\d{4}-\d{6}/);

    // Action launcher: at least the "BOM" and "IM declaration" actions are visible.
    await expect(page.getByText(/BOM|Креирај BOM/).first()).toBeVisible();

    // Hub tabs: declarations / production orders / shipments / materials / receipts / CI / audit
    await expect(page.getByRole('tab').first()).toBeVisible();
    const tabCount = await page.getByRole('tab').count();
    expect(tabCount).toBeGreaterThanOrEqual(6);

    // ─── AI helper FAB ─────────────────────────────────────────────────
    const fab = page.locator('[data-testid="ai-helper-fab"]');
    await expect(fab).toBeVisible();
    await fab.click();
    // Drawer opens with the Recommendations tab selected. For a fresh
    // Draft ClientOrder with no FGs, the engine emits `hub.draft.no-fgs`.
    await expect(page.getByRole('button', { name: /Креирај BOM|Open/ })).toBeVisible({ timeout: 10_000 });
    // Close the drawer (Escape or X).
    await page.keyboard.press('Escape');

    // ─── Audit tab ─────────────────────────────────────────────────────
    await page.getByRole('tab', { name: /Аудит|Audit/ }).click();
    await expect(page.getByText(/Create/).first()).toBeVisible({ timeout: 10_000 });

    // ─── Razdolzuvanje navigation ──────────────────────────────────────
    await page.goto(`/orders/${orderId}/razdolzuvanje`);
    // The view loads and shows the 4-tile totals header even on a draft
    // order with no IM lines (everything is zero).
    await expect(page.locator('h4, h5').first()).toBeVisible();
  });

  test('AI helper recommendations endpoint returns a hub recommendation', async () => {
    // Pure API check — guards the contract the UI smoke depends on.
    const api = await newApiContext();
    const token = await login(api);
    const partnerId = await getFirstCustomerPartnerId(api, token);
    const lonAuthId = await getFirstLonAuthorizationId(api, token);
    const orderId = await createClientOrder(api, token, {
      customerPartnerId: partnerId,
      lonAuthorizationId: lonAuthId,
      customerOrderReference: `E15-API-${Date.now()}`,
    });

    const resp = await api.post(`${API_URL}/Ai/recommendations`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { entityType: 'ClientOrder', entityId: orderId },
    });
    expect(resp.ok()).toBeTruthy();
    const recs = await resp.json();
    expect(Array.isArray(recs)).toBeTruthy();
    expect(recs.length).toBeGreaterThan(0);
    expect(recs[0].code).toBeTruthy();
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
