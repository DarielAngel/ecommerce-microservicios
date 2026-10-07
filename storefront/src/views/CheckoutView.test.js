import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises, RouterLinkStub } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

const { client, push, replace } = vi.hoisted(() => ({
  client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  push: vi.fn(),
  replace: vi.fn()
}))
vi.mock('../api/useApi', () => ({ useApi: () => client }))
vi.mock('vue-router', () => ({ useRouter: () => ({ push, replace }) }))

import CheckoutView from './CheckoutView.vue'
import { useCartStore } from '../stores/cart'
import { useAuthStore } from '../stores/auth'

const casa = { id: 'a1', label: 'Casa', isDefault: false, formatted: 'Ana Martínez, Calle 1, Lima, Perú' }
const oficina = { id: 'a2', label: 'Oficina', isDefault: true, formatted: 'Ana Martínez, Av. 2, Lima, Perú' }

function givenAddresses(list) {
  client.get.mockImplementation(async (path) => {
    if (path === '/api/addresses') return list
    throw new Error(`GET inesperado: ${path}`)
  })
}

async function mountView() {
  const pinia = createPinia()
  setActivePinia(pinia)
  useAuthStore().$patch({ user: { fullName: 'Ana Martínez' } })
  useCartStore().setCart({
    items: [{ variantId: 'v1', productName: 'Taza', quantity: 2, lineTotal: 16 }],
    subtotal: 16, totalItemCount: 2
  })
  const wrapper = mount(CheckoutView, {
    global: { plugins: [pinia], stubs: { RouterLink: RouterLinkStub, CouponField: true } }
  })
  await flushPromises()
  return wrapper
}

const checkoutBody = () => client.post.mock.calls.find(([path]) => path === '/api/orders/checkout')?.[1]

describe('CheckoutView — dirección de envío', () => {
  beforeEach(() => {
    for (const fn of [client.get, client.post, push, replace]) fn.mockReset()
    sessionStorage.setItem('checkout-variant-ids', JSON.stringify(['v1']))
    vi.spyOn(window, 'open').mockReturnValue(null)
    client.post.mockImplementation(async (path, body) => {
      if (path === '/api/orders/checkout') return { orderId: 'o1', approveUrl: null }
      if (path === '/api/addresses') return { id: 'nueva', label: body.label, isDefault: true, formatted: 'Ana Martínez, Calle Nueva 9, Quito, Ecuador' }
      throw new Error(`POST inesperado: ${path}`)
    })
  })

  it('con direcciones guardadas, viene elegida la predeterminada y se paga con su texto', async () => {
    givenAddresses([oficina, casa])
    const wrapper = await mountView()

    const options = wrapper.findAll('[data-testid="checkout-address-option"] input')
    expect(options).toHaveLength(2)
    expect(options[0].element.checked).toBe(true)
    expect(wrapper.find('[data-testid="checkout-new-address"]').exists()).toBe(false)

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(checkoutBody()).toMatchObject({ variantIds: ['v1'], shippingAddress: oficina.formatted })
    expect(push).toHaveBeenCalledWith({ name: 'order-pending', params: { id: 'o1' } })
  })

  it('se puede elegir otra dirección de la libreta', async () => {
    givenAddresses([oficina, casa])
    const wrapper = await mountView()

    await wrapper.findAll('[data-testid="checkout-address-option"] input')[1].setValue(true)
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(checkoutBody().shippingAddress).toBe(casa.formatted)
  })

  it('sin libreta muestra el formulario (con quién recibe precargado), guarda la dirección y paga con ella', async () => {
    givenAddresses([])
    const wrapper = await mountView()

    expect(wrapper.get('#shipping-recipient').element.value).toBe('Ana Martínez')
    await wrapper.get('#shipping-street').setValue('Calle Nueva 9')
    await wrapper.get('#shipping-city').setValue('Quito')
    await wrapper.get('#shipping-country').setValue('Ecuador')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    const saved = client.post.mock.calls.find(([path]) => path === '/api/addresses')[1]
    expect(saved).toMatchObject({ street: 'Calle Nueva 9', city: 'Quito', country: 'Ecuador', makeDefault: true })
    expect(checkoutBody().shippingAddress).toBe('Ana Martínez, Calle Nueva 9, Quito, Ecuador')
  })

  it('una dirección nueva sin guardar viaja formateada y no toca la libreta', async () => {
    givenAddresses([oficina])
    const wrapper = await mountView()

    await wrapper.get('[data-testid="checkout-address-new"]').setValue(true)
    await wrapper.get('[data-testid="checkout-save-address"]').setValue(false)
    await wrapper.get('#shipping-street').setValue('Hotel Sol, hab. 12')
    await wrapper.get('#shipping-city').setValue('Cusco')
    await wrapper.get('#shipping-country').setValue('Perú')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(client.post.mock.calls.some(([path]) => path === '/api/addresses')).toBe(false)
    expect(checkoutBody().shippingAddress).toBe('Ana Martínez, Hotel Sol, hab. 12, Cusco, Perú')
  })

  it('dirección nueva incompleta: avisa y no llama al checkout', async () => {
    givenAddresses([])
    const wrapper = await mountView()

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('Completa la dirección de envío')
    expect(client.post).not.toHaveBeenCalled()
  })

  it('si no se pueden cargar las direcciones, igual se puede pagar escribiéndola', async () => {
    client.get.mockRejectedValue(new Error('Users caído'))
    const wrapper = await mountView()

    expect(wrapper.find('[data-testid="checkout-new-address"]').exists()).toBe(true)
  })
})
