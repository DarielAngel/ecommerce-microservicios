import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'

const { fetchSuggestions } = vi.hoisted(() => ({ fetchSuggestions: vi.fn() }))
vi.mock('../api/catalog', () => ({ fetchSuggestions }))

import SearchBox from './SearchBox.vue'

const stub = { template: '<div />' }
const products = [
  { id: 'p1', name: 'Galaxy S24', categoryName: 'Celulares', minPrice: 799, primaryImageFileName: null },
  { id: 'p2', name: 'Galaxy A15 5G', categoryName: 'Celulares', minPrice: 199, primaryImageFileName: null }
]

async function mountBox() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'home', component: stub },
      { path: '/products/:id', name: 'product-detail', component: stub }
    ]
  })
  router.push('/')
  await router.isReady()
  const wrapper = mount(SearchBox, { props: { debounceMs: 0 }, global: { plugins: [router] }, attachTo: document.body })
  return { wrapper, router }
}

async function type(wrapper, text) {
  const input = wrapper.get('#global-search')
  await input.setValue(text)
  await input.trigger('input')
  await new Promise((r) => setTimeout(r, 0))
  await flushPromises()
  return input
}

const visibleOptions = (wrapper) => wrapper.findAll('[data-testid="search-option"]')

describe('SearchBox', () => {
  beforeEach(() => {
    fetchSuggestions.mockReset().mockResolvedValue(products)
  })

  it('con una sola letra no consulta ni abre el menú', async () => {
    const { wrapper } = await mountBox()
    const input = await type(wrapper, 'g')

    expect(fetchSuggestions).not.toHaveBeenCalled()
    expect(input.attributes('aria-expanded')).toBe('false')
  })

  it('muestra las sugerencias y la opción de ver todos los resultados', async () => {
    const { wrapper } = await mountBox()
    const input = await type(wrapper, 'gal')

    expect(fetchSuggestions).toHaveBeenCalledWith('gal')
    expect(input.attributes('aria-expanded')).toBe('true')
    expect(visibleOptions(wrapper)).toHaveLength(3)
    expect(wrapper.text()).toContain('Galaxy S24')
    expect(wrapper.text()).toContain('$799.00')
    expect(wrapper.get('[data-testid="search-see-all"]').text()).toContain('Ver todos los resultados para "gal"')
  })

  it('con las flechas se resalta una opción y Enter abre ese producto', async () => {
    const { wrapper, router } = await mountBox()
    const input = await type(wrapper, 'gal')

    await input.trigger('keydown', { key: 'ArrowDown' })
    await input.trigger('keydown', { key: 'ArrowDown' })
    expect(input.attributes('aria-activedescendant')).toBe('search-option-1')
    expect(visibleOptions(wrapper)[1].attributes('aria-selected')).toBe('true')

    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(router.currentRoute.value.fullPath).toBe('/products/p2')
  })

  it('ArrowUp desde el principio va a la última opción', async () => {
    const { wrapper } = await mountBox()
    const input = await type(wrapper, 'gal')

    await input.trigger('keydown', { key: 'ArrowUp' })
    expect(input.attributes('aria-activedescendant')).toBe('search-option-2')
  })

  it('Enter sin opción resaltada busca lo escrito, como antes', async () => {
    const { wrapper, router } = await mountBox()
    await type(wrapper, 'galaxy')

    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(router.currentRoute.value.fullPath).toBe('/?q=galaxy')
  })

  it('un clic en una sugerencia abre el producto', async () => {
    const { wrapper, router } = await mountBox()
    await type(wrapper, 'gal')

    await visibleOptions(wrapper)[0].trigger('mousedown')
    await flushPromises()
    expect(router.currentRoute.value.fullPath).toBe('/products/p1')
  })

  it('Escape cierra el menú', async () => {
    const { wrapper } = await mountBox()
    const input = await type(wrapper, 'gal')

    await input.trigger('keydown', { key: 'Escape' })
    expect(input.attributes('aria-expanded')).toBe('false')
  })

  it('sin coincidencias lo dice, y ofrece igual buscar el texto', async () => {
    fetchSuggestions.mockResolvedValue([])
    const { wrapper } = await mountBox()
    await type(wrapper, 'zzzz')

    expect(wrapper.get('[data-testid="search-no-results"]').text()).toContain('No encontramos productos con "zzzz"')
    expect(visibleOptions(wrapper)).toHaveLength(1)
  })

  it('descarta una respuesta vieja si el cliente siguió escribiendo', async () => {
    let resolveSlow
    fetchSuggestions
      .mockImplementationOnce(() => new Promise((r) => { resolveSlow = r }))
      .mockResolvedValueOnce([products[1]])
    const { wrapper } = await mountBox()

    await type(wrapper, 'ga')        // respuesta lenta
    await type(wrapper, 'galaxy a')  // respuesta rápida
    resolveSlow([products[0]])       // llega tarde la vieja
    await flushPromises()

    expect(wrapper.text()).toContain('Galaxy A15 5G')
    expect(wrapper.text()).not.toContain('Galaxy S24')
  })
})
