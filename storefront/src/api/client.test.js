import { describe, it, expect, beforeEach, vi } from 'vitest'
import { api, ApiError } from './client'

function mockFetchResponse({ ok = true, status = 200, body = null, contentType = 'application/json' } = {}) {
  return {
    ok,
    status,
    headers: { get: () => contentType },
    json: async () => body
  }
}

describe('api client (storefront)', () => {
  beforeEach(() => {
    global.fetch = vi.fn()
  })

  it('GET arma la URL con los query params, omitiendo los vacíos', async () => {
    global.fetch.mockResolvedValue(mockFetchResponse({ body: [] }))

    await api.get('/api/products', { params: { searchTerm: 'abrigo', categoryId: '', page: 2 } })

    const calledUrl = global.fetch.mock.calls[0][0]
    expect(calledUrl.toString()).toContain('searchTerm=abrigo')
    expect(calledUrl.toString()).toContain('page=2')
    expect(calledUrl.toString()).not.toContain('categoryId')
  })

  it('agrega el header Authorization solo cuando se pasa un token', async () => {
    global.fetch.mockResolvedValue(mockFetchResponse({ body: {} }))

    await api.get('/api/cart', { token: 'abc123' })

    const options = global.fetch.mock.calls[0][1]
    expect(options.headers.Authorization).toBe('Bearer abc123')
  })

  it('POST serializa el body como JSON', async () => {
    global.fetch.mockResolvedValue(mockFetchResponse({ body: { ok: true } }))

    await api.post('/api/categories', { name: 'Ropa' })

    const options = global.fetch.mock.calls[0][1]
    expect(options.method).toBe('POST')
    expect(JSON.parse(options.body)).toEqual({ name: 'Ropa' })
  })

  it('si la respuesta no es exitosa, lanza ApiError con el mensaje del servidor', async () => {
    global.fetch.mockResolvedValue(
      mockFetchResponse({ ok: false, status: 409, body: { message: 'No hay suficiente stock.' } })
    )

    await expect(api.post('/api/cart/items', { variantId: 'x', quantity: 99 }))
      .rejects.toMatchObject({ message: 'No hay suficiente stock.', status: 409 })
  })

  it('si la respuesta no es exitosa y no trae cuerpo JSON, arma un mensaje genérico', async () => {
    global.fetch.mockResolvedValue(
      mockFetchResponse({ ok: false, status: 500, body: null, contentType: 'text/plain' })
    )

    const error = await api.get('/api/stock/low-stock').catch((e) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error.message).toContain('500')
  })
})

describe('api client (storefront) — parámetros de tipo arreglo', () => {
  beforeEach(() => {
    global.fetch = vi.fn().mockResolvedValue(mockFetchResponse({ body: [] }))
  })

  it('envía los arreglos como parámetros repetidos (ids=a&ids=b), que es lo que ASP.NET espera para Guid[]', async () => {
    await api.get('/api/reviews/summaries', { params: { productIds: ['a', 'b'], q: 'z' } })

    const url = new URL(global.fetch.mock.calls[0][0])
    expect(url.searchParams.getAll('productIds')).toEqual(['a', 'b'])
    expect(url.searchParams.get('q')).toBe('z')
  })

  it('omite los arreglos vacíos', async () => {
    await api.get('/api/reviews/summaries', { params: { productIds: [] } })

    const url = new URL(global.fetch.mock.calls[0][0])
    expect(url.searchParams.has('productIds')).toBe(false)
  })

  it('sin red, el error se explica en español en vez de "Failed to fetch"', async () => {
    global.fetch.mockRejectedValue(new TypeError('Failed to fetch'))
    const onLine = vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)

    const error = await api.get('/api/cart').catch((e) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(0)
    expect(error.message).toBe('Sin conexión a internet. Revisa tu red e intenta de nuevo.')
    onLine.mockRestore()
  })

  it('con red pero sin respuesta del servidor, también avisa en español', async () => {
    global.fetch.mockRejectedValue(new TypeError('Failed to fetch'))

    const error = await api.get('/api/products').catch((e) => e)

    expect(error.message).toBe('No pudimos conectar con la tienda. Intenta de nuevo en un momento.')
  })
})
