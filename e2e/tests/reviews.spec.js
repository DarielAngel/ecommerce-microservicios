import { test, expect } from '@playwright/test'
import { loadSeedData, registerNewCustomer, fillShippingAddress } from './helpers.js'
import { STOREFRONT_URL } from '../playwright.config.js'

/** Escribe y publica una reseña desde la página del producto (el cliente ya debe tener sesión). */
async function publishReview(page, { stars = 4, title, comment = 'Funciona muy bien.' }) {
  await page.getByRole('radio', { name: `${stars} ${stars === 1 ? 'estrella' : 'estrellas'}` }).click()
  await page.getByLabel('Título').fill(title)
  await page.getByLabel(/Comentario/).fill(comment)
  await page.getByRole('button', { name: 'Publicar reseña' }).click()
  await expect(page.getByText('Tu reseña', { exact: true })).toBeVisible()
}

/** Cada reseña aparece dos veces (tarjeta "Tu reseña" y lista pública): se busca solo en la lista. */
const inPublicList = (page, text) => page.locator('li').filter({ hasText: text })

test.describe('Storefront — reseñas y calificaciones', () => {
  test('un visitante ve las opiniones y la invitación a iniciar sesión, sin formulario', async ({ page }) => {
    const seed = await loadSeedData()

    await page.goto(`${STOREFRONT_URL}/products/${seed.productId}`)

    await expect(page.getByRole('heading', { name: 'Opiniones de clientes' })).toBeVisible()
    await expect(page.getByRole('link', { name: 'Inicia sesión para dejar tu reseña' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Publicar reseña' })).toHaveCount(0)
  })

  test('un cliente publica una reseña y la ve en la lista y en el resumen del producto', async ({ page }) => {
    const seed = await loadSeedData()
    await registerNewCustomer(page)
    await page.goto(`${STOREFRONT_URL}/products/${seed.productId}`)

    const title = `Reseña e2e ${Date.now()}`
    await publishReview(page, { stars: 4, title })

    await expect(page.getByText('¡Gracias por tu reseña!')).toBeVisible()
    await expect(inPublicList(page, title)).toBeVisible()
    // El nombre se muestra protegido: nombre + inicial, nunca el nombre completo.
    await expect(inPublicList(page, title).getByText('Cliente E.')).toBeVisible()
    // El resumen bajo el título del producto ya cuenta reseñas.
    await expect(page.getByTestId('rating-link')).toContainText('reseña')
    // El formulario se reemplaza por la reseña propia.
    await expect(page.getByRole('button', { name: 'Publicar reseña' })).toHaveCount(0)
  })

  test('publicar sin elegir estrellas muestra el error y no envía nada', async ({ page }) => {
    const seed = await loadSeedData()
    await registerNewCustomer(page)
    await page.goto(`${STOREFRONT_URL}/products/${seed.productId}`)

    await page.getByLabel('Título').fill('Sin estrellas')
    await page.getByRole('button', { name: 'Publicar reseña' }).click()

    await expect(page.getByRole('alert')).toContainText('Elige una calificación')
    await expect(page.getByText('Tu reseña', { exact: true })).toHaveCount(0)
  })

  test('el cliente puede editar su reseña (y no puede crear una segunda)', async ({ page }) => {
    const seed = await loadSeedData()
    await registerNewCustomer(page)
    await page.goto(`${STOREFRONT_URL}/products/${seed.productId}`)

    const original = `Original ${Date.now()}`
    const edited = `Editada ${Date.now()}`
    await publishReview(page, { stars: 2, title: original })

    // Ya reseñó: no hay formulario nuevo, solo Editar / Eliminar.
    await expect(page.getByRole('button', { name: 'Publicar reseña' })).toHaveCount(0)

    await page.getByRole('button', { name: 'Editar' }).click()
    await page.getByRole('radio', { name: '5 estrellas' }).click()
    await page.getByLabel('Título').fill(edited)
    await page.getByRole('button', { name: 'Guardar cambios' }).click()

    await expect(inPublicList(page, edited)).toBeVisible()
    await expect(inPublicList(page, original)).toHaveCount(0)
  })

  test('eliminar pide confirmación; luego la reseña desaparece y vuelve el formulario', async ({ page }) => {
    const seed = await loadSeedData()
    await registerNewCustomer(page)
    await page.goto(`${STOREFRONT_URL}/products/${seed.productId}`)

    const title = `Para borrar ${Date.now()}`
    await publishReview(page, { stars: 3, title })

    await page.getByRole('button', { name: 'Eliminar' }).click()
    await expect(page.getByText('¿Seguro que quieres eliminarla?')).toBeVisible()
    await page.getByRole('button', { name: 'Sí, eliminar' }).click()

    await expect(inPublicList(page, title)).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Publicar reseña' })).toBeVisible()
  })

  test('las estrellas y la cantidad aparecen en la tarjeta del catálogo', async ({ page }) => {
    const seed = await loadSeedData()
    await registerNewCustomer(page)
    await page.goto(`${STOREFRONT_URL}/products/${seed.productId}`)
    await publishReview(page, { stars: 5, title: `Para el catálogo ${Date.now()}` })

    await page.goto(`${STOREFRONT_URL}/`)
    const search = page.getByPlaceholder('Buscar productos...')
    await search.fill(seed.productName)
    await search.press('Enter')

    const card = page.getByRole('link', { name: new RegExp(seed.productName) })
    await expect(card.getByRole('img', { name: /Calificación/ })).toBeVisible()
  })

  test('quien compró el producto ve la insignia "Compra verificada" en su reseña', async ({ page }) => {
    const seed = await loadSeedData()
    await registerNewCustomer(page)

    // 1) Compra real (PayPal simulado): publica OrderPaidEvent con los ProductIds.
    await page.goto(`${STOREFRONT_URL}/products/${seed.productId}`)
    await page.getByLabel('Cantidad').fill('1')
    await page.getByRole('button', { name: 'Agregar al carrito' }).click()
    await expect(page.getByText('Agregado al carrito.')).toBeVisible()

    await page.goto(`${STOREFRONT_URL}/cart`)
    await page.getByRole('button', { name: 'Continuar al checkout' }).click()
    await fillShippingAddress(page)
    await page.getByRole('button', { name: 'Pagar con PayPal' }).click()
    await expect(page).toHaveURL(/\/orders\/.+\/pending$/)
    await page.getByRole('button', { name: 'Ya aprobé el pago — confirmar' }).click()
    await expect(page.getByText('¡Pago confirmado!')).toBeVisible()

    // 2) Reseña del producto comprado.
    await page.goto(`${STOREFRONT_URL}/products/${seed.productId}`)
    await publishReview(page, { stars: 5, title: `Comprado ${Date.now()}` })

    // 3) El evento viaja por RabbitMQ de forma asíncrona: si la reseña se creó antes de que
    //    llegara, el servicio la verifica al recibirlo. Se reintenta recargando hasta verla.
    await expect(async () => {
      await page.reload()
      await expect(page.getByTestId('verified-badge').first()).toBeVisible({ timeout: 2000 })
    }).toPass({ timeout: 30_000 })
  })
})
