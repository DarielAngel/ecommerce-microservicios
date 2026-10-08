<script setup>
import { ref, onMounted } from 'vue'
import { useApi } from '../../api/useApi'

const apiClient = useApi()
const items = ref([])
const loading = ref(true)
const error = ref('')

const typeLabels = {
  UserRegistered: 'Bienvenida',
  OrderPaid: 'Confirmación de pedido',
  OrderShipped: 'Pedido enviado',
  CartAbandoned: 'Carrito abandonado',
  ReturnRefunded: 'Devolución reembolsada',
  ReturnRejected: 'Devolución rechazada'
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    items.value = await apiClient.get('/api/notifications', { params: { count: 100 } })
  } catch (err) {
    error.value = err.message
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div>
    <h1 class="text-2xl font-semibold text-gray-900 mb-6">Notificaciones enviadas</h1>

    <div class="bg-white border border-gray-200 rounded-xl overflow-hidden">
      <div v-if="loading" class="p-6 text-sm text-gray-500">Cargando...</div>
      <div v-else-if="error" class="p-6 text-sm text-red-600">{{ error }}</div>
      <div v-else-if="items.length === 0" class="p-6 text-sm text-gray-500">Todavía no se ha enviado ningún email.</div>
      <table v-else class="w-full text-sm">
        <thead class="bg-gray-50 text-gray-500 text-xs uppercase">
          <tr>
            <th class="text-left px-4 py-2 font-medium">Tipo</th>
            <th class="text-left px-4 py-2 font-medium">Destinatario</th>
            <th class="text-left px-4 py-2 font-medium">Referencia</th>
            <th class="text-left px-4 py-2 font-medium">Enviado</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-100">
          <tr v-for="n in items" :key="n.id">
            <td class="px-4 py-2.5 text-gray-900">{{ typeLabels[n.type] || n.type }}</td>
            <td class="px-4 py-2.5 text-gray-700">{{ n.recipientEmail }}</td>
            <td class="px-4 py-2.5 font-mono text-xs text-gray-500">{{ n.referenceId }}</td>
            <td class="px-4 py-2.5 text-gray-500 text-xs">{{ new Date(n.sentAtUtc).toLocaleString() }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
