<script setup>
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

const links = [
  { to: { name: 'dashboard' }, label: 'Inicio', icon: '🏠' },
  { to: { name: 'products' }, label: 'Productos', icon: '📦' },
  { to: { name: 'categories' }, label: 'Categorías', icon: '🏷️' },
  { to: { name: 'inventory' }, label: 'Inventario', icon: '📊' },
  { to: { name: 'coupons' }, label: 'Cupones', icon: '🎟️' },
  { to: { name: 'orders' }, label: 'Órdenes', icon: '🧾' },
  { to: { name: 'returns' }, label: 'Devoluciones', icon: '↩️' },
  { to: { name: 'reviews' }, label: 'Reseñas', icon: '⭐' },
  { to: { name: 'admins' }, label: 'Administradores', icon: '👤' },
  { to: { name: 'notifications' }, label: 'Notificaciones', icon: '✉️' }
]

function isActive(name) {
  return route.name === name
}

function logout() {
  auth.logout()
  router.push({ name: 'login' })
}
</script>

<template>
  <div class="min-h-screen flex bg-gray-50">
    <aside class="w-60 bg-white border-r border-gray-200 flex flex-col">
      <div class="px-5 py-5 border-b border-gray-200">
        <p class="text-sm font-semibold text-gray-900">Panel de Admin</p>
        <p class="text-xs text-gray-500 truncate">{{ auth.fullName }}</p>
      </div>
      <nav class="flex-1 px-3 py-4 space-y-1">
        <router-link v-for="link in links" :key="link.label" :to="link.to"
          class="flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm font-medium transition-colors"
          :class="isActive(link.to.name) ? 'bg-brand-50 text-brand-700' : 'text-gray-600 hover:bg-gray-100'">
          <span>{{ link.icon }}</span>
          <span>{{ link.label }}</span>
        </router-link>
      </nav>
      <div class="p-3 border-t border-gray-200">
        <button @click="logout"
          class="w-full text-sm text-gray-600 hover:text-gray-900 hover:bg-gray-100 rounded-lg px-3 py-2 text-left transition-colors">
          Cerrar sesión
        </button>
      </div>
    </aside>

    <main class="flex-1 p-8 overflow-y-auto">
      <router-view />
    </main>
  </div>
</template>
