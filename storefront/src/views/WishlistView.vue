<script setup>
import { ref, computed, onMounted } from 'vue'
import { api, ApiError } from '../api/client'
import { useApi } from '../api/useApi'
import { wishlistApi } from '../api/wishlist'
import { useWishlistStore } from '../stores/wishlist'
import { useCartStore } from '../stores/cart'
import { useToastStore } from '../stores/toast'
import { formatMoney } from '../utils/format'
import { imageUrl } from '../utils/images'
import { pickQuickAddVariant, priceLabelFor, primaryImageOf } from '../utils/wishlist'
import SkeletonCard from '../components/SkeletonCard.vue'

const apiClient = useApi()
const wishlist = useWishlistStore()
const cartStore = useCartStore()
const toast = useToastStore()

// Cada fila: { productId, addedAtUtc, product (o null si ya no existe) }
const rows = ref([])
const loading = ref(true)
const error = ref('')
const busy = ref({}) // productId -> 'cart' | 'remove'

// Si el cliente quita un favorito desde otra pestaña de la página (corazón), la fila desaparece.
const visibleRows = computed(() => rows.value.filter((r) => wishlist.has(r.productId)))

async function fetchProduct(productId) {
  try {
    return await api.get(`/api/products/${productId}`)
  } catch (err) {
    // 404 = el producto se eliminó del catálogo: lo mostramos como "ya no disponible".
    if (err instanceof ApiError && err.status === 404) return null
    throw err
  }
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const items = await wishlistApi.list(apiClient)
    wishlist.productIds = items.map((i) => i.productId)
    wishlist.loaded = true

    // Los detalles se piden en paralelo; si uno falla por red, no tiramos toda la página.
    const products = await Promise.allSettled(items.map((i) => fetchProduct(i.productId)))
    rows.value = items.map((item, index) => ({
      ...item,
      product: products[index].status === 'fulfilled' ? products[index].value : null,
      failed: products[index].status === 'rejected'
    }))
  } catch (err) {
    error.value = err.message
  } finally {
    loading.value = false
  }
}

async function removeRow(productId) {
  busy.value[productId] = 'remove'
  try {
    await wishlist.remove(productId)
  } catch (err) {
    toast.push({ type: 'error', message: err.message })
  } finally {
    delete busy.value[productId]
  }
}

