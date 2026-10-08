// Ayudas de "Mis puntos" (Fase 6).

const shortId = (id) => (id ?? '').slice(0, 8)

/** ¿El movimiento suma al saldo? (ganados por una compra, o devueltos por una devolución). */
export function entryAdds(entry) {
  return entry.kind === 'Earned' || entry.kind === 'Restored'
}

/** Texto de un movimiento del historial. */
export function entryText(entry) {
  const order = `#${shortId(entry.orderId)}`
  if (entry.kind === 'Earned') return `Ganaste por el pedido ${order}`
  if (entry.kind === 'Reversed') return `Descontados por la devolución del pedido ${order}`
  if (entry.kind === 'Restored') return `Te devolvimos los usados en el pedido ${order}`
  const base = `Usados en el pedido ${order}`
  return entry.status === 'Reserved' ? `${base} (esperando el pago)` : base
}

/** "+120" o "−300". */
export function entryPoints(entry) {
  return entryAdds(entry) ? `+${entry.points}` : `−${entry.points}`
}

/** Clave única de un movimiento (una orden puede tener varios ajustes por devoluciones). */
export function entryKey(entry) {
  return `${entry.orderId}-${entry.kind}-${entry.referenceId ?? ''}`
}

/** Puntos que da una compra pagada por `amount` (lo mismo que calcula Lealtad). */
export function pointsFor(amount, pointsPerDollar = 1) {
  return amount > 0 ? Math.floor(amount * pointsPerDollar) : 0
}

/** Cuánto falta para poder usar puntos, o 0 si ya se puede. */
export function pointsToMinimum(balance, minRedeemPoints) {
  return Math.max(minRedeemPoints - balance, 0)
}
