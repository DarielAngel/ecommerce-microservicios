<script setup>
import ProductCard from './ProductCard.vue'
import FavoriteButton from './FavoriteButton.vue'

// Fila horizontal de tarjetas (relacionados, vistos recientemente). En móvil se desliza; en pantallas
// anchas muestra 4 o 5 por fila.
defineProps({
  title: { type: String, required: true },
  products: { type: Array, required: true },
  testid: { type: String, default: undefined }
})
defineEmits(['clear'])
</script>

<template>
  <section v-if="products.length" :data-testid="testid" class="space-y-3">
    <div class="flex items-baseline justify-between gap-3">
      <h2 class="text-lg font-semibold tracking-tight text-ink">{{ title }}</h2>
      <slot name="action" />
    </div>
    <div class="-mx-4 flex snap-x gap-4 overflow-x-auto px-4 pb-2 sm:mx-0 sm:px-0">
      <div v-for="p in products" :key="p.id" class="w-40 flex-shrink-0 snap-start sm:w-48">
        <ProductCard :product="p">
          <template #corner>
            <FavoriteButton :product-id="p.id" />
          </template>
        </ProductCard>
      </div>
    </div>
  </section>
</template>
