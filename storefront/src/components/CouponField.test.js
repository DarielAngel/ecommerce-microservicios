import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

const { client } = vi.hoisted(() => ({ client: { get: vi.fn() } }))
vi.mock('../api/useApi', () => ({ useApi: () => client }))

import CouponField from './CouponField.vue'

const quote = { code: 'VERANO10', description: 'Verano: 10 % de descuento', subtotal: 80, discountAmount: 8, total: 72 }

function mountField(props = {}) {
  setActivePinia(createPinia())
  return mount(CouponField, { props: { subtotal: 80, modelValue: null, ...props } })
}

async function apply(wrapper, code) {
  await wrapper.get('#coupon-code').setValue(code)
  await wrapper.get('form').trigger('submit')
  await flushPromises()
}

describe('CouponField', () => {
  beforeEach(() => {
    client.get.mockReset()
  })

  it('el botón Aplicar está desactivado hasta que se escribe un código', async () => {
    const wrapper = mountField()
    expect(wrapper.get('[data-testid="coupon-apply"]').attributes('disabled')).toBeDefined()

    await wrapper.get('#coupon-code').setValue('verano10')
    expect(wrapper.get('[data-testid="coupon-apply"]').attributes('disabled')).toBeUndefined()
  })

  it('valida el código con el subtotal actual y emite el cupón aplicado', async () => {
    client.get.mockResolvedValue(quote)
    const wrapper = mountField()

    await apply(wrapper, ' verano10 ')

    expect(client.get).toHaveBeenCalledWith('/api/coupons/validate', { params: { code: 'verano10', subtotal: '80.00' } })
    expect(wrapper.emitted('update:modelValue').at(-1)).toEqual([quote])
  })

  it('si el cupón no aplica, muestra el motivo que da el servidor', async () => {
    client.get.mockRejectedValue(new Error('Este cupón requiere una compra mínima de $100.00 (tu compra: $80.00).'))
    const wrapper = mountField()

    await apply(wrapper, 'GRANDE100')

    expect(wrapper.get('[data-testid="coupon-error"]').text()).toContain('compra mínima de $100.00')
    expect(wrapper.emitted('update:modelValue').at(-1)).toEqual([null])
  })

  it('con un cupón aplicado muestra el código, el ahorro y permite quitarlo', async () => {
    const wrapper = mountField({ modelValue: quote })

    expect(wrapper.get('[data-testid="coupon-applied"]').text()).toContain('VERANO10')
    expect(wrapper.get('[data-testid="coupon-applied"]').text()).toContain('−$8.00')

    await wrapper.get('[data-testid="coupon-remove"]').trigger('click')
    expect(wrapper.emitted('update:modelValue').at(-1)).toEqual([null])
  })

  it('si cambia el subtotal, quita el cupón (el descuento calculado ya no vale)', async () => {
    const wrapper = mountField({ modelValue: quote })

    await wrapper.setProps({ subtotal: 50 })

    expect(wrapper.emitted('update:modelValue').at(-1)).toEqual([null])
  })
})
