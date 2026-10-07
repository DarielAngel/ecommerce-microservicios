import { test, expect } from '@playwright/test'
import { loadSeedData, loginToAdminPanel, registerNewCustomer } from './helpers.js'
import { ADMIN_PANEL_URL, GATEWAY_URL, STOREFRONT_URL } from '../playwright.config.js'

/** Crea un cupón por la API como el Admin sembrado. Devuelve el código (único por corrida). */
async function createCouponViaApi(seed, overrides = {}) {
  const login = await fetch(`${GATEWAY_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: seed.adminEmail, password: seed.adminPassword })
  })
  const { accessToken } = await login.json()

  const code = `E2E${Date.now().toString(36).toUpperCase()}`
  const response = await fetch(`${GATEWAY_URL}/api/coupons`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${accessToken}` },
    body: JSON.stringify({
      code, description: 'Cupón de prueba E2E: 10 %', type: 'Percentage', value: 10,
      minimumSubtotal: 0, oncePerCustomer: false, isActive: true, ...overrides
    })
  })
  if (!response.ok) throw new Error(`No se pudo crear el cupón de prueba (${response.status}): ${await response.text()}`)
  return code
}

/** Cliente nuevo con el producto sembrado (1 unidad, $25.50) en el carrito, parado en el checkout. */
async function goToCheckoutWithSeededProduct(page, seed) {
  await registerNewCustomer(page)
  await page.goto(`${STOREFRONT_URL}/products/${seed.productId}`)
  await page.getByLabel('Cantidad').fill('1')
  await page.getByRole('button', { name: 'Agregar al carrito' }).click()
  await expect(page.getByText('Agregado al carrito.')).toBeVisible()

  await page.goto(`${STOREFRONT_URL}/cart`)
  await page.getByRole('button', { name: 'Continuar al checkout' }).click()
  await expect(page).toHaveURL(/\/checkout$/)
}

test.describe('Cupones — checkout', () => {
  test('aplicar un cupón muestra el descuento y la orden se paga con el total rebajado', async ({ page }) => {
    const seed = await loadSeedData()
    const code = await createCouponViaApi(seed)
    await goToCheckoutWithSeededProduct(page, seed)

    await expect(page.getByTestId('checkout-total')).toHaveText('$25.50')
    await page.getByLabel('Código de cupón').fill(code.toLowerCase())
    await page.getByRole('button', { name: 'Aplicar' }).click()

    // 10 % de $25.50 = $2.55 → total $22.95
    await expect(page.getByTestId('coupon-applied')).toContainText(code)
    await expect(page.getByTestId('checkout-discount')).toHaveText('−$2.55')
    await expect(page.getByTestId('checkout-total')).toHaveText('$22.95')

    await page.getByLabel('Dirección de envío').fill('Calle Falsa 123, Ciudad de Prueba')
    await page.getByRole('button', { name: 'Pagar con PayPal' }).click()
    await expect(page).toHaveURL(/\/orders\/.+\/pending$/)
    await page.getByRole('button', { name: 'Ya aprobé el pago — confirmar' }).click()

    await expect(page.getByText('¡Pago confirmado!')).toBeVisible()
    await expect(page.getByText('Total pagado: $22.95')).toBeVisible()
    await expect(page.getByText(`Ahorraste $2.55 con el cupón ${code}`)).toBeVisible()
  })

  test('un código inexistente muestra el motivo y no cambia el total', async ({ page }) => {
    const seed = await loadSeedData()
    await goToCheckoutWithSeededProduct(page, seed)

    await page.getByLabel('Código de cupón').fill('NOEXISTE999')
    await page.getByRole('button', { name: 'Aplicar' }).click()

    await expect(page.getByTestId('coupon-error')).toContainText('no existe')
    await expect(page.getByTestId('checkout-total')).toHaveText('$25.50')
  })

  test('un cupón con compra mínima mayor que el carrito explica cuánto falta', async ({ page }) => {
    const seed = await loadSeedData()
    const code = await createCouponViaApi(seed, { minimumSubtotal: 100 })
    await goToCheckoutWithSeededProduct(page, seed)

    await page.getByLabel('Código de cupón').fill(code)
    await page.getByRole('button', { name: 'Aplicar' }).click()

    await expect(page.getByTestId('coupon-error')).toContainText('compra mínima de $100.00')
  })
})

test.describe('Cupones — panel de Admin', () => {
  test('el Admin crea un cupón desde el formulario y aparece en la tabla', async ({ page }) => {
    const seed = await loadSeedData()
    await loginToAdminPanel(page, seed.adminEmail, seed.adminPassword)
    await page.goto(`${ADMIN_PANEL_URL}/coupons`)

    const code = `PANEL${Date.now().toString(36).toUpperCase()}`
    await page.getByLabel('Código').fill(code)
    await page.getByLabel('Descripción (la ve el cliente)').fill('Cupón creado desde el panel')
    await page.getByLabel('Tipo').selectOption('FixedAmount')
    await page.getByLabel('Monto ($)').fill('7')
    await page.getByRole('button', { name: 'Crear cupón' }).click()

    await expect(page.getByText(`Cupón ${code} creado.`)).toBeVisible()
    const row = page.getByTestId('coupon-row').filter({ hasText: code })
    await expect(row).toContainText('$7.00')
    await expect(row).toContainText('Activo')
  })
})
