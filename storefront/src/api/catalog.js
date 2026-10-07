import { api } from './client'

// Descubrimiento (Fase 4). Todo es un "extra" de la página: si falla, devolvemos vacío en vez de
// romper la pantalla — igual que las estrellas de reseñas.

export async function fetchSuggestions(term, limit = 6) {
  const q = term.trim()
  if (q.length < 2) return []
  try {
    return await api.get('/api/products/suggestions', { params: { q, limit } })
  } catch {
    return []
  }
}

export async function fetchRelated(productId, limit = 8) {
  try {
    return await api.get(`/api/products/${productId}/related`, { params: { limit } })
  } catch {
    return []
  }
}

/// Disponibilidad pública por variante, indexada por id: { status: 'InStock'|'LowStock'|'OutOfStock', quantityLeft }.
export async function fetchAvailability(variantIds) {
  if (!variantIds.length) return {}
  try {
    const list = await api.get('/api/stock/availability', { params: { variantIds } })
    return Object.fromEntries(list.map((a) => [a.variantId, a]))
  } catch {
    return {}
  }
}
