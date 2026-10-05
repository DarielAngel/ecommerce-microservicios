import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises, RouterLinkStub } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

const { publicApi, authedApi } = vi.hoisted(() => ({
  publicApi: { get: vi.fn() },
  authedApi: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() }
}))
vi.mock('../api/client', () => ({ api: publicApi, BASE_URL: 'http://localhost:5000' }))
vi.mock('../api/useApi', () => ({ useApi: () => authedApi }))

import ReviewsSection from './ReviewsSection.vue'
import { useAuthStore } from '../stores/auth'
import { useToastStore } from '../stores/toast'

const summary = { productId: 'p1', average: 4.0, count: 3, distribution: { 1: 0, 2: 0, 3: 1, 4: 1, 5: 1 } }
const emptySummary = { productId: 'p1', average: 0, count: 0, distribution: { 1: 0, 2: 0, 3: 0, 4: 0, 5: 0 } }

const reviews = [
  { id: 'r1', productId: 'p1', authorName: 'Ana P.', rating: 5, title: 'Excelente', comment: 'Me encantó',
    isVerifiedPurchase: true, createdAtUtc: '2026-03-05T10:00:00Z', updatedAtUtc: '2026-03-05T10:00:00Z' },
  { id: 'r2', productId: 'p1', authorName: 'Luis M.', rating: 3, title: 'Normal', comment: 'Cumple',
    isVerifiedPurchase: false, createdAtUtc: '2026-03-04T10:00:00Z', updatedAtUtc: '2026-03-04T10:00:00Z' }
]

const myReview = {
  id: 'mine-1', productId: 'p1', authorName: 'Yo M.', rating: 4, title: 'Mi opinión', comment: 'Buen producto',
  isVerifiedPurchase: false, createdAtUtc: '2026-03-06T10:00:00Z', updatedAtUtc: '2026-03-06T10:00:00Z'
}

function setupPublic({ sum = summary, items = reviews, totalPages = 1 } = {}) {
  publicApi.get.mockImplementation(async (path) =>
    path.endsWith('/summary') ? sum : { items, page: 1, pageSize: 5, totalCount: items.length, totalPages })
}

const notFound = () => Object.assign(new Error('Todavía no reseñaste este producto.'), { status: 404 })

async function mountSection({ authenticated = false, mine = null } = {}) {
  const pinia = createPinia()
  setActivePinia(pinia)
  if (authenticated) {
    const auth = useAuthStore(pinia)
    auth.accessToken = 'token'
    auth.user = { fullName: 'Yo Mismo' }
    authedApi.get.mockImplementation(async () => { if (mine) return mine; throw notFound() })
  }
  const wrapper = mount(ReviewsSection, {
    props: { productId: 'p1' },
    global: { plugins: [pinia], stubs: { RouterLink: RouterLinkStub } }
  })
  await flushPromises()
  return { wrapper, pinia }
}

async function fillAndSubmit(wrapper, { stars, title, comment }) {
  if (stars) await wrapper.findAll('[data-testid="review-form"] [role="radio"]')[stars - 1].trigger('click')
  if (title !== undefined) await wrapper.get('#review-title').setValue(title)
  if (comment !== undefined) await wrapper.get('#review-comment').setValue(comment)
  await wrapper.get('[data-testid="review-form"]').trigger('submit')
  await flushPromises()
}

describe('ReviewsSection — lectura pública', () => {
  beforeEach(() => {
    publicApi.get.mockReset()
    Object.values(authedApi).forEach((fn) => fn.mockReset())
  })

  it('muestra el promedio, la cantidad y la distribución por estrellas', async () => {
    setupPublic()
    const { wrapper } = await mountSection()

    expect(wrapper.text()).toContain('4.0')
    expect(wrapper.text()).toContain('3 reseñas')
    expect(wrapper.get('[data-testid="distribution-5"]').text()).toContain('1')
    expect(wrapper.get('[data-testid="distribution-1"]').text()).toContain('0')
  })

  it('lista las reseñas con autor, título y comentario', async () => {
    setupPublic()
    const { wrapper } = await mountSection()

    expect(wrapper.text()).toContain('Ana P.')
    expect(wrapper.text()).toContain('Excelente')
    expect(wrapper.text()).toContain('Me encantó')
    expect(wrapper.text()).toContain('Luis M.')
  })

  it('marca "Compra verificada" solo en las reseñas verificadas', async () => {
    setupPublic()
    const { wrapper } = await mountSection()

    expect(wrapper.findAll('[data-testid="verified-badge"]')).toHaveLength(1)
  })

  it('sin reseñas muestra el mensaje vacío', async () => {
    setupPublic({ sum: emptySummary, items: [] })
    const { wrapper } = await mountSection()

    expect(wrapper.text()).toContain('Todavía no hay reseñas')
  })

  it('un invitado ve la invitación a iniciar sesión y no el formulario', async () => {
    setupPublic()
    const { wrapper } = await mountSection({ authenticated: false })

    expect(wrapper.find('[data-testid="review-form"]').exists()).toBe(false)
    expect(wrapper.getComponent(RouterLinkStub).props('to')).toMatchObject({ name: 'login' })
    expect(wrapper.text()).toContain('Inicia sesión')
  })

  it('cambiar el orden vuelve a pedir la lista con ese sort', async () => {
    setupPublic()
    const { wrapper } = await mountSection()

    await wrapper.get('#review-sort').setValue('highest')
    await flushPromises()

    const listCalls = publicApi.get.mock.calls.filter(([path]) => !path.endsWith('/summary'))
    expect(listCalls.at(-1)[1].params.sort).toBe('highest')
    expect(listCalls.at(-1)[1].params.page).toBe(1)
  })

  it('"Cargar más" aparece si hay más páginas y pide la siguiente', async () => {
    setupPublic({ totalPages: 2 })
    const { wrapper } = await mountSection()

    await wrapper.get('[data-testid="load-more"]').trigger('click')
    await flushPromises()

    const listCalls = publicApi.get.mock.calls.filter(([path]) => !path.endsWith('/summary'))
    expect(listCalls.at(-1)[1].params.page).toBe(2)
  })

  it('no muestra "Cargar más" si todo cabe en una página', async () => {
    setupPublic({ totalPages: 1 })
    const { wrapper } = await mountSection()

    expect(wrapper.find('[data-testid="load-more"]').exists()).toBe(false)
  })
})

