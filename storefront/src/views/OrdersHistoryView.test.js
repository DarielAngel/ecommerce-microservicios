import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises, RouterLinkStub } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

const { client, push } = vi.hoisted(() => ({
  client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  push: vi.fn()
}))
vi.mock('../api/useApi', () => ({ useApi: () => client }))
vi.mock('vue-router', () => ({ useRouter: () => ({ push }) }))

import OrdersHistoryView from './OrdersHistoryView.vue'
import { useCartStore } from '../stores/cart'
import { useToastStore } from '../stores/toast'

const shipped = {
  orderId: 'bbbbbbbb-0000', status: 'Shipped', totalAmount: 36, subtotal: 36, discountAmount: 0, couponCode: null,
  createdAtUtc: '2026-10-08T10:00:00Z', paidAtUtc: '2026-10-08T10:01:00Z', shippedAtUtc: '2026-10-09T09:00:00Z',
  estimatedDeliveryFrom: '2026-10-12', estimatedDeliveryTo: '2026-10-14',
  shippingAddress: 'Ana Martínez, Calle 1, Lima, Perú',
  lines: [
    { variantId: 'v1', productId: 'p1', productName: 'Taza', quantity: 2, lineTotal: 16 },
    { variantId: 'v2', productId: 'p2', productName: 'Plato', quantity: 1, lineTotal: 20 }
  ]
}
const pending = {
  orderId: 'aaaaaaaa-0000', status: 'PendingPayment', totalAmount: 8, subtotal: 8, discountAmount: 0, couponCode: null,
  createdAtUtc: '2026-10-07T10:00:00Z', paidAtUtc: null, shippedAtUtc: null, estimatedDeliveryFrom: null, estimatedDeliveryTo: null,
  shippingAddress: 'Calle 1', lines: [{ variantId: 'v1', productId: 'p1', productName: 'Taza', quantity: 1, lineTotal: 8 }]
}

async function mountView(list) {
  client.get.mockResolvedValue(list)
  const pinia = createPinia()
  setActivePinia(pinia)
  const wrapper = mount(OrdersHistoryView, { global: { plugins: [pinia], stubs: { RouterLink: RouterLinkStub } } })
  await flushPromises()
  return wrapper
}

const orderItems = (w) => w.findAll('[data-testid="order-item"]')

describe('OrdersHistoryView', () => {
  beforeEach(() => {
    for (const fn of [client.get, client.post, push]) fn.mockReset()
  })

  it('ordena del más nuevo al más viejo y muestra la entrega estimada en el resumen', async () => {
    const wrapper = await mountView([pending, shipped])

    expect(orderItems(wrapper)[0].text()).toContain('#bbbbbbbb')
    expect(orderItems(wrapper)[0].get('[data-testid="order-delivery"]').text()).toMatch(/^Llega entre el/)
    expect(orderItems(wrapper)[1].find('[data-testid="order-delivery"]').exists()).toBe(false)
  })

  it('al abrir un pedido enviado muestra la línea de tiempo, la dirección y "Comprar de nuevo"', async () => {
    const wrapper = await mountView([shipped])
    await orderItems(wrapper)[0].get('button').trigger('click')

    const steps = wrapper.findAll('[data-testid="order-step"]')
    expect(steps.map((s) => s.attributes('data-state'))).toEqual(['done', 'done', 'done', 'current'])
    expect(steps[3].text()).toContain('(estimado)')
    expect(wrapper.get('[data-testid="order-address"]').text()).toBe(shipped.shippingAddress)
    expect(wrapper.find('[data-testid="buy-again"]').exists()).toBe(true)
  })

  it('un pedido esperando el pago no ofrece "Comprar de nuevo"', async () => {
    const wrapper = await mountView([pending])
    await orderItems(wrapper)[0].get('button').trigger('click')

    expect(wrapper.find('[data-testid="buy-again"]').exists()).toBe(false)
  })

  it('"Comprar de nuevo" agrega las líneas, actualiza el carrito y lleva al carrito avisando lo que faltó', async () => {
    const wrapper = await mountView([shipped])
    client.post.mockImplementation(async (path, body) => {
      if (body.variantId === 'v2') throw Object.assign(new Error('Este producto ya no está disponible.'), { status: 404 })
      return { items: [], totalItemCount: body.quantity }
    })

    await orderItems(wrapper)[0].get('button').trigger('click')
    await wrapper.get('[data-testid="buy-again"]').trigger('click')
    await flushPromises()

    expect(client.post).toHaveBeenCalledWith('/api/cart/items', { variantId: 'v1', quantity: 2 })
    expect(useCartStore().itemCount).toBe(2)
    expect(push).toHaveBeenCalledWith({ name: 'cart' })
    expect(useToastStore().toasts.at(-1).message).toBe('Agregamos 1 producto al carrito. Ya no está disponible: Plato.')
  })

  it('si nada está disponible, avisa y no cambia de pantalla', async () => {
    const wrapper = await mountView([shipped])
    client.post.mockRejectedValue(Object.assign(new Error('no'), { status: 404 }))

    await orderItems(wrapper)[0].get('button').trigger('click')
    await wrapper.get('[data-testid="buy-again"]').trigger('click')
    await flushPromises()

    expect(push).not.toHaveBeenCalled()
    expect(useToastStore().toasts.at(-1)).toMatchObject({ type: 'error', message: 'Ninguno de estos productos está disponible ahora.' })
  })

  it('muestra los puntos usados y los que suma un pedido pagado', async () => {
    const wrapper = await mountView([{ ...shipped, totalAmount: 31.5, loyaltyPoints: 500, loyaltyDiscount: 5 }, pending])

    expect(orderItems(wrapper)[0].get('[data-testid="order-points-used"]').text()).toBe('· 500 puntos (−$5.00)')
    expect(orderItems(wrapper)[0].get('[data-testid="order-points-earned"]').text()).toBe('· +31 puntos')
    expect(orderItems(wrapper)[1].find('[data-testid="order-points-earned"]').exists()).toBe(false)
  })
})
