<script setup>
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { useWishlistStore } from '../stores/wishlist'
import { useToastStore } from '../stores/toast'

const props = defineProps({
  productId: { type: String, required: true },
  size: { type: String, default: 'md' } // 'md' para tarjetas, 'lg' para el detalle
})

const auth = useAuthStore()
const wishlist = useWishlistStore()
const toast = useToastStore()
const router = useRouter()

const active = computed(() => wishlist.has(props.productId))
const label = computed(() => (active.value ? 'Quitar de favoritos' : 'Agregar a favoritos'))

async function onClick() {
  // Invitado: lo mandamos a iniciar sesión y lo traemos de vuelta a donde estaba.
  if (!auth.isAuthenticated) {
    router.push({ name: 'login', query: { redirect: router.currentRoute.value.fullPath } })
    return
  }

  try {
    const nowFavorite = await wishlist.toggle(props.productId)
    toast.push({
      type: 'success',
      message: nowFavorite ? 'Agregado a tus favoritos' : 'Quitado de tus favoritos',
      duration: 2500
    })
  } catch (err) {
    toast.push({ type: 'error', message: err.message || 'No pudimos actualizar tus favoritos.' })
  }
}
</script>

<template>
  <button type="button" data-testid="favorite-toggle" :aria-label="label" :title="label" :aria-pressed="active"
    :disabled="wishlist.isPending(productId)" @click.stop.prevent="onClick"
    class="flex items-center justify-center rounded-full border border-line bg-surface/90 shadow-card backdrop-blur transition hover:scale-105 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 disabled:opacity-60"
    :class="size === 'lg' ? 'h-11 w-11' : 'h-9 w-9'">
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" stroke-width="1.8" stroke="currentColor" aria-hidden="true"
      :fill="active ? 'currentColor' : 'none'"
      :class="[size === 'lg' ? 'h-6 w-6' : 'h-5 w-5', active ? 'text-rose-500' : 'text-ink-soft']">
      <path stroke-linecap="round" stroke-linejoin="round"
        d="M21 8.25c0-2.485-2.099-4.5-4.688-4.5-1.935 0-3.597 1.126-4.312 2.733-.715-1.607-2.377-2.733-4.313-2.733C5.1 3.75 3 5.765 3 8.25c0 7.22 9 12 9 12s9-4.78 9-12Z" />
    </svg>
  </button>
</template>
