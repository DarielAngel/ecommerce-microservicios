import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'

const { client } = vi.hoisted(() => ({ client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() } }))
vi.mock('../../api/useApi', () => ({ useApi: () => client }))

import ReviewsView from './ReviewsView.vue'

const review = (id, over = {}) => ({
  id, productId: 'p-taza-0001', authorName: 'Ana P.', rating: 1, title: `Mala ${id}`, comment: 'Llegó rota',
  isVerifiedPurchase: true, createdAtUtc: '2026-10-07T10:00:00Z', updatedAtUtc: '2026-10-07T10:00:00Z', ...over
})
const page = (items, totalCount = items.length) => ({ items, page: 1, pageSize: 20, totalCount, totalPages: 1 })

/** Respuestas por ruta: la lista de reseñas y el nombre de cada producto. */
function givenApi({ reviews = [review('r1')], total, products = { 'p-taza-0001': 'Taza de cerámica' } } = {}) {
  client.get.mockImplementation(async (path) => {
    if (path === '/api/reviews/admin') return page(reviews, total ?? reviews.length)
    const id = path.replace('/api/products/', '')
    if (id in products) return { id, name: products[id] }
    throw Object.assign(new Error('no existe'), { status: 404 })
  })
}

async function mountView() {
  const wrapper = mount(ReviewsView)
  await flushPromises()
  return wrapper
}

const listCalls = () => client.get.mock.calls.filter(([path]) => path === '/api/reviews/admin')
const lastListParams = () => listCalls().at(-1)[1].params

describe('ReviewsView', () => {
  beforeEach(() => {
    for (const fn of Object.values(client)) fn.mockReset()
    givenApi()
  })

  it('lista las reseñas con estrellas, autor, compra verificada y el nombre del producto', async () => {
    const wrapper = await mountView()
    const row = wrapper.get('[data-testid="review-row"]')

    expect(row.text()).toContain('★☆☆☆☆')
    expect(row.text()).toContain('Mala r1')
    expect(row.text()).toContain('Llegó rota')
    expect(row.text()).toContain('Compra verificada')
    expect(row.get('[data-testid="review-product"]').text()).toBe('Taza de cerámica')
    expect(lastListParams()).toMatchObject({ page: 1, pageSize: 20, rating: '', search: '' })
  })

  it('pide el nombre de cada producto una sola vez y avisa si el producto ya no existe', async () => {
    givenApi({ reviews: [review('r1'), review('r2'), review('r3', { productId: 'p-borrado-01' })] })
    const wrapper = await mountView()

    const productCalls = client.get.mock.calls.filter(([path]) => path.startsWith('/api/products/'))
    expect(productCalls.map(([p]) => p)).toEqual(['/api/products/p-taza-0001', '/api/products/p-borrado-01'])
    expect(wrapper.findAll('[data-testid="review-product"]')[2].text()).toBe('Producto eliminado (p-borrad)')
  })

  it('filtra por estrellas y texto al buscar, y vuelve a la página 1', async () => {
    const wrapper = await mountView()

    await wrapper.get('#review-rating').setValue('2')
    await wrapper.get('#review-search').setValue('  estafa ')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(lastListParams()).toMatchObject({ rating: '2', search: 'estafa', page: 1 })
  })

  it('al tocar el producto muestra solo sus reseñas, y se puede volver a todas', async () => {
    const wrapper = await mountView()

    await wrapper.get('[data-testid="review-product"]').trigger('click')
    await flushPromises()
    expect(lastListParams().productId).toBe('p-taza-0001')
    expect(wrapper.get('[data-testid="reviews-product-filter"]').text()).toContain('Taza de cerámica')

    await wrapper.get('[data-testid="reviews-clear"]').trigger('click')
    await flushPromises()
    expect(lastListParams().productId).toBeNull()
  })

  it('pagina con "Siguiente"', async () => {
    givenApi({ total: 45 })
    const wrapper = await mountView()

    expect(wrapper.get('[data-testid="reviews-count"]').text()).toBe('Página 1 de 3 · 45 reseñas')
    await wrapper.get('[data-testid="reviews-next"]').trigger('click')
    await flushPromises()

    expect(lastListParams().page).toBe(2)
  })

  it('eliminar pide confirmación en la misma fila y luego recarga', async () => {
    const wrapper = await mountView()

    await wrapper.get('[data-testid="review-delete"]').trigger('click')
    expect(client.delete).not.toHaveBeenCalled()
    await wrapper.get('[data-testid="review-confirm-delete"]').trigger('click')
    await flushPromises()

    expect(client.delete).toHaveBeenCalledWith('/api/reviews/r1')
    expect(listCalls()).toHaveLength(2)
    expect(wrapper.text()).toContain('Reseña "Mala r1" eliminada.')
  })

  it('si otro Admin ya la eliminó (404), avisa y refresca sin mostrar error', async () => {
    client.delete.mockRejectedValue(Object.assign(new Error('La reseña no existe.'), { status: 404 }))
    const wrapper = await mountView()

    await wrapper.get('[data-testid="review-delete"]').trigger('click')
    await wrapper.get('[data-testid="review-confirm-delete"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[role="alert"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Esa reseña ya no existía')
    expect(listCalls()).toHaveLength(2)
  })

  it('sin resultados con filtros lo dice distinto que sin reseñas', async () => {
    givenApi({ reviews: [] })
    const wrapper = await mountView()
    expect(wrapper.get('[data-testid="reviews-empty"]').text()).toBe('Todavía no hay reseñas.')

    await wrapper.get('#review-search').setValue('nada')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper.get('[data-testid="reviews-empty"]').text()).toBe('Ninguna reseña coincide con estos filtros.')
  })
})
