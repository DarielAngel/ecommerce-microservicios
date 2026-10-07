import { describe, it, expect, beforeEach, vi } from 'vitest'

const { publicApi } = vi.hoisted(() => ({ publicApi: { get: vi.fn() } }))
vi.mock('./client', () => ({ api: publicApi, BASE_URL: 'http://localhost:5000' }))

import { fetchSuggestions, fetchRelated, fetchAvailability } from './catalog'

describe('api de descubrimiento', () => {
  beforeEach(() => {
    publicApi.get.mockReset()
  })

  it('no pide sugerencias con menos de 2 letras', async () => {
    expect(await fetchSuggestions(' a ')).toEqual([])
    expect(publicApi.get).not.toHaveBeenCalled()
  })

  it('pide sugerencias sin espacios sobrantes', async () => {
    publicApi.get.mockResolvedValue([{ id: '1' }])
    await fetchSuggestions('  gal ', 4)
    expect(publicApi.get).toHaveBeenCalledWith('/api/products/suggestions', { params: { q: 'gal', limit: 4 } })
  })

  it('si Catálogo falla, sugerencias y relacionados devuelven vacío', async () => {
    publicApi.get.mockRejectedValue(new Error('caído'))
    expect(await fetchSuggestions('galaxy')).toEqual([])
    expect(await fetchRelated('p1')).toEqual([])
  })

  it('la disponibilidad se indexa por variante y no llama si no hay variantes', async () => {
    expect(await fetchAvailability([])).toEqual({})
    publicApi.get.mockResolvedValue([{ variantId: 'v1', status: 'LowStock', quantityLeft: 2 }])

    const result = await fetchAvailability(['v1'])

    expect(publicApi.get).toHaveBeenCalledWith('/api/stock/availability', { params: { variantIds: ['v1'] } })
    expect(result.v1.quantityLeft).toBe(2)
  })
})
