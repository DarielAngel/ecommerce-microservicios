import { describe, it, expect } from 'vitest'
import { pickQuickAddVariant, priceLabelFor, primaryImageOf } from './wishlist'

const v = (id, price, isActive = true) => ({ id, sku: id, price, attributes: {}, isActive })

describe('pickQuickAddVariant', () => {
  it('con una sola variante activa, la elige', () => {
    expect(pickQuickAddVariant({ isActive: true, variants: [v('a', 10)] }).id).toBe('a')
  })

  it('ignora las variantes inactivas al contar', () => {
    expect(pickQuickAddVariant({ isActive: true, variants: [v('a', 10, false), v('b', 12)] }).id).toBe('b')
  })

  it('con varias variantes activas no adivina: devuelve null para que el cliente elija', () => {
    expect(pickQuickAddVariant({ isActive: true, variants: [v('a', 10), v('b', 12)] })).toBeNull()
  })

  it('sin variantes, con producto inactivo o sin producto, devuelve null', () => {
    expect(pickQuickAddVariant({ isActive: true, variants: [] })).toBeNull()
    expect(pickQuickAddVariant({ isActive: false, variants: [v('a', 10)] })).toBeNull()
    expect(pickQuickAddVariant(null)).toBeNull()
  })
})

describe('priceLabelFor', () => {
  it('un solo precio: sin "desde"', () => {
    expect(priceLabelFor({ variants: [v('a', 10), v('b', 10)] })).toEqual({ amount: 10, from: false })
  })

  it('precios distintos: el menor, marcado como "desde"', () => {
    expect(priceLabelFor({ variants: [v('a', 15), v('b', 9.5)] })).toEqual({ amount: 9.5, from: true })
  })

  it('no cuenta variantes inactivas y devuelve null si no queda ninguna', () => {
    expect(priceLabelFor({ variants: [v('a', 1, false), v('b', 20)] })).toEqual({ amount: 20, from: false })
    expect(priceLabelFor({ variants: [v('a', 1, false)] })).toBeNull()
  })
})

describe('primaryImageOf', () => {
  it('prefiere la imagen principal; si no hay, la primera; si no hay imágenes, null', () => {
    expect(primaryImageOf({ images: [{ id: 1 }, { id: 2, isPrimary: true }] }).id).toBe(2)
    expect(primaryImageOf({ images: [{ id: 1 }, { id: 2 }] }).id).toBe(1)
    expect(primaryImageOf({ images: [] })).toBeNull()
  })
})
