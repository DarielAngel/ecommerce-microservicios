import { api } from './client'

/// Resúmenes de calificación de varios productos en UNA sola llamada, indexados por id de producto.
/// Las estrellas son un extra visual: si el servicio de reseñas falla, devolvemos {} en vez de
/// propagar el error, para que nunca rompa el catálogo.
export async function fetchRatingSummaries(productIds) {
  if (!productIds.length) return {}

  try {
    const list = await api.get('/api/reviews/summaries', { params: { productIds } })
    return Object.fromEntries(list.map((summary) => [summary.productId, summary]))
  } catch {
    return {}
  }
}
