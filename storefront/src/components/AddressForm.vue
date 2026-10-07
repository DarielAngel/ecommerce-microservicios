<script setup>
// Campos de una dirección (Fase 5). No guarda nada por sí mismo: edita el objeto que recibe por
// v-model y el padre decide qué hacer (guardarlo en la libreta o usarlo solo para este pedido).
const form = defineModel({ type: Object, required: true })

defineProps({
  // En el checkout el nombre de la dirección solo importa si se va a guardar.
  showLabel: { type: Boolean, default: true },
  showMakeDefault: { type: Boolean, default: true },
  idPrefix: { type: String, default: 'address' }
})

const input = 'w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm text-ink focus:outline-none focus:ring-2 focus:ring-brand-500'
const label = 'mb-1 block text-xs font-medium text-ink-soft'
</script>

<template>
  <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
    <div v-if="showLabel">
      <label :for="`${idPrefix}-label`" :class="label">Nombre de la dirección</label>
      <input :id="`${idPrefix}-label`" v-model="form.label" :class="input" maxlength="40" placeholder="Casa, Oficina…" />
    </div>
    <div :class="showLabel ? '' : 'sm:col-span-2'">
      <label :for="`${idPrefix}-recipient`" :class="label">Quién recibe</label>
      <input :id="`${idPrefix}-recipient`" v-model="form.recipientName" :class="input" maxlength="120" autocomplete="name" required />
    </div>
    <div class="sm:col-span-2">
      <label :for="`${idPrefix}-street`" :class="label">Calle y número</label>
      <input :id="`${idPrefix}-street`" v-model="form.street" :class="input" maxlength="200" autocomplete="address-line1" required />
    </div>
    <div class="sm:col-span-2">
      <label :for="`${idPrefix}-details`" :class="label">Piso, departamento o referencias <span class="text-ink-muted">(opcional)</span></label>
      <input :id="`${idPrefix}-details`" v-model="form.details" :class="input" maxlength="200" autocomplete="address-line2" />
    </div>
    <div>
      <label :for="`${idPrefix}-city`" :class="label">Ciudad</label>
      <input :id="`${idPrefix}-city`" v-model="form.city" :class="input" maxlength="100" autocomplete="address-level2" required />
    </div>
    <div>
      <label :for="`${idPrefix}-region`" :class="label">Provincia o estado <span class="text-ink-muted">(opcional)</span></label>
      <input :id="`${idPrefix}-region`" v-model="form.region" :class="input" maxlength="100" autocomplete="address-level1" />
    </div>
    <div>
      <label :for="`${idPrefix}-postal`" :class="label">Código postal <span class="text-ink-muted">(opcional)</span></label>
      <input :id="`${idPrefix}-postal`" v-model="form.postalCode" :class="input" maxlength="20" autocomplete="postal-code" />
    </div>
    <div>
      <label :for="`${idPrefix}-country`" :class="label">País</label>
      <input :id="`${idPrefix}-country`" v-model="form.country" :class="input" maxlength="60" autocomplete="country-name" required />
    </div>
    <div class="sm:col-span-2">
      <label :for="`${idPrefix}-phone`" :class="label">Teléfono <span class="text-ink-muted">(opcional, para el repartidor)</span></label>
      <input :id="`${idPrefix}-phone`" v-model="form.phone" :class="input" maxlength="30" type="tel" autocomplete="tel" />
    </div>
    <label v-if="showMakeDefault" class="flex items-center gap-2 text-sm text-ink-soft sm:col-span-2">
      <input v-model="form.makeDefault" type="checkbox" class="h-4 w-4 rounded border-line text-brand-600" />
      Usar como dirección predeterminada
    </label>
  </div>
</template>
