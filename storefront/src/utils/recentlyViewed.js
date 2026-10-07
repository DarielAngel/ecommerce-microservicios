// "Vistos recientemente" (T4.3): vive solo en este navegador, sin backend. Guardamos lo justo para
// pintar una tarjeta (no precios "en vivo": al entrar al producto se ven los actuales).
const KEY = 'storefront-recently-viewed'
export const MAX_RECENT = 12

export function loadRecentlyViewed() {
  try {
    const list = JSON.parse(localStorage.getItem(KEY) || '[]')
    return Array.isArray(list) ? list : []
  } catch {
    return [] // navegador sin almacenamiento o dato corrupto: simplemente no mostramos la sección
  }
}

function save(list) {
  try {
    localStorage.setItem(KEY, JSON.stringify(list))
  } catch {
    // modo privado o cuota llena: no es crítico
  }
}

/// Registra que se vio un producto: lo pone primero, sin duplicados, y conserva como máximo MAX_RECENT.
export function addRecentlyViewed(product) {
  if (!product?.id) return loadRecentlyViewed()
  const entry = {
    id: product.id,
    name: product.name,
    categoryName: product.categoryName ?? '',
    minPrice: product.minPrice ?? null,
    primaryImageFileName: product.primaryImageFileName ?? null
  }
  const list = [entry, ...loadRecentlyViewed().filter((p) => p.id !== product.id)].slice(0, MAX_RECENT)
  save(list)
  return list
}

export function clearRecentlyViewed() {
  try {
    localStorage.removeItem(KEY)
  } catch {
    // nada que hacer
  }
}

/// Convierte el detalle de un producto (variantes, imágenes) al formato de tarjeta.
export function summaryFromDetail(product, categoryName = '') {
  const active = (product.variants ?? []).filter((v) => v.isActive !== false)
  const primary = (product.images ?? []).find((i) => i.isPrimary) ?? product.images?.[0]
  return {
    id: product.id,
    name: product.name,
    categoryName,
    minPrice: active.length ? Math.min(...active.map((v) => v.price)) : null,
    primaryImageFileName: primary?.fileName ?? null
  }
}
