<script setup>
// Formulario para pedir la devolución de un pedido enviado (Fase 7).
import { reactive, ref, computed } from 'vue'
import { RETURN_REASONS, returnableLines, buildReturnRequest } from '../utils/returns'
import { formatMoney } from '../utils/format'

const props = defineProps({
  order: { type: Object, required: true },
  submitting: { type: Boolean, default: false }
})
const emit = defineEmits(['submit', 'cancel'])

const lines = computed(() => returnableLines(props.order))
// Si hay un solo producto con una sola unidad, ya viene elegido.
const quantities = reactive(Object.fromEntries(
  lines.value.map((l) => [l.variantId, lines.value.length === 1 && l.returnableQuantity === 1 ? 1 : 0])))
const reason = ref('')
const comment = ref('')
const error = ref('')

const input = 'w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm text-ink focus:outline-none focus:ring-2 focus:ring-brand-500'
const label = 'mb-1 block text-xs font-medium text-ink-soft'
const idPrefix = `return-${props.order.orderId.slice(0, 8)}`

function onSubmit() {
  const result = buildReturnRequest(props.order, quantities, reason.value, comment.value)
  error.value = result.error ?? ''
  if (result.request) emit('submit', result.request)
}
</script>

<template>
  <form @submit.prevent="onSubmit" class="space-y-3 rounded-lg border border-line bg-surface p-3" data-testid="return-form" novalidate>
    <p class="text-sm font-medium text-ink">¿Qué quieres devolver?</p>
    <ul class="space-y-2">
      <li v-for="l in lines" :key="l.variantId" class="flex items-center justify-between gap-3 text-sm" data-testid="return-line">
        <label :for="`${idPrefix}-${l.variantId}`" class="min-w-0 text-ink-soft">
          {{ l.productName }}
          <span class="text-xs text-ink-muted">· {{ formatMoney(l.unitPrice) }} c/u · hasta {{ l.returnableQuantity }}</span>
        </label>
        <input :id="`${idPrefix}-${l.variantId}`" v-model.number="quantities[l.variantId]" type="number" min="0"
          :max="l.returnableQuantity" inputmode="numeric" class="w-20 rounded-lg border border-line bg-surface px-2 py-1 text-sm text-ink"
          :aria-label="`Unidades de ${l.productName} a devolver`" />
      </li>
    </ul>

    <div>
      <label :for="`${idPrefix}-reason`" :class="label">Motivo</label>
      <select :id="`${idPrefix}-reason`" v-model="reason" :class="input">
        <option value="" disabled>Elige un motivo</option>
        <option v-for="r in RETURN_REASONS" :key="r.value" :value="r.value">{{ r.label }}</option>
      </select>
    </div>
    <div>
      <label :for="`${idPrefix}-comment`" :class="label">
        Comentario <span v-if="reason !== 'Other'" class="text-ink-muted">(opcional)</span>
      </label>
      <textarea :id="`${idPrefix}-comment`" v-model="comment" :class="input" rows="2" maxlength="500"
        placeholder="Cuéntanos qué pasó"></textarea>
    </div>

    <p class="text-xs text-ink-muted">
      Te devolvemos lo que pagaste por esos productos (con el cupón y los puntos repartidos entre todo el pedido).
      Cuando la aprobemos, el dinero vuelve a tu cuenta de PayPal.
    </p>
    <p v-if="error" class="text-sm text-red-600 dark:text-red-400" role="alert">{{ error }}</p>

    <div class="flex flex-wrap gap-2">
      <button type="submit" :disabled="submitting" data-testid="return-submit"
        class="rounded-full bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-60">
        {{ submitting ? 'Enviando…' : 'Pedir la devolución' }}
      </button>
      <button type="button" @click="emit('cancel')" class="rounded-full px-4 py-2 text-sm text-ink-soft hover:text-ink">Cancelar</button>
    </div>
  </form>
</template>
