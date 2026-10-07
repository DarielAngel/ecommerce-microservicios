#!/usr/bin/env node
// =====================================================================================
// Carga datos de demostración REALISTAS en la tienda, a través del Gateway (como lo haría
// un Admin desde el panel), para que las pruebas manuales no estén llenas de "Producto E2E kx9f".
//
//   - 23 categorías (con subcategorías) y 105 productos reales con marca, descripción,
//     variantes (talla, color, capacidad...), precio de referencia en USD, stock e imagen.
//   - 10 cupones / ofertas: activos, programados, vencidos, pausados y agotables.
//   - 10 clientes de demostración y ~340 reseñas en español.
//
// Uso (con la app levantada: docker compose up -d):
//   node scripts/seed-demo-data.mjs
//
// Es IDEMPOTENTE: lo que ya existe (por nombre o código) se deja como está, así que se puede
// correr de nuevo sin duplicar nada. Requiere Node 18 o superior (sin dependencias).
// =====================================================================================

import { readFile } from 'node:fs/promises'
import { existsSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

const here = path.dirname(fileURLToPath(import.meta.url))
const root = path.resolve(here, '..')

// ---- Configuración: variables de entorno, luego .env de la raíz, luego valores de desarrollo ----
function readDotEnv() {
  const file = path.join(root, '.env')
  if (!existsSync(file)) return {}
  return Object.fromEntries(
    readFileSync(file, 'utf-8').split(/\r?\n/)
      .map((line) => line.match(/^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*?)\s*$/))
      .filter(Boolean)
      .map(([, key, value]) => [key, value.replace(/^["']|["']$/g, '')])
  )
}
const dotEnv = readDotEnv()
const env = (key, fallback) => process.env[key] || dotEnv[key] || fallback

const GATEWAY = env('GATEWAY_URL', 'http://localhost:5000').replace(/\/$/, '')
const PROVISIONING_KEY = env('ADMIN_PROVISIONING_KEY', 'clave-admin-de-desarrollo')
const ADMIN = {
  email: env('DEMO_ADMIN_EMAIL', 'admin@demo-tienda.test'),
  password: env('DEMO_ADMIN_PASSWORD', 'Admin12345!'),
  fullName: 'Administrador Demo'
}

const catalog = JSON.parse(await readFile(path.join(here, 'seed-data', 'catalog.json'), 'utf-8'))
const reviewsData = JSON.parse(await readFile(path.join(here, 'seed-data', 'reviews.json'), 'utf-8'))
const addressesData = JSON.parse(await readFile(path.join(here, 'seed-data', 'addresses.json'), 'utf-8'))

// ---- Utilidades ----
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms))
const summary = { categorías: 0, productos: 0, imágenes: 0, stock: 0, cupones: 0, clientes: 0, reseñas: 0, direcciones: 0, avisos: 0 }

function warn(message) {
  summary.avisos++
  console.warn(`  ⚠ ${message}`)
}

/// Ejecuta `fn` sobre cada ítem con, como mucho, `limit` en paralelo.
async function mapLimit(items, limit, fn) {
  let next = 0
  const workers = Array.from({ length: Math.min(limit, items.length) }, async () => {
    while (next < items.length) {
      const index = next++
      await fn(items[index], index)
    }
  })
  await Promise.all(workers)
}

class HttpError extends Error {
  constructor(status, body, url) {
    super(`HTTP ${status} en ${url}: ${typeof body === 'string' ? body : body?.message ?? JSON.stringify(body)}`)
    this.status = status
    this.body = body
  }
}

async function http(method, url, { token, json, form, headers = {} } = {}) {
  const init = { method, headers: { ...headers } }
  if (token) init.headers.Authorization = `Bearer ${token}`
  if (json !== undefined) {
    init.headers['Content-Type'] = 'application/json'
    init.body = JSON.stringify(json)
  }
  if (form) init.body = form
  const response = await fetch(GATEWAY + url, init)
  const text = await response.text()
  let body = text
  try { body = text ? JSON.parse(text) : null } catch { /* texto plano */ }
  if (!response.ok) throw new HttpError(response.status, body, url)
  return body
}

// ---- Sesión del Admin (el access token dura 15 minutos: se renueva solo ante un 401) ----
let adminToken = null

async function adminLogin() {
  adminToken = (await http('POST', '/api/auth/login', { json: { email: ADMIN.email, password: ADMIN.password } })).accessToken
}

async function asAdmin(method, url, options = {}) {
  try {
    return await http(method, url, { ...options, token: adminToken })
  } catch (err) {
    if (err.status !== 401) throw err
    await adminLogin()
    return http(method, url, { ...options, token: adminToken })
  }
}

async function waitFor(url, label, accept = (status) => status < 500) {
  for (let attempt = 1; attempt <= 60; attempt++) {
    try {
      const response = await fetch(GATEWAY + url)
      if (accept(response.status)) return
    } catch { /* todavía no responde */ }
    if (attempt === 1) process.stdout.write(`Esperando a ${label}`)
    process.stdout.write('.')
    await sleep(2000)
  }
  throw new Error(`\n${label} no respondió en ${GATEWAY}${url}. ¿Corriste 'docker compose up -d'?`)
}

// =====================================================================================
// 1. Admin
// =====================================================================================
async function ensureAdmin() {
  try {
    await http('POST', '/api/admins', { json: ADMIN, headers: { 'X-Admin-Provisioning-Key': PROVISIONING_KEY } })
    console.log(`Admin creado: ${ADMIN.email}`)
  } catch (err) {
    // 409 = ya existe (corrida anterior; versiones viejas de Users devolvían 400). Cualquier otro
    // error es real (ej. clave de aprovisionamiento incorrecta → 401).
    if (err.status !== 409 && err.status !== 400) throw new Error(`No se pudo crear el Admin: ${err.message}. Revisa ADMIN_PROVISIONING_KEY en .env.`)
    console.log(`Admin: ${ADMIN.email} (ya existía)`)
  }
  try {
    await adminLogin()
  } catch (err) {
    throw new Error(`No se pudo iniciar sesión como ${ADMIN.email} (${err.status}). Si ya existía con otra contraseña, ` +
      `define DEMO_ADMIN_EMAIL y DEMO_ADMIN_PASSWORD.`)
  }
}

// =====================================================================================
// 2. Categorías
// =====================================================================================
async function seedCategories() {
  const existing = await http('GET', '/api/categories')
  const idByName = new Map(existing.map((c) => [c.name.toLowerCase(), c.id]))

  async function ensure(name, parentCategoryId = null) {
    const known = idByName.get(name.toLowerCase())
    if (known) return known
    const created = await asAdmin('POST', '/api/categories', { json: { name, parentCategoryId } })
    idByName.set(name.toLowerCase(), created.id)
    summary.categorías++
    return created.id
  }

  for (const group of catalog.categories) {
    const parentId = await ensure(group.name)
    for (const child of group.children) await ensure(child, parentId)
  }
  return idByName
}

// =====================================================================================
// 3. Productos, imágenes y stock
// =====================================================================================

/// Nombre que se muestra en la tienda: "Apple iPhone 15 128 GB". Los libros van sin la editorial.
function displayName(product, parentName) {
  if (parentName === 'Libros') return product.name
  return product.name.toLowerCase().startsWith(product.brand.toLowerCase()) ? product.name : `${product.brand} ${product.name}`
}

async function listAllProductNames() {
  const byName = new Map()
  for (let page = 1; ; page++) {
    // Como Admin y con includeInactive: un producto que desactivaste no se vuelve a crear.
    const result = await asAdmin('GET', `/api/products?pageSize=100&page=${page}&includeInactive=true`)
    for (const p of result.items) byName.set(p.name.toLowerCase(), p.id)
    if (page >= (result.totalPages || 1)) break
  }
  return byName
}

async function setStock(variantId, quantity) {
  // Catálogo avisa a Inventario por RabbitMQ al crear la variante: la fila de stock aparece
  // unos instantes después. Reintentamos ante 404 (consistencia eventual, no un error).
  for (let attempt = 1; attempt <= 12; attempt++) {
    try {
      await asAdmin('POST', `/api/stock/${variantId}/adjust`, { json: { newQuantityOnHand: quantity } })
      summary.stock++
      return
    } catch (err) {
      if (err.status !== 404 || attempt === 12) {
        warn(`No se pudo fijar el stock de la variante ${variantId}: ${err.message}`)
        return
      }
      await sleep(400 * attempt)
    }
  }
}

async function uploadImage(productId, fileName) {
  const bytes = await readFile(path.join(here, 'seed-data', 'images', fileName))
  const form = new FormData()
  form.append('file', new Blob([bytes], { type: 'image/jpeg' }), fileName)
  form.append('isPrimary', 'true')
  await asAdmin('POST', `/api/products/${productId}/images`, { form })
  summary.imágenes++
}

async function seedProducts(categoryIds) {
  const parentOf = new Map()
  for (const group of catalog.categories) {
    parentOf.set(group.name, group.name)
    for (const child of group.children) parentOf.set(child, group.name)
  }

  const existing = await listAllProductNames()
  const productIdByCatalogName = new Map()

  await mapLimit(catalog.products, 4, async (product) => {
    const name = displayName(product, parentOf.get(product.category))
    const knownId = existing.get(name.toLowerCase())

    if (knownId) {
      productIdByCatalogName.set(product.name, knownId)
      // Si una corrida anterior se cortó antes de subir la imagen, la completamos.
      const detail = await http('GET', `/api/products/${knownId}`)
      if (detail.images.length === 0) await uploadImage(knownId, product.image).catch((e) => warn(`${name}: ${e.message}`))
      return
    }

    let created
    try {
      created = await asAdmin('POST', '/api/products', {
        json: {
          name,
          description: product.description,
          categoryId: categoryIds.get(product.category.toLowerCase()),
          variants: product.variants.map((v) => ({ sku: v.sku, price: v.price, attributes: v.attributes }))
        }
      })
    } catch (err) {
      warn(`No se pudo crear "${name}": ${err.message}`)
      return
    }

    summary.productos++
    productIdByCatalogName.set(product.name, created.id)
    console.log(`  + ${name}`)

    await uploadImage(created.id, product.image).catch((e) => warn(`Imagen de "${name}": ${e.message}`))

    // Variantes en el mismo orden en que se enviaron; las emparejamos por SKU por las dudas.
    for (const variant of created.variants) {
      const source = product.variants.find((v) => v.sku === variant.sku)
      if (source) await setStock(variant.id, source.stock)
    }
  })

  return productIdByCatalogName
}

// =====================================================================================
// 4. Cupones / ofertas
// =====================================================================================

/// Black Friday = el viernes siguiente al cuarto jueves de noviembre. Se calcula para el año
/// en curso (o el siguiente, si ya pasó), así el cupón siempre aparece como "Programado".
function nextBlackFriday(now = new Date()) {
  for (let year = now.getUTCFullYear(); ; year++) {
    const nov1 = new Date(Date.UTC(year, 10, 1))
    const firstThursday = 1 + ((4 - nov1.getUTCDay() + 7) % 7)
    const friday = new Date(Date.UTC(year, 10, firstThursday + 21 + 1, 5, 0, 0)) // 00:00 en UTC-5
    if (friday > now) return friday
  }
}

function couponDefinitions(now = new Date()) {
  const days = (n) => new Date(now.getTime() + n * 86400000).toISOString()
  const blackFriday = nextBlackFriday(now)
  const cyberMonday = new Date(blackFriday.getTime() + 4 * 86400000 - 60000)

  return [
    { code: 'BIENVENIDA10', description: '10 % de descuento en tu primera compra (hasta $30)', type: 'Percentage', value: 10,
      maxDiscountAmount: 30, minimumSubtotal: 0, oncePerCustomer: true },
    { code: 'AHORRA5', description: '$5 de descuento en compras desde $40', type: 'FixedAmount', value: 5, minimumSubtotal: 40 },
    { code: 'OTONO20', description: 'Especial de otoño: 20 % en compras desde $80 (hasta $60)', type: 'Percentage', value: 20,
      maxDiscountAmount: 60, minimumSubtotal: 80, endsAtUtc: days(60) },
    { code: 'TECH50', description: '$50 de descuento en compras desde $500', type: 'FixedAmount', value: 50, minimumSubtotal: 500,
      usageLimit: 50 },
    { code: 'FLASH30', description: 'Oferta relámpago: 30 % (hasta $100). ¡Solo 10 cupones!', type: 'Percentage', value: 30,
      maxDiscountAmount: 100, minimumSubtotal: 0, usageLimit: 10, endsAtUtc: days(3) },
    { code: 'VIP15', description: '15 % para clientes VIP (un uso por cliente)', type: 'Percentage', value: 15, minimumSubtotal: 0,
      usageLimit: 200, oncePerCustomer: true },
    { code: 'MASCOTAS12', description: '$12 de descuento en compras desde $60', type: 'FixedAmount', value: 12, minimumSubtotal: 60 },
    { code: 'BLACKFRIDAY', description: 'Black Friday: 25 % en toda la tienda (hasta $150)', type: 'Percentage', value: 25,
      maxDiscountAmount: 150, minimumSubtotal: 0, startsAtUtc: blackFriday.toISOString(), endsAtUtc: cyberMonday.toISOString() },
    { code: 'VERANO15', description: 'Rebajas de verano: 15 % (ya terminó)', type: 'Percentage', value: 15, minimumSubtotal: 0,
      startsAtUtc: days(-90), endsAtUtc: days(-10) },
    { code: 'NAVIDAD25', description: 'Navidad: 25 % (se activa en diciembre)', type: 'Percentage', value: 25, maxDiscountAmount: 80,
      minimumSubtotal: 50, isActive: false }
  ]
}

async function seedCoupons() {
  let existing
  try {
    existing = new Set((await asAdmin('GET', '/api/coupons')).map((c) => c.code))
  } catch (err) {
    warn(`El servicio de cupones no respondió (${err.status ?? err.message}); se omiten los cupones.`)
    return
  }
  for (const coupon of couponDefinitions()) {
    if (existing.has(coupon.code)) continue
    try {
      await asAdmin('POST', '/api/coupons', { json: { isActive: true, oncePerCustomer: false, ...coupon } })
      summary.cupones++
    } catch (err) {
      warn(`Cupón ${coupon.code}: ${err.message}`)
    }
  }
}

// =====================================================================================
// 5. Clientes y reseñas
// =====================================================================================
async function customerToken(customer) {
  const credentials = { email: customer.email, password: reviewsData.password }
  try {
    const result = await http('POST', '/api/auth/register', { json: { ...credentials, fullName: customer.fullName } })
    summary.clientes++
    return result.accessToken
  } catch (err) {
    if (err.status >= 500) throw err
    return (await http('POST', '/api/auth/login', { json: credentials })).accessToken // ya existía
  }
}

async function customerTokens() {
  const tokens = new Map()
  for (const customer of reviewsData.customers) {
    try {
      tokens.set(customer.email, await customerToken(customer))
    } catch (err) {
      warn(`Cliente ${customer.email}: ${err.message}`)
    }
  }
  return tokens
}

async function seedReviews(productIds, tokens) {

  await mapLimit(reviewsData.reviews, 6, async (review) => {
    const productId = productIds.get(review.product)
    const token = tokens.get(review.email)
    if (!productId || !token) return
    try {
      await http('POST', `/api/reviews/products/${productId}`, {
        token, json: { rating: review.rating, title: review.title, comment: review.comment }
      })
      summary.reseñas++
    } catch (err) {
      if (err.status !== 409) warn(`Reseña de ${review.email} para "${review.product}": ${err.message}`) // 409 = ya existía
    }
  })
}

// =====================================================================================
// 6. Libreta de direcciones (Fase 5): así el checkout de los clientes demo ya viene completo.
// =====================================================================================
async function seedAddresses(tokens) {
  for (const { email, addresses } of addressesData) {
    const token = tokens.get(email)
    if (!token) continue
    try {
      // Idempotente: si el cliente ya tiene direcciones (de una corrida anterior o propias), no se tocan.
      const existing = await http('GET', '/api/addresses', { token })
      if (existing.length > 0) continue
      for (const [i, address] of addresses.entries()) {
        await http('POST', '/api/addresses', { token, json: { ...address, makeDefault: i === 0 } })
        summary.direcciones++
      }
    } catch (err) {
      warn(`Direcciones de ${email}: ${err.message}`)
    }
  }
}

// =====================================================================================
async function main() {
  console.log(`Sembrando datos de demostración en ${GATEWAY}\n`)
  await waitFor('/api/categories', 'el Gateway y Catálogo', (s) => s === 200)
  await waitFor('/api/coupons/validate?code=X&subtotal=1', 'el servicio de cupones', (s) => s === 401)
  console.log('')

  await ensureAdmin()

  console.log('Categorías...')
  const categoryIds = await seedCategories()

  console.log('Productos (con imagen y stock)...')
  const productIds = await seedProducts(categoryIds)

  console.log('Cupones...')
  await seedCoupons()

  console.log('Clientes y reseñas...')
  const tokens = await customerTokens()
  await seedReviews(productIds, tokens)

  console.log('Direcciones de los clientes...')
  await seedAddresses(tokens)

  console.log('\nListo. Creados en esta corrida:')
  for (const [key, value] of Object.entries(summary)) console.log(`  ${key.padEnd(11)} ${value}`)
  console.log(`
Accesos de demostración:
  Panel de Admin (http://localhost:8081):  ${ADMIN.email} / ${ADMIN.password}
  Tienda (http://localhost:5173):          ${reviewsData.customers[0].email} / ${reviewsData.password}
                                           (hay 10 clientes, todos con la misma contraseña)
Cupones para probar en el checkout: BIENVENIDA10, AHORRA5, OTONO20, TECH50, FLASH30, VIP15, MASCOTAS12`)
  if (summary.avisos > 0) process.exitCode = 1
}

main().catch((err) => {
  console.error(`\n✖ ${err.message}`)
  process.exit(1)
})
