import { defineStore } from 'pinia'
import { useApi } from '../api/useApi'
import { wishlistApi } from '../api/wishlist'

// Ids de los productos favoritos del cliente. Lo usan los corazones de todo el sitio, así que se
// carga una vez al iniciar sesión y después se mantiene al día de forma optimista en cada clic.
export const useWishlistStore = defineStore('wishlist', {
  state: () => ({
    productIds: [],
    loaded: false,
    pending: {} // productId -> true mientras su request está en vuelo (evita dobles clics)
  }),

  getters: {
    count: (state) => state.productIds.length,
    has: (state) => (productId) => state.productIds.includes(productId),
    isPending: (state) => (productId) => !!state.pending[productId]
  },

  actions: {
    async load() {
      const items = await wishlistApi.list(useApi())
      this.productIds = items.map((item) => item.productId)
      this.loaded = true
    },

    /// Marca o desmarca un favorito. Actualiza la pantalla al instante y, si el servidor falla,
    /// deshace el cambio y relanza el error para que quien llamó pueda avisar al cliente.
    async toggle(productId) {
      if (this.pending[productId]) return this.has(productId)

      const wasFavorite = this.has(productId)
      this.pending[productId] = true
      this.setLocal(productId, !wasFavorite)

      try {
        const client = useApi()
        if (wasFavorite) await wishlistApi.remove(client, productId)
        else await wishlistApi.add(client, productId)
        return !wasFavorite
      } catch (err) {
        this.setLocal(productId, wasFavorite)
        throw err
      } finally {
        delete this.pending[productId]
      }
    },

    async remove(productId) {
      if (!this.has(productId)) return
      await this.toggle(productId)
    },

    setLocal(productId, isFavorite) {
      if (isFavorite && !this.has(productId)) this.productIds.unshift(productId)
      if (!isFavorite) this.productIds = this.productIds.filter((id) => id !== productId)
    },

    clear() {
      this.productIds = []
      this.loaded = false
      this.pending = {}
    }
  }
})
