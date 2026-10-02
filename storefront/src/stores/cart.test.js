import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useCartStore } from './cart'

describe('useCartStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('arranca sin carrito y con itemCount en 0', () => {
    const cart = useCartStore()

    expect(cart.cart).toBeNull()
    expect(cart.itemCount).toBe(0)
  })

  it('setCart guarda el carrito y expone totalItemCount como itemCount', () => {
    const cart = useCartStore()

    cart.setCart({ userId: 'u1', items: [{ variantId: 'v1', quantity: 3 }], subtotal: 30, totalItemCount: 3 })

    expect(cart.itemCount).toBe(3)
    expect(cart.cart.items).toHaveLength(1)
  })

  it('clear vuelve a dejar el carrito en null', () => {
    const cart = useCartStore()
    cart.setCart({ userId: 'u1', items: [], subtotal: 0, totalItemCount: 0 })

    cart.clear()

    expect(cart.cart).toBeNull()
    expect(cart.itemCount).toBe(0)
  })
})
