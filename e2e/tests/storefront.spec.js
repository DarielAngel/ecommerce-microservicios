import { test, expect } from '@playwright/test'
import { loadSeedData, registerNewCustomer } from './helpers.js'
import { STOREFRONT_URL } from '../playwright.config.js'

test.describe('Storefront — catálogo público', () => {
  test('un visitante (sin login) puede buscar el producto sembrado y ver su detalle', async ({ page }) => {
    const seed = await loadSeedData()

    await page.goto(`${STOREFRONT_URL}/`)
    await page.getByPlaceholder('Buscar productos...').fill(seed.productName)

    const card = page.getByRole('link', { name: new RegExp(seed.productName) })
    await expect(card).toBeVisible()
    await card.click()

    await expect(page.getByRole('heading', { name: seed.productName })).toBeVisible()
  })
})

test.describe('Storefront — carrito y checkout', () => {
  test('un cliente nuevo puede agregar al carrito y ver el total actualizado', async ({ page }) => {
    const seed = await loadSeedData()
    await registerNewCustomer(page)

    await page.goto(`${STOREFRONT_URL}/products/${seed.productId}`)
    await page.getByLabel('Cantidad').fill('2')
    await page.getByRole('button', { name: 'Agregar al carrito' }).click()

    await expect(page.getByText('Agregado al carrito.')).toBeVisible()
    // El badge del carrito en el header debe reflejar los 2 ítems agregados.
    await expect(page.getByRole('link', { name: /Carrito/ }).getByText('2')).toBeVisible()

    await page.getByRole('link', { name: /Carrito/ }).click()
    await expect(page.getByText(seed.productName)).toBeVisible()
    // "$51.00" (2 × $25.50) aparece dos veces en la pantalla: el total de la línea y el
    // subtotal del resumen — .first() evita el choque con el modo estricto de Playwright.
    await expect(page.getByText('$51.00').first()).toBeVisible()
  })

  test('pedir más cantidad de la disponible se revierte sola, sin romper el carrito', async ({ page }) => {
    const seed = await loadSeedData()
    await registerNewCustomer(page)

    await page.goto(`${STOREFRONT_URL}/products/${seed.productId}`)
    await page.getByLabel('Cantidad').fill('1')
    await page.getByRole('button', { name: 'Agregar al carrito' }).click()
    await expect(page.getByText('Agregado al carrito.')).toBeVisible()

    await page.goto(`${STOREFRONT_URL}/cart`)
    const quantityInput = page.locator('input[type="number"]').first()
    await quantityInput.fill('99999')
    await quantityInput.blur()

    // El error aparece junto a la línea, y el input vuelve solo al último valor válido
    // (regresión del bug real que corregimos: antes se quedaba "pegado" en el valor inválido).
    await expect(page.getByText(/No hay suficiente stock/)).toBeVisible()
    await expect(quantityInput).toHaveValue('1')
  })

  test('checkout completo: carrito -> pagar con PayPal simulado -> orden Paid -> aparece en Mis pedidos', async ({ page }) => {
    const seed = await loadSeedData()
    await registerNewCustomer(page)

    await page.goto(`${STOREFRONT_URL}/products/${seed.productId}`)
    await page.getByLabel('Cantidad').fill('1')
    await page.getByRole('button', { name: 'Agregar al carrito' }).click()
    await expect(page.getByText('Agregado al carrito.')).toBeVisible()

    await page.goto(`${STOREFRONT_URL}/cart`)
    await page.getByRole('button', { name: 'Continuar al checkout' }).click()

    await expect(page).toHaveURL(/\/checkout$/)
    await page.getByLabel('Dirección de envío').fill('Calle Falsa 123, Ciudad de Prueba')
    await page.getByRole('button', { name: 'Pagar con PayPal' }).click()

    // Con PayPal:Provider=Fake, approveUrl viene vacío — no se abre ninguna pestaña, se
    // navega directo a la pantalla de confirmación.
    await expect(page).toHaveURL(/\/orders\/.+\/pending$/)
    await page.getByRole('button', { name: 'Ya aprobé el pago — confirmar' }).click()

    await expect(page.getByText('¡Pago confirmado!')).toBeVisible()

    await page.getByRole('link', { name: 'Ver mis pedidos' }).click()
    await expect(page).toHaveURL(/\/orders$/)
    await expect(page.getByText('Pagada')).toBeVisible()
  })
})
