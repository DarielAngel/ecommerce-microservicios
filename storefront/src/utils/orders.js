// Ayudas de "Mis pedidos" (Fase 5): línea de tiempo, entrega estimada y "Comprar de nuevo".

/** "2026-10-12" (un día, sin hora) → Date local, sin correrse de día por la zona horaria. */
export function parseDay(value) {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value ?? '')
  return match ? new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3])) : null
}

const dayFormat = new Intl.DateTimeFormat('es', { weekday: 'short', day: 'numeric', month: 'short' })
const dateTimeFormat = new Intl.DateTimeFormat('es', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })

/** "lun, 12 oct" */
export function formatDay(date) {
  return date ? dayFormat.format(date).replace(/\./g, '') : ''
}

/** "7 oct, 15:30" (en la hora local del cliente). */
export function formatMoment(value) {
  if (!value) return ''
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '' : dateTimeFormat.format(date).replace(/\./g, '')
}

/**
 * Texto de la entrega estimada, o '' si el pedido no tiene (todavía no se pagó, o falló).
 * "Llega entre el lun, 12 oct y el jue, 15 oct" · "Llega el lun, 12 oct"
 */
export function deliveryText(order) {
  const from = parseDay(order?.estimatedDeliveryFrom)
  const to = parseDay(order?.estimatedDeliveryTo)
  if (!from) return ''
  if (!to || to.getTime() === from.getTime()) return `Llega el ${formatDay(from)}`
  return `Llega entre el ${formatDay(from)} y el ${formatDay(to)}`
}

/**
 * Pasos de la línea de tiempo. Cada uno: { key, label, date, state } con state
 * 'done' (ya pasó), 'current' (el siguiente que esperamos), 'pending' o 'failed'.
 */
export function timelineSteps(order) {
  const steps = [{ key: 'placed', label: 'Pedido realizado', date: order.createdAtUtc, state: 'done' }]

  if (order.status === 'Failed' || order.status === 'Cancelled') {
    steps.push({
      key: 'failed',
      label: order.status === 'Failed' ? 'El pago no se completó' : 'Pedido cancelado',
      date: null,
      state: 'failed'
    })
    return steps
  }

  const paid = order.status === 'Paid' || order.status === 'Shipped'
  const shipped = order.status === 'Shipped'
  steps.push({ key: 'paid', label: paid ? 'Pago confirmado' : 'Esperando el pago', date: order.paidAtUtc ?? null, state: paid ? 'done' : 'current' })
  steps.push({ key: 'shipped', label: 'Enviado', date: order.shippedAtUtc ?? null, state: shipped ? 'done' : paid ? 'current' : 'pending' })
  steps.push({ key: 'delivered', label: 'Entregado', date: null, state: shipped ? 'current' : 'pending', estimate: deliveryText(order) })
  return steps
}

/** ¿Tiene sentido ofrecer "Comprar de nuevo"? (un pedido a medio pagar todavía puede completarse). */
export function canBuyAgain(order) {
  return order.status !== 'PendingPayment' && (order.lines?.length ?? 0) > 0
}

/**
 * "Comprar de nuevo": agrega al carrito cada línea del pedido, con la misma cantidad.
 * `addItem(variantId, quantity)` llama al carrito y lanza el error de la API si no puede.
 * - 409 (no alcanza el stock): reintenta con 1 unidad → queda en `partial`.
 * - 404 (el producto ya no está a la venta) o sin stock ni para 1 → `unavailable`.
 * Otros errores (sin conexión, sesión vencida…) se propagan: no tiene sentido seguir.
 * Devuelve { added, partial, unavailable, cart } (cart = la última respuesta del carrito).
 */
export async function buyAgain(lines, addItem) {
  const result = { added: [], partial: [], unavailable: [], cart: null }
  for (const line of lines) {
    try {
      result.cart = await addItem(line.variantId, line.quantity)
      result.added.push(line)
    } catch (err) {
      if (err?.status === 409 && line.quantity > 1) {
        try {
          result.cart = await addItem(line.variantId, 1)
          result.partial.push(line)
          continue
        } catch (retryErr) {
          if (retryErr?.status !== 409 && retryErr?.status !== 404) throw retryErr
        }
      } else if (err?.status !== 409 && err?.status !== 404) {
        throw err
      }
      result.unavailable.push(line)
    }
  }
  return result
}

/** Resumen para el aviso que se muestra después de "Comprar de nuevo". */
export function buyAgainMessage({ added, partial, unavailable }) {
  const names = (lines) => lines.map((l) => l.productName).join(', ')
  const parts = []
  const ok = added.length + partial.length
  if (ok > 0) parts.push(ok === 1 ? 'Agregamos 1 producto al carrito.' : `Agregamos ${ok} productos al carrito.`)
  if (partial.length) parts.push(`De ${names(partial)} agregamos 1 unidad: no hay stock para la cantidad original.`)
  if (unavailable.length) parts.push(`${unavailable.length === 1 ? 'Ya no está disponible' : 'Ya no están disponibles'}: ${names(unavailable)}.`)
  return parts.join(' ')
}
