import { defineStore } from 'pinia'

// El carrito real vive en el servidor (Módulo 5) — este store solo cachea el último
// resultado conocido para que el ícono del header no tenga que refetchear todo el tiempo.
export const useCartStore = defineStore('cart', {
  state: () => ({
    cart: null // { userId, items: [...], subtotal, totalItemCount }
  }),

  getters: {
    itemCount: (state) => state.cart?.totalItemCount ?? 0
  },

  actions: {
    setCart(cart) {
      this.cart = cart
    },
    clear() {
      this.cart = null
    }
  }
})
