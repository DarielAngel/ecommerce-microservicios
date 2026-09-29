<script setup>
import { ref, onMounted } from 'vue'
import { useApi } from '../../api/useApi'

const apiClient = useApi()

const lowStock = ref([])
const loadingLowStock = ref(true)
const lowStockError = ref('')

const variantId = ref('')
const stock = ref(null)
const lookupError = ref('')
const looking = ref(false)

const newQuantity = ref('')
const adjusting = ref(false)
const adjustError = ref('')
const adjustSuccess = ref('')

async function loadLowStock() {
  loadingLowStock.value = true
  lowStockError.value = ''
  try {
    lowStock.value = await apiClient.get('/api/stock/low-stock')
  } catch (err) {
    lowStockError.value = err.message
  } finally {
    loadingLowStock.value = false
  }
}

async function lookup(id) {
  const targetId = id || variantId.value
  if (!targetId) return
  variantId.value = targetId
  lookupError.value = ''
  adjustSuccess.value = ''
  looking.value = true
  try {
    stock.value = await apiClient.get(`/api/stock/${targetId}`)
    newQuantity.value = stock.value.quantityOnHand
  } catch (err) {
    stock.value = null
    lookupError.value = err.message
  } finally {
    looking.value = false
  }
}

async function adjust() {
  adjustError.value = ''
  adjustSuccess.value = ''
  adjusting.value = true
  try {
    stock.value = await apiClient.post(`/api/stock/${variantId.value}/adjust`, {
      newQuantityOnHand: Number(newQuantity.value)
    })
    adjustSuccess.value = 'Stock actualizado.'
    await loadLowStock()
  } catch (err) {
    adjustError.value = err.message
  } finally {
    adjusting.value = false
  }
}

onMounted(loadLowStock)
</script>

<template>
  <div>
    <h1 class="text-2xl font-semibold text-gray-900 mb-6">Inventario</h1>

    <div class="bg-white border border-gray-200 rounded-xl p-5 mb-6">
      <h2 class="text-sm font-medium text-gray-900 mb-3">Consultar / ajustar stock por variante</h2>
      <form @submit.prevent="() => lookup()" class="flex gap-3 mb-4">
        <input v-model="variantId" placeholder="Id de la variante (GUID)" required
          class="flex-1 rounded-lg border border-gray-300 px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-brand-500" />
        <button type="submit" :disabled="looking"
          class="bg-gray-800 hover:bg-gray-900 disabled:opacity-60 text-white text-sm font-medium rounded-lg px-4 py-2">
          Buscar
        </button>
      </form>
      <p v-if="lookupError" class="text-sm text-red-600 mb-3">{{ lookupError }}</p>

      <div v-if="stock" class="border border-gray-100 rounded-lg p-4">
        <div class="grid grid-cols-2 sm:grid-cols-4 gap-3 text-sm mb-4">
          <div><p class="text-xs text-gray-500">Disponible</p><p class="font-medium">{{ stock.quantityAvailable }}</p></div>
          <div><p class="text-xs text-gray-500">En mano</p><p class="font-medium">{{ stock.quantityOnHand }}</p></div>
          <div><p class="text-xs text-gray-500">Reservado</p><p class="font-medium">{{ stock.quantityReserved }}</p></div>
          <div><p class="text-xs text-gray-500">Umbral bajo stock</p><p class="font-medium">{{ stock.lowStockThreshold }}</p></div>
        </div>
        <div class="flex items-end gap-3">
          <div>
            <label class="block text-xs font-medium text-gray-500 mb-1">Nueva cantidad en mano</label>
            <input v-model="newQuantity" type="number" min="0"
              class="w-32 rounded-lg border border-gray-300 px-3 py-1.5 text-sm" />
          </div>
          <button @click="adjust" :disabled="adjusting"
            class="bg-brand-600 hover:bg-brand-700 disabled:opacity-60 text-white text-sm font-medium rounded-lg px-4 py-1.5">
            {{ adjusting ? 'Ajustando...' : 'Ajustar' }}
          </button>
        </div>
        <p v-if="adjustError" class="text-sm text-red-600 mt-2">{{ adjustError }}</p>
        <p v-if="adjustSuccess" class="text-sm text-green-600 mt-2">{{ adjustSuccess }}</p>
      </div>
    </div>

    <div class="bg-white border border-gray-200 rounded-xl overflow-hidden">
      <div class="px-5 py-3 border-b border-gray-100">
        <h2 class="text-sm font-medium text-gray-900">Variantes con bajo stock</h2>
      </div>
      <div v-if="loadingLowStock" class="p-6 text-sm text-gray-500">Cargando...</div>
      <div v-else-if="lowStockError" class="p-6 text-sm text-red-600">{{ lowStockError }}</div>
      <div v-else-if="lowStock.length === 0" class="p-6 text-sm text-gray-500">Sin alertas de bajo stock. 🎉</div>
      <table v-else class="w-full text-sm">
        <thead class="bg-gray-50 text-gray-500 text-xs uppercase">
          <tr>
            <th class="text-left px-4 py-2 font-medium">Variant Id</th>
            <th class="text-left px-4 py-2 font-medium">Disponible</th>
            <th class="text-left px-4 py-2 font-medium">Umbral</th>
            <th class="px-4 py-2"></th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-100">
          <tr v-for="item in lowStock" :key="item.variantId">
            <td class="px-4 py-2.5 font-mono text-xs text-gray-700">{{ item.variantId }}</td>
            <td class="px-4 py-2.5 text-red-600 font-medium">{{ item.quantityAvailable }}</td>
            <td class="px-4 py-2.5 text-gray-500">{{ item.lowStockThreshold }}</td>
            <td class="px-4 py-2.5 text-right">
              <button @click="lookup(item.variantId)" class="text-brand-600 hover:text-brand-700 font-medium text-xs">
                Ajustar
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
