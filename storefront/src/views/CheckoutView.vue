<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useApi } from '../api/useApi'
import { useCartStore } from '../stores/cart'
import { useAuthStore } from '../stores/auth'
import { addressesApi } from '../api/addresses'
import { emptyAddress, formatAddress, isComplete, errorText } from '../utils/addresses'
import CouponField from '../components/CouponField.vue'
import AddressForm from '../components/AddressForm.vue'

const apiClient = useApi()
const cartStore = useCartStore()
const router = useRouter()
const auth = useAuthStore()

const variantIds = ref([])

// Dirección de envío (Fase 5): una de la libreta (la predeterminada ya viene elegida) u otra nueva,
// que por defecto se guarda para la próxima compra.
const addresses = ref([])
const addressesLoaded = ref(false)
const selectedAddressId = ref('new')
const newAddress = ref(emptyAddress(auth.fullName ?? ''))
const saveNewAddress = ref(true)
const usingNewAddress = computed(() => selectedAddressId.value === 'new')
const submitting = ref(false)
const error = ref('')

const selectedItems = computed(() =>
  (cartStore.cart?.items ?? []).filter(i => variantIds.value.includes(i.variantId))
)
const subtotal = computed(() => selectedItems.value.reduce((sum, i) => sum + i.lineTotal, 0))
const coupon = ref(null) // { code, description, discountAmount, total } o null
const total = computed(() => subtotal.value - (coupon.value?.discountAmount ?? 0))

onMounted(() => {
  try {
    variantIds.value = JSON.parse(sessionStorage.getItem('checkout-variant-ids') || '[]')
  } catch {
    variantIds.value = []
  }

  if (variantIds.value.length === 0) {
    router.replace({ name: 'cart' })
    return
  }
  loadAddresses()
})

async function loadAddresses() {
  try {
    addresses.value = await addressesApi.list(apiClient)
    const preferred = addresses.value.find((a) => a.isDefault) ?? addresses.value[0]
    if (preferred) selectedAddressId.value = preferred.id
  } catch {
    // Sin libreta (o Users caído) se puede pagar igual escribiendo la dirección.
    addresses.value = []
  } finally {
    addressesLoaded.value = true
  }
}

/** El texto que viaja a Órdenes; si la dirección es nueva y se guarda, usa el del servidor. */
async function resolveShippingAddress() {
  if (!usingNewAddress.value) {
    return addresses.value.find((a) => a.id === selectedAddressId.value).formatted
  }
  if (!saveNewAddress.value) return formatAddress(newAddress.value)

  const saved = await addressesApi.create(apiClient, {
    ...newAddress.value,
    makeDefault: addresses.value.length === 0
  })
  // Desde ahora es una dirección más de la libreta: si el pago falla y reintenta, ya aparece elegida.
  addresses.value = [...addresses.value, saved]
  selectedAddressId.value = saved.id
  return saved.formatted
}

