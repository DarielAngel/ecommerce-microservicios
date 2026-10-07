import { mkdir, writeFile } from 'node:fs/promises'
import { GATEWAY_URL, ADMIN_PROVISIONING_KEY } from './playwright.config.js'

const SEED_FILE = new URL('./.auth/test-data.json', import.meta.url)

async function waitForUrl(url, label, maxAttempts = 30, delayMs = 2000) {
  for (let attempt = 1; attempt <= maxAttempts; attempt++) {
    try {
      const response = await fetch(url)
      if (response.ok) return
    } catch {
      // el servicio todavía no responde — reintentar
    }
    await new Promise((resolve) => setTimeout(resolve, delayMs))
  }
  throw new Error(`${label} (${url}) no respondió después de ${maxAttempts} intentos. ¿Corriste 'docker compose up -d'?`)
}

async function waitForStatus(url, label, expectedStatus, maxAttempts = 30, delayMs = 2000) {
  for (let attempt = 1; attempt <= maxAttempts; attempt++) {
    try {
      const response = await fetch(url)
      if (response.status === expectedStatus) return
    } catch {
      // el servicio todavía no responde — reintentar
    }
    await new Promise((resolve) => setTimeout(resolve, delayMs))
  }
  throw new Error(`${label} (${url}) no respondió después de ${maxAttempts} intentos.`)
}

// El Gateway (con Catálogo detrás) y los servicios de Reseñas y Cupones arrancan por separado.
async function waitForGateway() {
  await waitForUrl(`${GATEWAY_URL}/api/categories`, 'El Gateway')
  await waitForUrl(
    `${GATEWAY_URL}/api/reviews/products/00000000-0000-0000-0000-000000000001/summary`,
    'El servicio de Reseñas'
  )
  // Cupones exige sesión: un 401 significa que el servicio ya responde detrás del Gateway.
  await waitForStatus(`${GATEWAY_URL}/api/coupons/validate?code=X&subtotal=1`, 'El servicio de Cupones', 401)
  await waitForStatus(`${GATEWAY_URL}/api/loyalty/me`, 'El servicio de Lealtad (puntos)', 401)
}

async function ensureAdmin(email, password, fullName) {
  const createResponse = await fetch(`${GATEWAY_URL}/api/admins`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-Admin-Provisioning-Key': ADMIN_PROVISIONING_KEY },
    body: JSON.stringify({ email, password, fullName })
  })

  // 201 = se creó ahora; si ya existía de una corrida anterior, el login de abajo igual funciona.
  if (!createResponse.ok && createResponse.status !== 400) {
    const body = await createResponse.text()
    throw new Error(`No se pudo crear el Admin de pruebas (${createResponse.status}): ${body}`)
  }

  const loginResponse = await fetch(`${GATEWAY_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password })
  })

  if (!loginResponse.ok) {
    throw new Error(`No se pudo loguear con el Admin de pruebas (${loginResponse.status}). Revisa ADMIN_PROVISIONING_KEY.`)
  }

  return (await loginResponse.json()).accessToken
}

async function seedCatalog(adminToken, suffix) {
  const categoryResponse = await fetch(`${GATEWAY_URL}/api/categories`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${adminToken}` },
    body: JSON.stringify({ name: `Categoría E2E ${suffix}` })
  })
  if (!categoryResponse.ok) throw new Error(`No se pudo crear la categoría de prueba (${categoryResponse.status})`)
  const category = await categoryResponse.json()

  const productName = `Producto E2E ${suffix}`
  const sku = `E2E-${suffix}`
  const productResponse = await fetch(`${GATEWAY_URL}/api/products`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${adminToken}` },
    body: JSON.stringify({
      name: productName,
      description: 'Producto sembrado automáticamente por los tests end-to-end.',
      categoryId: category.id,
      variants: [{ sku, price: 25.5, attributes: { Talla: 'U' } }]
    })
  })
  if (!productResponse.ok) throw new Error(`No se pudo crear el producto de prueba (${productResponse.status})`)
  const product = await productResponse.json()
  const variantId = product.variants[0].id

  // Catálogo publica VariantCreatedEvent por RabbitMQ al crear la variante, pero Inventario
  // lo consume de forma ASÍNCRONA — la fila de stock para esta variante puede no existir
  // todavía apenas termina la llamada de arriba. Reintentamos con backoff corto en vez de
  // asumir que ya está lista: es la forma correcta de manejar consistencia eventual entre
  // servicios, no un parche sobre un bug.
  let stockResponse
  const maxAttempts = 10
  for (let attempt = 1; attempt <= maxAttempts; attempt++) {
    stockResponse = await fetch(`${GATEWAY_URL}/api/stock/${variantId}/adjust`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${adminToken}` },
      body: JSON.stringify({ newQuantityOnHand: 50 })
    })
    if (stockResponse.ok) break
    if (stockResponse.status !== 404 || attempt === maxAttempts) break
    await new Promise((resolve) => setTimeout(resolve, 500 * attempt)) // 0.5s, 1s, 1.5s...
  }
  if (!stockResponse.ok) {
    throw new Error(
      `No se pudo ajustar el stock de prueba (${stockResponse.status}) después de ${maxAttempts} intentos. ` +
      `Si sigue dando 404, revisa que notifications-service/inventory-service estén consumiendo RabbitMQ ` +
      `correctamente ('docker compose logs inventory-service').`
    )
  }

  return { categoryId: category.id, productId: product.id, productName, sku, variantId }
}

export default async function globalSetup() {
  await waitForGateway()

  const suffix = Date.now().toString(36)
  const adminEmail = `e2e-admin-${suffix}@test.com`
  const adminPassword = 'E2ePassword123.'
  const adminToken = await ensureAdmin(adminEmail, adminPassword, 'Admin E2E')

  const catalog = await seedCatalog(adminToken, suffix)

  await mkdir(new URL('./.auth', import.meta.url), { recursive: true })
  await writeFile(SEED_FILE, JSON.stringify({ adminEmail, adminPassword, ...catalog }, null, 2))
}

export { SEED_FILE }
