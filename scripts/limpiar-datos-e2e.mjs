#!/usr/bin/env node
/**
 * Borra de tu Docker local los datos que dejan las pruebas end-to-end (Playwright):
 * "Categoría E2E …", "Categoría Playwright …", "Producto E2E …", "Escaso E2E …" y los
 * cupones "E2E…", junto con su stock, reseñas, favoritos y líneas de carrito.
 *
 * No toca nada más: ni tus usuarios, ni tus pedidos, ni los datos de demostración.
 * Habla directo con los Postgres de docker-compose (docker exec), así que el stack
 * tiene que estar levantado.
 *
 *   node scripts/limpiar-datos-e2e.mjs            → muestra qué va a borrar y lo borra
 *   node scripts/limpiar-datos-e2e.mjs --simular  → solo muestra qué borraría
 */
import { execFileSync } from 'node:child_process'

const simulate = process.argv.includes('--simular')

const DBS = {
  catalog: { container: 'ecommerce-postgres-catalog', user: 'catalog_svc', db: 'catalog_db' },
  inventory: { container: 'ecommerce-postgres-inventory', user: 'inventory_svc', db: 'inventory_db' },
  cart: { container: 'ecommerce-postgres-cart', user: 'cart_svc', db: 'cart_db' },
  reviews: { container: 'ecommerce-postgres-reviews', user: 'reviews_svc', db: 'reviews_db' },
  wishlist: { container: 'ecommerce-postgres-wishlist', user: 'wishlist_svc', db: 'wishlist_db' },
  promotions: { container: 'ecommerce-postgres-promotions', user: 'promotions_svc', db: 'promotions_db' }
}

/** Corre SQL en el Postgres de un servicio y devuelve las filas (columnas separadas por "|"). */
function sql(service, statement) {
  const { container, user, db } = DBS[service]
  const out = execFileSync(
    'docker',
    ['exec', '-i', container, 'psql', '-U', user, '-d', db, '-v', 'ON_ERROR_STOP=1', '-At', '-c', statement],
    { encoding: 'utf-8', stdio: ['ignore', 'pipe', 'pipe'] }
  )
  return out.split('\n').map((l) => l.trim()).filter(Boolean)
}

/** ¿Existe la tabla? (los servicios nuevos pueden no haberla creado todavía). */
function hasTable(service, table) {
  return sql(service, `SELECT to_regclass('public.${table}') IS NOT NULL`)[0] === 't'
}

const uuidList = (ids) => ids.map((id) => `'${id}'`).join(',')

const E2E_CATEGORY = `(name LIKE 'Categoría E2E %' OR name LIKE 'Categoría Playwright %')`
const E2E_PRODUCT = `(name LIKE 'Producto E2E %' OR name LIKE 'Escaso E2E %')`

function main() {
  try {
    sql('catalog', 'SELECT 1')
  } catch (err) {
    console.error('No pude conectarme al Postgres de Catálogo. ¿Está levantado el stack? (docker compose ps)')
    console.error(String(err.stderr || err.message).trim())
    process.exit(1)
  }

  // 1) Qué hay que borrar en Catálogo.
  const categoryIds = sql('catalog', `SELECT "Id" FROM categories WHERE ${E2E_CATEGORY}`)
  const productRows = sql(
    'catalog',
    `SELECT "Id", name FROM products WHERE ${E2E_PRODUCT}` +
      (categoryIds.length ? ` OR category_id IN (${uuidList(categoryIds)})` : '')
  )
  const productIds = productRows.map((r) => r.split('|')[0])
  const variantIds = productIds.length
    ? sql('catalog', `SELECT "Id" FROM product_variants WHERE "ProductId" IN (${uuidList(productIds)})`)
    : []
  const couponCodes = hasTable('promotions', 'coupons')
    ? sql('promotions', `SELECT code FROM coupons WHERE code LIKE 'E2E%'`)
    : []

  console.log(`Datos de pruebas E2E encontrados:
  • ${categoryIds.length} categorías
  • ${productIds.length} productos (${variantIds.length} variantes)
  • ${couponCodes.length} cupones`)

  if (!categoryIds.length && !productIds.length && !couponCodes.length) {
    console.log('\nNo hay nada que limpiar. ✔')
    return
  }
  if (simulate) {
    console.log('\n(--simular: no se borró nada)')
    return
  }

  // 2) Lo que depende de esos productos en otros servicios.
  if (productIds.length) {
    const products = uuidList(productIds)
    if (variantIds.length && hasTable('inventory', 'stock_items')) {
      sql('inventory', `DELETE FROM stock_items WHERE variant_id IN (${uuidList(variantIds)})`)
    }
    if (hasTable('cart', 'cart_items')) sql('cart', `DELETE FROM cart_items WHERE product_id IN (${products})`)
    if (hasTable('wishlist', 'wishlist_items')) sql('wishlist', `DELETE FROM wishlist_items WHERE product_id IN (${products})`)
    if (hasTable('reviews', 'reviews')) sql('reviews', `DELETE FROM reviews WHERE product_id IN (${products})`)
    if (hasTable('reviews', 'verified_purchases')) sql('reviews', `DELETE FROM verified_purchases WHERE product_id IN (${products})`)

    // 3) Los productos (con sus imágenes y variantes) en una sola transacción.
    sql(
      'catalog',
      `BEGIN;
       DELETE FROM product_images WHERE "ProductId" IN (${products});
       DELETE FROM product_variants WHERE "ProductId" IN (${products});
       DELETE FROM products WHERE "Id" IN (${products});
       COMMIT;`
    )
  }

  // 4) Las categorías E2E que quedaron vacías (sin productos ni subcategorías).
  if (categoryIds.length) {
    sql(
      'catalog',
      `DELETE FROM categories c WHERE c."Id" IN (${uuidList(categoryIds)})
         AND NOT EXISTS (SELECT 1 FROM products p WHERE p.category_id = c."Id")
         AND NOT EXISTS (SELECT 1 FROM categories h WHERE h.parent_category_id = c."Id")`
    )
  }

  // 5) Cupones de prueba (y sus reservas/usos).
  if (couponCodes.length) {
    const coupons = `SELECT "Id" FROM coupons WHERE code LIKE 'E2E%'`
    sql(
      'promotions',
      `BEGIN;
       ${hasTable('promotions', 'coupon_redemptions') ? `DELETE FROM coupon_redemptions WHERE coupon_id IN (${coupons});` : ''}
       DELETE FROM coupons WHERE code LIKE 'E2E%';
       COMMIT;`
    )
  }

  const left = sql('catalog', `SELECT count(*) FROM categories WHERE ${E2E_CATEGORY}`)[0]
  console.log(`\nListo: datos de pruebas E2E borrados.${left !== '0' ? ` (Quedaron ${left} categorías E2E con contenido ajeno: no se tocaron.)` : ' ✔'}`)
  console.log('Recarga la tienda (F5) para verlo.')
}

main()
