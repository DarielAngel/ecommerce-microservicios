<script setup>
import { onMounted, watch } from 'vue'
import { useAuthStore } from '../stores/auth'
import { useCartStore } from '../stores/cart'
import { useWishlistStore } from '../stores/wishlist'
import { useApi } from '../api/useApi'
import AppHeader from './AppHeader.vue'
import AppFooter from './AppFooter.vue'
import ToastHost from './ToastHost.vue'
import PwaPrompts from './PwaPrompts.vue'

const auth = useAuthStore()
const cart = useCartStore()
const wishlist = useWishlistStore()
const apiClient = useApi()

// El contador del carrito del encabezado se sincroniza al abrir la app y cada vez que
// el usuario inicia o cierra sesión.
async function refreshCartBadge() {
  if (!auth.isAuthenticated) return
  try {
    cart.setCart(await apiClient.get('/api/cart'))
  } catch {
    // No es crítico: el contador es una ayuda visual, no interrumpimos la navegación por esto.
  }
}

// Lo mismo con los favoritos: los corazones de todo el sitio dependen de esta lista.
async function refreshWishlist() {
  if (!auth.isAuthenticated) return
  try {
    await wishlist.load()
  } catch {
    // Sin favoritos cargados los corazones se ven vacíos; no bloqueamos la tienda por eso.
  }
}

onMounted(() => {
  refreshCartBadge()
  refreshWishlist()
})
watch(() => auth.isAuthenticated, (isLoggedIn) => {
  if (isLoggedIn) {
    refreshCartBadge()
    refreshWishlist()
  } else {
    cart.clear()
    wishlist.clear()
  }
})
</script>

<template>
  <div class="flex min-h-screen flex-col bg-canvas text-ink">
    <PwaPrompts />
    <AppHeader />
    <main class="mx-auto w-full max-w-6xl flex-1 px-4 py-6">
      <router-view />
    </main>
    <AppFooter />
    <ToastHost />
  </div>
</template>
