<script setup>
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { useCartStore } from '../stores/cart'
import { useWishlistStore } from '../stores/wishlist'
import { STORE } from '../config'
import ThemeToggle from './ThemeToggle.vue'
import SearchBox from './SearchBox.vue'

const auth = useAuthStore()
const cart = useCartStore()
const wishlist = useWishlistStore()
const router = useRouter()

// El buscador (con sugerencias mientras se escribe) vive en SearchBox.

function logout() {
  auth.logout()
  cart.clear()
  wishlist.clear()
  router.push({ name: 'home' })
}
</script>

<template>
  <header class="sticky top-0 z-30 border-b border-line bg-surface/85 backdrop-blur">
    <div class="mx-auto flex max-w-6xl flex-wrap items-center gap-x-4 gap-y-2 px-4 py-3">
      <router-link :to="{ name: 'home' }" class="flex items-center gap-2 text-lg font-bold tracking-tight text-ink">
        <span class="flex h-8 w-8 items-center justify-center rounded-xl bg-brand-600 text-base text-white" aria-hidden="true">🛍️</span>
        <span>{{ STORE.name }}</span>
      </router-link>

      <SearchBox class="order-3 w-full sm:order-none sm:mx-2 sm:w-auto sm:flex-1" />

      <nav class="ml-auto flex items-center gap-1 text-sm sm:ml-0" aria-label="Principal">
        <router-link v-if="auth.isAuthenticated" :to="{ name: 'orders' }"
          class="hidden rounded-full px-3 py-2 text-ink-soft transition-colors hover:bg-surface-muted sm:inline-block">
          Mis pedidos
        </router-link>

        <router-link v-if="auth.isAuthenticated" :to="{ name: 'wishlist' }" data-testid="wishlist-link"
          class="relative rounded-full px-3 py-2 text-ink-soft transition-colors hover:bg-surface-muted" aria-label="Mis favoritos">
          <span class="inline-flex items-center gap-1.5">
            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.7" stroke="currentColor" class="h-5 w-5" aria-hidden="true">
              <path stroke-linecap="round" stroke-linejoin="round" d="M21 8.25c0-2.485-2.099-4.5-4.688-4.5-1.935 0-3.597 1.126-4.312 2.733-.715-1.607-2.377-2.733-4.313-2.733C5.1 3.75 3 5.765 3 8.25c0 7.22 9 12 9 12s9-4.78 9-12Z" />
            </svg>
            <span class="hidden lg:inline">Favoritos</span>
          </span>
          <span v-if="wishlist.count > 0" data-testid="wishlist-badge"
            class="absolute -right-0.5 -top-0.5 flex h-5 min-w-5 items-center justify-center rounded-full bg-rose-500 px-1 text-[11px] font-bold text-white">
            {{ wishlist.count }}
          </span>
        </router-link>

        <router-link :to="{ name: 'cart' }" class="relative rounded-full px-3 py-2 text-ink-soft transition-colors hover:bg-surface-muted"
          aria-label="Carrito">
          <span class="inline-flex items-center gap-1.5">
            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.7" stroke="currentColor" class="h-5 w-5" aria-hidden="true">
              <path stroke-linecap="round" stroke-linejoin="round" d="M2.25 3h1.386c.51 0 .955.343 1.087.835l.383 1.437M7.5 14.25a3 3 0 0 0-3 3h15.75m-12.75-3h11.218c1.121-2.3 2.1-4.684 2.924-7.138a60.114 60.114 0 0 0-16.536-1.84M7.5 14.25 5.106 5.272M6 20.25a.75.75 0 1 1-1.5 0 .75.75 0 0 1 1.5 0Zm12.75 0a.75.75 0 1 1-1.5 0 .75.75 0 0 1 1.5 0Z" />
            </svg>
            <span class="hidden sm:inline">Carrito</span>
          </span>
          <span v-if="cart.itemCount > 0" data-testid="cart-badge"
            class="absolute -right-0.5 -top-0.5 flex h-5 min-w-5 items-center justify-center rounded-full bg-accent-500 px-1 text-[11px] font-bold text-white">
            {{ cart.itemCount }}
          </span>
        </router-link>

        <ThemeToggle />

        <template v-if="auth.isAuthenticated">
          <!-- El nombre lleva a la cuenta del cliente: por ahora, su libreta de direcciones. -->
          <router-link :to="{ name: 'addresses' }" title="Mis direcciones" data-testid="account-link"
            class="ml-1 hidden max-w-[10rem] truncate rounded-full px-2 py-2 text-ink-soft transition-colors hover:bg-surface-muted md:inline">{{ auth.fullName }}</router-link>
          <button type="button" data-testid="logout" @click="logout"
            class="rounded-full px-3 py-2 text-ink-soft transition-colors hover:bg-surface-muted">Salir</button>
        </template>
        <template v-else>
          <router-link :to="{ name: 'login' }" class="rounded-full px-3 py-2 text-ink-soft transition-colors hover:bg-surface-muted">Iniciar sesión</router-link>
          <router-link :to="{ name: 'register' }"
            class="rounded-full bg-brand-600 px-4 py-2 font-medium text-white transition-colors hover:bg-brand-700">Registrarme</router-link>
        </template>
      </nav>
    </div>
  </header>
</template>
