<script setup>
// Devoluciones (Fase 7): el Admin aprueba (y se reembolsa en PayPal) o rechaza con una nota para el cliente.
import { ref, reactive, onMounted } from 'vue'
import { useApi } from '../../api/useApi'

const apiClient = useApi()

const tabs = [
  { status: 'Requested', label: 'Pendientes' },
  { status: 'Approved', label: 'Reembolso pendiente' },
  { status: 'Refunded', label: 'Reembolsadas' },
  { status: 'Rejected', label: 'Rechazadas' },
  { status: '', label: 'Todas' }
]
const statusLabels = {
  Requested: 'Pendiente',
  Approved: 'Aprobada · reembolso pendiente',
  Refunded: 'Reembolsada',
  Rejected: 'Rechazada'
}
const statusStyles = {
  Requested: 'bg-amber-100 text-amber-700',
  Approved: 'bg-orange-100 text-orange-700',
  Refunded: 'bg-green-100 text-green-700',
  Rejected: 'bg-gray-100 text-gray-600'
}
const reasonLabels = {
  DoesNotFit: 'No le quedó bien',
  Damaged: 'Llegó dañado o con fallas',
  WrongItem: 'No es lo que pidió',
  NotAsDescribed: 'No es como se describía',
  ChangedMind: 'Ya no lo quiere',
  Other: 'Otro motivo'
}

const status = ref('Requested')
const returns = ref([])
const loading = ref(true)
const error = ref('')
const notice = ref('')

// Por devolución: nota escrita, si está escribiendo un rechazo, si hay una acción en curso y su error.
const notes = reactive({})
const rejecting = ref(null)
const busyId = ref(null)
const rowErrors = reactive({})

const money = (value) => `$${Number(value ?? 0).toFixed(2)}`
const shortId = (id) => id.slice(0, 8).toUpperCase()

async function load() {
  loading.value = true
  error.value = ''
  try {
    returns.value = await apiClient.get('/api/orders/returns', { params: { status: status.value, count: 200 } })
  } catch (err) {
    error.value = err.message
  } finally {
    loading.value = false
  }
}

function selectTab(tab) {
  status.value = tab
  notice.value = ''
  rejecting.value = null
  load()
}

async function act(r, action) {
  busyId.value = r.returnId
  rowErrors[r.returnId] = ''
  try {
    const note = (notes[r.returnId] ?? '').trim() || null
    const updated = await apiClient.post(`/api/orders/returns/${r.returnId}/${action}`, { note })
    const what = r.isCancellation ? 'Cancelación' : 'Devolución'
    notice.value = action === 'approve'
      ? `${what} del pedido #${shortId(r.orderId)} reembolsada: ${money(updated.refundAmount)} a ${r.userEmail}.`
      : `${what} del pedido #${shortId(r.orderId)} rechazada. Le avisamos a ${r.userEmail}.`
    rejecting.value = null
    delete notes[r.returnId]
    await load()
  } catch (err) {
    rowErrors[r.returnId] = err.message
    // Si quedó aprobada aunque falló el reembolso, la lista lo tiene que mostrar.
    if (action === 'approve') await load()
  } finally {
    busyId.value = null
  }
}

function reject(r) {
  if (!(notes[r.returnId] ?? '').trim()) {
    rowErrors[r.returnId] = 'Escribe una nota para el cliente explicando por qué se rechaza.'
    return
  }
  act(r, 'reject')
}

onMounted(load)
</script>

