<script setup>
import { ref, onMounted, watch } from 'vue'
import { useApi } from '../../api/useApi'

const apiClient = useApi()
const items = ref([])
const page = ref(1)
const totalPages = ref(1)
const searchTerm = ref('')
const loading = ref(true)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    const result = await apiClient.get('/api/products', {
      params: { searchTerm: searchTerm.value, page: page.value, pageSize: 20 }
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
watch(page, load)

onMounted(load)
</script>

<template>
  <div>
    <div class="flex items-center justify-between mb-6">
      <h1 class="text-2xl font-semibold text-gray-900">Productos</h1>
      <router-link :to="{ name: 'product-new' }"
        class="bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium rounded-lg px-4 py-2">
        + Nuevo producto
      </router-link>
    </div>

    <input v-model="searchTerm" type="search" placeholder="Buscar por nombre o descripción..."
      class="w-full max-w-md mb-4 rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500" />

    <div class="bg-white border border-gray-200 rounded-xl overflow-hidden">
      <div v-if="loading" class="p-6 text-sm text-gray-500">Cargando...</div>
      <div v-else-if="error" class="p-6 text-sm text-red-600">{{ error }}</div>
      <div v-else-if="items.length === 0" class="p-6 text-sm text-gray-500">No se encontraron productos.</div>
      <table v-else class="w-full text-sm">
        <thead class="bg-gray-50 text-gray-500 text-xs uppercase">
          <tr>
            <th class="text-left px-4 py-2 font-medium">Nombre</th>
            <th class="text-left px-4 py-2 font-medium">Categoría</th>
            <th class="text-left px-4 py-2 font-medium">Precio desde</th>
            <th class="text-left px-4 py-2 font-medium">Estado</th>
            <th class="px-4 py-2"></th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-100">
          <tr v-for="p in items" :key="p.id">
            <td class="px-4 py-2.5 text-gray-900">{{ p.name }}</td>
            <td class="px-4 py-2.5 text-gray-500">{{ p.categoryName }}</td>
            <td class="px-4 py-2.5 text-gray-500">{{ p.minPrice != null ? `$${p.minPrice.toFixed(2)}` : '—' }}</td>
            <td class="px-4 py-2.5">
              <span class="text-xs px-2 py-0.5 rounded-full"
                :class="p.isActive ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-600'">
                {{ p.isActive ? 'Activo' : 'Inactivo' }}
              </span>
            </td>
            <td class="px-4 py-2.5 text-right">
              <router-link :to="{ name: 'product-edit', params: { id: p.id } }"
                class="text-brand-600 hover:text-brand-700 font-medium">Editar</router-link>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="totalPages > 1" class="flex items-center gap-3 mt-4 text-sm">
      <button :disabled="page <= 1" @click="page--"
        class="px-3 py-1.5 rounded-lg border border-gray-300 disabled:opacity-40">Anterior</button>
      <span class="text-gray-500">Página {{ page }} de {{ totalPages }}</span>
      <button :disabled="page >= totalPages" @click="page++"
        class="px-3 py-1.5 rounded-lg border border-gray-300 disabled:opacity-40">Siguiente</button>
    </div>
  </div>
</template>
