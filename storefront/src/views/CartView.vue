<script setup>
import { reactive, ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useApi } from '../api/useApi'
import { useCartStore } from '../stores/cart'

const apiClient = useApi()
const cartStore = useCartStore()
const router = useRouter()

const loading = ref(true)
const error = ref('')
const selected = ref(new Set())
const stockByVariant = reactive({}) // { [variantId]: quantityAvailable }

// Espejo LOCAL y reactivo de las cantidades, para que el <input> tenga algo a lo que
// enlazarse con v-model de verdad. Si el servidor rechaza un cambio, revertimos este
// valor al último confirmado — así el input SIEMPRE refleja un estado real, nunca se
// queda "pegado" en algo que el servidor nunca aceptó.
const quantities = reactive({})
const rowError = reactive({}) // { [variantId]: mensaje de error de esa línea, o null }
const busyVariantId = ref(null)

function syncLocalState(cart) {
  cartStore.setCart(cart)
  for (const item of cart.items) {
    quantities[item.variantId] = item.quantity
  }
}

async function loadStockFor(variantId) {
  try {
    const stock = await apiClient.get(`/api/stock/${variantId}`)
    stockByVariant[variantId] = stock.quantityAvailable
  } catch {
    // Informativo, no crítico: si falla, simplemente no mostramos el número.
  }
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const cart = await apiClient.get('/api/cart')
    syncLocalState(cart)
    selected.value = new Set(cart.items.map(i => i.variantId))
    await Promise.all(cart.items.map(i => loadStockFor(i.variantId)))
  } catch (err) {
    error.value = err.message
  } finally {
    loading.value = false
  }
}

function toggleSelected(variantId) {
  if (selected.value.has(variantId)) selected.value.delete(variantId)
  else selected.value.add(variantId)
  selected.value = new Set(selected.value) // fuerza reactividad
}

async function onQuantityChange(item) {
  const requested = Number(quantities[item.variantId])
  rowError[item.variantId] = null

  if (!Number.isFinite(requested) || requested < 0) {
    quantities[item.variantId] = item.quantity // valor inválido: revertir de inmediato
    return
  }

  busyVariantId.value = item.variantId
  try {
    const cart = await apiClient.put(`/api/cart/items/${item.variantId}`, { quantity: requested })
    syncLocalState(cart)
    if (requested === 0) selected.value.delete(item.variantId)
  } catch (err) {
    // La causa más común: pediste más de lo disponible. Revertimos al último valor
    // confirmado por el servidor — el input nunca se queda mostrando algo inválido.
    quantities[item.variantId] = item.quantity
    rowError[item.variantId] = err.message
  } finally {
    busyVariantId.value = null
  }
}

async function removeItem(variantId) {
  busyVariantId.value = variantId
  rowError[variantId] = null
  try {
    const cart = await apiClient.delete(`/api/cart/items/${variantId}`)
    syncLocalState(cart)
    selected.value.delete(variantId)
  } catch (err) {
    error.value = err.message
  } finally {
    busyVariantId.value = null
  }
}

function goToCheckout() {
  const variantIds = Array.from(selected.value)
  sessionStorage.setItem('checkout-variant-ids', JSON.stringify(variantIds))
  router.push({ name: 'checkout' })
}

onMounted(load)
</script>

<template>
  <div>
    <h1 class="text-xl font-semibold text-gray-900 mb-6">Tu carrito</h1>

    <div v-if="loading" class="text-center text-gray-400 py-16">Cargando...</div>
    <div v-else-if="error" class="text-center text-red-600 py-16">{{ error }}</div>
    <div v-else-if="!cartStore.cart || cartStore.cart.items.length === 0" class="text-center text-gray-400 py-16">
      Tu carrito está vacío. <router-link :to="{ name: 'home' }" class="text-brand-600 underline">Ir a comprar</router-link>
    </div>

    <div v-else class="grid grid-cols-1 lg:grid-cols-3 gap-6">
      <div class="lg:col-span-2 space-y-3">
        <div v-for="item in cartStore.cart.items" :key="item.variantId"
          class="bg-white border border-gray-200 rounded-xl p-4">
          <div class="flex items-center gap-4">
            <input type="checkbox" :checked="selected.has(item.variantId)" @change="toggleSelected(item.variantId)"
              class="w-4 h-4 rounded border-gray-300" />
            <div class="flex-1 min-w-0">
              <p class="text-sm font-medium text-gray-900 truncate">{{ item.productName }}</p>
              <p class="text-xs text-gray-500">
                {{ item.sku }} · ${{ item.unitPrice.toFixed(2) }} c/u
                <span v-if="stockByVariant[item.variantId] !== undefined" class="text-gray-400">
                  · {{ stockByVariant[item.variantId] }} disponibles
                </span>
              </p>
            </div>
            <input v-model.number="quantities[item.variantId]" type="number" min="0"
              :max="stockByVariant[item.variantId]" :disabled="busyVariantId === item.variantId"
              @change="onQuantityChange(item)"
              class="w-16 rounded-lg border border-gray-300 px-2 py-1 text-sm text-center" />
            <p class="text-sm font-semibold text-gray-900 w-20 text-right">${{ item.lineTotal.toFixed(2) }}</p>
            <button @click="removeItem(item.variantId)" :disabled="busyVariantId === item.variantId"
              class="text-red-500 hover:text-red-700 text-sm">✕</button>
          </div>
          <p v-if="rowError[item.variantId]" class="text-xs text-red-600 mt-2 pl-8">{{ rowError[item.variantId] }}</p>
        </div>
      </div>

      <div class="bg-white border border-gray-200 rounded-xl p-5 h-fit">
        <div class="flex justify-between text-sm mb-2">
          <span class="text-gray-500">Ítems seleccionados</span>
          <span class="text-gray-900">{{ selected.size }}</span>
        </div>
        <div class="flex justify-between text-lg font-semibold mb-4">
          <span>Subtotal</span>
          <span>${{ cartStore.cart.subtotal.toFixed(2) }}</span>
        </div>
        <button @click="goToCheckout" :disabled="selected.size === 0"
          class="w-full bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium rounded-lg py-2.5">
          Continuar al checkout
        </button>
        <p class="text-xs text-gray-400 mt-2 text-center">
          El subtotal de arriba es de todo el carrito — el checkout usa solo lo marcado.
        </p>
      </div>
    </div>
  </div>
</template>
