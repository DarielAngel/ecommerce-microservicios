<script setup>
import { ref, onMounted } from 'vue'
import { useApi } from '../api/useApi'

const apiClient = useApi()
const orders = ref([])
const loading = ref(true)
const error = ref('')
const expandedId = ref(null)

const statusLabels = {
  PendingPayment: 'Pago pendiente',
  Paid: 'Pagada',
  Shipped: 'Enviada',
  Failed: 'Pago fallido',
  Cancelled: 'Cancelada'
}
const statusStyles = {
  PendingPayment: 'bg-amber-100 text-amber-700',
  Paid: 'bg-blue-100 text-blue-700',
  Shipped: 'bg-green-100 text-green-700',
  Failed: 'bg-red-100 text-red-700',
  Cancelled: 'bg-gray-100 text-gray-600'
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    orders.value = await apiClient.get('/api/orders')
  } catch (err) {
    error.value = err.message
  } finally {
    loading.value = false
  }
}

function toggle(id) {
  expandedId.value = expandedId.value === id ? null : id
}

onMounted(load)
</script>

<template>
  <div class="max-w-2xl mx-auto">
    <h1 class="text-xl font-semibold text-gray-900 mb-6">Mis pedidos</h1>

    <div v-if="loading" class="text-center text-gray-400 py-16">Cargando...</div>
    <div v-else-if="error" class="text-center text-red-600 py-16">{{ error }}</div>
    <div v-else-if="orders.length === 0" class="text-center text-gray-400 py-16">
      Todavía no tienes pedidos. <router-link :to="{ name: 'home' }" class="text-brand-600 underline">Ir a comprar</router-link>
    </div>

    <div v-else class="space-y-3">
      <div v-for="o in orders" :key="o.orderId" class="bg-white border border-gray-200 rounded-xl overflow-hidden">
        <button @click="toggle(o.orderId)" class="w-full flex items-center justify-between p-4 text-left">
          <div>
            <p class="text-sm font-medium text-gray-900">Pedido #{{ o.orderId.slice(0, 8) }}</p>
            <p class="text-xs text-gray-500">${{ o.totalAmount.toFixed(2) }}</p>
          </div>
          <span class="text-xs px-2 py-0.5 rounded-full" :class="statusStyles[o.status] || 'bg-gray-100 text-gray-600'">
            {{ statusLabels[o.status] || o.status }}
          </span>
        </button>
        <div v-if="expandedId === o.orderId" class="border-t border-gray-100 px-4 py-3 bg-gray-50">
          <ul class="text-xs text-gray-700 space-y-1">
            <li v-for="line in o.lines" :key="line.variantId">
              {{ line.quantity }}× {{ line.productName }} — ${{ line.lineTotal.toFixed(2) }}
            </li>
          </ul>
        </div>
      </div>
    </div>
  </div>
</template>
