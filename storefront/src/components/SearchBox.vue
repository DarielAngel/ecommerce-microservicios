<script setup>
import { ref, computed, watch, onBeforeUnmount } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { fetchSuggestions } from '../api/catalog'
import { formatMoney } from '../utils/format'
import { imageUrl } from '../utils/images'

const props = defineProps({
  // Espera entre teclas antes de consultar (las pruebas la ponen en 0).
  debounceMs: { type: Number, default: 200 }
})

const router = useRouter()
const route = useRoute()

const query = ref(route.query.q ?? '')
const suggestions = ref([])
const open = ref(false)
const activeIndex = ref(-1) // -1 = ninguna opción resaltada
let timer = null
let requestId = 0

watch(() => route.query.q, (value) => { query.value = value ?? '' })

// Opciones del menú: las sugerencias y, al final, "ver todos los resultados".
const options = computed(() => [
  ...suggestions.value.map((p) => ({ kind: 'product', product: p })),
  ...(query.value.trim().length >= 2 ? [{ kind: 'all' }] : [])
])
const activeId = computed(() => (activeIndex.value >= 0 ? `search-option-${activeIndex.value}` : undefined))

function onInput() {
  cancelPending()
  const term = query.value
  if (term.trim().length < 2) {
    suggestions.value = []
    open.value = false
    activeIndex.value = -1
    return
  }
  timer = setTimeout(async () => {
    const id = ++requestId
    const result = await fetchSuggestions(term)
    // Si el cliente siguió escribiendo, esta respuesta ya es vieja: se descarta.
    if (id !== requestId) return
    suggestions.value = result
    activeIndex.value = -1
    open.value = true
  }, props.debounceMs)
}

// Cancela la consulta pendiente y descarta la que esté en vuelo: si el cliente ya buscó
// (Enter), eligió o salió del campo, una respuesta tardía no debe reabrir el menú encima
// de los resultados.
function cancelPending() {
  clearTimeout(timer)
  requestId++
}

function close() {
  cancelPending()
  open.value = false
  activeIndex.value = -1
}

function goToProduct(product) {
  close()
  router.push({ name: 'product-detail', params: { id: product.id } })
}

function submitSearch() {
  close()
  suggestions.value = [] // eran de lo escrito antes de buscar; al volver a enfocar no reaparecen
  const q = query.value.trim()
  router.push({ name: 'home', query: q ? { q } : {} })
}

function choose(option) {
  if (option.kind === 'product') goToProduct(option.product)
  else submitSearch()
}

function onKeydown(event) {
  if (event.key === 'Escape') {
    close()
    return
  }
  if (!open.value || options.value.length === 0) return
  if (event.key === 'ArrowDown') {
    event.preventDefault()
    activeIndex.value = (activeIndex.value + 1) % options.value.length
  } else if (event.key === 'ArrowUp') {
    event.preventDefault()
    activeIndex.value = activeIndex.value <= 0 ? options.value.length - 1 : activeIndex.value - 1
  }
}

function onSubmit() {
  // Enter con una opción resaltada la abre; sin resaltar, busca lo escrito (como siempre).
  if (open.value && activeIndex.value >= 0) choose(options.value[activeIndex.value])
  else submitSearch()
}

onBeforeUnmount(() => clearTimeout(timer))
</script>

<template>
  <form role="search" @submit.prevent="onSubmit" class="relative">
    <label for="global-search" class="sr-only">Buscar productos</label>
    <div class="relative">
      <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.8" stroke="currentColor"
        class="pointer-events-none absolute left-3 top-1/2 h-5 w-5 -translate-y-1/2 text-ink-muted" aria-hidden="true">
        <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
      </svg>
      <input id="global-search" v-model="query" type="search" placeholder="Buscar productos..." autocomplete="off"
        role="combobox" aria-autocomplete="list" aria-controls="search-suggestions" :aria-expanded="open"
        :aria-activedescendant="activeId"
        @input="onInput" @keydown="onKeydown" @blur="close" @focus="suggestions.length && (open = true)"
        class="w-full rounded-full border border-line bg-surface-muted py-2 pl-10 pr-4 text-sm focus:border-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-500/40" />
    </div>

    <ul v-show="open" id="search-suggestions" role="listbox" aria-label="Sugerencias"
      class="absolute left-0 right-0 top-full z-40 mt-2 overflow-hidden rounded-2xl border border-line bg-surface py-1 shadow-card-hover">
      <li v-if="suggestions.length === 0" class="px-4 py-3 text-sm text-ink-muted" data-testid="search-no-results">
        No encontramos productos con "{{ query.trim() }}".
      </li>
      <!-- mousedown.prevent: el clic no le quita el foco al input antes de elegir la opción -->
      <li v-for="(option, i) in options" :id="`search-option-${i}`" :key="option.kind === 'product' ? option.product.id : 'all'"
        role="option" :aria-selected="i === activeIndex" data-testid="search-option"
        @mousedown.prevent="choose(option)" @mousemove="activeIndex = i"
        class="flex cursor-pointer items-center gap-3 px-3 py-2 text-sm"
        :class="i === activeIndex ? 'bg-surface-muted' : ''">
        <template v-if="option.kind === 'product'">
          <span class="flex h-10 w-10 flex-shrink-0 items-center justify-center overflow-hidden rounded-lg bg-surface-muted">
            <img v-if="option.product.primaryImageFileName" :src="imageUrl(option.product.primaryImageFileName)" alt=""
              class="h-full w-full object-cover" />
            <span v-else aria-hidden="true">🛍️</span>
          </span>
          <span class="min-w-0 flex-1">
            <span class="block truncate font-medium text-ink">{{ option.product.name }}</span>
            <span class="block truncate text-xs text-ink-muted">{{ option.product.categoryName }}</span>
          </span>
          <span v-if="option.product.minPrice != null" class="flex-shrink-0 font-semibold text-brand-ink">
            {{ formatMoney(option.product.minPrice) }}
          </span>
        </template>
        <span v-else class="w-full border-t border-line pt-2 text-brand-ink" data-testid="search-see-all">
          Ver todos los resultados para "{{ query.trim() }}"
        </span>
      </li>
    </ul>
  </form>
</template>
