import { describe, it, expect, beforeEach, vi } from 'vitest'

const { publicApi } = vi.hoisted(() => ({ publicApi: { get: vi.fn() } }))
vi.mock('./client', () => ({ api: publicApi, BASE_URL: 'http://localhost:5000' }))

import { fetchRatingSummaries } from './reviews'

describe('fetchRatingSummaries', () => {
  // Con llaves a propósito: si el callback devolviera el mock, Vitest lo ejecutaría como limpieza.
  beforeEach(() => {
    publicApi.get.mockReset()
  })

  it('no llama a la API si no hay productos', async () => {
    expect(await fetchRatingSummaries([])).toEqual({})
    expect(publicApi.get).not.toHaveBeenCalled()
  })

  it('devuelve los resúmenes indexados por id de producto', async () => {
    publicApi.get.mockResolvedValue([
      { productId: 'a', average: 4.5, count: 2, distribution: {} },
      { productId: 'b', average: 0, count: 0, distribution: {} }
    ])

    const result = await fetchRatingSummaries(['a', 'b'])

    expect(publicApi.get).toHaveBeenCalledWith('/api/reviews/summaries', { params: { productIds: ['a', 'b'] } })
    expect(result.a.average).toBe(4.5)
    expect(result.b.count).toBe(0)
  })

  it('si el servicio de reseñas falla, devuelve {} — las estrellas son un extra, no deben romper el catálogo', async () => {
    publicApi.get.mockRejectedValue(new Error('servicio caído'))

    expect(await fetchRatingSummaries(['a'])).toEqual({})
  })
})
