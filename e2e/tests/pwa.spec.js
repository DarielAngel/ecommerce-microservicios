import { test, expect } from '@playwright/test'
import { loadSeedData } from './helpers.js'
import { STOREFRONT_URL } from '../playwright.config.js'

// PWA (Fase 8): la tienda se puede instalar y abre sin conexión. El service worker solo existe en la versión
// compilada (docker compose / vite preview), no con `pnpm run dev`.

/** Espera a que el service worker esté activo y controle la página (la primera visita solo lo instala). */
async function waitForServiceWorker(page) {
  await page.evaluate(async () => {
    await navigator.serviceWorker.ready
  })
  if (!(await page.evaluate(() => Boolean(navigator.serviceWorker.controller)))) {
    await page.reload()
    await page.waitForFunction(() => Boolean(navigator.serviceWorker.controller))
  }
}

test.describe('PWA — instalable y sin conexión', () => {
  test('publica un manifiesto válido con sus íconos', async ({ page, request }) => {
    await page.goto(`${STOREFRONT_URL}/`)

    const href = await page.locator('link[rel="manifest"]').getAttribute('href')
    const manifestResponse = await request.get(new URL(href, STOREFRONT_URL).toString())
    expect(manifestResponse.ok()).toBe(true)
    const manifest = await manifestResponse.json()

    expect(manifest.display).toBe('standalone')
    expect(manifest.start_url).toBe('/')
    expect(manifest.lang).toBe('es')
    const sizes = manifest.icons.map((i) => i.sizes)
    expect(sizes).toEqual(expect.arrayContaining(['192x192', '512x512']))
    expect(manifest.icons.some((i) => i.purpose === 'maskable')).toBe(true)

    for (const icon of manifest.icons) {
      const response = await request.get(new URL(icon.src, STOREFRONT_URL).toString())
      expect(response.ok(), icon.src).toBe(true)
      expect(response.headers()['content-type']).toContain('image/png')
    }

    await waitForServiceWorker(page)
  })

  test('sin conexión la tienda abre y muestra el producto que ya se vio', async ({ page, context }) => {
    const seed = await loadSeedData()
    await page.goto(`${STOREFRONT_URL}/`)
    await waitForServiceWorker(page)

    // Con red: ver el producto (queda guardado en el teléfono).
    await page.goto(`${STOREFRONT_URL}/products/${seed.productId}`)
    await expect(page.getByRole('heading', { name: seed.productName })).toBeVisible()

    // Sin red: recargar la misma página. La tienda abre desde la caché y avisa.
    await context.setOffline(true)
    await page.reload()
    await expect(page.getByTestId('offline-banner')).toBeVisible()
    await expect(page.getByRole('heading', { name: seed.productName })).toBeVisible()

    // Navegar a otra página también funciona (la "cáscara" de la app está guardada).
    await page.goto(`${STOREFRONT_URL}/`)
    await expect(page.getByTestId('offline-banner')).toBeVisible()
    await expect(page.locator('header')).toBeVisible()

    // Vuelve la red: el aviso se va solo.
    await context.setOffline(false)
    await expect(page.getByTestId('offline-banner')).toBeHidden()
  })
})
