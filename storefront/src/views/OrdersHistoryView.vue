<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useApi } from '../api/useApi'
import { useCartStore } from '../stores/cart'
import { useToastStore } from '../stores/toast'
import { formatMoney } from '../utils/format'
import { timelineSteps, deliveryText, formatMoment, canBuyAgain, buyAgain, buyAgainMessage, couponReleased } from '../utils/orders'
import { pointsFor } from '../utils/loyalty'
import { returnStatus, reasonLabel, returnDeadlineText } from '../utils/returns'
import AccountNav from '../components/AccountNav.vue'
import ReturnRequestForm from '../components/ReturnRequestForm.vue'
import CancelOrderPanel from '../components/CancelOrderPanel.vue'

const apiClient = useApi()
const cartStore = useCartStore()
const toast = useToastStore()
const router = useRouter()

const orders = ref([])
const loading = ref(true)
const error = ref('')
const expandedId = ref(null)
const buyingAgainId = ref(null)
const returningId = ref(null)
const sendingReturn = ref(false)
const cancellingId = ref(null)
const sendingCancel = ref(false)

const statusLabels = {
  PendingPayment: 'Pago pendiente',
  Paid: 'Pagada',
  Shipped: 'Enviada',
  Failed: 'Pago fallido',
  Cancelled: 'Cancelada',
  Refunded: 'Reembolsada'
}
const statusStyles = {
  PendingPayment: 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300',
  Paid: 'bg-blue-100 text-blue-700 dark:bg-blue-500/15 dark:text-blue-300',
  Shipped: 'bg-green-100 text-green-700 dark:bg-green-500/15 dark:text-green-300',
  Failed: 'bg-red-100 text-red-700 dark:bg-red-500/15 dark:text-red-300',
  Cancelled: 'bg-surface-muted text-ink-soft',
  Refunded: 'bg-surface-muted text-ink-soft'
}
const dotStyles = {
  done: 'bg-brand-600 border-brand-600',
  current: 'bg-surface border-brand-600',
  pending: 'bg-surface border-line',
  failed: 'bg-red-600 border-red-600'
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const list = await apiClient.get('/api/orders')
    // Lo más nuevo arriba.
    orders.value = [...list].sort((a, b) => (b.createdAtUtc ?? '').localeCompare(a.createdAtUtc ?? ''))
  } catch (err) {
    error.value = err.message
  } finally {
    loading.value = false
  }
}

function toggle(id) {
  expandedId.value = expandedId.value === id ? null : id
}

async function onBuyAgain(order) {
  buyingAgainId.value = order.orderId
  try {
    const result = await buyAgain(order.lines, (variantId, quantity) =>
      apiClient.post('/api/cart/items', { variantId, quantity }))
    if (result.cart) cartStore.setCart(result.cart)

    const anyAdded = result.added.length + result.partial.length > 0
    toast.push({
      type: anyAdded ? (result.unavailable.length || result.partial.length ? 'info' : 'success') : 'error',
      message: anyAdded ? buyAgainMessage(result) : 'Ninguno de estos productos está disponible ahora.',
      duration: result.unavailable.length || result.partial.length ? 8000 : 4000
    })
    if (anyAdded) router.push({ name: 'cart' })
  } catch (err) {
    toast.push({ type: 'error', message: err.message })
  } finally {
    buyingAgainId.value = null
  }
}

const returnTones = {
  info: 'border-amber-200 bg-amber-50 text-amber-800 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-200',
  success: 'border-emerald-200 bg-emerald-50 text-emerald-800 dark:border-emerald-500/30 dark:bg-emerald-500/10 dark:text-emerald-200',
  danger: 'border-red-200 bg-red-50 text-red-800 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-200'
}

async function onRequestReturn(order, request) {
  sendingReturn.value = true
  try {
    const updated = await apiClient.post(`/api/orders/${order.orderId}/returns`, request)
    orders.value = orders.value.map((o) => (o.orderId === updated.orderId ? updated : o))
    returningId.value = null
    toast.push({ type: 'success', message: 'Recibimos tu solicitud de devolución. Te avisaremos por correo cuando la revisemos.' })
  } catch (err) {
    toast.push({ type: 'error', message: err.message, duration: 8000 })
  } finally {
    sendingReturn.value = false
  }
}

