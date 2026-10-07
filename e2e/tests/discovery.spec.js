import { test, expect } from '@playwright/test'
import { loadSeedData } from './helpers.js'
import { GATEWAY_URL, STOREFRONT_URL } from '../playwright.config.js'

async function adminToken(seed) {
  const response = await fetch(`${GATEWAY_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: seed.adminEmail, password: seed.adminPassword })
  })
  return (await response.json()).accessToken
}

/** Crea un producto en la categoría sembrada con un stock dado (espera a que Inventario lo registre). */
async function createProductWithStock(seed, name, quantity) {
  const token = await adminToken(seed)
  const auth = { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` }
  const created = await fetch(`${GATEWAY_URL}/api/products`, {
    method: 'POST',
    headers: auth,
    body: JSON.stringify({
      name,
      description: 'Producto creado por la prueba de descubrimiento.',
      categoryId: seed.categoryId,
      variants: [{ sku: `E2E-D-${Date.now().toString(36)}`, price: 42, attributes: {} }]
    })
  })
  if (!created.ok) throw new Error(`No se pudo crear "${name}" (${created.status})`)
  const product = await created.json()

  for (let attempt = 1; attempt <= 10; attempt++) {
    const adjust = await fetch(`${GATEWAY_URL}/api/stock/${product.variants[0].id}/adjust`, {
      method: 'POST', headers: auth, body: JSON.stringify({ newQuantityOnHand: quantity })
    })
    if (adjust.ok) return product
    await new Promise((r) => setTimeout(r, 500 * attempt)) // Inventario lo registra por RabbitMQ
  }
  throw new Error(`No se pudo fijar el stock de "${name}"`)
}

test.describe('Descubrimiento (Fase 4)', () => {
  test('las sugerencias aparecen al escribir, sin importar mayúsculas, y se eligen con el teclado', async ({ page }) => {
    const seed = await loadSeedData()
    await page.goto(`${STOREFRONT_URL}/`)

    const search = page.getByRole('combobox', { name: 'Buscar productos' })
    await search.fill(seed.productName.toLowerCase())

    const option = page.getByRole('option', { name: new RegExp(seed.productName) })
    await expect(option).toBeVisible()

    await search.press('ArrowDown')
    await search.press('Enter')
    await expect(page).toHaveURL(new RegExp(`/products/${seed.productId}$`))
  })

  test('el detalle muestra relacionados de la misma categoría y la insignia de pocas unidades con stock real', async ({ page }) => {
    const seed = await loadSeedData()
    const scarceName = `Escaso E2E ${Date.now().toString(36)}`
    const scarce = await createProductWithStock(seed, scarceName, 3)

    // El producto sembrado tiene 50 unidades: sin insignia; y el escaso aparece como relacionado.
    await page.goto(`${STOREFRONT_URL}/products/${seed.productId}`)
    await expect(page.getByTestId('related-products')).toContainText(scarceName)
    await expect(page.getByTestId('availability-badge')).toHaveCount(0)

    // Desde "También te puede interesar" se pasa al otro producto (misma pantalla, otro id).
    await page.getByTestId('related-products').getByRole('link', { name: new RegExp(scarceName) }).click()
    await expect(page).toHaveURL(new RegExp(`/products/${scarce.id}$`))
    await expect(page.getByRole('heading', { level: 1 })).toHaveText(scarceName)
    await expect(page.getByTestId('availability-badge')).toHaveText('¡Quedan solo 3!')
  })

  test('"Vistos recientemente" recuerda los productos visitados y se puede borrar', async ({ page }) => {
    const seed = await loadSeedData()

    await page.goto(`${STOREFRONT_URL}/products/${seed.productId}`)
    await expect(page.getByRole('heading', { level: 1 })).toHaveText(seed.productName)

    await page.goto(`${STOREFRONT_URL}/`)
    const rail = page.getByTestId('recently-viewed')
    await expect(rail).toContainText(seed.productName)

    await page.getByTestId('clear-recently-viewed').click()
    await expect(rail).toHaveCount(0)
  })
})
