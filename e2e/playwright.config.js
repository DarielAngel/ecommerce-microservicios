import { defineConfig, devices } from '@playwright/test'

// Las URLs apuntan a los contenedores ya levantados con `docker compose up -d`
// (o a `pnpm run dev` si estás probando en modo desarrollo) — Playwright no levanta
// nada por sí mismo, asume que el stack ya está corriendo.
export const STOREFRONT_URL = process.env.STOREFRONT_URL || 'http://localhost:5173'
export const ADMIN_PANEL_URL = process.env.ADMIN_PANEL_URL || 'http://localhost:8081'
export const GATEWAY_URL = process.env.GATEWAY_URL || 'http://localhost:5000'
export const ADMIN_PANEL_SITE_KEY = process.env.ADMIN_PANEL_SITE_KEY || 'clave-panel-de-desarrollo'
export const ADMIN_PROVISIONING_KEY = process.env.ADMIN_PROVISIONING_KEY || 'clave-admin-de-desarrollo'

export default defineConfig({
  testDir: './tests',
  fullyParallel: false, // los specs comparten datos sembrados por globalSetup — más predecible en serie
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  globalSetup: './global-setup.js',
  use: {
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } }
  ]
})
