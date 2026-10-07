import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises, RouterLinkStub } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

// useApi = llamadas con sesión (favoritos, carrito); api = catálogo público.
const { client, publicApi, ApiError } = vi.hoisted(() => {
  class ApiError extends Error {
    constructor(message, status) { super(message); this.status = status }
  }
  return {
    client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
    publicApi: { get: vi.fn() },
    ApiError
  }
})
vi.mock('../api/useApi', () => ({ useApi: () => client }))
vi.mock('../api/client', () => ({ api: publicApi, ApiError, BASE_URL: 'http://localhost:5000' }))

import WishlistView from './WishlistView.vue'
import { useCartStore } from '../stores/cart'
import { useWishlistStore } from '../stores/wishlist'
import { useToastStore } from '../stores/toast'

const variant = (id, price) => ({ id, sku: id, price, attributes: {}, isActive: true })
const products = {
  simple: { id: 'simple', name: 'Taza', isActive: true, variants: [variant('v1', 8)], images: [] },
  multi: { id: 'multi', name: 'Camiseta', isActive: true, variants: [variant('s', 10), variant('m', 12)], images: [] }
}

function givenWishlist(ids) {
  client.get.mockImplementation(async (path) => {
    if (path === '/api/wishlist') return ids.map((productId) => ({ productId, addedAtUtc: '2026-10-01T00:00:00Z' }))
    throw new Error(`GET inesperado: ${path}`)
  })
}

async function mountView() {
  const pinia = createPinia()
  setActivePinia(pinia)
  const wrapper = mount(WishlistView, { global: { plugins: [pinia], stubs: { RouterLink: RouterLinkStub } } })
  await flushPromises()
  return wrapper
}

const items = (wrapper) => wrapper.findAll('[data-testid="wishlist-item"]')

describe('WishlistView', () => {
  beforeEach(() => {
    for (const fn of [client.get, client.post, client.put, client.delete, publicApi.get]) fn.mockReset()
    publicApi.get.mockImplementation(async (path) => {
      const id = path.split('/').pop()
      if (products[id]) return products[id]
      throw new ApiError('El producto no existe.', 404)
    })
    client.delete.mockResolvedValue(null)
  })

  it('sin favoritos muestra el estado vacío con un enlace a la tienda', async () => {
    givenWishlist([])
    const wrapper = await mountView()

    expect(wrapper.find('[data-testid="wishlist-empty"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Todavía no tienes favoritos')
  })

  it('muestra cada favorito con su nombre, precio y la cantidad total', async () => {
    givenWishlist(['simple', 'multi'])
    const wrapper = await mountView()

    expect(items(wrapper)).toHaveLength(2)
    expect(wrapper.text()).toContain('Taza')
    expect(wrapper.text()).toContain('$8.00')
    expect(wrapper.text()).toContain('Desde $10.00')
    expect(wrapper.get('[data-testid="wishlist-count"]').text()).toBe('2 productos')
  })

  it('con una sola variante ofrece "Mover al carrito"; con varias, "Elegir opción"', async () => {
    givenWishlist(['simple', 'multi'])
    const wrapper = await mountView()
    const [simple, multi] = items(wrapper)

    expect(simple.find('[data-testid="wishlist-move-to-cart"]').exists()).toBe(true)
    expect(multi.find('[data-testid="wishlist-move-to-cart"]').exists()).toBe(false)
    expect(multi.find('[data-testid="wishlist-choose-variant"]').exists()).toBe(true)
  })

  it('mover al carrito agrega 1 unidad, actualiza el carrito y lo quita de favoritos', async () => {
    givenWishlist(['simple'])
    client.post.mockResolvedValue({ items: [], totalItemCount: 1 })
    const wrapper = await mountView()

    await wrapper.get('[data-testid="wishlist-move-to-cart"]').trigger('click')
    await flushPromises()

    expect(client.post).toHaveBeenCalledWith('/api/cart/items', { variantId: 'v1', quantity: 1 })
    expect(useCartStore().itemCount).toBe(1)
    expect(client.delete).toHaveBeenCalledWith('/api/wishlist/simple')
    expect(useWishlistStore().has('simple')).toBe(false)
    expect(items(wrapper)).toHaveLength(0)
  })

  it('si no hay stock, el carrito lo rechaza: el favorito se queda y se muestra el motivo', async () => {
    givenWishlist(['simple'])
    client.post.mockRejectedValue(new ApiError('No hay suficiente stock. Disponible: 0', 409))
    const wrapper = await mountView()

    await wrapper.get('[data-testid="wishlist-move-to-cart"]').trigger('click')
    await flushPromises()

    expect(client.delete).not.toHaveBeenCalled()
    expect(items(wrapper)).toHaveLength(1)
    const toast = useToastStore().toasts.at(-1)
    expect(toast.type).toBe('error')
    expect(toast.message).toContain('stock')
  })

  it('un producto eliminado del catálogo aparece como "ya no disponible" y se puede quitar', async () => {
    givenWishlist(['borrado'])
    const wrapper = await mountView()

    expect(wrapper.text()).toContain('Este producto ya no está disponible')

    await wrapper.get('[data-testid="wishlist-remove"]').trigger('click')
    await flushPromises()

    expect(client.delete).toHaveBeenCalledWith('/api/wishlist/borrado')
    expect(wrapper.find('[data-testid="wishlist-empty"]').exists()).toBe(true)
  })

  it('si falla la lista de favoritos, muestra el error', async () => {
    client.get.mockRejectedValue(new Error('Servicio de favoritos caído'))
    const wrapper = await mountView()

    expect(wrapper.text()).toContain('Servicio de favoritos caído')
  })
})