async function onCancel(order, request) {
  sendingCancel.value = true
  try {
    const updated = await apiClient.post(`/api/orders/${order.orderId}/cancel`, request)
    orders.value = orders.value.map((o) => (o.orderId === updated.orderId ? updated : o))
    cancellingId.value = null
    toast.push({
      type: 'success',
      message: updated.status === 'Cancelled'
        ? (couponReleased(updated) ? `Pedido cancelado. Tu cupón ${updated.couponCode} vuelve a estar disponible.` : 'Pedido cancelado.')
        : 'Pedimos la cancelación. Te avisaremos por correo cuando la revisemos.'
    })
  } catch (err) {
    toast.push({ type: 'error', message: err.message, duration: 8000 })
  } finally {
    sendingCancel.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="max-w-2xl mx-auto">
    <AccountNav />
    <h1 class="sr-only">Mis pedidos</h1>

    <div v-if="loading" class="text-center text-ink-muted py-16">Cargando...</div>
    <div v-else-if="error" class="text-center text-red-600 dark:text-red-400 py-16">{{ error }}</div>
    <div v-else-if="orders.length === 0" class="text-center text-ink-muted py-16">
      Todavía no tienes pedidos. <router-link :to="{ name: 'home' }" class="text-brand-ink underline">Ir a comprar</router-link>
    </div>

    <div v-else class="space-y-3">
      <div v-for="o in orders" :key="o.orderId" data-testid="order-item" class="bg-surface border border-line rounded-xl overflow-hidden">
        <button @click="toggle(o.orderId)" class="w-full flex items-center justify-between gap-3 p-4 text-left"
          :aria-expanded="expandedId === o.orderId">
          <div class="min-w-0">
            <p class="text-sm font-medium text-ink">
              Pedido #{{ o.orderId.slice(0, 8) }}
              <span v-if="o.createdAtUtc" class="font-normal text-ink-muted">· {{ formatMoment(o.createdAtUtc) }}</span>
            </p>
            <p class="text-xs text-ink-muted">
              {{ formatMoney(o.totalAmount) }}
              <span v-if="o.couponCode" class="text-emerald-700 dark:text-emerald-400">· cupón {{ o.couponCode }} (−{{ formatMoney(o.discountAmount) }})</span>
              <span v-if="couponReleased(o)" class="text-emerald-700 dark:text-emerald-400" data-testid="order-coupon-released">· el cupón vuelve a estar disponible</span>
              <span v-if="o.loyaltyPoints" class="text-emerald-700 dark:text-emerald-400" data-testid="order-points-used">· {{ o.loyaltyPoints }} puntos (−{{ formatMoney(o.loyaltyDiscount) }})</span>
              <span v-if="o.status === 'Paid' || o.status === 'Shipped'" data-testid="order-points-earned">· +{{ pointsFor(o.totalAmount) }} puntos</span>
              <span v-if="o.refundedAmount > 0" class="text-emerald-700 dark:text-emerald-400" data-testid="order-refunded">· reembolsado {{ formatMoney(o.refundedAmount) }}</span>
            </p>
            <p v-if="deliveryText(o)" class="mt-1 text-xs font-medium text-brand-ink" data-testid="order-delivery">{{ deliveryText(o) }}</p>
          </div>
          <span class="flex-shrink-0 text-xs px-2 py-0.5 rounded-full" :class="statusStyles[o.status] || 'bg-surface-muted text-ink-soft'">
            {{ statusLabels[o.status] || o.status }}
          </span>
        </button>

        <div v-if="expandedId === o.orderId" class="border-t border-line bg-canvas px-4 py-4 space-y-4">
          <!-- Línea de tiempo: un punto por paso (lleno = ya pasó, con borde = lo próximo). -->
          <ol class="space-y-3" data-testid="order-timeline">
            <li v-for="step in timelineSteps(o)" :key="step.key" class="flex gap-3" :data-state="step.state"
              data-testid="order-step">
              <span class="mt-1 h-3 w-3 flex-shrink-0 rounded-full border-2" :class="dotStyles[step.state]" aria-hidden="true"></span>
              <div class="text-sm">
                <p :class="step.state === 'pending' ? 'text-ink-muted' : step.state === 'failed' ? 'text-red-600 dark:text-red-400' : 'text-ink'">
                  {{ step.label }}
                  <span v-if="step.date" class="text-xs text-ink-muted">· {{ formatMoment(step.date) }}</span>
                </p>
                <p v-if="step.estimate" class="text-xs text-ink-muted">{{ step.estimate }} (estimado)</p>
              </div>
            </li>
          </ol>

          <div>
            <p class="mb-1 text-xs font-medium uppercase tracking-wide text-ink-muted">Productos</p>
            <ul class="text-sm text-ink-soft space-y-1">
              <li v-for="line in o.lines" :key="line.variantId">
                {{ line.quantity }}× {{ line.productName }} — {{ formatMoney(line.lineTotal) }}
              </li>
            </ul>
          </div>

          <div v-if="o.shippingAddress">
            <p class="mb-1 text-xs font-medium uppercase tracking-wide text-ink-muted">Se envía a</p>
            <p class="text-sm text-ink-soft" data-testid="order-address">{{ o.shippingAddress }}</p>
          </div>

          <!-- Devoluciones (Fase 7) -->
          <div v-if="o.returns?.length" data-testid="order-returns">
            <p class="mb-1 text-xs font-medium uppercase tracking-wide text-ink-muted">
              {{ o.returns.every((r) => r.isCancellation) ? 'Cancelación' : 'Devoluciones' }}
            </p>
            <ul class="space-y-2">
              <li v-for="r in o.returns" :key="r.returnId" class="rounded-lg border px-3 py-2 text-sm"
                :class="returnTones[returnStatus(r).tone]" data-testid="order-return" :data-status="r.status">
                <p class="font-medium">{{ returnStatus(r).label }}</p>
                <p class="text-xs">
                  {{ r.lines.map((l) => `${l.quantity}× ${l.productName}`).join(', ') }} · {{ reasonLabel(r.reason) }}
                  <span v-if="r.loyaltyPointsToRestore > 0 && r.status === 'Refunded'"> · +{{ r.loyaltyPointsToRestore }} puntos devueltos</span>
                </p>
                <p v-if="r.adminNote" class="mt-1 text-xs" data-testid="order-return-note">Nota de la tienda: {{ r.adminNote }}</p>
              </li>
            </ul>
          </div>

          <CancelOrderPanel v-if="cancellingId === o.orderId" :order="o" :submitting="sendingCancel"
            @submit="(request) => onCancel(o, request)" @cancel="cancellingId = null" />

          <ReturnRequestForm v-if="returningId === o.orderId" :order="o" :submitting="sendingReturn"
            @submit="(request) => onRequestReturn(o, request)" @cancel="returningId = null" />

          <div class="flex flex-wrap items-center gap-2">
            <button v-if="o.canCancel && cancellingId !== o.orderId" type="button" data-testid="cancel-order"
              @click="cancellingId = o.orderId"
              class="rounded-full border border-red-200 px-4 py-2 text-sm font-medium text-red-700 hover:bg-red-50 dark:border-red-500/30 dark:text-red-300 dark:hover:bg-red-500/10">
              Cancelar pedido
            </button>
            <button v-if="o.canRequestReturn && returningId !== o.orderId" type="button" data-testid="request-return"
              @click="returningId = o.orderId"
              class="rounded-full border border-line px-4 py-2 text-sm font-medium text-ink hover:bg-surface-muted">
              Solicitar devolución
            </button>
            <button v-if="canBuyAgain(o)" type="button" data-testid="buy-again" :disabled="buyingAgainId === o.orderId"
              @click="onBuyAgain(o)"
              class="rounded-full bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-60">
              {{ buyingAgainId === o.orderId ? 'Agregando…' : 'Comprar de nuevo' }}
            </button>
          </div>
          <p v-if="o.canRequestReturn" class="text-xs text-ink-muted" data-testid="return-deadline">{{ returnDeadlineText(o, formatMoment) }}</p>
        </div>
      </div>
    </div>
  </div>
</template>
