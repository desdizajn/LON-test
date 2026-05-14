import { defineConfig, devices } from '@playwright/test';

/**
 * Phase 17 §E15 — Playwright config for the LON happy-path E2E.
 *
 * BASE_URL controls where the test points (default = local frontend
 * dev server on http://localhost:3000). Override to https://elon.elbosoft.click
 * to run against the live VPS deployment as a smoke check.
 *
 * API_URL is used by setup hooks (auth.ts, seeds.ts) that hit the API
 * directly to keep the UI-driven part of the test snappy.
 */
export default defineConfig({
  testDir: './tests',
  fullyParallel: false,                 // happy-path is sequential by nature.
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['html'], ['list']] : 'list',
  timeout: 60_000,
  expect: { timeout: 15_000 },

  use: {
    baseURL: process.env.BASE_URL || 'http://localhost:3000',
    extraHTTPHeaders: {
      Accept: 'application/json',
    },
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
