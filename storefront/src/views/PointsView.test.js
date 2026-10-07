import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises, RouterLinkStub } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

const { client } = vi.hoisted(() => ({ client: { get: vi.fn() } }))
vi.mock('../api/useApi', () => ({ useApi: () => client }))

import PointsView from './PointsView.vue'

const rules = { pointsPerDollar: 1, pointValue: 0.01, minRedeemPoints: 100, maxRedeemShare: 0.5 }

async function mountView(summary) {
  client.get.mockReset().mockResolvedValue(summary)
  setActivePinia(createPinia())
  const wrapper = mount(PointsView, { global: { stubs: { RouterLink: RouterLinkStub } } })
  await flushPromises()
  return wrapper
}

describe('PointsView', () => {
  beforeEach(() => client.get.mockReset())

  it('muestra el saldo, su valor y el historial', async () => {
    const wrapper = await mountView({
      balance: 450, balanceValue: 4.5, rules,
      history: [
        { orderId: 'bbbbbbbb-1', kind: 'Redeemed', points: 300, discountAmount: 3, status: 'Confirmed', createdAtUtc: '2026-10-08T10:00:00Z' },
        { orderId: 'aaaaaaaa-1', kind: 'Earned', points: 750, discountAmount: 0, status: 'Confirmed', createdAtUtc: '2026-10-07T10:00:00Z' }
      ]
    })

    expect(client.get).toHaveBeenCalledWith('/api/loyalty/me')
    expect(wrapper.get('[data-testid="points-balance-value"]').text()).toBe('450')
    expect(wrapper.get('[data-testid="points-balance"]').text()).toContain('$4.50')
    const entries = wrapper.findAll('[data-testid="points-entry"]')
    expect(entries[0].text()).toContain('Usados en el pedido #bbbbbbbb')
    expect(entries[0].text()).toContain('−300')
    expect(entries[1].text()).toContain('+750')
  })

  it('sin movimientos invita a comprar y dice cuánto falta para el mínimo', async () => {
    const wrapper = await mountView({ balance: 0, balanceValue: 0, rules, history: [] })

    expect(wrapper.find('[data-testid="points-empty"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Te faltan 100 para poder usarlos')
  })
})
