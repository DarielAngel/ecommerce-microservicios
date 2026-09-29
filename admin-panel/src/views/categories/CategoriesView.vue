<script setup>
import { ref, onMounted } from 'vue'
import { useApi } from '../../api/useApi'

const apiClient = useApi()
const categories = ref([])
const loading = ref(true)
const error = ref('')

const newName = ref('')
const newParentId = ref('')
const creating = ref(false)
const createError = ref('')

async function loadCategories() {
  loading.value = true
  error.value = ''
  try {
    categories.value = await apiClient.get('/api/categories')
  } catch (err) {
    error.value = err.message
  } finally {
    loading.value = false
  }
}

async function createCategory() {
  createError.value = ''
  creating.value = true
  try {
    await apiClient.post('/api/categories', {
      name: newName.value,
      parentCategoryId: newParentId.value || null
    })
    newName.value = ''
    newParentId.value = ''
    await loadCategories()
  } catch (err) {
    createError.value = err.message
  } finally {
    creating.value = false
  }
}

onMounted(loadCategories)
</script>

<template>
  <div>
    <h1 class="text-2xl font-semibold text-gray-900 mb-6">Categorías</h1>

    <div class="bg-white border border-gray-200 rounded-xl p-5 mb-6">
      <h2 class="text-sm font-medium text-gray-900 mb-3">Nueva categoría</h2>
      <form @submit.prevent="createCategory" class="flex flex-wrap gap-3 items-end">
        <div class="flex-1 min-w-[180px]">
          <label class="block text-xs font-medium text-gray-500 mb-1">Nombre</label>
          <input v-model="newName" required
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500" />
        </div>
        <div class="flex-1 min-w-[180px]">
          <label class="block text-xs font-medium text-gray-500 mb-1">Categoría padre (opcional)</label>
          <select v-model="newParentId"
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500">
            <option value="">— Ninguna —</option>
            <option v-for="c in categories" :key="c.id" :value="c.id">{{ c.name }}</option>
          </select>
        </div>
        <button type="submit" :disabled="creating"
          class="bg-brand-600 hover:bg-brand-700 disabled:opacity-60 text-white text-sm font-medium rounded-lg px-4 py-2">
          {{ creating ? 'Creando...' : 'Crear' }}
        </button>
      </form>
      <p v-if="createError" class="text-sm text-red-600 mt-2">{{ createError }}</p>
    </div>

    <div class="bg-white border border-gray-200 rounded-xl overflow-hidden">
      <div v-if="loading" class="p-6 text-sm text-gray-500">Cargando...</div>
      <div v-else-if="error" class="p-6 text-sm text-red-600">{{ error }}</div>
      <div v-else-if="categories.length === 0" class="p-6 text-sm text-gray-500">No hay categorías todavía.</div>
      <table v-else class="w-full text-sm">
        <thead class="bg-gray-50 text-gray-500 text-xs uppercase">
          <tr>
            <th class="text-left px-4 py-2 font-medium">Nombre</th>
            <th class="text-left px-4 py-2 font-medium">Slug</th>
            <th class="text-left px-4 py-2 font-medium">Categoría padre</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-100">
          <tr v-for="c in categories" :key="c.id">
            <td class="px-4 py-2.5 text-gray-900">{{ c.name }}</td>
            <td class="px-4 py-2.5 text-gray-500">{{ c.slug }}</td>
            <td class="px-4 py-2.5 text-gray-500">
              {{ categories.find(p => p.id === c.parentCategoryId)?.name ?? '—' }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
