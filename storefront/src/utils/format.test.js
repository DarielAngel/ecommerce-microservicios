import { describe, it, expect } from 'vitest'
import { formatMoney, formatDate } from './format'

describe('formatMoney', () => {
  it('da formato de moneda con dos decimales', () => {
    expect(formatMoney(12.3)).toBe('$12.30')
  })

  it('separa los miles', () => {
    expect(formatMoney(1234.5)).toBe('$1,234.50')
  })

  it('devuelve cadena vacía si el valor no es un número', () => {
    expect(formatMoney(null)).toBe('')
    expect(formatMoney(undefined)).toBe('')
    expect(formatMoney('abc')).toBe('')
  })
})

describe('formatDate', () => {
  it('da formato de fecha corta en español', () => {
    expect(formatDate('2026-03-05T10:00:00Z')).toMatch(/2026/)
  })

  it('devuelve cadena vacía si no hay fecha', () => {
    expect(formatDate(null)).toBe('')
  })
})
