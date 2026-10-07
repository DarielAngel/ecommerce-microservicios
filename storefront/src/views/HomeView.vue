<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api } from '../api/client'
import { STORE } from '../config'
import { fetchRatingSummaries } from '../api/reviews'
import ProductCard from '../components/ProductCard.vue'
import FavoriteButton from '../components/FavoriteButton.vue'
import ProductRail from '../components/ProductRail.vue'
import { loadRecentlyViewed, clearRecentlyViewed } from '../utils/recentlyViewed'
import SkeletonCard from '../components/SkeletonCard.vue'
import { splitCategories } from '../utils/categories'

const route = useRoute()
const router = useRouter()

const items = ref([])
const ratings = ref({}) // productId -> { average, count }
const categories = ref([])
const searchTerm = ref(route.query.q ?? '')
const categoryId = ref('')
const minPrice = ref('')
const maxPrice = ref('')
const sortBy = ref('name')
const page = ref(1)
const totalPages = ref(1)
const totalCount = ref(null)
const loading = ref(true)
const error = ref('')
const recentlyViewed = ref(loadRecentlyViewed())

function clearHistory() {
  clearRecentlyViewed()
  recentlyViewed.value = []
}

const sortOptions = [
  { value: 'name', label: 'Nombre' },
  { value: 'newest', label: 'Más reciente' },
  { value: 'price_asc', label: 'Precio: menor a mayor' },
  { value: 'price_desc', label: 'Precio: mayor a menor' }
]

const MAX_CATEGORY_CHIPS = 7
const categoryGroups = computed(() => splitCategories(categories.value, categoryId.value, MAX_CATEGORY_CHIPS))

const hasActiveFilters = computed(() =>
  !!(searchTerm.value || categoryId.value || minPrice.value !== '' || maxPrice.value !== '')
)

async function loadCategories() {
  categories.value = await api.get('/api/categories')
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const result = await api.get('/api/products', {
      params: {
        searchTerm: searchTerm.value,
        categoryId: categoryId.value,
        minPrice: minPrice.value,
        maxPrice: maxPrice.value,
        sortBy: sortBy.value,
        page: page.value,
        pageSize: 12
      }
    })
    items.value = result.items
    totalPages.value = result.totalPages || 1
    totalCount.value = result.totalCount ?? null
  } catch (err) {
    error.value = err.message
  } finally {
    loading.value = false
  }

  // Las estrellas llegan después de pintar el catálogo (una sola llamada para toda la página).
  ratings.value = await fetchRatingSummaries(items.value.map((p) => p.id))
}

function clearFilters() {
  categoryId.value = ''
  minPrice.value = ''
  maxPrice.value = ''
  sortBy.value = 'name'
  // Limpiar la búsqueda pasa por la URL (?q=), que es la fuente de verdad del encabezado.
  if (route.query.q) router.replace({ name: 'home', query: {} })
}

// El precio se escribe tecla a tecla: debounce para no disparar una llamada por cada una.
let debounceTimeout
watch([minPrice, maxPrice], () => {
  clearTimeout(debounceTimeout)
  debounceTimeout = setTimeout(() => { page.value = 1; load() }, 350)
})
watch([categoryId, sortBy], () => { page.value = 1; load() })
watch(page, load)

// La búsqueda llega del encabezado vía la URL.
watch(() => route.query.q, (value) => {
  searchTerm.value = value ?? ''
  page.value = 1
  load()
})

onMounted(async () => {
  await loadCategories()
  await load()
})
</script>

