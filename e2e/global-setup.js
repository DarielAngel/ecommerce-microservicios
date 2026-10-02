import { mkdir, writeFile } from 'node:fs/promises'
import { GATEWAY_URL, ADMIN_PROVISIONING_KEY } from './playwright.config.js'

const SEED_FILE = new URL('./.auth/test-data.json', import.meta.url)

async function waitForGateway(maxAttempts = 30, delayMs = 2000) {
  for (let attempt = 1; attempt <= maxAttempts; attempt++) {
    try {
      const response = await fetch(`${GATEWAY_URL}/api/categories`)
      if (response.ok) return
    } catch {
      // el Gateway (o Catalog detrás de él) todavía no responde — reintentar
    }
    await new Promise((resolve) => setTimeout(resolve, delayMs))
  }
  throw new Error(`El Gateway en ${GATEWAY_URL} no respondió después de ${maxAttempts} intentos. ¿Corriste 'docker compose up -d'?`)
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

  const stockResponse = await fetch(`${GATEWAY_URL}/api/stock/${variantId}/adjust`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${adminToken}` },
    body: JSON.stringify({ newQuantityOnHand: 50 })
  })
  if (!stockResponse.ok) throw new Error(`No se pudo ajustar el stock de prueba (${stockResponse.status})`)

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
