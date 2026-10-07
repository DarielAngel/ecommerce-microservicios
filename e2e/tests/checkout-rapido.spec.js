import { test, expect } from '@playwright/test'
import { loadSeedData, registerNewCustomer } from './helpers.js'
import { GATEWAY_URL, STOREFRONT_URL } from '../playwright.config.js'

async function adminToken(seed) {
  const response = await fetch(`${GATEWAY_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: seed.adminEmail, password: seed.adminPassword })
  })
  return (await response.json()).accessToken
}

/** Agrega una dirección desde "Mis direcciones". */
async function addAddress(page, { label, street, city = 'Córdoba', country = 'Argentina' }) {
  await page.getByTestId('address-new').click()
  await page.getByLabel('Nombre de la dirección').fill(label)
  await page.getByLabel('Calle y número').fill(street)
  await page.getByLabel('Ciudad', { exact: true }).fill(city)
  await page.getByLabel('País').fill(country)
  await page.getByRole('button', { name: 'Guardar' }).click()
  await expect(page.getByTestId('address-form')).toHaveCount(0)
}

test.describe('Checkout rápido y seguimiento (Fase 5)', () => {
  test('libreta de direcciones: agregar, cambiar la predeterminada y borrar', async ({ page }) => {
    await registerNewCustomer(page)
    await page.getByTestId('account-link').click()
    await expect(page).toHaveURL(/\/addresses$/)
    await expect(page.getByTestId('addresses-empty')).toBeVisible()

    await addAddress(page, { label: 'Casa', street: 'Av. Siempre Viva 742' })
    const items = page.getByTestId('address-item')
    await expect(items).toHaveCount(1)
    await expect(items.first().getByTestId('address-default-badge')).toBeVisible()

    await addAddress(page, { label: 'Oficina', street: 'Bv. San Juan 100' })
    await expect(items).toHaveCount(2)
    await items.filter({ hasText: 'Oficina' }).getByTestId('address-make-default').click()
    await expect(items.first()).toContainText('Oficina')
    await expect(items.first().getByTestId('address-default-badge')).toBeVisible()

    const casa = items.filter({ hasText: 'Casa' })
    await casa.getByTestId('address-delete').click()
    await casa.getByTestId('address-confirm-delete').click()
    await expect(items).toHaveCount(1)
    await expect(items.first()).toContainText('Bv. San Juan 100')
  })

  test('paga con la dirección guardada, sigue el pedido hasta "Enviado" y lo compra de nuevo', async ({ page }) => {
    const seed = await loadSeedData()
    await registerNewCustomer(page)
    await page.goto(`${STOREFRONT_URL}/addresses`)
    await addAddress(page, { label: 'Casa', street: 'Av. Colón 1234' })

    // Carrito → checkout: la dirección guardada ya viene elegida, no hay que escribir nada.
    await page.goto(`${STOREFRONT_URL}/products/${seed.productId}`)
    await page.getByLabel('Cantidad').fill('1')
    await page.getByRole('button', { name: 'Agregar al carrito' }).click()
    await expect(page.getByText('Agregado al carrito.')).toBeVisible()
    await page.goto(`${STOREFRONT_URL}/cart`)
    await page.getByRole('button', { name: 'Continuar al checkout' }).click()

    const saved = page.getByTestId('checkout-address-option')
    await expect(saved).toHaveCount(1)
    await expect(saved.getByRole('radio')).toBeChecked()
    await expect(saved).toContainText('Av. Colón 1234')
    await page.getByRole('button', { name: 'Pagar con PayPal' }).click()

    await expect(page).toHaveURL(/\/orders\/.+\/pending$/)
    const orderId = page.url().match(/\/orders\/([^/]+)\/pending$/)[1]
    await page.getByRole('button', { name: 'Ya aprobé el pago — confirmar' }).click()
    await expect(page.getByText('¡Pago confirmado!')).toBeVisible()

    // Mis pedidos: entrega estimada y línea de tiempo esperando el envío.
    await page.goto(`${STOREFRONT_URL}/orders`)
    const order = page.getByTestId('order-item').filter({ hasText: orderId.slice(0, 8) })
    await expect(order.getByTestId('order-delivery')).toContainText('Llega')
    await order.getByRole('button', { name: new RegExp(orderId.slice(0, 8)) }).click()
    const steps = order.getByTestId('order-step')
    await expect(steps.nth(1)).toContainText('Pago confirmado')
    await expect(steps.nth(2)).toHaveAttribute('data-state', 'current')
    await expect(order.getByTestId('order-address')).toContainText('Av. Colón 1234')

    // El Admin lo despacha: al recargar, "Enviado" ya está hecho y lo próximo es la entrega.
    const shipResponse = await fetch(`${GATEWAY_URL}/api/orders/${orderId}/ship`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${await adminToken(seed)}` }
    })
    expect(shipResponse.ok).toBe(true)
    await page.reload()
    await order.getByRole('button', { name: new RegExp(orderId.slice(0, 8)) }).click()
    await expect(order).toContainText('Enviada')
    await expect(steps.nth(2)).toHaveAttribute('data-state', 'done')
    await expect(steps.nth(3)).toContainText('(estimado)')

    // Comprar de nuevo: el carrito quedó vacío con el pago; vuelve a tener el producto.
    await order.getByTestId('buy-again').click()
    await expect(page).toHaveURL(/\/cart$/)
    await expect(page.getByText(seed.productName)).toBeVisible()
    await expect(page.getByTestId('cart-badge')).toHaveText('1')
  })
})
