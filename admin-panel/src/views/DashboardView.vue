<script setup>
import { ref, onMounted } from 'vue'
import { useApi } from '../api/useApi'

const apiClient = useApi()
const lowStockCount = ref(null)
const loadError = ref('')

onMounted(async () => {
  try {
    const items = await apiClient.get('/api/stock/low-stock')
    lowStockCount.value = items.length
  } catch (err) {
    loadError.value = err.message
  }
})

const sections = [
  { to: { name: 'products' }, title: 'Productos', desc: 'Crear y editar productos, variantes e imágenes.' },
  { to: { name: 'categories' }, title: 'Categorías', desc: 'Organizar el catálogo.' },
  { to: { name: 'inventory' }, title: 'Inventario', desc: 'Ajustar stock y ver alertas de bajo inventario.' },
  { to: { name: 'coupons' }, title: 'Cupones', desc: 'Crear, pausar y ver el uso de los cupones.' },
  { to: { name: 'orders' }, title: 'Órdenes', desc: 'Ver todas las órdenes y marcarlas como enviadas.' },
  { to: { name: 'returns' }, title: 'Devoluciones', desc: 'Aprobar y reembolsar, o rechazar con una nota.' },
  { to: { name: 'reviews' }, title: 'Reseñas', desc: 'Revisar y eliminar reseñas que no cumplen las reglas.' },
  { to: { name: 'admins' }, title: 'Administradores', desc: 'Crear nuevas cuentas de Admin.' },
  { to: { name: 'notifications' }, title: 'Notificaciones', desc: 'Auditoría de emails enviados.' }
]
</script>

<template>
  <div>
    <h1 class="text-2xl font-semibold text-gray-900 mb-1">Panel de Administración</h1>
    <p class="text-gray-500 mb-6">Gestiona el catálogo, el inventario y las órdenes del e-commerce.</p>

    <div v-if="lowStockCount !== null && lowStockCount > 0"
      class="mb-6 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
      ⚠️ Hay <b>{{ lowStockCount }}</b> variante(s) con bajo stock.
      <router-link :to="{ name: 'inventory' }" class="underline font-medium">Ver inventario</router-link>
    </div>
    <div v-if="loadError" class="mb-6 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
      {{ loadError }}
    </div>

    <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
      <router-link v-for="s in sections" :key="s.title" :to="s.to"
        class="block bg-white border border-gray-200 rounded-xl p-5 hover:border-brand-300 hover:shadow-sm transition-all">
        <p class="font-medium text-gray-900 mb-1">{{ s.title }}</p>
        <p class="text-sm text-gray-500">{{ s.desc }}</p>
      </router-link>
    </div>
  </div>
</template>
