<script setup>
import { ref, onMounted, watch, computed } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '../api/client'
import { useApi } from '../api/useApi'
import { useAuthStore } from '../stores/auth'
import { useCartStore } from '../stores/cart'
import { imageUrl } from '../utils/images'
import { formatMoney } from '../utils/format'
import StarRating from '../components/StarRating.vue'
import ReviewsSection from '../components/ReviewsSection.vue'
import FavoriteButton from '../components/FavoriteButton.vue'
import ProductRail from '../components/ProductRail.vue'
import { fetchRelated, fetchAvailability } from '../api/catalog'
import { availabilityBadge } from '../utils/availability'
import { addRecentlyViewed, loadRecentlyViewed, summaryFromDetail } from '../utils/recentlyViewed'

const props = defineProps({ id: { type: String, required: true } })

const router = useRouter()
const auth = useAuthStore()
const cartStore = useCartStore()
const apiClient = useApi()

const product = ref(null)
const rating = ref(null) // resumen de reseñas, lo emite ReviewsSection y se mantiene al día
const loading = ref(true)
const error = ref('')

const selectedVariantId = ref('')
const quantity = ref(1)
const adding = ref(false)
const addError = ref('')
const addSuccess = ref(false)
const availableStock = ref(null) // null = no cargado (o no logueado todavía)
const selectedImageId = ref(null)
const related = ref([])
const recentlyViewed = ref([])
const availability = ref({}) // variantId -> { status, quantityLeft }, del stock real (público)

const badge = computed(() => availabilityBadge(availability.value[selectedVariantId.value]))
const soldOut = computed(() => availability.value[selectedVariantId.value]?.status === 'OutOfStock')

const selectedVariant = computed(() =>
  product.value?.variants.find(v => v.id === selectedVariantId.value) ?? null
)

// La imagen marcada como principal va primero; el resto, en el orden que ya trae la API.
const orderedImages = computed(() => {
  if (!product.value) return []
  return [...product.value.images].sort((a, b) => (b.isPrimary ? 1 : 0) - (a.isPrimary ? 1 : 0))
})

const heroImage = computed(() =>
  orderedImages.value.find(img => img.id === selectedImageId.value) ?? orderedImages.value[0] ?? null
)

// El endpoint de stock exige estar logueado — si el visitante todavía no inició
// sesión, simplemente no mostramos el número (no es un error, es esperado).
async function loadStock(variantId) {
  availableStock.value = null
  if (!variantId || !auth.isAuthenticated) return
  try {
    const stock = await apiClient.get(`/api/stock/${variantId}`)
    availableStock.value = stock.quantityAvailable
  } catch {
    // silencioso: mostrar el stock es una ayuda, no algo crítico para poder comprar
  }
}

watch(selectedVariantId, (id) => loadStock(id))

async function load(id) {
  loading.value = true
  error.value = ''
  product.value = null
  related.value = []
  availability.value = {}
  selectedImageId.value = null
  addSuccess.value = false
  addError.value = ''
  quantity.value = 1
  try {
    product.value = await api.get(`/api/products/${id}`)
    if (product.value.variants.length > 0) {
      selectedVariantId.value = product.value.variants[0].id
      await loadStock(selectedVariantId.value)
    }
  } catch (err) {
    error.value = err.message
    return
  } finally {
    loading.value = false
  }

  // Lo de abajo es un extra: se carga después de pintar el producto y nunca rompe la página.
  const categories = await api.get('/api/categories').catch(() => [])
  const categoryName = categories.find((c) => c.id === product.value.categoryId)?.name ?? ''
  // "Vistos recientemente" sin el producto actual (que pasa a ser el primero de la lista).
  recentlyViewed.value = addRecentlyViewed(summaryFromDetail(product.value, categoryName)).filter((p) => p.id !== id)

  const [relatedList, stock] = await Promise.all([
    fetchRelated(id),
    fetchAvailability(product.value.variants.map((v) => v.id))
  ])
  related.value = relatedList
  availability.value = stock
}

onMounted(() => {
  recentlyViewed.value = loadRecentlyViewed().filter((p) => p.id !== props.id)
  load(props.id)
})

// Ir de un producto a otro (desde "También te puede interesar") reutiliza esta misma pantalla:
// sin esto se quedaría mostrando el producto anterior.
watch(() => props.id, (id) => {
  load(id)
  window.scrollTo?.({ top: 0, behavior: 'smooth' })
})

async function addToCart() {
  if (!auth.isAuthenticated) {
    router.push({ name: 'login', query: { redirect: router.currentRoute.value.fullPath } })
    return
  }
  addError.value = ''
  addSuccess.value = false
  adding.value = true
  try {
    const cart = await apiClient.post('/api/cart/items', {
      variantId: selectedVariantId.value,
      quantity: Number(quantity.value)
    })
    cartStore.setCart(cart)
    addSuccess.value = true
  } catch (err) {
    addError.value = err.message
  } finally {
    adding.value = false
  }
}
</script>

