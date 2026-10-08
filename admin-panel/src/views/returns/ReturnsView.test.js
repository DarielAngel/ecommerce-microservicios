import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'

const { client } = vi.hoisted(() => ({ client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() } }))
vi.mock('../../api/useApi', () => ({ useApi: () => client }))

import ReturnsView from './ReturnsView.vue'

const pending = {
  returnId: 'r1', orderId: 'abcdef12-0000', status: 'Requested', reason: 'Damaged', comment: 'Llegó roto', adminNote: null,
  refundAmount: 16, loyaltyPointsToRestore: 200, completesOrder: false, createdAtUtc: '2026-10-08T10:00:00Z',
  lines: [{ variantId: 'v1', productName: 'Taza', unitPrice: 20, quantity: 1 }],
  userEmail: 'ana@test.com', userFullName: 'Ana', orderTotal: 40, orderRefundedAmount: 0
}

async function mountView(list = [pending]) {
  client.get.mockResolvedValue(list)
  const wrapper = mount(ReturnsView)
  await flushPromises()
  return wrapper
}

const lastListParams = () => client.get.mock.calls.at(-1)[1].params

describe('ReturnsView', () => {
  beforeEach(() => {
    for (const fn of Object.values(client)) fn.mockReset()
  })

  it('empieza por las pendientes y muestra cliente, productos, motivo y lo que se devolverá', async () => {
    const wrapper = await mountView()

    expect(client.get).toHaveBeenCalledWith('/api/orders/returns', { params: { status: 'Requested', count: 200 } })
    const row = wrapper.get('[data-testid="return-row"]')
    expect(row.text()).toContain('Pedido #ABCDEF12 · Ana')
    expect(row.text()).toContain('1× Taza')
    expect(row.text()).toContain('Llegó dañado o con fallas — "Llegó roto"')
    expect(row.get('[data-testid="return-amount"]').text()).toContain('$16.00')
    expect(row.get('[data-testid="return-amount"]').text()).toContain('+200 puntos usados')
    expect(row.get('[data-testid="return-approve"]').text()).toBe('Aprobar y reembolsar $16.00')
  })

  it('aprobar envía la nota opcional, avisa cuánto se reembolsó y recarga', async () => {
    const wrapper = await mountView()
    client.post.mockResolvedValue({ ...pending, status: 'Refunded' })

    await wrapper.get('[data-testid="return-note"]').setValue('Recibido en buen estado')
    await wrapper.get('[data-testid="return-approve"]').trigger('click')
    await flushPromises()

    expect(client.post).toHaveBeenCalledWith('/api/orders/returns/r1/approve', { note: 'Recibido en buen estado' })
    expect(wrapper.text()).toContain('reembolsada: $16.00 a ana@test.com')
    expect(client.get).toHaveBeenCalledTimes(2)
  })

  it('rechazar pide nota antes de enviar', async () => {
    const wrapper = await mountView()
    client.post.mockResolvedValue({ ...pending, status: 'Rejected' })

    await wrapper.get('[data-testid="return-reject"]').trigger('click')
    await wrapper.get('[data-testid="return-confirm-reject"]').trigger('click')
    expect(client.post).not.toHaveBeenCalled()
    expect(wrapper.get('[role="alert"]').text()).toMatch(/Escribe una nota/)

    await wrapper.get('[data-testid="return-note"]').setValue('El producto tiene uso')
    await wrapper.get('[data-testid="return-confirm-reject"]').trigger('click')
    await flushPromises()

    expect(client.post).toHaveBeenCalledWith('/api/orders/returns/r1/reject', { note: 'El producto tiene uso' })
    expect(wrapper.text()).toContain('rechazada. Le avisamos a ana@test.com')
  })

  it('si el reembolso falla muestra el motivo en la fila y recarga (queda en "Reembolso pendiente")', async () => {
    const wrapper = await mountView()
    client.post.mockRejectedValue(new Error('No se pudo reembolsar: PayPal rechazó el reembolso (422). La devolución quedó aprobada; puedes reintentar.'))
    client.get.mockResolvedValue([{ ...pending, status: 'Approved' }])

    await wrapper.get('[data-testid="return-approve"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="return-row"] [role="alert"]').text()).toContain('422')
    expect(wrapper.get('[data-testid="return-approve"]').text()).toBe('Reintentar reembolso')
  })

  it('cambia de sección con las pestañas', async () => {
    const wrapper = await mountView([])
    expect(wrapper.find('[data-testid="returns-empty"]').exists()).toBe(true)

    await wrapper.get('[data-testid="returns-tab-Refunded"]').trigger('click')
    await flushPromises()
    expect(lastListParams().status).toBe('Refunded')

    await wrapper.get('[data-testid="returns-tab-all"]').trigger('click')
    await flushPromises()
    expect(lastListParams().status).toBe('')
  })

  it('una aprobada sin reembolso se puede cancelar con una nota', async () => {
    const wrapper = await mountView([{ ...pending, status: 'Approved' }])
    client.post.mockResolvedValue({ ...pending, status: 'Rejected' })

    await wrapper.get('[data-testid="return-reject"]').trigger('click')
    expect(wrapper.get('[data-testid="return-reject"], [data-testid="return-confirm-reject"]').text()).toBe('Cancelar y avisar al cliente')
    await wrapper.get('[data-testid="return-note"]').setValue('PayPal no lo permite')
    await wrapper.get('[data-testid="return-confirm-reject"]').trigger('click')
    await flushPromises()

    expect(client.post).toHaveBeenCalledWith('/api/orders/returns/r1/reject', { note: 'PayPal no lo permite' })
  })
})
