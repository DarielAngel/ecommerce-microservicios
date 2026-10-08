<script setup>
// Cancelar un pedido antes del envío (Fase 7). Sin pagar: solo confirmar. Pagado: se pide con un motivo y la
// tienda la aprueba (el reembolso lo hace un Admin).
import { ref } from 'vue'
import { RETURN_REASONS } from '../utils/returns'

const props = defineProps({
  order: { type: Object, required: true },
  submitting: { type: Boolean, default: false }
})
const emit = defineEmits(['submit', 'cancel'])

const paid = props.order.status === 'Paid'
const reason = ref('ChangedMind')
const comment = ref('')
const input = 'w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm text-ink focus:outline-none focus:ring-2 focus:ring-brand-500'
const idPrefix = `cancel-${props.order.orderId.slice(0, 8)}`

function onSubmit() {
  emit('submit', { reason: reason.value, comment: comment.value.trim() || null })
}
</script>

<template>
  <form @submit.prevent="onSubmit" class="space-y-3 rounded-lg border border-red-200 bg-red-50/60 p-3 dark:border-red-500/30 dark:bg-red-500/10"
    data-testid="cancel-form">
    <p class="text-sm font-medium text-ink">
      {{ paid ? '¿Quieres cancelar este pedido?' : '¿Cancelar este pedido? Todavía no se cobró nada.' }}
    </p>
    <template v-if="paid">
      <p class="text-xs text-ink-soft">
        Todavía no salió. Revisamos tu pedido y, si lo cancelamos, te devolvemos todo lo que pagaste a tu cuenta de PayPal.
      </p>
      <div>
        <label :for="`${idPrefix}-reason`" class="mb-1 block text-xs font-medium text-ink-soft">Motivo</label>
        <select :id="`${idPrefix}-reason`" v-model="reason" :class="input">
          <option v-for="r in RETURN_REASONS" :key="r.value" :value="r.value">{{ r.label }}</option>
        </select>
      </div>
      <div>
        <label :for="`${idPrefix}-comment`" class="mb-1 block text-xs font-medium text-ink-soft">
          Comentario <span class="text-ink-muted">(opcional)</span>
        </label>
        <textarea :id="`${idPrefix}-comment`" v-model="comment" :class="input" rows="2" maxlength="500"></textarea>
      </div>
    </template>
    <div class="flex flex-wrap gap-2">
      <button type="submit" :disabled="submitting" data-testid="cancel-submit"
        class="rounded-full bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-60">
        {{ submitting ? 'Enviando…' : paid ? 'Pedir la cancelación' : 'Sí, cancelar' }}
      </button>
      <button type="button" @click="emit('cancel')" class="rounded-full px-4 py-2 text-sm text-ink-soft hover:text-ink">No</button>
    </div>
  </form>
</template>
