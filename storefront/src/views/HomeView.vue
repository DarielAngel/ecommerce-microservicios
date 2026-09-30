<script setup>
import { ref, onMounted, watch } from 'vue'
import { api } from '../api/client'

const items = ref([])
const categories = ref([])
const searchTerm = ref('')
const categoryId = ref('')
const page = ref(1)
const totalPages = ref(1)
const loading = ref(true)
const error = ref('')

async function loadCategories() {
  categories.value = await api.get('/api/categories')
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const result = await api.get('/api/products', {
      params: { searchTerm: searchTerm.value, categoryId: categoryId.value, page: page.value, pageSize: 12 }
    })
    items.value = result.items
    totalPages.value = result.totalPages || 1
  } catch (err) {
    error.value = err.message
  } finally {
    loading.value = false
  }
}

let searchTimeout
watch(searchTerm, () => {
  clearTimeout(searchTimeout)
  searchTimeout = setTimeout(() => { page.value = 1; load() }, 350)
})
watch([categoryId, page], load)

onMounted(async () => {
  await loadCategories()
  await load()
})
</script>

<template>
  <div>
    <div class="flex flex-col sm:flex-row gap-3 mb-6">
      <input v-model="searchTerm" type="search" placeholder="Buscar productos..."
        class="flex-1 rounded-lg border border-gray-300 px-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500" />
      <select v-model="categoryId"
        class="rounded-lg border border-gray-300 px-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500">
        <option value="">Todas las categorías</option>
        <option v-for="c in categories" :key="c.id" :value="c.id">{{ c.name }}</option>
      </select>
    </div>

    <div v-if="loading" class="text-center text-gray-400 py-16">Cargando...</div>
    <div v-else-if="error" class="text-center text-red-600 py-16">{{ error }}</div>
    <div v-else-if="items.length === 0" class="text-center text-gray-400 py-16">No se encontraron productos.</div>

    <div v-else class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-5">
      <router-link v-for="p in items" :key="p.id" :to="{ name: 'product-detail', params: { id: p.id } }"
        class="bg-white border border-gray-200 rounded-xl overflow-hidden hover:shadow-md hover:border-brand-300 transition-all">
        <div class="aspect-square bg-gray-100 flex items-center justify-center text-4xl">
          🛒
        </div>
        <div class="p-3">
          <p class="text-sm font-medium text-gray-900 truncate">{{ p.name }}</p>
          <p class="text-xs text-gray-500 mb-1">{{ p.categoryName }}</p>
          <p class="text-sm font-semibold text-brand-700">
            {{ p.minPrice != null ? `$${p.minPrice.toFixed(2)}` : 'Consultar' }}
          </p>
        </div>
      </router-link>
    </div>

    <div v-if="totalPages > 1" class="flex items-center justify-center gap-3 mt-8 text-sm">
      <button :disabled="page <= 1" @click="page--"
        class="px-3 py-1.5 rounded-lg border border-gray-300 disabled:opacity-40">Anterior</button>
      <span class="text-gray-500">Página {{ page }} de {{ totalPages }}</span>
      <button :disabled="page >= totalPages" @click="page++"
        class="px-3 py-1.5 rounded-lg border border-gray-300 disabled:opacity-40">Siguiente</button>
    </div>
  </div>
</template>
