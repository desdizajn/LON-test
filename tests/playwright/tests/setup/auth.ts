/**
 * Phase 17 §E15 — page-level auth helper. Drives the login form so the
 * SPA initialises localStorage with auth_token + auth_expires_at + user.
 */
import { Page, expect } from '@playwright/test';

export async function uiLogin(page: Page, username = 'admin', password = 'Admin123!'): Promise<void> {
  await page.goto('/login');
  // The Login page uses MUI TextFields; first one is username, second is password.
  const inputs = page.locator('input');
  await inputs.nth(0).fill(username);
  await inputs.nth(1).fill(password);
  await page.locator('button[type="submit"], button:has-text("Login"), button:has-text("Sign in"), button:has-text("Најави")').first().click();
  // Successful login should land us on the dashboard or initial route.
  await expect(page).toHaveURL(/\/(management\/dashboard|dashboard|orders|warehouse|production)/, { timeout: 20_000 });
}
