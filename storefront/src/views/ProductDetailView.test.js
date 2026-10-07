import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises, RouterLinkStub } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

const { publicApi, client, catalog } = vi.hoisted(() => ({
  publicApi: { get: vi.fn() },
  client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  catalog: { fetchRelated: vi.fn(), fetchAvailability: vi.fn() }
}))
vi.mock('../api/client', () => ({ api: publicApi, ApiError: Error, BASE_URL: 'http://localhost:5000' }))
vi.mock('../api/useApi', () => ({ useApi: () => client }))
vi.mock('../api/catalog', () => catalog)
vi.mock('vue-router', () => ({ useRouter: () => ({ push: vi.fn(), currentRoute: { value: { fullPath: '/' } } }) }))

import ProductDetailView from './ProductDetailView.vue'

const product = (id, name) => ({
  id, name, description: 'Desc', slug: id, categoryId: 'cat-1', isActive: true,
  variants: [{ id: `${id}-v1`, sku: 'SKU', price: 99, attributes: {}, isActive: true }],
  images: []
})
const products = { p1: product('p1', 'Galaxy S24'), p2: product('p2', 'Pixel 8a') }

function mountView(id = 'p1') {
  setActivePinia(createPinia())
  return mount(ProductDetailView, {
    props: { id },
    global: { stubs: { RouterLink: RouterLinkStub, ReviewsSection: true, FavoriteButton: true } }
  })
}

describe('ProductDetailView — descubrimiento', () => {
  beforeEach(() => {
    localStorage.clear()
    publicApi.get.mockReset().mockImplementation(async (path) => {
      if (path === '/api/categories') return [{ id: 'cat-1', name: 'Celulares' }]
      const id = path.split('/').pop()
      return products[id]
    })
    catalog.fetchRelated.mockReset().mockResolvedValue([
      { id: 'p9', name: 'Moto G84', categoryName: 'Celulares', minPrice: 249, primaryImageFileName: null }
    ])
    catalog.fetchAvailability.mockReset().mockResolvedValue({})
  })

  it('con pocas unidades reales muestra cuántas quedan', async () => {
    catalog.fetchAvailability.mockResolvedValue({ 'p1-v1': { status: 'LowStock', quantityLeft: 3 } })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.get('[data-testid="availability-badge"]').text()).toBe('¡Quedan solo 3!')
    expect(catalog.fetchAvailability).toHaveBeenCalledWith(['p1-v1'])
  })

  it('agotado: insignia y botón deshabilitado', async () => {
    catalog.fetchAvailability.mockResolvedValue({ 'p1-v1': { status: 'OutOfStock', quantityLeft: 0 } })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.get('[data-testid="availability-badge"]').text()).toBe('Agotado')
    const button = wrapper.findAll('button').find((b) => b.text() === 'Sin stock')
    expect(button.attributes('disabled')).toBeDefined()
  })

  it('con stock de sobra no muestra insignia', async () => {
    catalog.fetchAvailability.mockResolvedValue({ 'p1-v1': { status: 'InStock', quantityLeft: null } })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('[data-testid="availability-badge"]').exists()).toBe(false)
  })

  it('muestra "También te puede interesar" con los relacionados', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(catalog.fetchRelated).toHaveBeenCalledWith('p1')
    expect(wrapper.get('[data-testid="related-products"]').text()).toContain('Moto G84')
  })

  it('registra la visita (con su categoría) y no se muestra a sí mismo en "Vistos recientemente"', async () => {
    localStorage.setItem('storefront-recently-viewed', JSON.stringify([
      { id: 'p2', name: 'Pixel 8a', categoryName: 'Celulares', minPrice: 499, primaryImageFileName: null }
    ]))
    const wrapper = mountView('p1')
    await flushPromises()

    const stored = JSON.parse(localStorage.getItem('storefront-recently-viewed'))
    expect(stored.map((p) => p.id)).toEqual(['p1', 'p2'])
    expect(stored[0].categoryName).toBe('Celulares')

    const rail = wrapper.get('[data-testid="recently-viewed"]')
    expect(rail.text()).toContain('Pixel 8a')
    expect(rail.text()).not.toContain('Galaxy S24')
  })

  it('al pasar a otro producto (misma pantalla, otro id) recarga todo', async () => {
    const wrapper = mountView('p1')
    await flushPromises()
    expect(wrapper.text()).toContain('Galaxy S24')

    await wrapper.setProps({ id: 'p2' })
    await flushPromises()

    expect(wrapper.text()).toContain('Pixel 8a')
    expect(catalog.fetchRelated).toHaveBeenLastCalledWith('p2')
    // Ahora el anterior aparece en "Vistos recientemente".
    expect(wrapper.get('[data-testid="recently-viewed"]').text()).toContain('Galaxy S24')
  })
})
