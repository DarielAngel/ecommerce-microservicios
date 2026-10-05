import { describe, it, expect } from 'vitest'
import { splitCategories } from './categories'

const make = (n) => Array.from({ length: n }, (_, i) => ({ id: `c${i + 1}`, name: `Cat ${i + 1}` }))

describe('splitCategories', () => {
  it('si caben todas, no hay desbordamiento', () => {
    const { visible, overflow } = splitCategories(make(5), '', 7)

    expect(visible).toHaveLength(5)
    expect(overflow).toHaveLength(0)
  })

  it('si sobran, muestra las primeras y manda el resto al desbordamiento, en orden', () => {
    const { visible, overflow } = splitCategories(make(10), '', 7)

    expect(visible.map((c) => c.id)).toEqual(['c1', 'c2', 'c3', 'c4', 'c5', 'c6', 'c7'])
    expect(overflow.map((c) => c.id)).toEqual(['c8', 'c9', 'c10'])
  })

  it('la categoría seleccionada siempre queda visible, aunque estuviera en el desbordamiento', () => {
    const { visible, overflow } = splitCategories(make(10), 'c9', 7)

    expect(visible.map((c) => c.id)).toContain('c9')
    expect(overflow.map((c) => c.id)).not.toContain('c9')
  })

  it('al promover la seleccionada no se pierde ni se duplica ninguna categoría', () => {
    const { visible, overflow } = splitCategories(make(10), 'c9', 7)
    const ids = [...visible, ...overflow].map((c) => c.id)

    expect(visible).toHaveLength(7)
    expect(ids).toHaveLength(10)
    expect(new Set(ids).size).toBe(10)
  })

  it('si la seleccionada ya estaba visible, no cambia nada', () => {
    const { visible, overflow } = splitCategories(make(10), 'c3', 7)

    expect(visible.map((c) => c.id)).toEqual(['c1', 'c2', 'c3', 'c4', 'c5', 'c6', 'c7'])
    expect(overflow).toHaveLength(3)
  })

  it('con una lista vacía devuelve todo vacío', () => {
    expect(splitCategories([], '', 7)).toEqual({ visible: [], overflow: [] })
  })
})
