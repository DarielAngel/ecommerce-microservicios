/// Texto y estilo de la insignia de disponibilidad (T4.4). Siempre viene de stock real; si no hay
/// dato (Inventario no respondió) no se muestra nada en vez de adivinar.
export function availabilityBadge(availability) {
  if (!availability) return null
  if (availability.status === 'OutOfStock') return { text: 'Agotado', tone: 'out' }
  if (availability.status === 'LowStock') {
    const n = availability.quantityLeft
    return { text: n === 1 ? '¡Queda solo 1!' : `¡Quedan solo ${n}!`, tone: 'low' }
  }
  return null // con stock de sobra no hace falta insignia
}
