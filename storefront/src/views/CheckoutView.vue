<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useApi } from '../api/useApi'
import { useCartStore } from '../stores/cart'

const apiClient = useApi()
const cartStore = useCartStore()
const router = useRouter()

const variantIds = ref([])
const shippingAddress = ref('')
const submitting = ref(false)
const error = ref('')

const selectedItems = computed(() =>
  (cartStore.cart?.items ?? []).filter(i => variantIds.value.includes(i.variantId))
)
const total = computed(() => selectedItems.value.reduce((sum, i) => sum + i.lineTotal, 0))

onMounted(() => {
  try {
    variantIds.value = JSON.parse(sessionStorage.getItem('checkout-variant-ids') || '[]')
  } catch {
    variantIds.value = []
  }

  if (variantIds.value.length === 0) {
    router.replace({ name: 'cart' })
  }
})

async function submit() {
  error.value = ''
  submitting.value = true

  // Clave para que el navegador NO bloquee el pop-up: la ventana se abre acá, de forma
  // síncrona, como reacción directa al clic — todavía en blanco, sin URL. Recién cuando
  // llega la respuesta del checkout (después del await) le asignamos la URL real de
  // PayPal. Si abrimos la ventana DESPUÉS del await, el navegador ya no lo reconoce como
  // una acción iniciada por el usuario y la bloquea en silencio (el bug que reportaste).
  const paypalWindow = window.open('', '_blank')

  try {
    const result = await apiClient.post('/api/orders/checkout', {
      variantIds: variantIds.value,
      shippingAddress: shippingAddress.value
    })
    sessionStorage.removeItem('checkout-variant-ids')

    if (result.approveUrl) {
      if (paypalWindow) {
        paypalWindow.location.href = result.approveUrl
      } else {
        // El navegador bloqueó incluso la ventana en blanco (bloqueador muy agresivo, o
        // pop-ups desactivados del todo). Navegamos a la pantalla de espera igual, pero le
        // pasamos la URL vía sessionStorage para que muestre un link manual — un ref local
        // no serviría, porque este componente se destruye al cambiar de ruta.
        sessionStorage.setItem(`approve-url-${result.orderId}`, result.approveUrl)
      }
    }
    router.push({ name: 'order-pending', params: { id: result.orderId } })
  } catch (err) {
    paypalWindow?.close()
    error.value = err.message
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="max-w-2xl mx-auto">
    <h1 class="text-xl font-semibold text-gray-900 mb-6">Checkout</h1>

    <div class="bg-white border border-gray-200 rounded-xl p-5 mb-5">
      <h2 class="text-sm font-medium text-gray-900 mb-3">Resumen</h2>
      <div v-for="item in selectedItems" :key="item.variantId" class="flex justify-between text-sm py-1.5">
        <span class="text-gray-600">{{ item.quantity }}× {{ item.productName }}</span>
        <span class="text-gray-900">${{ item.lineTotal.toFixed(2) }}</span>
      </div>
      <div class="flex justify-between text-base font-semibold pt-3 mt-2 border-t border-gray-100">
        <span>Total</span>
        <span>${{ total.toFixed(2) }}</span>
      </div>
    </div>

    <form @submit.prevent="submit" class="bg-white border border-gray-200 rounded-xl p-5 space-y-4">
      <div>
        <label for="shipping-address" class="block text-sm font-medium text-gray-700 mb-1">Dirección de envío</label>
        <textarea id="shipping-address" v-model="shippingAddress" required rows="3" placeholder="Calle, número, ciudad, país..."
          class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"></textarea>
      </div>

      <p v-if="error" class="text-sm text-red-600">{{ error }}</p>

      <button type="submit" :disabled="submitting || selectedItems.length === 0"
        class="w-full bg-brand-600 hover:bg-brand-700 disabled:opacity-60 text-white text-sm font-medium rounded-lg py-2.5">
        {{ submitting ? 'Procesando...' : 'Pagar con PayPal' }}
      </button>
      <p class="text-xs text-gray-400 text-center">
        Se abrirá una pestaña nueva de PayPal para aprobar el pago.
      </p>
    </form>
  </div>
</template>
