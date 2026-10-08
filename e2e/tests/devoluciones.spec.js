import { test, expect } from '@playwright/test'
import { loadSeedData, registerNewCustomer, fillShippingAddress, loginToAdminPanel } from './helpers.js'
import { ADMIN_PANEL_URL, GATEWAY_URL, STOREFRONT_URL } from '../playwright.config.js'

async function adminToken(seed) {
  const response = await fetch(`${GATEWAY_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: seed.adminEmail, password: seed.adminPassword })
  })
  return (await response.json()).accessToken
}

async function stockOnHand(seed, token) {
  const response = await fetch(`${GATEWAY_URL}/api/stock/${seed.variantId}`, { headers: { Authorization: `Bearer ${token}` } })
  return (await response.json()).quantityOnHand
}

test.describe('Devoluciones y reembolsos (Fase 7)', () => {
  test('el cliente pide devolver 1 de 2, el Admin la aprueba: reembolso, stock y puntos se ajustan', async ({ page }) => {
    const seed = await loadSeedData()
    await registerNewCustomer(page)

    // Compra 2 × $25.50 = $51 (51 puntos) con PayPal simulado.
    await page.goto(`${STOREFRONT_URL}/products/${seed.productId}`)
    await page.getByLabel('Cantidad').fill('2')
    await page.getByRole('button', { name: 'Agregar al carrito' }).click()
    await expect(page.getByText('Agregado al carrito.')).toBeVisible()
    await page.goto(`${STOREFRONT_URL}/cart`)
    await page.getByRole('button', { name: 'Continuar al checkout' }).click()
    await fillShippingAddress(page)
    await page.getByRole('button', { name: 'Pagar con PayPal' }).click()
    await expect(page).toHaveURL(/\/orders\/.+\/pending$/)
    const orderId = page.url().match(/\/orders\/([^/]+)\/pending$/)[1]
    await page.getByRole('button', { name: 'Ya aprobé el pago — confirmar' }).click()
    await expect(page.getByText('¡Pago confirmado!')).toBeVisible()

    // El Admin lo despacha (solo un pedido enviado se puede devolver).
    const token = await adminToken(seed)
    const ship = await fetch(`${GATEWAY_URL}/api/orders/${orderId}/ship`, { method: 'POST', headers: { Authorization: `Bearer ${token}` } })
    expect(ship.ok).toBe(true)

    // Mis pedidos → Solicitar devolución de 1 unidad.
    await page.goto(`${STOREFRONT_URL}/orders`)
    const order = page.getByTestId('order-item').filter({ hasText: orderId.slice(0, 8) })
    await order.getByRole('button', { name: new RegExp(orderId.slice(0, 8)) }).click()
    await expect(order.getByTestId('return-deadline')).toContainText('Puedes pedir la devolución hasta el')
    await order.getByTestId('request-return').click()
    await order.getByTestId('return-line').first().getByRole('spinbutton').fill('1')
    await order.getByLabel('Motivo').selectOption('Damaged')
    await order.getByLabel(/Comentario/).fill('La caja llegó aplastada.')
    await order.getByTestId('return-submit').click()
    await expect(page.getByText('Recibimos tu solicitud de devolución.')).toBeVisible()
    await expect(order.getByTestId('order-return')).toContainText('Solicitada')
    await expect(order.getByTestId('request-return')).toHaveCount(0)

    const stockBefore = await stockOnHand(seed, token)

    // Panel de Admin → Devoluciones → aprobar: se devuelve la mitad de lo pagado.
    await loginToAdminPanel(page, seed.adminEmail, seed.adminPassword)
    await page.getByRole('link', { name: 'Devoluciones' }).first().click()
    const row = page.getByTestId('return-row').filter({ hasText: `#${orderId.slice(0, 8).toUpperCase()}` })
    await expect(row).toContainText('La caja llegó aplastada.')
    await row.getByTestId('return-approve').click()
    await expect(page.getByText(/reembolsada: \$25\.50/)).toBeVisible()

    // El cliente ve el reembolso.
    await page.goto(`${STOREFRONT_URL}/orders`)
    await expect(order.getByTestId('order-refunded')).toHaveText('· reembolsado $25.50')
    await order.getByRole('button', { name: new RegExp(orderId.slice(0, 8)) }).click()
    await expect(order.getByTestId('order-return')).toContainText('Reembolsada: $25.50')
    // Todavía queda 1 unidad: puede pedir otra devolución.
    await expect(order.getByTestId('request-return')).toBeVisible()

    // Los puntos (51) bajan 25 por lo devuelto, y la unidad vuelve al stock (llegan por eventos).
    await expect(async () => {
      await page.goto(`${STOREFRONT_URL}/points`)
      await expect(page.getByTestId('points-balance-value')).toHaveText('26', { timeout: 1000 })
    }).toPass({ timeout: 30000 })
    await expect(page.getByTestId('points-entry').filter({ hasText: 'Descontados por la devolución' })).toContainText('−25')
    await expect.poll(() => stockOnHand(seed, token), { timeout: 30000 }).toBe(stockBefore + 1)
  })

  test('el Admin rechaza con una nota y el cliente la ve', async ({ page }) => {
    const seed = await loadSeedData()
    await registerNewCustomer(page)
    await page.goto(`${STOREFRONT_URL}/products/${seed.productId}`)
    await page.getByLabel('Cantidad').fill('1')
    await page.getByRole('button', { name: 'Agregar al carrito' }).click()
    await expect(page.getByText('Agregado al carrito.')).toBeVisible()
    await page.goto(`${STOREFRONT_URL}/cart`)
    await page.getByRole('button', { name: 'Continuar al checkout' }).click()
    await fillShippingAddress(page)
    await page.getByRole('button', { name: 'Pagar con PayPal' }).click()
    await expect(page).toHaveURL(/\/orders\/.+\/pending$/)
    const orderId = page.url().match(/\/orders\/([^/]+)\/pending$/)[1]
    await page.getByRole('button', { name: 'Ya aprobé el pago — confirmar' }).click()
    await expect(page.getByText('¡Pago confirmado!')).toBeVisible()
    const token = await adminToken(seed)
    expect((await fetch(`${GATEWAY_URL}/api/orders/${orderId}/ship`, { method: 'POST', headers: { Authorization: `Bearer ${token}` } })).ok).toBe(true)

    await page.goto(`${STOREFRONT_URL}/orders`)
    const order = page.getByTestId('order-item').filter({ hasText: orderId.slice(0, 8) })
    await order.getByRole('button', { name: new RegExp(orderId.slice(0, 8)) }).click()
    await order.getByTestId('request-return').click()
    await order.getByLabel('Motivo').selectOption('ChangedMind')
    await order.getByTestId('return-submit').click()
    await expect(order.getByTestId('order-return')).toContainText('Solicitada')

    await loginToAdminPanel(page, seed.adminEmail, seed.adminPassword)
    await page.goto(`${ADMIN_PANEL_URL}/returns`)
    const row = page.getByTestId('return-row').filter({ hasText: `#${orderId.slice(0, 8).toUpperCase()}` })
    await row.getByTestId('return-reject').click()
    await row.getByTestId('return-note').fill('Pasaron más de 7 días desde la entrega para este motivo.')
    await row.getByTestId('return-confirm-reject').click()
    await expect(page.getByText(/rechazada\. Le avisamos/)).toBeVisible()

    await page.goto(`${STOREFRONT_URL}/orders`)
    await order.getByRole('button', { name: new RegExp(orderId.slice(0, 8)) }).click()
    await expect(order.getByTestId('order-return')).toContainText('Rechazada')
    await expect(order.getByTestId('order-return-note')).toHaveText('Nota de la tienda: Pasaron más de 7 días desde la entrega para este motivo.')
  })
})