<template>
  <div>
    <h1 class="text-2xl font-semibold text-gray-900">Devoluciones</h1>
    <p class="mb-6 text-sm text-gray-500">
      Aprueba cuando recibas el producto: el dinero vuelve al cliente por PayPal, el stock se repone y sus puntos se ajustan.
    </p>

    <div class="mb-4 flex flex-wrap gap-2" role="tablist">
      <button v-for="t in tabs" :key="t.status || 'all'" type="button" role="tab" :aria-selected="status === t.status"
        @click="selectTab(t.status)" :data-testid="`returns-tab-${t.status || 'all'}`"
        class="rounded-full px-3 py-1.5 text-sm font-medium"
        :class="status === t.status ? 'bg-brand-600 text-white' : 'bg-white border border-gray-200 text-gray-600 hover:bg-gray-100'">
        {{ t.label }}
      </button>
    </div>

    <p v-if="notice" class="mb-4 rounded-lg border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-700" role="status">{{ notice }}</p>

    <div v-if="loading" class="rounded-xl border border-gray-200 bg-white p-6 text-sm text-gray-500">Cargando...</div>
    <div v-else-if="error" class="rounded-xl border border-gray-200 bg-white p-6 text-sm text-red-600" role="alert">{{ error }}</div>
    <div v-else-if="returns.length === 0" class="rounded-xl border border-gray-200 bg-white p-6 text-sm text-gray-500" data-testid="returns-empty">
      No hay devoluciones en esta sección.
    </div>

    <ul v-else class="space-y-3">
      <li v-for="r in returns" :key="r.returnId" class="rounded-xl border border-gray-200 bg-white p-4" data-testid="return-row">
        <div class="flex flex-wrap items-start justify-between gap-3">
          <div class="min-w-0">
            <p class="text-sm font-medium text-gray-900">
              <span v-if="r.isCancellation" class="mr-1 rounded-full bg-red-100 px-2 py-0.5 text-xs text-red-700" data-testid="return-is-cancellation">Cancelación</span>
              Pedido #{{ shortId(r.orderId) }} · {{ r.userFullName }}
              <span class="font-normal text-gray-500">({{ r.userEmail }})</span>
            </p>
            <p class="text-xs text-gray-500">Pedida el {{ new Date(r.createdAtUtc).toLocaleString() }}</p>
          </div>
          <span class="rounded-full px-2 py-0.5 text-xs" :class="statusStyles[r.status]">{{ statusLabels[r.status] ?? r.status }}</span>
        </div>

        <ul class="mt-3 text-sm text-gray-700">
          <li v-for="l in r.lines" :key="l.variantId">{{ l.quantity }}× {{ l.productName }} <span class="text-gray-400">· {{ money(l.unitPrice) }} c/u</span></li>
        </ul>
        <p class="mt-2 text-sm text-gray-700"><b>Motivo:</b> {{ reasonLabels[r.reason] ?? r.reason }}<span v-if="r.comment"> — "{{ r.comment }}"</span></p>

        <p v-if="r.status !== 'Rejected'" class="mt-2 text-sm text-gray-900" data-testid="return-amount">
          {{ r.status === 'Requested' ? 'Se devolverán' : 'Devolución' }}: <b>{{ money(r.refundAmount) }}</b>
          <span v-if="r.loyaltyPointsToRestore > 0" class="text-gray-500"> · +{{ r.loyaltyPointsToRestore }} puntos usados</span>
          <span v-if="r.completesOrder" class="text-gray-500"> · completa el pedido</span>
          <span class="text-gray-400"> (pedido de {{ money(r.orderTotal) }}, ya devuelto {{ money(r.orderRefundedAmount) }})</span>
        </p>
        <p v-if="r.adminNote" class="mt-1 text-sm text-gray-600" data-testid="return-admin-note"><b>Nota:</b> {{ r.adminNote }}</p>

        <div v-if="r.status === 'Requested' || r.status === 'Approved'" class="mt-3 space-y-2">
          <p v-if="r.status === 'Approved'" class="text-xs text-gray-500">
            PayPal todavía no confirmó este reembolso. Reintenta; si no se puede reembolsar, cancela la devolución con una nota
            (antes de cancelar se verifica que el dinero no se haya devuelto).
          </p>
          <textarea v-if="r.status === 'Requested' || rejecting === r.returnId" v-model="notes[r.returnId]" rows="2" maxlength="500"
            :aria-label="`Nota para el cliente (pedido ${shortId(r.orderId)})`" data-testid="return-note"
            :placeholder="rejecting === r.returnId ? 'Por qué se rechaza (la ve el cliente)' : 'Nota para el cliente (opcional al aprobar)'"
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm"></textarea>
          <div class="flex flex-wrap gap-2">
            <button v-if="rejecting !== r.returnId" type="button" :disabled="busyId === r.returnId" @click="act(r, 'approve')"
              data-testid="return-approve"
              class="rounded-lg bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-60">
              {{ busyId === r.returnId ? 'Reembolsando…' : r.status === 'Approved' ? 'Reintentar reembolso'
                : r.isCancellation ? `Cancelar el pedido y reembolsar ${money(r.refundAmount)}` : `Aprobar y reembolsar ${money(r.refundAmount)}` }}
            </button>
            <template v-if="r.status === 'Requested' || r.status === 'Approved'">
              <button v-if="rejecting !== r.returnId" type="button" @click="rejecting = r.returnId" data-testid="return-reject"
                class="rounded-lg px-3 py-1.5 text-sm text-red-600 hover:bg-red-50">{{ r.status === 'Approved' ? 'Cancelar devolución' : 'Rechazar' }}</button>
              <template v-else>
                <button type="button" :disabled="busyId === r.returnId" @click="reject(r)" data-testid="return-confirm-reject"
                  class="rounded-lg bg-red-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-60">
                  {{ r.status === 'Approved' ? 'Cancelar y avisar al cliente' : 'Rechazar y avisar al cliente' }}
                </button>
                <button type="button" @click="rejecting = null" class="px-2 py-1.5 text-sm text-gray-600 hover:text-gray-900">Cancelar</button>
              </template>
            </template>
          </div>
          <p v-if="rowErrors[r.returnId]" class="text-sm text-red-600" role="alert">{{ rowErrors[r.returnId] }}</p>
        </div>
      </li>
    </ul>
  </div>
</template>
