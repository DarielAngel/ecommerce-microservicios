<script setup>
import { ref, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useApi } from '../../api/useApi'
import { api } from '../../api/client'

const apiClient = useApi()
const route = useRoute()
const router = useRouter()

const lowStock = ref([])
const loadingLowStock = ref(true)
const lowStockError = ref('')

// Buscador de producto -> variante: así el Admin nunca necesita copiar un Id a mano.
const productSearch = ref('')
const productResults = ref([])
const searchingProducts = ref(false)
const selectedProduct = ref(null) // producto completo (con variantes), una vez elegido

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

let searchTimeout
// Al elegir un producto, escribimos su nombre en productSearch para que se vea en el input
// — pero eso por sí solo dispararía este watcher de nuevo 350ms después, reabriendo el
// dropdown de resultados encima de los botones de variante que acabamos de mostrar. Esta
// bandera distingue "lo escribió el usuario" de "lo acabamos de setear nosotros".
const suppressNextSearch = ref(false)

watch(productSearch, () => {
  if (suppressNextSearch.value) {
    suppressNextSearch.value = false
    return
  }
  clearTimeout(searchTimeout)
  if (!productSearch.value.trim()) {
    productResults.value = []
    return
  }
  searchTimeout = setTimeout(async () => {
    searchingProducts.value = true
    try {
      // Con la sesión del Admin, para encontrar también productos desactivados.
      const result = await apiClient.get('/api/products', { params: { searchTerm: productSearch.value, pageSize: 8, includeInactive: true } })
      productResults.value = result.items
    } catch {
      productResults.value = []
    } finally {
      searchingProducts.value = false
    }
  }, 350)
})

async function selectProduct(summary) {
  productResults.value = []
  suppressNextSearch.value = true
  productSearch.value = summary.name
  selectedProduct.value = await api.get(`/api/products/${summary.id}`)
}

function variantLabel(v) {
  const attrs = Object.entries(v.attributes).map(([k, val]) => `${k}: ${val}`).join(', ')
  return `${v.sku}${attrs ? ' — ' + attrs : ''} ($${v.price.toFixed(2)})`
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

onMounted(async () => {
  await loadLowStock()
  // Deep-link desde Productos ("Ver/ajustar stock" en una variante) o desde la tabla de
  // bajo stock de esta misma pantalla: /inventory?variantId=...
  if (route.query.variantId) {
    await lookup(route.query.variantId)
    router.replace({ query: {} }) // limpia el query param, no queda pegado en la URL
  }
})
</script>

<template>
  <div>
    <h1 class="text-2xl font-semibold text-gray-900 mb-6">Inventario</h1>

    <div class="bg-white border border-gray-200 rounded-xl p-5 mb-6">
      <h2 class="text-sm font-medium text-gray-900 mb-3">Buscar producto</h2>

      <div class="relative mb-4">
        <label for="product-search" class="sr-only">Buscar producto por nombre</label>
        <input id="product-search" v-model="productSearch" placeholder="Escribe el nombre del producto..."
          class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500" />

        <div v-if="productResults.length > 0"
          class="absolute z-10 mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg overflow-hidden">
          <button v-for="p in productResults" :key="p.id" @click="selectProduct(p)"
            class="w-full text-left px-3 py-2 text-sm hover:bg-gray-50 flex justify-between items-center">
            <span>{{ p.name }}</span>
            <span class="text-xs text-gray-400">{{ p.categoryName }}</span>
          </button>
        </div>
      </div>

      <div v-if="selectedProduct" class="mb-2">
        <p class="text-xs text-gray-500 mb-2">Variantes de "{{ selectedProduct.name }}" — elige una:</p>
        <div class="flex flex-wrap gap-2">
          <button v-for="v in selectedProduct.variants" :key="v.id" @click="lookup(v.id)"
            class="text-xs px-3 py-1.5 rounded-lg border"
            :class="variantId === v.id ? 'border-brand-600 bg-brand-50 text-brand-700' : 'border-gray-300 text-gray-700 hover:border-brand-300'">
            {{ variantLabel(v) }}
          </button>
        </div>
      </div>

      <details class="mt-4">
        <summary class="text-xs text-gray-400 cursor-pointer select-none">
          O pega el Id de la variante directamente (avanzado)
        </summary>
        <form @submit.prevent="() => lookup()" class="flex gap-3 mt-2">
          <label for="variant-id-search" class="sr-only">Id de la variante</label>
          <input id="variant-id-search" v-model="variantId" placeholder="Id de la variante (GUID)" required
            class="flex-1 rounded-lg border border-gray-300 px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-brand-500" />
          <button type="submit" :disabled="looking"
            class="bg-gray-800 hover:bg-gray-900 disabled:opacity-60 text-white text-sm font-medium rounded-lg px-4 py-2">
            Buscar
          </button>
        </form>
      </details>

      <p v-if="lookupError" class="text-sm text-red-600 mt-3">{{ lookupError }}</p>

      <div v-if="stock" class="border border-gray-100 rounded-lg p-4 mt-4">
        <div class="grid grid-cols-2 sm:grid-cols-4 gap-3 text-sm mb-4">
          <div><p class="text-xs text-gray-500">Disponible</p><p class="font-medium">{{ stock.quantityAvailable }}</p></div>
          <div><p class="text-xs text-gray-500">En mano</p><p class="font-medium">{{ stock.quantityOnHand }}</p></div>
          <div><p class="text-xs text-gray-500">Reservado</p><p class="font-medium">{{ stock.quantityReserved }}</p></div>
          <div><p class="text-xs text-gray-500">Umbral bajo stock</p><p class="font-medium">{{ stock.lowStockThreshold }}</p></div>
        </div>
        <div class="flex items-end gap-3">
          <div>
            <label for="new-quantity" class="block text-xs font-medium text-gray-500 mb-1">Nueva cantidad en mano</label>
            <input id="new-quantity" v-model="newQuantity" type="number" min="0"
              class="w-32 rounded-lg border border-gray-300 px-3 py-1.5 text-sm" />
          </div>
          <button @click="adjust" :disabled="adjusting" data-testid="stock-adjust-main"
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
