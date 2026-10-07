// Ayudas de "Mis puntos" (Fase 6).

const shortId = (id) => (id ?? '').slice(0, 8)

/** Texto de un movimiento del historial. */
export function entryText(entry) {
  if (entry.kind === 'Earned') return `Ganaste por el pedido #${shortId(entry.orderId)}`
  const base = `Usados en el pedido #${shortId(entry.orderId)}`
  return entry.status === 'Reserved' ? `${base} (esperando el pago)` : base
}

/** "+120" o "−300". */
export function entryPoints(entry) {
  return entry.kind === 'Earned' ? `+${entry.points}` : `−${entry.points}`
}

/** Puntos que da una compra pagada por `amount` (lo mismo que calcula Lealtad). */
export function pointsFor(amount, pointsPerDollar = 1) {
  return amount > 0 ? Math.floor(amount * pointsPerDollar) : 0
}

/** Cuánto falta para poder usar puntos, o 0 si ya se puede. */
export function pointsToMinimum(balance, minRedeemPoints) {
  return Math.max(minRedeemPoints - balance, 0)
}
