<script setup>
import { formatMoney } from '../utils/format'
import { imageUrl } from '../utils/images'
import StarRating from './StarRating.vue'

defineProps({
  product: { type: Object, required: true },
  // Resumen de reseñas { average, count }; opcional: sin reseñas no se dibujan estrellas vacías.
  rating: { type: Object, default: null }
})
</script>

<template>
  <article class="group relative overflow-hidden rounded-2xl border border-line bg-surface shadow-card transition-all duration-200 hover:-translate-y-0.5 hover:shadow-card-hover">
    <router-link :to="{ name: 'product-detail', params: { id: product.id } }" class="block focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-500">
      <div class="aspect-square overflow-hidden bg-surface-muted">
        <img v-if="product.primaryImageFileName" :src="imageUrl(product.primaryImageFileName)" :alt="product.name"
          loading="lazy" class="h-full w-full object-cover transition-transform duration-300 group-hover:scale-105" />
        <div v-else class="flex h-full w-full items-center justify-center text-5xl">
          <span>🛍️</span>
        </div>
      </div>

      <div class="space-y-1 p-3.5">
        <p class="truncate text-xs text-ink-muted">{{ product.categoryName }}</p>
        <p class="truncate text-sm font-medium text-ink">{{ product.name }}</p>
        <div v-if="rating && rating.count > 0" class="flex items-center gap-1.5">
          <StarRating :value="rating.average" size="sm" />
          <span class="text-xs text-ink-muted">({{ rating.count }})</span>
        </div>
        <p class="pt-0.5 text-base font-semibold text-brand-ink">
          {{ product.minPrice != null ? formatMoney(product.minPrice) : 'Consultar' }}
        </p>
      </div>
    </router-link>

    <!-- Esquina superior derecha: reservada para acciones (ej. favoritos). -->
    <div class="absolute right-2 top-2">
      <slot name="corner" />
    </div>
  </article>
</template>
