import { describe, it, expect, vi } from 'vitest'
import { parseDay, deliveryText, timelineSteps, canBuyAgain, buyAgain, buyAgainMessage, couponReleased } from './orders'

const apiError = (status) => Object.assign(new Error(`Error ${status}`), { status })
const line = (variantId, productName, quantity) => ({ variantId, productName, quantity })

describe('utils/orders — entrega estimada', () => {
  it('parseDay no se corre de día por la zona horaria', () => {
    const d = parseDay('2026-10-12')
    expect([d.getFullYear(), d.getMonth(), d.getDate()]).toEqual([2026, 9, 12])
    expect(parseDay('no es fecha')).toBeNull()
  })

  it('arma "Llega entre…" o "Llega el…", y nada sin estimación', () => {
    expect(deliveryText({ estimatedDeliveryFrom: '2026-10-12', estimatedDeliveryTo: '2026-10-15' }))
      .toMatch(/^Llega entre el .*12.* y el .*15.*/)
    expect(deliveryText({ estimatedDeliveryFrom: '2026-10-12', estimatedDeliveryTo: '2026-10-12' })).toMatch(/^Llega el .*12/)
    expect(deliveryText({ estimatedDeliveryFrom: null })).toBe('')
  })
})

describe('utils/orders — línea de tiempo', () => {
  const base = { createdAtUtc: '2026-10-07T15:00:00Z', estimatedDeliveryFrom: '2026-10-12', estimatedDeliveryTo: '2026-10-15' }
  const states = (steps) => steps.map((s) => `${s.key}:${s.state}`)

  it('pagado: lo próximo es el envío, y la entrega lleva la estimación', () => {
    const steps = timelineSteps({ ...base, status: 'Paid', paidAtUtc: '2026-10-07T15:05:00Z' })
    expect(states(steps)).toEqual(['placed:done', 'paid:done', 'shipped:current', 'delivered:pending'])
    expect(steps.at(-1).estimate).toMatch(/^Llega entre/)
  })

  it('enviado: lo próximo es la entrega', () => {
    const steps = timelineSteps({ ...base, status: 'Shipped', paidAtUtc: '2026-10-07T15:05:00Z', shippedAtUtc: '2026-10-08T10:00:00Z' })
    expect(states(steps)).toEqual(['placed:done', 'paid:done', 'shipped:done', 'delivered:current'])
    expect(steps[2].date).toBe('2026-10-08T10:00:00Z')
  })

  it('pago pendiente', () => {
    expect(states(timelineSteps({ ...base, status: 'PendingPayment' })))
      .toEqual(['placed:done', 'paid:current', 'shipped:pending', 'delivered:pending'])
  })

  it('fallido o cancelado: corta la línea con el motivo', () => {
    expect(states(timelineSteps({ ...base, status: 'Failed' }))).toEqual(['placed:done', 'failed:failed'])
    expect(timelineSteps({ ...base, status: 'Cancelled' })[1].label).toBe('Pedido cancelado')
  })

  it('un pedido devuelto completo termina en "Devuelto y reembolsado"', () => {
    const steps = timelineSteps({ ...base, status: 'Refunded', paidAtUtc: '2026-10-07T15:05:00Z', shippedAtUtc: '2026-10-08T10:00:00Z',
      returns: [{ refundedAtUtc: '2026-10-12T09:00:00Z' }, { refundedAtUtc: '2026-10-10T09:00:00Z' }] })
    expect(states(steps)).toEqual(['placed:done', 'paid:done', 'shipped:done', 'delivered:done', 'refunded:done'])
    expect(steps.at(-1).date).toBe('2026-10-12T09:00:00Z')
  })
})

describe('utils/orders — comprar de nuevo', () => {
  it('se ofrece en pedidos cerrados con líneas, no en uno esperando el pago', () => {
    expect(canBuyAgain({ status: 'Shipped', lines: [line('v1', 'Taza', 1)] })).toBe(true)
    expect(canBuyAgain({ status: 'Failed', lines: [line('v1', 'Taza', 1)] })).toBe(true)
    expect(canBuyAgain({ status: 'PendingPayment', lines: [line('v1', 'Taza', 1)] })).toBe(false)
  })

  it('agrega cada línea con su cantidad', async () => {
    const addItem = vi.fn(async (variantId, qty) => ({ totalItemCount: qty }))
    const result = await buyAgain([line('v1', 'Taza', 2), line('v2', 'Plato', 1)], addItem)

    expect(addItem.mock.calls).toEqual([['v1', 2], ['v2', 1]])
    expect(result.added).toHaveLength(2)
    expect(result.cart).toEqual({ totalItemCount: 1 })
  })

  it('sin stock para la cantidad original reintenta con 1; si ya no existe, lo informa', async () => {
    const addItem = vi.fn(async (variantId, qty) => {
      if (variantId === 'v1' && qty > 1) throw apiError(409)
      if (variantId === 'v2') throw apiError(404)
      if (variantId === 'v3') throw apiError(409)
      return { ok: true }
    })
    const result = await buyAgain([line('v1', 'Taza', 3), line('v2', 'Plato', 1), line('v3', 'Vaso', 2)], addItem)

    expect(result.partial.map((l) => l.variantId)).toEqual(['v1'])
    expect(result.unavailable.map((l) => l.variantId)).toEqual(['v2', 'v3'])
    expect(buyAgainMessage(result)).toBe(
      'Agregamos 1 producto al carrito. De Taza agregamos 1 unidad: no hay stock para la cantidad original. Ya no están disponibles: Plato, Vaso.')
  })

  it('un error inesperado (sin conexión, sesión vencida) se propaga', async () => {
    const addItem = vi.fn(async () => { throw apiError(500) })
    await expect(buyAgain([line('v1', 'Taza', 1)], addItem)).rejects.toThrow('Error 500')
  })
})

describe('utils/orders — cupón liberado', () => {
  it('solo cuando el pedido con cupón se canceló o se reembolsó entero', () => {
    expect(couponReleased({ couponCode: 'VERANO25', status: 'Cancelled' })).toBe(true)
    expect(couponReleased({ couponCode: 'VERANO25', status: 'Refunded' })).toBe(true)
    expect(couponReleased({ couponCode: 'VERANO25', status: 'Shipped', refundedAmount: 10 })).toBe(false)
    expect(couponReleased({ couponCode: null, status: 'Cancelled' })).toBe(false)
    expect(couponReleased(null)).toBe(false)
  })
})
