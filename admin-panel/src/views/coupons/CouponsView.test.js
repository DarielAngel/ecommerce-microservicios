import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'

const { client } = vi.hoisted(() => ({ client: { get: vi.fn(), post: vi.fn(), put: vi.fn() } }))
vi.mock('../../api/useApi', () => ({ useApi: () => client }))

import CouponsView from './CouponsView.vue'

const coupon = {
  id: 'c1', code: 'VERANO10', description: 'Verano', type: 'Percentage', value: 10, maxDiscountAmount: 30,
  minimumSubtotal: 25, startsAtUtc: null, endsAtUtc: null, usageLimit: 100, oncePerCustomer: true,
  isActive: true, timesUsed: 12, activeReservations: 0
}

async function mountView() {
  const wrapper = mount(CouponsView)
  await flushPromises()
  return wrapper
}

describe('CouponsView', () => {
  beforeEach(() => {
    client.get.mockReset().mockResolvedValue([coupon])
    client.post.mockReset().mockResolvedValue({})
    client.put.mockReset().mockResolvedValue({})
  })

  it('lista los cupones con su descuento, usos y estado', async () => {
    const wrapper = await mountView()
    const row = wrapper.get('[data-testid="coupon-row"]')

    expect(row.text()).toContain('VERANO10')
    expect(row.text()).toContain('10 % (máx. $30.00)')
    expect(row.text()).toContain('12 / 100')
    expect(row.text()).toContain('1 por cliente')
    expect(row.text()).toContain('Activo')
  })

  it('crea un cupón enviando el código en mayúsculas y recarga la lista', async () => {
    const wrapper = await mountView()

    await wrapper.get('#coupon-code').setValue('black-friday')
    await wrapper.get('#coupon-description').setValue('Black Friday')
    await wrapper.get('#coupon-type').setValue('FixedAmount')
    await wrapper.get('#coupon-value').setValue('15')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(client.post).toHaveBeenCalledWith('/api/coupons', expect.objectContaining({
      code: 'BLACK-FRIDAY', type: 'FixedAmount', value: 15, maxDiscountAmount: null
    }))
    expect(client.get).toHaveBeenCalledTimes(2)
    expect(wrapper.text()).toContain('Cupón BLACK-FRIDAY creado.')
  })

  it('muestra el error de la API sin perder lo escrito', async () => {
    client.post.mockRejectedValue(new Error('Ya existe un cupón con el código "VERANO10".'))
    const wrapper = await mountView()

    await wrapper.get('#coupon-code').setValue('verano10')
    await wrapper.get('#coupon-description').setValue('Repetido')
    await wrapper.get('#coupon-value').setValue('5')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[role="alert"]').text()).toContain('Ya existe')
    expect(wrapper.get('#coupon-code').element.value).toBe('verano10')
  })

  it('editar carga el cupón en el formulario (código bloqueado) y guarda con PUT', async () => {
    const wrapper = await mountView()

    await wrapper.findAll('button').find((b) => b.text() === 'Editar').trigger('click')
    expect(wrapper.get('#coupon-code').element.disabled).toBe(true)
    expect(wrapper.get('#coupon-value').element.value).toBe('10')

    await wrapper.get('#coupon-value').setValue('15')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(client.put).toHaveBeenCalledWith('/api/coupons/c1', expect.objectContaining({ code: 'VERANO10', value: 15, usageLimit: 100 }))
  })

  it('pausar envía el mismo cupón con isActive en false', async () => {
    const wrapper = await mountView()

    await wrapper.findAll('button').find((b) => b.text() === 'Pausar').trigger('click')
    await flushPromises()

    expect(client.put).toHaveBeenCalledWith('/api/coupons/c1', expect.objectContaining({ isActive: false, value: 10 }))
  })
})
