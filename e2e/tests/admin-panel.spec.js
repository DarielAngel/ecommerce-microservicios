import { test, expect } from '@playwright/test'
import { loadSeedData, loginToAdminPanel } from './helpers.js'
import { ADMIN_PANEL_URL } from '../playwright.config.js'

test.describe('Panel de Admin — acceso', () => {
  test('pide la clave de sitio y luego el login real, en ese orden', async ({ page }) => {
    const seed = await loadSeedData()

    await page.goto(`${ADMIN_PANEL_URL}/login`)
    await expect(page.getByText('Ingresa la clave de acceso al panel.')).toBeVisible()

    await loginToAdminPanel(page, seed.adminEmail, seed.adminPassword)

    await expect(page.getByRole('heading', { name: 'Panel de Administración' })).toBeVisible()
  })

  test('un login con credenciales invalidas muestra el error del servidor, no un error generico', async ({ page }) => {
    await page.goto(`${ADMIN_PANEL_URL}/login`)
    await page.getByLabel('Clave de acceso').fill('clave-panel-de-desarrollo')
    await page.getByRole('button', { name: 'Continuar' }).click()

    await page.getByLabel('Email').fill('no-existe@test.com')
    await page.getByLabel('Contraseña').fill('ClaveInventada123.')
    await page.getByRole('button', { name: 'Iniciar sesión' }).click()

    await expect(page.locator('p.text-red-600')).toBeVisible()
    await expect(page).toHaveURL(/\/login$/)
  })
})

test.describe('Panel de Admin — gestión', () => {
  test.beforeEach(async ({ page }) => {
    const seed = await loadSeedData()
    await loginToAdminPanel(page, seed.adminEmail, seed.adminPassword)
  })

  test('crea una categoría nueva y aparece en la tabla', async ({ page }) => {
    const categoryName = `Categoría Playwright ${Date.now()}`

    await page.goto(`${ADMIN_PANEL_URL}/categories`)
    await page.getByLabel('Nombre').fill(categoryName)
    await page.getByRole('button', { name: 'Crear' }).click()

    await expect(page.getByRole('cell', { name: categoryName })).toBeVisible()
  })

  test('el producto sembrado por el setup aparece en la lista de Productos', async ({ page }) => {
    const seed = await loadSeedData()

    await page.goto(`${ADMIN_PANEL_URL}/products`)
    await page.getByPlaceholder('Buscar por nombre o descripción...').fill(seed.productName)

    await expect(page.getByRole('cell', { name: seed.productName })).toBeVisible()
  })

  test('consulta el stock de la variante sembrada y lo ajusta', async ({ page }) => {
    const seed = await loadSeedData()

    await page.goto(`${ADMIN_PANEL_URL}/inventory`)
    await page.getByLabel('Id de la variante').fill(seed.variantId)
    await page.getByRole('button', { name: 'Buscar' }).click()

    await expect(page.getByText('Disponible').locator('..').getByText('50')).toBeVisible()

    await page.getByLabel('Nueva cantidad en mano').fill('42')
    // No getByRole('button', { name: 'Ajustar' }): la tabla de "bajo stock" puede tener sus
    // propios botones "Ajustar" (uno por fila, con datos reales de pruebas manuales
    // anteriores) — el data-testid distingue el botón principal sin ambigüedad.
    await page.getByTestId('stock-adjust-main').click()

    await expect(page.getByText('Stock actualizado.')).toBeVisible()
  })

  test('la sección de Notificaciones carga sin error (aunque esté vacía)', async ({ page }) => {
    await page.goto(`${ADMIN_PANEL_URL}/notifications`)

    await expect(page.getByRole('heading', { name: 'Notificaciones enviadas' })).toBeVisible()
    await expect(page.getByText(/Ocurrió un error/)).not.toBeVisible()
  })
})
