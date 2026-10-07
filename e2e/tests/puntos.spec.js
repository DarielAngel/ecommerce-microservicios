import { test, expect } from '@playwright/test'
import { loadSeedData, registerNewCustomer, fillShippingAddress } from './helpers.js'
import { STOREFRONT_URL } from '../playwright.config.js'

/** Compra el producto sembrado ($25.50 c/u) de punta a punta con PayPal simulado. */
async function buy(page, seed, { quantity, usePoints = false, firstTime = false }) {
  await page.goto(`${STOREFRONT_URL}/products/${seed.productId}`)
  await page.getByLabel('Cantidad').fill(String(quantity))
  await page.getByRole('button', { name: 'Agregar al carrito' }).click()
  await expect(page.getByText('Agregado al carrito.')).toBeVisible()
  await page.goto(`${STOREFRONT_URL}/cart`)
  await page.getByRole('button', { name: 'Continuar al checkout' }).click()
  await expect(page).toHaveURL(/\/checkout$/)

  if (firstTime) await fillShippingAddress(page)
  if (usePoints) await page.getByTestId('checkout-use-points').check()

  await page.getByRole('button', { name: 'Pagar con PayPal' }).click()
  await expect(page).toHaveURL(/\/orders\/.+\/pending$/)
  await page.getByRole('button', { name: 'Ya aprobé el pago — confirmar' }).click()
  await expect(page.getByText('¡Pago confirmado!')).toBeVisible()
}

/** Los puntos llegan por un evento (RabbitMQ): se recarga "Mis puntos" hasta ver el saldo esperado. */
async function expectBalance(page, value) {
  await expect(async () => {
    await page.goto(`${STOREFRONT_URL}/points`)
    await expect(page.getByTestId('points-balance-value')).toHaveText(String(value), { timeout: 1000 })
  }).toPass({ timeout: 30000 })
}

test.describe('Puntos de lealtad (Fase 6)', () => {
  test('una compra suma puntos y en la siguiente se usan como descuento', async ({ page }) => {
    const seed = await loadSeedData()
    await registerNewCustomer(page)

    // 4 × $25.50 = $102 → 102 puntos.
    await buy(page, seed, { quantity: 4, firstTime: true })
    await expectBalance(page, 102)
    await expect(page.getByTestId('points-entry')).toContainText('Ganaste por el pedido')

    // Segunda compra de $25.50: la mitad son $12.75, alcanza para usar los 102 puntos ($1.02).
    await page.goto(`${STOREFRONT_URL}/products/${seed.productId}`)
    await buy(page, seed, { quantity: 1, usePoints: true })

    await page.goto(`${STOREFRONT_URL}/orders`)
    const lastOrder = page.getByTestId('order-item').first()
    await expect(lastOrder.getByTestId('order-points-used')).toHaveText('· 102 puntos (−$1.02)')
    await expect(lastOrder).toContainText('$24.48')

    // Saldo final: 0 tras usarlos, +24 por lo pagado en la segunda compra.
    await expectBalance(page, 24)
    await expect(page.getByTestId('points-entry').filter({ hasText: 'Usados en el pedido' })).toContainText('−102')
  })
})