async function submit() {
  error.value = ''
  if (usingNewAddress.value && !isComplete(newAddress.value)) {
    error.value = 'Completa la dirección de envío: quién recibe, la calle y el número, la ciudad y el país.'
    return
  }
  submitting.value = true

  // Clave para que el navegador NO bloquee el pop-up: la ventana se abre acá, de forma
  // síncrona, como reacción directa al clic — todavía en blanco, sin URL. Recién cuando
  // llega la respuesta del checkout (después del await) le asignamos la URL real de
  // PayPal. Si abrimos la ventana DESPUÉS del await, el navegador ya no lo reconoce como
  // una acción iniciada por el usuario y la bloquea en silencio (el bug que reportaste).
  const paypalWindow = window.open('', '_blank')

  try {
    const shippingAddress = await resolveShippingAddress()
    const result = await apiClient.post('/api/orders/checkout', {
      variantIds: variantIds.value,
      shippingAddress,
      couponCode: coupon.value?.code ?? null
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
    error.value = errorText(err)
    // Si lo que falló fue el cupón (se agotó o venció mientras tanto), lo quitamos para que el
    // cliente pueda pagar sin él o probar otro.
    if (coupon.value && /cup[oó]n/i.test(err.message || '')) coupon.value = null
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="max-w-2xl mx-auto">
    <h1 class="text-xl font-semibold text-ink mb-6">Checkout</h1>

    <div class="bg-surface border border-line rounded-xl p-5 mb-5">
      <h2 class="text-sm font-medium text-ink mb-3">Resumen</h2>
      <div v-for="item in selectedItems" :key="item.variantId" class="flex justify-between text-sm py-1.5">
        <span class="text-ink-soft">{{ item.quantity }}× {{ item.productName }}</span>
        <span class="text-ink">${{ item.lineTotal.toFixed(2) }}</span>
      </div>
      <div class="mt-2 border-t border-line pt-3 space-y-1.5">
        <div class="flex justify-between text-sm">
          <span class="text-ink-soft">Subtotal</span>
          <span class="text-ink" data-testid="checkout-subtotal">${{ subtotal.toFixed(2) }}</span>
        </div>
        <div v-if="coupon" class="flex justify-between text-sm text-emerald-700 dark:text-emerald-400">
          <span>Cupón {{ coupon.code }}</span>
          <span data-testid="checkout-discount">−${{ coupon.discountAmount.toFixed(2) }}</span>
        </div>
        <div class="flex justify-between text-base font-semibold">
          <span>Total</span>
          <span data-testid="checkout-total">${{ total.toFixed(2) }}</span>
        </div>
      </div>
      <div class="mt-4">
        <CouponField v-model="coupon" :subtotal="subtotal" />
      </div>
    </div>

    <form @submit.prevent="submit" class="bg-surface border border-line rounded-xl p-5 space-y-4">
      <fieldset>
        <legend class="mb-2 flex w-full items-center justify-between text-sm font-medium text-ink-soft">
          <span>Dirección de envío</span>
          <router-link :to="{ name: 'addresses' }" class="text-xs font-normal text-brand-ink hover:underline">Administrar direcciones</router-link>
        </legend>

        <div v-if="!addressesLoaded" class="py-3 text-sm text-ink-muted">Cargando tus direcciones...</div>
        <div v-else class="space-y-2" data-testid="checkout-addresses">
          <label v-for="a in addresses" :key="a.id" data-testid="checkout-address-option"
            class="flex cursor-pointer items-start gap-3 rounded-lg border p-3 text-sm"
            :class="selectedAddressId === a.id ? 'border-brand-500 bg-brand-soft' : 'border-line'">
            <input v-model="selectedAddressId" type="radio" name="shipping" :value="a.id" class="mt-1" />
            <span class="min-w-0">
              <span class="block font-medium text-ink">
                {{ a.label }}
                <span v-if="a.isDefault" class="ml-1 text-xs font-normal text-ink-muted">(predeterminada)</span>
              </span>
              <span class="block text-ink-soft">{{ a.formatted }}</span>
            </span>
          </label>

          <label v-if="addresses.length" class="flex cursor-pointer items-center gap-3 rounded-lg border p-3 text-sm"
            :class="usingNewAddress ? 'border-brand-500 bg-brand-soft' : 'border-line'">
            <input v-model="selectedAddressId" type="radio" name="shipping" value="new" data-testid="checkout-address-new" />
            <span class="font-medium text-ink">Enviar a otra dirección</span>
          </label>

          <div v-if="usingNewAddress" class="space-y-3 rounded-lg border border-line p-3" data-testid="checkout-new-address">
            <AddressForm v-model="newAddress" id-prefix="shipping" :show-label="saveNewAddress" :show-make-default="false" />
            <label class="flex items-center gap-2 text-sm text-ink-soft">
              <input v-model="saveNewAddress" type="checkbox" class="h-4 w-4" data-testid="checkout-save-address" />
              Guardarla en mis direcciones para la próxima compra
            </label>
          </div>
        </div>
      </fieldset>

      <p v-if="error" class="text-sm text-red-600 dark:text-red-400">{{ error }}</p>

      <button type="submit" :disabled="submitting || selectedItems.length === 0"
        class="w-full bg-brand-600 hover:bg-brand-700 disabled:opacity-60 text-white text-sm font-medium rounded-lg py-2.5">
        {{ submitting ? 'Procesando...' : 'Pagar con PayPal' }}
      </button>
      <p class="text-xs text-ink-muted text-center">
        Se abrirá una pestaña nueva de PayPal para aprobar el pago.
      </p>
    </form>
  </div>
</template>