describe('ReviewsSection — escribir, editar y eliminar', () => {
  beforeEach(() => {
    publicApi.get.mockReset()
    Object.values(authedApi).forEach((fn) => fn.mockReset())
    setupPublic()
  })

  it('un cliente sin reseña ve el formulario', async () => {
    const { wrapper } = await mountSection({ authenticated: true })

    expect(wrapper.find('[data-testid="review-form"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Publicar reseña')
  })

  it('publicar sin elegir calificación muestra un error y no llama a la API', async () => {
    const { wrapper } = await mountSection({ authenticated: true })

    await fillAndSubmit(wrapper, { title: 'Sin estrellas', comment: 'x' })

    expect(wrapper.text()).toContain('Elige una calificación')
    expect(authedApi.post).not.toHaveBeenCalled()
  })

  it('publicar sin título muestra un error y no llama a la API', async () => {
    const { wrapper } = await mountSection({ authenticated: true })

    await fillAndSubmit(wrapper, { stars: 4, title: '   ' })

    expect(wrapper.text()).toContain('título')
    expect(authedApi.post).not.toHaveBeenCalled()
  })

  it('publicar envía la reseña, avisa con un toast y muestra "Tu reseña"', async () => {
    authedApi.post.mockResolvedValue(myReview)
    const { wrapper, pinia } = await mountSection({ authenticated: true })

    await fillAndSubmit(wrapper, { stars: 4, title: 'Mi opinión', comment: 'Buen producto' })

    expect(authedApi.post).toHaveBeenCalledWith('/api/reviews/products/p1', { rating: 4, title: 'Mi opinión', comment: 'Buen producto' })
    expect(wrapper.text()).toContain('Tu reseña')
    expect(wrapper.find('[data-testid="review-form"]').exists()).toBe(false)
    expect(useToastStore(pinia).toasts.some((t) => t.type === 'success')).toBe(true)
  })

  it('después de publicar recarga el resumen y la lista', async () => {
    authedApi.post.mockResolvedValue(myReview)
    const { wrapper } = await mountSection({ authenticated: true })
    const callsBefore = publicApi.get.mock.calls.length

    await fillAndSubmit(wrapper, { stars: 4, title: 'Mi opinión' })

    expect(publicApi.get.mock.calls.length).toBeGreaterThan(callsBefore)
  })

  it('si el servidor rechaza (ej. 409), muestra su mensaje y conserva el formulario', async () => {
    authedApi.post.mockRejectedValue(new Error('Ya reseñaste este producto. Edita tu reseña existente.'))
    const { wrapper } = await mountSection({ authenticated: true })

    await fillAndSubmit(wrapper, { stars: 5, title: 'Otra vez' })

    expect(wrapper.text()).toContain('Ya reseñaste este producto')
    expect(wrapper.find('[data-testid="review-form"]').exists()).toBe(true)
  })

  it('con reseña propia muestra Editar y Eliminar en lugar del formulario', async () => {
    const { wrapper } = await mountSection({ authenticated: true, mine: myReview })

    expect(wrapper.text()).toContain('Tu reseña')
    expect(wrapper.text()).toContain('Mi opinión')
    expect(wrapper.find('[data-testid="edit-review"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="delete-review"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="review-form"]').exists()).toBe(false)
  })

  it('editar abre el formulario con los datos actuales y guarda con PUT', async () => {
    authedApi.put.mockResolvedValue({ ...myReview, rating: 5, title: 'Cambié de opinión' })
    const { wrapper } = await mountSection({ authenticated: true, mine: myReview })

    await wrapper.get('[data-testid="edit-review"]').trigger('click')
    expect(wrapper.get('#review-title').element.value).toBe('Mi opinión')

    await fillAndSubmit(wrapper, { stars: 5, title: 'Cambié de opinión', comment: 'Buen producto' })

    expect(authedApi.put).toHaveBeenCalledWith('/api/reviews/mine-1', { rating: 5, title: 'Cambié de opinión', comment: 'Buen producto' })
    expect(wrapper.text()).toContain('Cambié de opinión')
  })

  it('cancelar la edición vuelve a mostrar la reseña sin llamar a la API', async () => {
    const { wrapper } = await mountSection({ authenticated: true, mine: myReview })

    await wrapper.get('[data-testid="edit-review"]').trigger('click')
    await wrapper.get('[data-testid="cancel-edit"]').trigger('click')

    expect(wrapper.find('[data-testid="review-form"]').exists()).toBe(false)
    expect(authedApi.put).not.toHaveBeenCalled()
  })

  it('eliminar pide confirmación y recién entonces llama a DELETE', async () => {
    authedApi.delete.mockResolvedValue(null)
    const { wrapper } = await mountSection({ authenticated: true, mine: myReview })

    await wrapper.get('[data-testid="delete-review"]').trigger('click')
    expect(authedApi.delete).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('¿Seguro')

    await wrapper.get('[data-testid="confirm-delete"]').trigger('click')
    await flushPromises()

    expect(authedApi.delete).toHaveBeenCalledWith('/api/reviews/mine-1')
    expect(wrapper.find('[data-testid="review-form"]').exists()).toBe(true)
  })
})