<template>
  <div class="space-y-6">
    <section v-if="!searchTerm"
      class="relative overflow-hidden rounded-3xl bg-gradient-to-br from-brand-600 via-brand-700 to-brand-900 px-6 py-10 text-white shadow-card sm:px-10 sm:py-14">
      <div class="relative z-10 max-w-xl">
        <p class="text-sm font-medium uppercase tracking-wider text-brand-200">Bienvenido a {{ STORE.name }}</p>
        <h1 class="mt-2 text-3xl font-bold leading-tight sm:text-4xl">{{ STORE.tagline }}</h1>
        <p class="mt-3 text-brand-100">Explora el catálogo, compara precios y compra con pago seguro.</p>
        <a href="#catalogo"
          class="mt-6 inline-flex items-center rounded-full bg-white px-5 py-2.5 text-sm font-semibold text-brand-700 transition hover:bg-brand-50">
          Ver productos
        </a>
      </div>
      <div class="pointer-events-none absolute -right-10 -top-10 h-56 w-56 rounded-full bg-white/10 blur-2xl"></div>
      <div class="pointer-events-none absolute -bottom-16 right-24 h-48 w-48 rounded-full bg-accent-400/20 blur-2xl"></div>
    </section>

    <ProductRail v-if="!searchTerm" title="Vistos recientemente" :products="recentlyViewed" testid="recently-viewed">
      <template #action>
        <button type="button" data-testid="clear-recently-viewed" @click="clearHistory"
          class="text-sm font-medium text-ink-muted hover:text-ink">Borrar historial</button>
      </template>
    </ProductRail>

    <section id="catalogo" class="space-y-4">
      <div class="flex flex-wrap items-center gap-2" role="group" aria-label="Categorías">
        <button type="button" @click="categoryId = ''"
          class="rounded-full border px-4 py-1.5 text-sm transition-colors"
          :class="categoryId === '' ? 'border-brand-600 bg-brand-600 text-white' : 'border-line bg-surface text-ink-soft hover:border-brand-300'">
          Todas
        </button>
        <button v-for="c in categoryGroups.visible" :key="c.id" type="button" @click="categoryId = c.id"
          class="max-w-[14rem] truncate rounded-full border px-4 py-1.5 text-sm transition-colors"
          :class="categoryId === c.id ? 'border-brand-600 bg-brand-600 text-white' : 'border-line bg-surface text-ink-soft hover:border-brand-300'"
          :title="c.name">
          {{ c.name }}
        </button>
        <select v-if="categoryGroups.overflow.length" aria-label="Más categorías" data-testid="more-categories" value=""
          @change="categoryId = $event.target.value"
          class="rounded-full border border-line bg-surface px-4 py-1.5 text-sm text-ink-soft focus:outline-none focus:ring-2 focus:ring-brand-500">
          <option value="" disabled selected>Más categorías ({{ categoryGroups.overflow.length }})</option>
          <option v-for="c in categoryGroups.overflow" :key="c.id" :value="c.id">{{ c.name }}</option>
        </select>
      </div>

      <div class="flex flex-wrap items-center gap-x-4 gap-y-3 rounded-2xl border border-line bg-surface p-3 text-sm">
        <div class="flex items-center gap-2">
          <label for="sort-by" class="text-ink-muted">Ordenar por</label>
          <select id="sort-by" v-model="sortBy"
            class="rounded-lg border border-line px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-brand-500">
            <option v-for="o in sortOptions" :key="o.value" :value="o.value">{{ o.label }}</option>
          </select>
        </div>

        <div class="flex items-center gap-2">
          <span class="text-ink-muted">Precio</span>
          <label for="min-price" class="sr-only">Precio mínimo</label>
          <input id="min-price" v-model="minPrice" type="number" min="0" placeholder="Mín."
            class="w-24 rounded-lg border border-line px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          <span class="text-ink-muted">—</span>
          <label for="max-price" class="sr-only">Precio máximo</label>
          <input id="max-price" v-model="maxPrice" type="number" min="0" placeholder="Máx."
            class="w-24 rounded-lg border border-line px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-brand-500" />
        </div>

        <div class="ml-auto flex items-center gap-3">
          <span v-if="totalCount !== null && !loading" class="text-ink-muted">{{ totalCount }} productos</span>
          <button v-if="hasActiveFilters" type="button" @click="clearFilters" class="font-medium text-brand-ink hover:underline">
            Limpiar filtros
          </button>
        </div>
      </div>

      <p v-if="searchTerm" class="text-sm text-ink-muted">
        Resultados para <span class="font-semibold text-ink">"{{ searchTerm }}"</span>
      </p>

      <div v-if="loading" class="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
        <SkeletonCard v-for="n in 8" :key="n" />
      </div>
      <div v-else-if="error" class="rounded-2xl border border-line bg-surface py-16 text-center text-red-600 dark:text-red-400">{{ error }}</div>
      <div v-else-if="items.length === 0" class="rounded-2xl border border-dashed border-line bg-surface py-16 text-center">
        <p class="text-4xl" aria-hidden="true">🔍</p>
        <p class="mt-3 font-medium text-ink">No encontramos productos con esos criterios</p>
        <p class="mt-1 text-sm text-ink-muted">Prueba con otra palabra o ajusta los filtros.</p>
        <button v-if="hasActiveFilters" type="button" @click="clearFilters"
          class="mt-4 rounded-full bg-brand-600 px-5 py-2 text-sm font-medium text-white hover:bg-brand-700">
          Limpiar filtros
        </button>
      </div>

      <div v-else class="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
        <ProductCard v-for="p in items" :key="p.id" :product="p" :rating="ratings[p.id]">
          <template #corner>
            <FavoriteButton :product-id="p.id" />
          </template>
        </ProductCard>
      </div>

      <div v-if="totalPages > 1" class="flex items-center justify-center gap-3 pt-4 text-sm">
        <button :disabled="page <= 1" @click="page--"
          class="rounded-full border border-line bg-surface px-4 py-2 transition-colors hover:border-brand-300 disabled:opacity-40">Anterior</button>
        <span class="text-ink-muted">Página {{ page }} de {{ totalPages }}</span>
        <button :disabled="page >= totalPages" @click="page++"
          class="rounded-full border border-line bg-surface px-4 py-2 transition-colors hover:border-brand-300 disabled:opacity-40">Siguiente</button>
      </div>
    </section>
  </div>
</template>
