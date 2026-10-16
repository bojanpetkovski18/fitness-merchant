import { defineConfig, devices } from '@playwright/test';
import { existsSync } from 'node:fs';
import { join } from 'node:path';

// The same repo-root .env that docker compose reads, so the journey signs in as the seeded demo admin.
// Variables already set (e.g. in CI) win.
const envFile = join(__dirname, '..', '..', '.env');
if (existsSync(envFile)) {
  process.loadEnvFile(envFile);
}

/**
 * Runs against an already running stack (docker compose up --build), never a dev server: the point is to test
 * the production image. E2E_BASE_URL overrides the default.
 */
export default defineConfig({
  testDir: './tests',
  timeout: 90_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  workers: 1,
  retries: 0, // a flaky journey should fail loudly, not pass on a retry
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:8080',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
