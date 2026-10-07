import { describe, it, expect, beforeEach } from 'vitest'
import { addRecentlyViewed, loadRecentlyViewed, clearRecentlyViewed, summaryFromDetail, MAX_RECENT } from './recentlyViewed'

const p = (id) => ({ id, name: `Producto ${id}`, categoryName: 'Audio', minPrice: 10, primaryImageFileName: null })

describe('vistos recientemente', () => {
  beforeEach(() => localStorage.clear())

  it('empieza vacío y lo último visto va primero', () => {
    expect(loadRecentlyViewed()).toEqual([])
    addRecentlyViewed(p('a'))
    addRecentlyViewed(p('b'))
    expect(loadRecentlyViewed().map((x) => x.id)).toEqual(['b', 'a'])
  })

  it('volver a ver un producto lo sube al principio sin duplicarlo', () => {
    addRecentlyViewed(p('a'))
    addRecentlyViewed(p('b'))
    addRecentlyViewed(p('a'))
    expect(loadRecentlyViewed().map((x) => x.id)).toEqual(['a', 'b'])
  })

  it(`conserva como máximo ${MAX_RECENT}`, () => {
    for (let i = 0; i < MAX_RECENT + 5; i++) addRecentlyViewed(p(`p${i}`))
    const list = loadRecentlyViewed()
    expect(list).toHaveLength(MAX_RECENT)
    expect(list[0].id).toBe(`p${MAX_RECENT + 4}`)
  })

  it('si el dato guardado está corrupto, devuelve vacío en vez de romper', () => {
    localStorage.setItem('storefront-recently-viewed', '{no es json')
    expect(loadRecentlyViewed()).toEqual([])
  })

  it('borrar el historial lo deja vacío', () => {
    addRecentlyViewed(p('a'))
    clearRecentlyViewed()
    expect(loadRecentlyViewed()).toEqual([])
  })

  it('summaryFromDetail toma el menor precio activo y la imagen principal', () => {
    const detail = {
      id: 'x', name: 'Galaxy', categoryId: 'c',
      variants: [{ price: 900, isActive: true }, { price: 700, isActive: false }, { price: 800, isActive: true }],
      images: [{ fileName: 'b.jpg', isPrimary: false }, { fileName: 'a.jpg', isPrimary: true }]
    }
    expect(summaryFromDetail(detail, 'Celulares')).toEqual({
      id: 'x', name: 'Galaxy', categoryName: 'Celulares', minPrice: 800, primaryImageFileName: 'a.jpg'
    })
  })
})
