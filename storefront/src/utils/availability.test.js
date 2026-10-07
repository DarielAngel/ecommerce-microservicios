import { describe, it, expect } from 'vitest'
import { availabilityBadge } from './availability'

describe('availabilityBadge', () => {
  it('sin dato de stock no muestra nada (nunca adivina)', () => {
    expect(availabilityBadge(undefined)).toBeNull()
  })

  it('con stock de sobra no muestra insignia', () => {
    expect(availabilityBadge({ status: 'InStock', quantityLeft: null })).toBeNull()
  })

  it('con pocas unidades dice cuántas quedan, en singular y plural', () => {
    expect(availabilityBadge({ status: 'LowStock', quantityLeft: 1 })).toEqual({ text: '¡Queda solo 1!', tone: 'low' })
    expect(availabilityBadge({ status: 'LowStock', quantityLeft: 4 })).toEqual({ text: '¡Quedan solo 4!', tone: 'low' })
  })

  it('agotado', () => {
    expect(availabilityBadge({ status: 'OutOfStock', quantityLeft: 0 })).toEqual({ text: 'Agotado', tone: 'out' })
  })
})
