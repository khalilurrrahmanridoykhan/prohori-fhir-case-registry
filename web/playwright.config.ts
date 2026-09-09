import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: './tests',
  timeout: 60_000,
  use: { baseURL: 'http://localhost:5173', headless: true },
  webServer: { command: 'npm run dev -- --host 127.0.0.1', url: 'http://localhost:5173', reuseExistingServer: !process.env.CI },
});
