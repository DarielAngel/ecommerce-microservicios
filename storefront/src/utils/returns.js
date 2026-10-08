// Ayudas de las devoluciones (Fase 7).

/** Motivos que acepta Órdenes, con el texto para el cliente. "Other" pide comentario. */
export const RETURN_REASONS = [
  { value: 'DoesNotFit', label: 'No me quedó bien (talla o medida)' },
  { value: 'Damaged', label: 'Llegó dañado o con fallas' },
  { value: 'WrongItem', label: 'No es lo que pedí' },
  { value: 'NotAsDescribed', label: 'No es como se describía' },
  { value: 'ChangedMind', label: 'Ya no lo quiero' },
  { value: 'Other', label: 'Otro motivo' }
]

export function reasonLabel(value) {
  return RETURN_REASONS.find((r) => r.value === value)?.label ?? value
}

const money = (value) => `$${Number(value ?? 0).toFixed(2)}`

/** Estado de una devolución en palabras del cliente: { label, tone } (tone: info | success | danger). */
export function returnStatus(ret) {
  if (ret.isCancellation) {
    switch (ret.status) {
      case 'Requested':
        return { label: 'Cancelación solicitada — la estamos revisando', tone: 'info' }
      case 'Approved':
        return { label: `Cancelación aprobada — reembolso de ${money(ret.refundAmount)} en proceso`, tone: 'info' }
      case 'Refunded':
        return { label: `Pedido cancelado y reembolsado: ${money(ret.refundAmount)}`, tone: 'success' }
      case 'Rejected':
        return { label: 'Cancelación rechazada', tone: 'danger' }
    }
  }
  switch (ret.status) {
    case 'Requested':
      return { label: 'Solicitada — la estamos revisando', tone: 'info' }
    case 'Approved':
      return { label: `Aprobada — reembolso de ${money(ret.refundAmount)} en proceso`, tone: 'info' }
    case 'Refunded':
      return { label: `Reembolsada: ${money(ret.refundAmount)}`, tone: 'success' }
    case 'Rejected':
      return { label: 'Rechazada', tone: 'danger' }
    default:
      return { label: ret.status, tone: 'info' }
  }
}

/** Líneas que todavía se pueden devolver, con su máximo. */
export function returnableLines(order) {
  return (order.lines ?? []).filter((l) => (l.returnableQuantity ?? 0) > 0)
}

/**
 * Arma lo que se envía a Órdenes desde el formulario, o devuelve el error para el cliente.
 * quantities: { [variantId]: número elegido }.
 */
export function buildReturnRequest(order, quantities, reason, comment) {
  const items = returnableLines(order)
    .map((l) => ({ variantId: l.variantId, quantity: Math.min(Math.max(Math.trunc(Number(quantities[l.variantId]) || 0), 0), l.returnableQuantity) }))
    .filter((i) => i.quantity > 0)
  if (items.length === 0) return { error: 'Elige al menos un producto para devolver.' }
  if (!reason) return { error: 'Elige el motivo de la devolución.' }
  const text = (comment ?? '').trim()
  if (reason === 'Other' && !text) return { error: 'Cuéntanos el motivo de la devolución.' }
  return { request: { items, reason, comment: text || null } }
}

/** "Puedes devolverlo hasta el lun, 9 nov" (o '' si no hay plazo). */
export function returnDeadlineText(order, format) {
  return order.returnDeadlineUtc ? `Puedes pedir la devolución hasta el ${format(order.returnDeadlineUtc)}` : ''
}
