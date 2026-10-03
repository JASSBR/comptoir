import { defineConfig, devices } from '@playwright/test';

/**
 * Smoke test of a deployed environment, through the facade: the 2014 screens, the new screens, the shared session and
 * the shadow traffic. It needs the real legacy (IIS), so it runs against Azure, not in the pull-request pipeline:
 *   E2E_BASE_URL=https://<facade> npm run e2e
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  retries: 1,
  reporter: process.env['CI'] ? [['github'], ['html', { open: 'never' }]] : 'list',
  // Free tiers sleep: the first request wakes IIS and the containers.
  timeout: 180_000,
  expect: { timeout: 60_000 },
  use: {
    baseURL: process.env['E2E_BASE_URL'] ?? 'http://localhost:5400',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    locale: 'fr-FR',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