<template>
  <div v-if="loading" class="text-center text-ink-muted py-16">Cargando...</div>
  <div v-else-if="error" class="text-center text-red-600 dark:text-red-400 py-16">{{ error }}</div>

  <div v-else>
  <div class="grid grid-cols-1 md:grid-cols-2 gap-8">
    <div>
      <div class="aspect-square overflow-hidden rounded-2xl border border-line bg-surface-muted flex items-center justify-center text-7xl">
        <img v-if="heroImage" :src="imageUrl(heroImage.fileName)" :alt="product.name" class="w-full h-full object-cover" />
        <span v-else>🛍️</span>
      </div>
      <div v-if="orderedImages.length > 1" class="flex gap-2 mt-3">
        <button v-for="img in orderedImages" :key="img.id" @click="selectedImageId = img.id"
          class="w-16 h-16 rounded-lg overflow-hidden border-2 flex-shrink-0"
          :class="heroImage?.id === img.id ? 'border-brand-600' : 'border-transparent'">
          <img :src="imageUrl(img.fileName)" :alt="product.name" class="w-full h-full object-cover" />
        </button>
      </div>
    </div>

    <div data-testid="product-info">
      <div class="mb-2 flex items-start justify-between gap-3">
        <h1 class="text-3xl font-bold tracking-tight text-ink">{{ product.name }}</h1>
        <FavoriteButton :product-id="product.id" size="lg" class="flex-shrink-0" />
      </div>
      <a href="#resenas" class="mb-3 inline-flex items-center gap-2 text-sm text-ink-muted hover:text-brand-ink" data-testid="rating-link">
        <template v-if="rating && rating.count > 0">
          <StarRating :value="rating.average" size="sm" />
          <span>{{ rating.average.toFixed(1) }} · {{ rating.count }} {{ rating.count === 1 ? 'reseña' : 'reseñas' }}</span>
        </template>
        <span v-else>Sé el primero en opinar</span>
      </a>
      <p class="text-ink-soft mb-5">{{ product.description }}</p>

      <div v-if="selectedVariant" class="mb-5 flex flex-wrap items-center gap-3">
        <span class="text-3xl font-bold text-brand-ink" data-testid="product-price">{{ formatMoney(selectedVariant.price) }}</span>
        <span v-if="badge" data-testid="availability-badge" class="rounded-full px-3 py-1 text-xs font-semibold"
          :class="badge.tone === 'out'
            ? 'bg-surface-muted text-ink-muted'
            : 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300'">
          {{ badge.text }}
        </span>
      </div>

      <div v-if="product.variants.length > 1" class="mb-4">
        <label for="variant-select" class="block text-sm font-medium text-ink-soft mb-1">Variante</label>
        <select id="variant-select" v-model="selectedVariantId" class="w-full rounded-lg border border-line px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500">
          <option v-for="v in product.variants" :key="v.id" :value="v.id">
            {{ v.sku }} — {{ Object.entries(v.attributes).map(([k, val]) => `${k}: ${val}`).join(', ') || 'Sin atributos' }}
          </option>
        </select>
      </div>

      <div class="flex items-end gap-3 mb-4">
        <div>
          <label for="quantity-input" class="block text-sm font-medium text-ink-soft mb-1">Cantidad</label>
          <p v-if="availableStock !== null" class="text-xs text-ink-muted mb-1">{{ availableStock }} disponibles</p>
          <input id="quantity-input" v-model="quantity" type="number" min="1" :max="availableStock ?? undefined"
            class="w-24 rounded-lg border border-line px-3 py-2 text-sm" />
        </div>
        <button @click="addToCart" :disabled="adding || !selectedVariantId || soldOut"
          class="rounded-full bg-brand-600 px-7 py-2.5 text-sm font-semibold text-white shadow-card transition-colors hover:bg-brand-700 disabled:opacity-60">
          {{ soldOut ? 'Sin stock' : adding ? 'Agregando...' : 'Agregar al carrito' }}
        </button>
      </div>

      <p v-if="addError" class="text-sm text-red-600 dark:text-red-400">{{ addError }}</p>
      <p v-if="addSuccess" class="text-sm text-green-600 dark:text-green-400">
        Agregado al carrito. <router-link :to="{ name: 'cart' }" class="underline font-medium">Ver carrito</router-link>
      </p>

      <ul class="mt-6 space-y-2 border-t border-line pt-5 text-sm text-ink-muted">
        <li class="flex items-center gap-2"><span aria-hidden="true">🔒</span> Pago seguro con PayPal</li>
        <li class="flex items-center gap-2"><span aria-hidden="true">📦</span> Disponibilidad verificada en tiempo real</li>
        <li class="flex items-center gap-2"><span aria-hidden="true">✉️</span> Te avisamos por correo cuando se envíe</li>
      </ul>
    </div>
  </div>

  <ProductRail title="También te puede interesar" :products="related" testid="related-products" class="mt-14" />

  <ReviewsSection :product-id="id" class="mt-14" @summary="rating = $event" />

  <ProductRail title="Vistos recientemente" :products="recentlyViewed" testid="recently-viewed" class="mt-14" />
  </div>
</template>
