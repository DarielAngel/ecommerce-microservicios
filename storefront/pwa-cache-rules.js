// Qué respuestas del Gateway guarda el service worker (PWA). Vive aparte para poder probarlo: un error acá
// podría guardar en el teléfono datos del cliente (carrito, pedidos...), y eso nunca debe pasar.

const escapeRegExp = (text) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')

/**
 * Catálogo público: productos (lista, detalle, sugerencias, relacionados), categorías y reseñas públicas.
 * Las reseñas propias (/mine) y todo lo demás (carrito, pedidos, puntos, direcciones, favoritos, cupones, pagos,
 * stock) quedan afuera: siempre van a la red.
 */
export function catalogPattern(apiBaseUrl) {
  const origin = escapeRegExp(new URL(apiBaseUrl).origin)
  return new RegExp(
    `^${origin}/api/(?:(?:products|categories)(?:[/?]|$)|reviews/(?:summaries|products/[^/?]+(?:/summary)?)(?:\\?|$))`)
}

/** Fotos de productos (/images/{guid}.jpg): cada subida tiene un nombre nuevo, así que nunca cambian. */
export function imagesPattern(apiBaseUrl) {
  return new RegExp(`^${escapeRegExp(new URL(apiBaseUrl).origin)}/images/`)
}
