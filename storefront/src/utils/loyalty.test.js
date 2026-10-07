import { describe, it, expect } from 'vitest'
import { entryText, entryPoints, pointsFor, pointsToMinimum } from './loyalty'

describe('utils/loyalty', () => {
  it('describe cada movimiento', () => {
    expect(entryText({ kind: 'Earned', orderId: 'abcdef12-xxxx' })).toBe('Ganaste por el pedido #abcdef12')
    expect(entryText({ kind: 'Redeemed', status: 'Confirmed', orderId: '12345678-x' })).toBe('Usados en el pedido #12345678')
    expect(entryText({ kind: 'Redeemed', status: 'Reserved', orderId: '12345678-x' })).toMatch(/esperando el pago/)
    expect(entryPoints({ kind: 'Earned', points: 120 })).toBe('+120')
    expect(entryPoints({ kind: 'Redeemed', points: 300 })).toBe('−300')
  })

  it('calcula los puntos de una compra y cuánto falta para el mínimo', () => {
    expect(pointsFor(249.99)).toBe(249)
    expect(pointsFor(0)).toBe(0)
    expect(pointsToMinimum(40, 100)).toBe(60)
    expect(pointsToMinimum(250, 100)).toBe(0)
  })
})
