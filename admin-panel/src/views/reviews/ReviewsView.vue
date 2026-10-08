<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import { useApi } from '../../api/useApi'

const PAGE_SIZE = 20

const apiClient = useApi()

const reviews = ref([])
const totalCount = ref(0)
const page = ref(1)
const loading = ref(true)
const error = ref('')
const notice = ref('')

// Filtros: lo que está escrito en el formulario vs. lo que se aplicó a la última búsqueda.
const form = reactive({ rating: '', search: '' })
const applied = reactive({ rating: '', search: '', productId: null })

// id de producto -> nombre ('' mientras carga, null si el producto ya no existe).
const productNames = reactive({})

const confirmingId = ref(null)
const deletingId = ref(null)

const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / PAGE_SIZE)))
const filtering = computed(() => Boolean(applied.rating || applied.search || applied.productId))

function stars(rating) {
  return '★'.repeat(rating) + '☆'.repeat(5 - rating)
}

function productLabel(id) {
  const name = productNames[id]
  if (name === undefined || name === '') return 'Cargando…'
  return name ?? `Producto eliminado (${id.slice(0, 8)})`
}

async function loadProductNames(ids) {
  const missing = [...new Set(ids)].filter((id) => !(id in productNames))
  missing.forEach((id) => { productNames[id] = '' })
  await Promise.all(missing.map(async (id) => {
    try {
      const product = await apiClient.get(`/api/products/${id}`)
      productNames[id] = product?.name ?? null
    } catch {
      productNames[id] = null
    }
  }))
}

async function load() {
  loading.value = true
  error.value = ''
  confirmingId.value = null
  try {
    const result = await apiClient.get('/api/reviews/admin', {
      params: {
        rating: applied.rating,
        search: applied.search,
        productId: applied.productId,
        page: page.value,
        pageSize: PAGE_SIZE
      }
    })
    reviews.value = result.items
    totalCount.value = result.totalCount
    loadProductNames(result.items.map((r) => r.productId))
  } catch (err) {
    error.value = err.message
  } finally {
    loading.value = false
  }
}

function applyFilters() {
  applied.rating = form.rating
  applied.search = form.search.trim()
  page.value = 1
  notice.value = ''
  load()
}

function onlyProduct(productId) {
  applied.productId = productId
  page.value = 1
  load()
}

function clearFilters() {
  form.rating = ''
  form.search = ''
  Object.assign(applied, { rating: '', search: '', productId: null })
  page.value = 1
  load()
}

function goTo(newPage) {
  page.value = newPage
  load()
}

async function remove(review) {
  deletingId.value = review.id
  try {
    await apiClient.delete(`/api/reviews/${review.id}`)
    notice.value = `Reseña "${review.title}" eliminada.`
    // Si era la única de la última página, volvemos a la anterior.
    if (reviews.value.length === 1 && page.value > 1) page.value -= 1
    await load()
  } catch (err) {
    if (err.status === 404) {
      // Otro Admin (o su autor) ya la borró: no es un error, solo refrescamos.
      notice.value = 'Esa reseña ya no existía; actualizamos la lista.'
      await load()
    } else {
      error.value = err.message
    }
  } finally {
    deletingId.value = null
  }
}

onMounted(load)
</script>

