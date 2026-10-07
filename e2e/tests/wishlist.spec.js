import { test, expect } from '@playwright/test'
import { loadSeedData, registerNewCustomer } from './helpers.js'
import { STOREFRONT_URL } from '../playwright.config.js'

test.describe('Storefront — favoritos', () => {
  test('un visitante que toca el corazón va a iniciar sesión', async ({ page }) => {
    const seed = await loadSeedData()
    await page.goto(`${STOREFRONT_URL}/products/${seed.productId}`)

    await page.getByTestId('favorite-toggle').click()

    await page.waitForURL(/\/login\?redirect=/)
    await expect(page.getByRole('heading', { name: 'Iniciar sesión' })).toBeVisible()
  })

  test('marcar desde el detalle, verlo en "Mis favoritos" y moverlo al carrito', async ({ page }) => {
    const seed = await loadSeedData()
    await registerNewCustomer(page)
    await page.goto(`${STOREFRONT_URL}/products/${seed.productId}`)

    const heart = page.getByTestId('favorite-toggle')
    await expect(heart).toHaveAttribute('aria-pressed', 'false')
    await heart.click()
    await expect(heart).toHaveAttribute('aria-pressed', 'true')
    await expect(page.getByText('Agregado a tus favoritos')).toBeVisible()
    await expect(page.getByTestId('wishlist-badge')).toHaveText('1')

    // Persistido en el servidor: sobrevive a recargar la página.
    await page.reload()
    await expect(page.getByTestId('favorite-toggle')).toHaveAttribute('aria-pressed', 'true')

    await page.getByRole('link', { name: 'Mis favoritos' }).click()
    await page.waitForURL(`${STOREFRONT_URL}/favorites`)
    const item = page.getByTestId('wishlist-item')
    await expect(item).toHaveCount(1)
    await expect(item).toContainText(seed.productName)

    // El producto semilla tiene una sola variante y stock: se puede mover con un clic.
    await item.getByTestId('wishlist-move-to-cart').click()
    await expect(page.getByText(`${seed.productName} se movió al carrito`)).toBeVisible()
    await expect(page.getByTestId('wishlist-empty')).toBeVisible()
    await expect(page.getByTestId('cart-badge')).toHaveText('1')
    await expect(page.getByTestId('wishlist-badge')).toHaveCount(0)
  })

  test('el corazón de la tarjeta del catálogo marca y desmarca', async ({ page }) => {
    const seed = await loadSeedData()
    await registerNewCustomer(page)

    const search = page.getByPlaceholder('Buscar productos...')
    await search.fill(seed.productName)
    await search.press('Enter')

    const heart = page.locator('article').filter({ hasText: seed.productName }).getByTestId('favorite-toggle')
    await heart.click()
    await expect(heart).toHaveAttribute('aria-pressed', 'true')
    // El clic en el corazón no debe abrir el detalle del producto.
    await expect(page).toHaveURL(new RegExp(`^${STOREFRONT_URL}/\\?q=`))

    await heart.click()
    await expect(heart).toHaveAttribute('aria-pressed', 'false')

    await page.goto(`${STOREFRONT_URL}/favorites`)
    await expect(page.getByTestId('wishlist-empty')).toBeVisible()
  })
})
