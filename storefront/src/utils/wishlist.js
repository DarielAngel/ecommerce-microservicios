/// Decide qué variante agregar al carrito con un solo clic desde "Mis favoritos".
/// Solo si hay exactamente UNA variante activa: si hay varias (talla, color...), elegir por el
/// cliente sería adivinar, así que devolvemos null y la página lo manda al detalle a elegir.
export function pickQuickAddVariant(product) {
  if (!product || product.isActive === false) return null
  const active = (product.variants ?? []).filter((v) => v.isActive !== false)
  return active.length === 1 ? active[0] : null
}

/// Precio a mostrar: el de la única variante, o "desde" el menor si hay varias.
export function priceLabelFor(product) {
  const prices = (product?.variants ?? []).filter((v) => v.isActive !== false).map((v) => v.price)
  if (prices.length === 0) return null
  const min = Math.min(...prices)
  return { amount: min, from: new Set(prices).size > 1 }
}

export function primaryImageOf(product) {
  const images = product?.images ?? []
  return images.find((img) => img.isPrimary) ?? images[0] ?? null
}
