import { readFile } from 'node:fs/promises'
import { SEED_FILE } from '../global-setup.js'
import { ADMIN_PANEL_URL, ADMIN_PANEL_SITE_KEY, STOREFRONT_URL } from '../playwright.config.js'

export async function loadSeedData() {
  return JSON.parse(await readFile(SEED_FILE, 'utf-8'))
}

/** Pasa las dos pantallas de login del panel de Admin: clave de sitio + credenciales reales. */
export async function loginToAdminPanel(page, email, password) {
  await page.goto(`${ADMIN_PANEL_URL}/login`)
  await page.getByLabel('Clave de acceso').fill(ADMIN_PANEL_SITE_KEY)
  await page.getByRole('button', { name: 'Continuar' }).click()

  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Contraseña').fill(password)
  await page.getByRole('button', { name: 'Iniciar sesión' }).click()

  await page.waitForURL(`${ADMIN_PANEL_URL}/`)
}

/** Registra un cliente nuevo (único por corrida) en el storefront y deja la sesión iniciada. */
export async function registerNewCustomer(page) {
  const suffix = Date.now().toString(36)
  const email = `e2e-cliente-${suffix}@test.com`
  const fullName = 'Cliente E2E'

  await page.goto(`${STOREFRONT_URL}/register`)
  await page.getByLabel('Nombre completo').fill(fullName)
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Contraseña').fill('E2ePassword123.')
  await page.getByRole('button', { name: 'Crear cuenta' }).click()

  await page.waitForURL(`${STOREFRONT_URL}/`)
  return { email, fullName }
}
