<script setup>
import { computed, ref } from 'vue'

const props = defineProps({
  value: { type: Number, default: 0 },      // modo lectura: admite decimales (ej. 4.5)
  modelValue: { type: Number, default: 0 }, // modo interactivo: v-model, entero 1..5
  interactive: { type: Boolean, default: false },
  size: { type: String, default: 'md' }     // 'sm' | 'md' | 'lg'
})
const emit = defineEmits(['update:modelValue'])

const STARS = [1, 2, 3, 4, 5]
const hovered = ref(0)

const sizeClass = computed(() => ({ sm: 'h-3.5 w-3.5', md: 'h-5 w-5', lg: 'h-7 w-7' })[props.size] ?? 'h-5 w-5')

const clamp = (n) => Math.min(5, Math.max(0, Number(n) || 0))
const displayValue = computed(() => clamp(props.value))
const fillPercent = computed(() => (displayValue.value / 5) * 100)
const ariaLabel = computed(() => `Calificación: ${displayValue.value.toFixed(1)} de 5`)

// En modo interactivo, al pasar el mouse se previsualiza esa calificación.
const activeValue = computed(() => hovered.value || props.modelValue)

function select(n) { emit('update:modelValue', n) }

function onKeydown(event) {
  const step = { ArrowRight: 1, ArrowUp: 1, ArrowLeft: -1, ArrowDown: -1 }[event.key]
  if (!step) return
  event.preventDefault()

  const next = props.modelValue + step
  if (next < 1 || next > 5) return
  select(next)
}

const STAR_PATH = 'M10.788 3.21c.448-1.077 1.976-1.077 2.424 0l2.082 5.006 5.404.434c1.164.093 1.636 1.545.749 2.305l-4.117 3.527 1.257 5.273c.271 1.136-.964 2.033-1.96 1.425L12 18.354 7.373 21.18c-.996.608-2.231-.29-1.96-1.425l1.257-5.273-4.117-3.527c-.887-.76-.415-2.212.749-2.305l5.404-.434 2.082-5.005Z'
</script>

<template>
  <!-- Modo lectura: capa gris de fondo + capa dorada recortada al porcentaje del valor. -->
  <span v-if="!interactive" role="img" :aria-label="ariaLabel" class="relative inline-flex align-middle">
    <span class="inline-flex text-line" aria-hidden="true">
      <svg v-for="n in STARS" :key="n" viewBox="0 0 24 24" fill="currentColor" :class="sizeClass"><path :d="STAR_PATH" /></svg>
    </span>
    <span data-testid="stars-fill" class="absolute inset-y-0 left-0 overflow-hidden" :style="{ width: fillPercent + '%' }" aria-hidden="true">
      <span class="inline-flex text-accent-500">
        <svg v-for="n in STARS" :key="n" viewBox="0 0 24 24" fill="currentColor" :class="[sizeClass, 'shrink-0']"><path :d="STAR_PATH" /></svg>
      </span>
    </span>
  </span>

  <!-- Modo interactivo: grupo de opciones accesible (teclado + lector de pantalla). -->
  <span v-else role="radiogroup" aria-label="Tu calificación" class="inline-flex" tabindex="0" @keydown="onKeydown" @mouseleave="hovered = 0">
    <button v-for="n in STARS" :key="n" type="button" role="radio" tabindex="-1"
      :aria-checked="String(modelValue === n)" :aria-label="n === 1 ? '1 estrella' : `${n} estrellas`"
      class="rounded p-0.5 transition-transform hover:scale-110 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-500"
      :class="n <= activeValue ? 'text-accent-500' : 'text-line'"
      @click="select(n)" @mouseenter="hovered = n">
      <svg viewBox="0 0 24 24" fill="currentColor" :class="sizeClass" aria-hidden="true"><path :d="STAR_PATH" /></svg>
    </button>
  </span>
</template>
