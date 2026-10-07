import { describe, it, expect, beforeEach, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

const { client } = vi.hoisted(() => ({
  client: { get: vi.fn(), put: vi.fn(), delete: vi.fn() }
}))
vi.mock('../api/useApi', () => ({ useApi: () => client }))

import { useWishlistStore } from './wishlist'

describe('wishlist store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    client.get.mockReset()
    client.put.mockReset()
    client.delete.mockReset()
  })

  it('load trae los ids de mis favoritos', async () => {
    client.get.mockResolvedValue([{ productId: 'p2', addedAtUtc: 'x' }, { productId: 'p1', addedAtUtc: 'y' }])
    const store = useWishlistStore()

    await store.load()

    expect(client.get).toHaveBeenCalledWith('/api/wishlist')
    expect(store.productIds).toEqual(['p2', 'p1'])
    expect(store.has('p1')).toBe(true)
    expect(store.count).toBe(2)
    expect(store.loaded).toBe(true)
  })

  it('toggle sobre un producto que no es favorito lo agrega (PUT) y devuelve true', async () => {
    client.put.mockResolvedValue(null)
    const store = useWishlistStore()

    expect(await store.toggle('p1')).toBe(true)

    expect(client.put).toHaveBeenCalledWith('/api/wishlist/p1')
    expect(store.has('p1')).toBe(true)
  })

  it('toggle sobre un favorito lo quita (DELETE) y devuelve false', async () => {
    client.delete.mockResolvedValue(null)
    const store = useWishlistStore()
    store.productIds = ['p1', 'p2']

    expect(await store.toggle('p1')).toBe(false)

    expect(client.delete).toHaveBeenCalledWith('/api/wishlist/p1')
    expect(store.productIds).toEqual(['p2'])
  })

  it('actualiza la pantalla antes de que responda el servidor (optimista)', async () => {
    let resolve
    client.put.mockReturnValue(new Promise((r) => { resolve = r }))
    const store = useWishlistStore()

    const pending = store.toggle('p1')
    expect(store.has('p1')).toBe(true)
    expect(store.isPending('p1')).toBe(true)

    resolve(null)
    await pending
    expect(store.isPending('p1')).toBe(false)
  })

  it('si el servidor falla, deshace el cambio y relanza el error', async () => {
    client.put.mockRejectedValue(new Error('Tu lista de favoritos está llena'))
    const store = useWishlistStore()

    await expect(store.toggle('p1')).rejects.toThrow('llena')

    expect(store.has('p1')).toBe(false)
    expect(store.isPending('p1')).toBe(false)
  })

  it('ignora un segundo clic mientras el primero está en vuelo', async () => {
    let resolve
    client.put.mockReturnValue(new Promise((r) => { resolve = r }))
    const store = useWishlistStore()

    const first = store.toggle('p1')
    await store.toggle('p1')
    resolve(null)
    await first

    expect(client.put).toHaveBeenCalledTimes(1)
    expect(store.has('p1')).toBe(true)
  })

  it('remove no llama al servidor si el producto no era favorito', async () => {
    const store = useWishlistStore()

    await store.remove('p9')

    expect(client.delete).not.toHaveBeenCalled()
  })

  it('clear vacía todo (al cerrar sesión)', () => {
    const store = useWishlistStore()
    store.productIds = ['p1']
    store.loaded = true

    store.clear()

    expect(store.count).toBe(0)
    expect(store.loaded).toBe(false)
  })
})
