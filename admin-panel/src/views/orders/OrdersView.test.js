import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'

const { client } = vi.hoisted(() => ({ client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() } }))
vi.mock('../../api/useApi', () => ({ useApi: () => client }))

import OrdersView from './OrdersView.vue'

const paid = {
  orderId: 'abcdef12-0000', userId: 'u1', userEmail: 'ana@test.com', userFullName: 'Ana', shippingAddress: 'Calle 1',
  status: 'Paid', totalAmount: 51, createdAtUtc: '2026-10-08T10:00:00Z', discountAmount: 0, couponCode: null,
  lines: [{ variantId: 'v1', productName: 'Taza', sku: 'TZ', quantity: 2, lineTotal: 51 }],
  refundedAmount: 0, hasPendingCancellation: false
}

async function mountView(list) {
  client.get.mockResolvedValue(list)
  const wrapper = mount(OrdersView)
  await flushPromises()
  return wrapper
}

describe('OrdersView', () => {
  beforeEach(() => {
    for (const fn of Object.values(client)) fn.mockReset()
  })

  it('cancela un pedido pagado con reembolso total, con confirmación', async () => {
    const wrapper = await mountView([paid])
    client.post.mockResolvedValue({ ...paid, status: 'Cancelled', refundedAmount: 51 })

    await wrapper.get('[data-testid="cancel-order"]').trigger('click')
    await wrapper.get('[data-testid="cancel-order-form"] input').setValue('Sin stock real')
    expect(wrapper.get('[data-testid="cancel-order-confirm"]').text()).toBe('Cancelar y reembolsar $51.00')
    await wrapper.get('[data-testid="cancel-order-confirm"]').trigger('click')
    await flushPromises()

    expect(client.post).toHaveBeenCalledWith('/api/orders/abcdef12-0000/cancel', { reason: 'Other', comment: 'Sin stock real' })
    expect(wrapper.text()).toContain('cancelado: se reembolsaron $51.00')
    expect(client.get).toHaveBeenCalledTimes(2)
  })

  it('con una cancelación pedida por el cliente no ofrece "Marcar enviada"', async () => {
    const wrapper = await mountView([{ ...paid, hasPendingCancellation: true }])

    expect(wrapper.get('[data-testid="pending-cancellation"]').text()).toBe('Cancelación pedida')
    expect(wrapper.text()).not.toContain('Marcar enviada')
    expect(wrapper.get('[data-testid="cancel-order"]').text()).toBe('Aprobar cancelación')
  })
})