<template>
  <div>
    <div class="flex items-end justify-between gap-4 mb-6">
      <div>
        <h1 class="text-2xl font-semibold text-gray-900">Reseñas</h1>
        <p class="text-sm text-gray-500">Todas las reseñas de la tienda, las más nuevas primero. Elimina las que no cumplan las reglas.</p>
      </div>
    </div>

    <form @submit.prevent="applyFilters" class="bg-white border border-gray-200 rounded-xl p-4 mb-4 flex flex-wrap items-end gap-3">
      <div>
        <label for="review-rating" class="block text-xs font-medium text-gray-600 mb-1">Estrellas</label>
        <select id="review-rating" v-model="form.rating" class="border border-gray-300 rounded-lg px-3 py-2 text-sm">
          <option value="">Todas</option>
          <option v-for="n in 5" :key="n" :value="String(n)">{{ n }} {{ n === 1 ? 'estrella' : 'estrellas' }}</option>
        </select>
      </div>
      <div class="flex-1 min-w-[14rem]">
        <label for="review-search" class="block text-xs font-medium text-gray-600 mb-1">Buscar en título, comentario o autor</label>
        <input id="review-search" v-model="form.search" type="search" maxlength="100" placeholder="ej. roto, estafa, Ana"
          class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm" />
      </div>
      <button type="submit" class="bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium rounded-lg px-4 py-2">Buscar</button>
      <button v-if="filtering" type="button" @click="clearFilters" data-testid="reviews-clear"
        class="text-sm text-gray-600 hover:text-gray-900 px-2 py-2">Quitar filtros</button>
    </form>

    <div v-if="applied.productId" class="mb-4 flex items-center gap-2 text-sm" data-testid="reviews-product-filter">
      <span class="text-gray-600">Solo de:</span>
      <span class="font-medium text-gray-900">{{ productLabel(applied.productId) }}</span>
      <button type="button" @click="onlyProduct(null)" class="text-brand-700 hover:underline">ver todos los productos</button>
    </div>

    <p v-if="notice" class="mb-4 text-sm text-green-700 bg-green-50 border border-green-200 rounded-lg px-3 py-2" role="status">{{ notice }}</p>

    <div class="bg-white border border-gray-200 rounded-xl overflow-hidden">
      <div v-if="loading" class="p-6 text-sm text-gray-500">Cargando...</div>
      <div v-else-if="error" class="p-6 text-sm text-red-600" role="alert">{{ error }}</div>
      <div v-else-if="reviews.length === 0" class="p-6 text-sm text-gray-500" data-testid="reviews-empty">
        {{ filtering ? 'Ninguna reseña coincide con estos filtros.' : 'Todavía no hay reseñas.' }}
      </div>

      <ul v-else class="divide-y divide-gray-100">
        <li v-for="r in reviews" :key="r.id" class="p-4" data-testid="review-row">
          <div class="flex items-start justify-between gap-4">
            <div class="min-w-0">
              <p class="text-sm">
                <span class="text-amber-500 tracking-tight" :aria-label="`${r.rating} de 5 estrellas`">{{ stars(r.rating) }}</span>
                <span class="ml-2 font-medium text-gray-900">{{ r.title }}</span>
              </p>
              <p v-if="r.comment" class="mt-1 text-sm text-gray-700 whitespace-pre-line break-words">{{ r.comment }}</p>
              <p class="mt-2 text-xs text-gray-500">
                {{ r.authorName }}
                <span v-if="r.isVerifiedPurchase" class="ml-1 text-green-700">· Compra verificada</span>
                · {{ new Date(r.createdAtUtc).toLocaleString() }}
                ·
                <button v-if="!applied.productId" type="button" @click="onlyProduct(r.productId)"
                  class="text-brand-700 hover:underline" data-testid="review-product">{{ productLabel(r.productId) }}</button>
                <span v-else>{{ productLabel(r.productId) }}</span>
              </p>
            </div>

            <div class="flex-shrink-0 text-right">
              <button v-if="confirmingId !== r.id" type="button" @click="confirmingId = r.id" data-testid="review-delete"
                class="text-sm text-red-600 hover:text-red-700 hover:bg-red-50 rounded-lg px-3 py-1.5">Eliminar</button>
              <div v-else class="flex items-center gap-2 text-sm">
                <span class="text-gray-700">¿Eliminar?</span>
                <button type="button" @click="remove(r)" :disabled="deletingId === r.id" data-testid="review-confirm-delete"
                  class="bg-red-600 hover:bg-red-700 text-white rounded-lg px-3 py-1.5 disabled:opacity-60">
                  {{ deletingId === r.id ? 'Eliminando…' : 'Sí, eliminar' }}
                </button>
                <button type="button" @click="confirmingId = null" class="text-gray-600 hover:text-gray-900 px-2 py-1.5">No</button>
              </div>
            </div>
          </div>
        </li>
      </ul>

      <div v-if="!loading && !error && totalCount > 0"
        class="flex items-center justify-between gap-3 border-t border-gray-100 px-4 py-3 text-sm text-gray-600">
        <span data-testid="reviews-count">Página {{ page }} de {{ totalPages }} · {{ totalCount }} {{ totalCount === 1 ? 'reseña' : 'reseñas' }}</span>
        <div class="flex gap-2">
          <button type="button" :disabled="page <= 1" @click="goTo(page - 1)"
            class="rounded-lg border border-gray-300 px-3 py-1.5 disabled:opacity-40">Anterior</button>
          <button type="button" :disabled="page >= totalPages" @click="goTo(page + 1)" data-testid="reviews-next"
            class="rounded-lg border border-gray-300 px-3 py-1.5 disabled:opacity-40">Siguiente</button>
        </div>
      </div>
    </div>
  </div>
</template>
