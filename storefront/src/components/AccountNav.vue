<script setup>
// Pestañas de "Mi cuenta": se muestran arriba de Mis pedidos, Mis direcciones y Mis puntos.
const links = [
  { name: 'orders', label: 'Mis pedidos' },
  { name: 'addresses', label: 'Mis direcciones' },
  { name: 'points', label: 'Mis puntos' }
]
</script>

<template>
  <nav class="mb-6 flex gap-1 overflow-x-auto border-b border-line" aria-label="Mi cuenta" data-testid="account-nav">
    <router-link v-for="l in links" :key="l.name" :to="{ name: l.name }"
      class="-mb-px whitespace-nowrap border-b-2 border-transparent px-3 py-2 text-sm text-ink-soft hover:text-ink"
      exact-active-class="!border-brand-600 !text-ink font-medium">
      {{ l.label }}
    </router-link>
  </nav>
</template>