/// "Mover" = agregar 1 unidad al carrito y, solo si eso funcionó, quitarlo de favoritos.
/// El carrito valida el stock: si no alcanza, el favorito se queda donde estaba.
async function moveToCart(row) {
  const variant = pickQuickAddVariant(row.product)
  if (!variant) return

  busy.value[row.productId] = 'cart'
  try {
    const cart = await apiClient.post('/api/cart/items', { variantId: variant.id, quantity: 1 })
    cartStore.setCart(cart)
    try {
      await wishlist.remove(row.productId)
    } catch {
      // Ya está en el carrito, que es lo importante; el favorito simplemente se queda.
    }
    toast.push({ type: 'success', message: `${row.product.name} se movió al carrito` })
  } catch (err) {
    toast.push({ type: 'error', message: err.message || 'No pudimos agregarlo al carrito.' })
  } finally {
    delete busy.value[row.productId]
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <div class="flex flex-wrap items-baseline justify-between gap-2">
      <h1 class="text-2xl font-bold tracking-tight text-ink">Mis favoritos</h1>
      <p v-if="!loading && !error && visibleRows.length" class="text-sm text-ink-muted" data-testid="wishlist-count">
        {{ visibleRows.length }} {{ visibleRows.length === 1 ? 'producto' : 'productos' }}
      </p>
    </div>

    <div v-if="loading" class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
      <SkeletonCard v-for="n in 3" :key="n" />
    </div>

    <div v-else-if="error" class="rounded-2xl border border-line bg-surface py-16 text-center text-red-600 dark:text-red-400">
      {{ error }}
    </div>

    <div v-else-if="visibleRows.length === 0" data-testid="wishlist-empty"
      class="rounded-2xl border border-dashed border-line bg-surface py-16 text-center">
      <p class="text-4xl" aria-hidden="true">🤍</p>
      <p class="mt-3 font-medium text-ink">Todavía no tienes favoritos</p>
      <p class="mt-1 text-sm text-ink-muted">Toca el corazón de un producto para guardarlo aquí.</p>
      <router-link :to="{ name: 'home' }"
        class="mt-4 inline-block rounded-full bg-brand-600 px-5 py-2 text-sm font-medium text-white hover:bg-brand-700">
        Explorar productos
      </router-link>
    </div>

    <ul v-else class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
      <li v-for="row in visibleRows" :key="row.productId" data-testid="wishlist-item"
        class="flex gap-4 rounded-2xl border border-line bg-surface p-3 shadow-card">
        <!-- Producto que ya no está en el catálogo (o que no pudimos cargar) -->
        <template v-if="!row.product">
          <div class="flex h-24 w-24 flex-shrink-0 items-center justify-center rounded-xl bg-surface-muted text-3xl" aria-hidden="true">📦</div>
          <div class="flex min-w-0 flex-1 flex-col">
            <p class="font-medium text-ink">{{ row.failed ? 'No pudimos cargar este producto' : 'Este producto ya no está disponible' }}</p>
            <button type="button" data-testid="wishlist-remove" :disabled="!!busy[row.productId]" @click="removeRow(row.productId)"
              class="mt-auto self-start text-sm font-medium text-ink-muted hover:text-red-600 disabled:opacity-50">
              Quitar
            </button>
          </div>
        </template>

        <template v-else>
          <router-link :to="{ name: 'product-detail', params: { id: row.productId } }"
            class="h-24 w-24 flex-shrink-0 overflow-hidden rounded-xl bg-surface-muted">
            <img v-if="primaryImageOf(row.product)" :src="imageUrl(primaryImageOf(row.product).fileName)" :alt="row.product.name"
              class="h-full w-full object-cover" loading="lazy" />
            <span v-else class="flex h-full w-full items-center justify-center text-3xl" aria-hidden="true">🛍️</span>
          </router-link>

          <div class="flex min-w-0 flex-1 flex-col">
            <router-link :to="{ name: 'product-detail', params: { id: row.productId } }"
              class="truncate font-medium text-ink hover:text-brand-ink">{{ row.product.name }}</router-link>
            <p v-if="priceLabelFor(row.product)" class="text-sm font-semibold text-brand-ink">
              <span v-if="priceLabelFor(row.product).from" class="font-normal text-ink-muted">Desde </span>{{ formatMoney(priceLabelFor(row.product).amount) }}
            </p>

            <div class="mt-auto flex flex-wrap items-center gap-x-3 gap-y-1 pt-2">
              <button v-if="pickQuickAddVariant(row.product)" type="button" data-testid="wishlist-move-to-cart"
                :disabled="!!busy[row.productId]" @click="moveToCart(row)"
                class="rounded-full bg-brand-600 px-3.5 py-1.5 text-sm font-medium text-white transition-colors hover:bg-brand-700 disabled:opacity-60">
                {{ busy[row.productId] === 'cart' ? 'Moviendo...' : 'Mover al carrito' }}
              </button>
              <router-link v-else-if="row.product.isActive !== false && row.product.variants?.length"
                :to="{ name: 'product-detail', params: { id: row.productId } }" data-testid="wishlist-choose-variant"
                class="rounded-full border border-brand-600 px-3.5 py-1.5 text-sm font-medium text-brand-ink hover:bg-brand-50 dark:hover:bg-brand-900/30">
                Elegir opción
              </router-link>
              <span v-else class="text-sm text-ink-muted">No disponible por ahora</span>

              <button type="button" data-testid="wishlist-remove" :disabled="!!busy[row.productId]" @click="removeRow(row.productId)"
                class="text-sm font-medium text-ink-muted hover:text-red-600 disabled:opacity-50">
                Quitar
              </button>
            </div>
          </div>
        </template>
      </li>
    </ul>
  </div>
</template>
