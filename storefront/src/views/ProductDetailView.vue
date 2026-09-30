<script setup>
import { ref, onMounted, watch, computed } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '../api/client'
import { useApi } from '../api/useApi'
import { useAuthStore } from '../stores/auth'
import { useCartStore } from '../stores/cart'

const props = defineProps({ id: { type: String, required: true } })

const router = useRouter()
const auth = useAuthStore()
const cartStore = useCartStore()
const apiClient = useApi()

const product = ref(null)
const loading = ref(true)
const error = ref('')

const selectedVariantId = ref('')
const quantity = ref(1)
const adding = ref(false)
const addError = ref('')
const addSuccess = ref(false)
const availableStock = ref(null) // null = no cargado (o no logueado todavía)

const selectedVariant = computed(() =>
  product.value?.variants.find(v => v.id === selectedVariantId.value) ?? null
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

onMounted(async () => {
  try {
    product.value = await api.get(`/api/products/${props.id}`)
    if (product.value.variants.length > 0) {
      selectedVariantId.value = product.value.variants[0].id
      await loadStock(selectedVariantId.value)
    }
  } catch (err) {
    error.value = err.message
  } finally {
    loading.value = false
  }
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
  <div v-if="loading" class="text-center text-gray-400 py-16">Cargando...</div>
  <div v-else-if="error" class="text-center text-red-600 py-16">{{ error }}</div>

  <div v-else class="grid grid-cols-1 md:grid-cols-2 gap-8">
    <div class="aspect-square bg-gray-100 rounded-xl flex items-center justify-center text-7xl">
      🛒
    </div>

    <div>
      <h1 class="text-2xl font-semibold text-gray-900 mb-2">{{ product.name }}</h1>
      <p class="text-gray-600 mb-4">{{ product.description }}</p>

      <div v-if="selectedVariant" class="text-2xl font-semibold text-gray-900 mb-4">
        ${{ selectedVariant.price.toFixed(2) }}
      </div>

      <div v-if="product.variants.length > 1" class="mb-4">
        <label class="block text-sm font-medium text-gray-700 mb-1">Variante</label>
        <select v-model="selectedVariantId" class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm">
          <option v-for="v in product.variants" :key="v.id" :value="v.id">
            {{ v.sku }} — {{ Object.entries(v.attributes).map(([k, val]) => `${k}: ${val}`).join(', ') || 'Sin atributos' }}
          </option>
        </select>
      </div>

      <div class="flex items-end gap-3 mb-4">
        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">
            Cantidad
            <span v-if="availableStock !== null" class="text-gray-400 font-normal">({{ availableStock }} disponibles)</span>
          </label>
          <input v-model="quantity" type="number" min="1" :max="availableStock ?? undefined"
            class="w-24 rounded-lg border border-gray-300 px-3 py-2 text-sm" />
        </div>
        <button @click="addToCart" :disabled="adding || !selectedVariantId"
          class="bg-brand-600 hover:bg-brand-700 disabled:opacity-60 text-white text-sm font-medium rounded-lg px-6 py-2.5">
          {{ adding ? 'Agregando...' : 'Agregar al carrito' }}
        </button>
      </div>

      <p v-if="addError" class="text-sm text-red-600">{{ addError }}</p>
      <p v-if="addSuccess" class="text-sm text-green-600">
        Agregado al carrito. <router-link :to="{ name: 'cart' }" class="underline font-medium">Ver carrito</router-link>
      </p>
    </div>
  </div>
</template>
