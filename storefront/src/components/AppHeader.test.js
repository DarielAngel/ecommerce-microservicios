import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import AppHeader from './AppHeader.vue'
import { useAuthStore } from '../stores/auth'
import { useCartStore } from '../stores/cart'
import { useWishlistStore } from '../stores/wishlist'

const stub = { template: '<div />' }

async function mountHeader() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'home', component: stub },
      { path: '/cart', name: 'cart', component: stub },
      { path: '/login', name: 'login', component: stub },
      { path: '/register', name: 'register', component: stub },
      { path: '/orders', name: 'orders', component: stub },
      { path: '/favorites', name: 'wishlist', component: stub },
      { path: '/addresses', name: 'addresses', component: stub }
    ]
  })
  router.push('/')
  await router.isReady()
  const pinia = createPinia()
  setActivePinia(pinia)
  const wrapper = mount(AppHeader, { global: { plugins: [pinia, router] } })
  return { wrapper, router, pinia }
}

describe('AppHeader', () => {
  beforeEach(() => {
    localStorage.clear()
    window.matchMedia = vi.fn().mockReturnValue({ matches: false, addEventListener: vi.fn(), removeEventListener: vi.fn() })
  })

  it('muestra el nombre de la tienda', async () => {
    const { wrapper } = await mountHeader()
    expect(wrapper.text()).toContain('Tienda')
  })

  it('un invitado ve Iniciar sesión y Registrarme, y no ve Mis pedidos', async () => {
    const { wrapper } = await mountHeader()

    expect(wrapper.text()).toContain('Iniciar sesión')
    expect(wrapper.text()).toContain('Registrarme')
    expect(wrapper.text()).not.toContain('Mis pedidos')
  })

  it('un usuario autenticado ve su nombre y Mis pedidos, y no ve Registrarme', async () => {
    const { wrapper, pinia } = await mountHeader()
    const auth = useAuthStore(pinia)
    auth.accessToken = 'token'
    auth.user = { fullName: 'Ana Pérez' }
    await flushPromises()

    expect(wrapper.text()).toContain('Ana Pérez')
    expect(wrapper.text()).toContain('Mis pedidos')
    expect(wrapper.text()).not.toContain('Registrarme')
  })

  it('buscar navega a la portada con ?q=', async () => {
    const { wrapper, router } = await mountHeader()

    await wrapper.get('input[type="search"]').setValue('zapatos')
    await wrapper.get('form[role="search"]').trigger('submit')
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('home')
    expect(router.currentRoute.value.query.q).toBe('zapatos')
  })

  it('buscar con texto vacío navega a la portada sin q', async () => {
    const { wrapper, router } = await mountHeader()
    await router.push({ name: 'home', query: { q: 'viejo' } })

    await wrapper.get('input[type="search"]').setValue('   ')
    await wrapper.get('form[role="search"]').trigger('submit')
    await flushPromises()

    expect(router.currentRoute.value.query.q).toBeUndefined()
  })

  it('muestra el contador del carrito cuando hay ítems', async () => {
    const { wrapper, pinia } = await mountHeader()
    useCartStore(pinia).setCart({ items: [], subtotal: 0, totalItemCount: 3 })
    await flushPromises()

    expect(wrapper.get('[data-testid="cart-badge"]').text()).toBe('3')
  })

  it('no muestra el contador si el carrito está vacío', async () => {
    const { wrapper } = await mountHeader()
    expect(wrapper.find('[data-testid="cart-badge"]').exists()).toBe(false)
  })

  it('el nombre del cliente lleva a Mis direcciones', async () => {
    const { wrapper, router } = await mountHeader()
    useAuthStore().$patch({ accessToken: 'token', user: { fullName: 'Ana Pérez' } })
    await flushPromises()

    const link = wrapper.get('[data-testid="account-link"]')
    expect(link.text()).toBe('Ana Pérez')
    await link.trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.name).toBe('addresses')
  })

  it('Salir cierra la sesión y vuelve a la portada', async () => {
    const { wrapper, router, pinia } = await mountHeader()
    const auth = useAuthStore(pinia)
    auth.accessToken = 'token'
    auth.user = { fullName: 'Ana' }
    await router.push({ name: 'orders' })
    await flushPromises()

    await wrapper.get('[data-testid="logout"]').trigger('click')
    await flushPromises()

    expect(auth.accessToken).toBeNull()
    expect(router.currentRoute.value.name).toBe('home')
  })

  it('el enlace a Mis favoritos solo aparece con sesión, con el contador de favoritos', async () => {
    const { wrapper: guest } = await mountHeader()
    expect(guest.find('[data-testid="wishlist-link"]').exists()).toBe(false)

    const { wrapper } = await mountHeader()
    useAuthStore().accessToken = 'token'
    useWishlistStore().productIds = ['a', 'b', 'c']
    await flushPromises()

    expect(wrapper.find('[data-testid="wishlist-link"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="wishlist-badge"]').text()).toBe('3')
  })

  it('al salir, se vacían los favoritos', async () => {
    const { wrapper } = await mountHeader()
    useAuthStore().accessToken = 'token'
    useWishlistStore().productIds = ['a']
    await flushPromises()

    await wrapper.get('[data-testid="logout"]').trigger('click')

    expect(useWishlistStore().count).toBe(0)
  })
})
