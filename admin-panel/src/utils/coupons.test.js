import { describe, it, expect } from 'vitest'
import { emptyCouponForm, toPayload, fromCoupon, describeDiscount, couponStatus, usageLabel } from './coupons'

const baseCoupon = {
  id: 'c1', code: 'VERANO10', description: 'Verano', type: 'Percentage', value: 10, maxDiscountAmount: null,
  minimumSubtotal: 0, startsAtUtc: null, endsAtUtc: null, usageLimit: null, oncePerCustomer: false,
  isActive: true, timesUsed: 0, activeReservations: 0
}

describe('toPayload', () => {
  it('normaliza el código, convierte números y deja null lo vacío', () => {
    const payload = toPayload({ ...emptyCouponForm(), code: ' verano10 ', description: ' Verano ', value: '10', usageLimit: '' })

    expect(payload).toMatchObject({
      code: 'VERANO10', description: 'Verano', type: 'Percentage', value: 10,
      maxDiscountAmount: null, minimumSubtotal: 0, usageLimit: null, startsAtUtc: null, endsAtUtc: null
    })
  })

  it('descarta el tope en un cupón de monto fijo (la API lo rechazaría)', () => {
    const payload = toPayload({ ...emptyCouponForm(), code: 'FIJO', description: 'x', type: 'FixedAmount', value: '5', maxDiscountAmount: '3' })
    expect(payload.maxDiscountAmount).toBeNull()
  })

  it('convierte las fechas locales del formulario a ISO UTC y de vuelta sin perder la hora', () => {
    const form = { ...emptyCouponForm(), code: 'BF', description: 'x', value: '20', startsAt: '2026-11-27T00:00', endsAt: '2026-11-30T23:59' }

    const payload = toPayload(form)
    expect(payload.startsAtUtc).toMatch(/Z$/)

    const roundTrip = fromCoupon({ ...baseCoupon, startsAtUtc: payload.startsAtUtc, endsAtUtc: payload.endsAtUtc })
    expect(roundTrip.startsAt).toBe('2026-11-27T00:00')
    expect(roundTrip.endsAt).toBe('2026-11-30T23:59')
  })
})

describe('describeDiscount', () => {
  it('describe porcentajes, con y sin tope, y montos fijos', () => {
    expect(describeDiscount(baseCoupon)).toBe('10 %')
    expect(describeDiscount({ ...baseCoupon, value: 20, maxDiscountAmount: 50 })).toBe('20 % (máx. $50.00)')
    expect(describeDiscount({ ...baseCoupon, type: 'FixedAmount', value: 15 })).toBe('$15.00')
  })
})

describe('couponStatus', () => {
  const now = new Date('2026-10-15T12:00:00Z')

  it('activo por defecto', () => {
    expect(couponStatus(baseCoupon, now).key).toBe('active')
  })

  it('pausado gana sobre todo lo demás', () => {
    expect(couponStatus({ ...baseCoupon, isActive: false, endsAtUtc: '2020-01-01T00:00:00Z' }, now).key).toBe('inactive')
  })

  it('vencido, programado y agotado (contando reservas en curso)', () => {
    expect(couponStatus({ ...baseCoupon, endsAtUtc: '2026-10-01T00:00:00Z' }, now).key).toBe('expired')
    expect(couponStatus({ ...baseCoupon, startsAtUtc: '2026-11-01T00:00:00Z' }, now).key).toBe('scheduled')
    expect(couponStatus({ ...baseCoupon, usageLimit: 5, timesUsed: 4, activeReservations: 1 }, now).key).toBe('exhausted')
    expect(couponStatus({ ...baseCoupon, usageLimit: 5, timesUsed: 4 }, now).key).toBe('active')
  })
})

describe('usageLabel', () => {
  it('muestra usados / límite, o solo usados si no hay límite', () => {
    expect(usageLabel({ ...baseCoupon, timesUsed: 3, usageLimit: 100 })).toBe('3 / 100')
    expect(usageLabel({ ...baseCoupon, timesUsed: 3 })).toBe('3')
  })
})
