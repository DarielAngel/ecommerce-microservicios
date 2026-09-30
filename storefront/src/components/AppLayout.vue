<script setup>
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { useCartStore } from '../stores/cart'
import { useApi } from '../api/useApi'

const auth = useAuthStore()
const cart = useCartStore()
const router = useRouter()
const apiClient = useApi()

async function refreshCartBadge() {
  if (!auth.isAuthenticated) return
  try {
    cart.setCart(await apiClient.get('/api/cart'))
  } catch {
    // silencioso: el badge del carrito no es crítico, no interrumpimos la navegación por esto
  }
}

onMounted(refreshCartBadge)

function logout() {
  auth.logout()
  cart.clear()
  router.push({ name: 'home' })
}
</script>

<template>
  <div class="min-h-screen flex flex-col bg-gray-50">
    <header class="bg-white border-b border-gray-200 sticky top-0 z-10">
      <div class="max-w-6xl mx-auto px-4 py-3 flex items-center justify-between gap-4">
        <router-link :to="{ name: 'home' }" class="text-lg font-semibold text-gray-900">
          🛍️ Tienda
        </router-link>

        <nav class="flex items-center gap-4 text-sm">
          <router-link v-if="auth.isAuthenticated" :to="{ name: 'orders' }" class="text-gray-600 hover:text-gray-900">
            Mis pedidos
          </router-link>
          <router-link :to="{ name: 'cart' }" class="relative text-gray-600 hover:text-gray-900">
            🛒 Carrito
            <span v-if="cart.itemCount > 0"
              class="absolute -top-2 -right-3 bg-brand-600 text-white text-[10px] font-medium rounded-full w-4 h-4 flex items-center justify-center">
              {{ cart.itemCount }}
            </span>
          </router-link>

          <template v-if="auth.isAuthenticated">
            <span class="text-gray-500 hidden sm:inline">Hola, {{ auth.fullName }}</span>
            <button @click="logout" class="text-gray-600 hover:text-gray-900">Salir</button>
          </template>
          <template v-else>
            <router-link :to="{ name: 'login' }" class="text-gray-600 hover:text-gray-900">Iniciar sesión</router-link>
            <router-link :to="{ name: 'register' }"
              class="bg-brand-600 hover:bg-brand-700 text-white rounded-lg px-3 py-1.5 font-medium">
              Registrarme
            </router-link>
          </template>
        </nav>
      </div>
    </header>

    <main class="flex-1 max-w-6xl mx-auto w-full px-4 py-6">
      <router-view />
    </main>

    <footer class="text-center text-xs text-gray-400 py-6">
      Proyecto de demostración — Ecommerce Microservicios
    </footer>
  </div>
</template>
