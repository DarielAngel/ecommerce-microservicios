<script setup>
import { ref, onMounted } from 'vue'
import { useApi } from '../../api/useApi'

const apiClient = useApi()
const orders = ref([])
const loading = ref(true)
const error = ref('')
const shippingId = ref(null)
const actionError = ref('')
const expandedId = ref(null)

const statusStyles = {
  PendingPayment: 'bg-amber-100 text-amber-700',
  Paid: 'bg-blue-100 text-blue-700',
  Shipped: 'bg-green-100 text-green-700',
  Failed: 'bg-red-100 text-red-700',
  Cancelled: 'bg-gray-100 text-gray-600',
  Refunded: 'bg-purple-100 text-purple-700'
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    orders.value = await apiClient.get('/api/orders/all', { params: { count: 200 } })
  } catch (err) {
    error.value = err.message
  } finally {
    loading.value = false
  }
}

function toggleExpand(id) {
  expandedId.value = expandedId.value === id ? null : id
}

async function markShipped(orderId) {
  actionError.value = ''
  shippingId.value = orderId
  try {
    await apiClient.post(`/api/orders/${orderId}/ship`, {})
    await load()
  } catch (err) {
    actionError.value = err.message
  } finally {
    shippingId.value = null
  }
}

onMounted(load)
</script>

<template>
  <div>
    <h1 class="text-2xl font-semibold text-gray-900 mb-6">Órdenes</h1>
    <p v-if="actionError" class="text-sm text-red-600 mb-4">{{ actionError }}</p>

    <div class="bg-white border border-gray-200 rounded-xl overflow-hidden">
      <div v-if="loading" class="p-6 text-sm text-gray-500">Cargando...</div>
      <div v-else-if="error" class="p-6 text-sm text-red-600">{{ error }}</div>
      <div v-else-if="orders.length === 0" class="p-6 text-sm text-gray-500">Todavía no hay órdenes.</div>
      <table v-else class="w-full text-sm">
        <thead class="bg-gray-50 text-gray-500 text-xs uppercase">
          <tr>
            <th class="text-left px-4 py-2 font-medium">Comprador</th>
            <th class="text-left px-4 py-2 font-medium">Total</th>
            <th class="text-left px-4 py-2 font-medium">Estado</th>
            <th class="text-left px-4 py-2 font-medium">Fecha</th>
            <th class="px-4 py-2"></th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-100">
          <template v-for="o in orders" :key="o.orderId">
            <tr class="cursor-pointer hover:bg-gray-50" @click="toggleExpand(o.orderId)">
              <td class="px-4 py-2.5">
                <p class="text-gray-900">{{ o.userFullName }}</p>
                <p class="text-xs text-gray-500">{{ o.userEmail }}</p>
              </td>
              <td class="px-4 py-2.5 text-gray-900">
                ${{ o.totalAmount.toFixed(2) }}
                <span v-if="o.couponCode" class="block text-xs text-green-700">{{ o.couponCode }} (−${{ o.discountAmount.toFixed(2) }})</span>
              </td>
              <td class="px-4 py-2.5">
                <span class="text-xs px-2 py-0.5 rounded-full" :class="statusStyles[o.status] || 'bg-gray-100 text-gray-600'">
                  {{ o.status }}
                </span>
              </td>
              <td class="px-4 py-2.5 text-gray-500 text-xs">{{ new Date(o.createdAtUtc).toLocaleString() }}</td>
              <td class="px-4 py-2.5 text-right" @click.stop>
                <button v-if="o.status === 'Paid'" @click="markShipped(o.orderId)" :disabled="shippingId === o.orderId"
                  class="text-brand-600 hover:text-brand-700 font-medium text-xs disabled:opacity-50">
                  {{ shippingId === o.orderId ? 'Marcando...' : 'Marcar enviada' }}
                </button>
                <span v-else-if="o.shippedAtUtc" class="text-xs text-gray-500" data-testid="shipped-at">
                  Enviada {{ new Date(o.shippedAtUtc).toLocaleString() }}
                </span>
              </td>
            </tr>
            <tr v-if="expandedId === o.orderId" class="bg-gray-50">
              <td colspan="5" class="px-4 py-3">
                <p class="text-xs text-gray-500 mb-2">📍 {{ o.shippingAddress }}</p>
                <ul class="text-xs text-gray-700 space-y-1">
                  <li v-for="line in o.lines" :key="line.variantId">
                    {{ line.quantity }}× {{ line.productName }} ({{ line.sku }}) — ${{ line.lineTotal.toFixed(2) }}
                  </li>
                </ul>
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </div>
  </div>
</template>
