<script setup>
import { ref, onMounted } from 'vue'
import { useApi } from '../api/useApi'
import { useCartStore } from '../stores/cart'

const props = defineProps({ id: { type: String, required: true } })

const apiClient = useApi()
const cartStore = useCartStore()

const confirming = ref(false)
const error = ref('')
const result = ref(null)
const manualApproveUrl = ref('')

onMounted(() => {
  // Si el checkout no pudo abrir el pop-up de PayPal solo (bloqueador agresivo), CheckoutView
  // dejó la URL guardada acá para que el usuario la abra manualmente con un clic propio.
  const key = `approve-url-${props.id}`
  const stored = sessionStorage.getItem(key)
  if (stored) {
    manualApproveUrl.value = stored
    sessionStorage.removeItem(key)
  }
})

async function confirmPayment() {
  error.value = ''
  confirming.value = true
  try {
    result.value = await apiClient.post(`/api/orders/${props.id}/confirm-payment`, {})

    if (result.value.status === 'Paid') {
      // El carrito cambió en el servidor (Órdenes quita los ítems comprados) — refrescamos el badge.
      try {
        cartStore.setCart(await apiClient.get('/api/cart'))
      } catch {
        // no crítico
      }
    }
  } catch (err) {
    error.value = err.message
  } finally {
    confirming.value = false
  }
}
</script>

<template>
  <div class="max-w-md mx-auto text-center mt-8">
    <div v-if="!result">
      <h1 class="text-xl font-semibold text-ink mb-2">Aprobando tu pago</h1>
      <p class="text-sm text-ink-soft mb-4">
        Se abrió una pestaña de PayPal para que apruebes el pago. Cuando termines ahí, vuelve
        aquí y confirma.
      </p>
      <p v-if="manualApproveUrl" class="text-sm bg-amber-50 border border-amber-200 text-amber-800 rounded-lg px-4 py-3 mb-6">
        Tu navegador bloqueó la pestaña automática.
        <a :href="manualApproveUrl" target="_blank" class="underline font-medium">Ábrela aquí manualmente</a>.
      </p>
      <button @click="confirmPayment" :disabled="confirming"
        class="bg-brand-600 hover:bg-brand-700 disabled:opacity-60 text-white text-sm font-medium rounded-lg px-6 py-2.5">
        {{ confirming ? 'Confirmando...' : 'Ya aprobé el pago — confirmar' }}
      </button>
      <p v-if="error" class="text-sm text-red-600 dark:text-red-400 mt-4">{{ error }}</p>
    </div>

    <div v-else-if="result.status === 'Paid'">
      <div class="text-5xl mb-3">🎉</div>
      <h1 class="text-xl font-semibold text-ink mb-2">¡Pago confirmado!</h1>
      <p class="text-sm text-ink-soft mb-6">
        Total pagado: ${{ result.totalAmount.toFixed(2) }}
        <span v-if="result.couponCode" class="block text-xs text-emerald-700 dark:text-emerald-400">
          Ahorraste ${{ result.discountAmount.toFixed(2) }} con el cupón {{ result.couponCode }}
        </span>
      </p>
      <router-link :to="{ name: 'orders' }" class="text-brand-ink font-medium text-sm underline">
        Ver mis pedidos
      </router-link>
    </div>

    <div v-else>
      <div class="text-5xl mb-3">⚠️</div>
      <h1 class="text-xl font-semibold text-ink mb-2">El pago no se pudo completar</h1>
      <p class="text-sm text-ink-soft mb-6">Estado: {{ result.status }}. El stock reservado ya se liberó.</p>
      <router-link :to="{ name: 'cart' }" class="text-brand-ink font-medium text-sm underline">
        Volver al carrito
      </router-link>
    </div>
  </div>
</template>
