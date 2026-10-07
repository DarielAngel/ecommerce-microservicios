import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'

const { client } = vi.hoisted(() => ({
  client: { get: vi.fn(), put: vi.fn(), delete: vi.fn() }
}))
vi.mock('../api/useApi', () => ({ useApi: () => client }))

import FavoriteButton from './FavoriteButton.vue'
import { useAuthStore } from '../stores/auth'
import { useWishlistStore } from '../stores/wishlist'
import { useToastStore } from '../stores/toast'

const stub = { template: '<div />' }

async function mountButton({ loggedIn = true, favorites = [] } = {}) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'home', component: stub },
      { path: '/products/:id', name: 'product-detail', component: stub },
      { path: '/login', name: 'login', component: stub }
    ]
  })
  router.push('/products/p1')
  await router.isReady()

  const pinia = createPinia()
  setActivePinia(pinia)
  if (loggedIn) useAuthStore().accessToken = 'token'
  useWishlistStore().productIds = favorites

  const wrapper = mount(FavoriteButton, { props: { productId: 'p1' }, global: { plugins: [pinia, router] } })
  return { wrapper, router }
}

describe('FavoriteButton', () => {
  beforeEach(() => {
    localStorage.clear()
    client.put.mockReset().mockResolvedValue(null)
    client.delete.mockReset().mockResolvedValue(null)
  })

  it('sin favorito: corazón vacío y etiqueta "Agregar a favoritos"', async () => {
    const { wrapper } = await mountButton()
    const button = wrapper.get('[data-testid="favorite-toggle"]')

    expect(button.attributes('aria-pressed')).toBe('false')
    expect(button.attributes('aria-label')).toBe('Agregar a favoritos')
    expect(wrapper.get('svg').attributes('fill')).toBe('none')
  })

  it('con favorito: corazón lleno y etiqueta "Quitar de favoritos"', async () => {
    const { wrapper } = await mountButton({ favorites: ['p1'] })
    const button = wrapper.get('[data-testid="favorite-toggle"]')

    expect(button.attributes('aria-pressed')).toBe('true')
    expect(button.attributes('aria-label')).toBe('Quitar de favoritos')
  })

  it('al hacer clic lo agrega y avisa con un toast', async () => {
    const { wrapper } = await mountButton()

    await wrapper.get('[data-testid="favorite-toggle"]').trigger('click')
    await flushPromises()

    expect(client.put).toHaveBeenCalledWith('/api/wishlist/p1')
    expect(wrapper.get('[data-testid="favorite-toggle"]').attributes('aria-pressed')).toBe('true')
    expect(useToastStore().toasts.at(-1).message).toBe('Agregado a tus favoritos')
  })

  it('si el servidor falla, vuelve al estado anterior y muestra el error', async () => {
    client.put.mockRejectedValue(new Error('Tu lista de favoritos está llena'))
    const { wrapper } = await mountButton()

    await wrapper.get('[data-testid="favorite-toggle"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="favorite-toggle"]').attributes('aria-pressed')).toBe('false')
    const toast = useToastStore().toasts.at(-1)
    expect(toast.type).toBe('error')
    expect(toast.message).toContain('llena')
  })

  it('un invitado va a iniciar sesión y vuelve a la misma página', async () => {
    const { wrapper, router } = await mountButton({ loggedIn: false })

    await wrapper.get('[data-testid="favorite-toggle"]').trigger('click')
    await flushPromises()

    expect(client.put).not.toHaveBeenCalled()
    expect(router.currentRoute.value.name).toBe('login')
    expect(router.currentRoute.value.query.redirect).toBe('/products/p1')
  })
})
