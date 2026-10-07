<script setup>
import { ref, watch } from 'vue'
import { useApi } from '../api/useApi'
import { couponsApi } from '../api/coupons'
import { formatMoney } from '../utils/format'

const props = defineProps({
  subtotal: { type: Number, required: true },
  // El cupón aplicado ({ code, description, discountAmount, total }) o null.
  modelValue: { type: Object, default: null }
})
const emit = defineEmits(['update:modelValue'])

const apiClient = useApi()
const code = ref('')
const checking = ref(false)
const error = ref('')

// Si cambia lo que se va a comprar, el descuento calculado ya no vale: hay que volver a aplicarlo.
watch(() => props.subtotal, () => {
  if (props.modelValue) emit('update:modelValue', null)
})

async function apply() {
  if (!code.value.trim()) return
  error.value = ''
  checking.value = true
  try {
    const quote = await couponsApi.validate(apiClient, code.value, props.subtotal)
    emit('update:modelValue', quote)
    code.value = ''
  } catch (err) {
    emit('update:modelValue', null)
    error.value = err.message || 'No pudimos validar el cupón.'
  } finally {
    checking.value = false
  }
}

function remove() {
  emit('update:modelValue', null)
  error.value = ''
}
</script>

<template>
  <div data-testid="coupon-field">
    <div v-if="modelValue" data-testid="coupon-applied"
      class="flex items-start justify-between gap-3 rounded-lg border border-emerald-300 bg-emerald-50 px-3 py-2 text-sm dark:border-emerald-700 dark:bg-emerald-900/20">
      <div class="min-w-0">
        <p class="font-semibold text-emerald-800 dark:text-emerald-300">
          🏷️ {{ modelValue.code }} · −{{ formatMoney(modelValue.discountAmount) }}
        </p>
        <p class="truncate text-xs text-emerald-700 dark:text-emerald-400">{{ modelValue.description }}</p>
      </div>
      <button type="button" data-testid="coupon-remove" @click="remove"
        class="flex-shrink-0 text-xs font-medium text-ink-muted hover:text-red-600">Quitar</button>
    </div>

    <form v-else class="flex gap-2" @submit.prevent="apply">
      <label for="coupon-code" class="sr-only">Código de cupón</label>
      <input id="coupon-code" v-model="code" type="text" placeholder="¿Tienes un cupón?" autocomplete="off"
        maxlength="30" class="min-w-0 flex-1 rounded-lg border border-line px-3 py-2 text-sm uppercase placeholder:normal-case focus:outline-none focus:ring-2 focus:ring-brand-500" />
      <button type="submit" data-testid="coupon-apply" :disabled="checking || !code.trim()"
        class="rounded-lg border border-brand-600 px-4 py-2 text-sm font-medium text-brand-ink hover:bg-brand-50 disabled:opacity-50 dark:hover:bg-brand-900/30">
        {{ checking ? 'Validando...' : 'Aplicar' }}
      </button>
    </form>

    <p v-if="error" role="alert" data-testid="coupon-error" class="mt-2 text-xs text-red-600 dark:text-red-400">{{ error }}</p>
  </div>
</template>
