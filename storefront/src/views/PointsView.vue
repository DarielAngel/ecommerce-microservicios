<script setup>
import { ref, onMounted } from 'vue'
import { useApi } from '../api/useApi'
import { loyaltyApi } from '../api/loyalty'
import { formatMoney } from '../utils/format'
import { formatMoment } from '../utils/orders'
import { entryText, entryPoints, entryAdds, entryKey, pointsToMinimum } from '../utils/loyalty'
import AccountNav from '../components/AccountNav.vue'

const apiClient = useApi()
const summary = ref(null)
const loading = ref(true)
const error = ref('')

onMounted(async () => {
  try {
    summary.value = await loyaltyApi.me(apiClient)
  } catch (err) {
    error.value = err.message
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="mx-auto max-w-2xl">
    <AccountNav />

    <div v-if="loading" class="py-16 text-center text-ink-muted">Cargando...</div>
    <div v-else-if="error" class="py-16 text-center text-red-600 dark:text-red-400">{{ error }}</div>
    <template v-else>
      <section class="mb-6 rounded-2xl bg-brand-soft p-6" data-testid="points-balance">
        <p class="text-sm font-medium text-brand-ink">Tus puntos</p>
        <p class="mt-1 text-4xl font-bold tracking-tight text-ink" data-testid="points-balance-value">{{ summary.balance }}</p>
        <p class="text-sm text-ink-soft">
          Equivalen a <b>{{ formatMoney(summary.balanceValue) }}</b> de descuento.
          <span v-if="pointsToMinimum(summary.balance, summary.rules.minRedeemPoints) > 0">
            Te faltan {{ pointsToMinimum(summary.balance, summary.rules.minRedeemPoints) }} para poder usarlos.
          </span>
        </p>
      </section>

      <section class="mb-6 rounded-xl border border-line bg-surface p-4 text-sm text-ink-soft">
        <p class="mb-1 font-medium text-ink">Cómo funcionan</p>
        <ul class="list-disc space-y-1 pl-5">
          <li>Ganas {{ summary.rules.pointsPerDollar }} punto por cada dólar que pagas (cuando se confirma el pago).</li>
          <li>Cada punto vale {{ formatMoney(summary.rules.pointValue) }}: 100 puntos = {{ formatMoney(100 * summary.rules.pointValue) }}.</li>
          <li>Desde {{ summary.rules.minRedeemPoints }} puntos los usas en el checkout, hasta la mitad de lo que pagas.</li>
        </ul>
      </section>

      <h2 class="mb-2 text-sm font-semibold text-ink">Movimientos</h2>
      <p v-if="summary.history.length === 0" class="rounded-xl border border-dashed border-line py-8 text-center text-sm text-ink-muted"
        data-testid="points-empty">
        Todavía no tienes movimientos. Tu primera compra ya suma puntos.
      </p>
      <ul v-else class="divide-y divide-line rounded-xl border border-line bg-surface">
        <li v-for="e in summary.history" :key="entryKey(e)" class="flex items-center justify-between gap-3 px-4 py-3"
          data-testid="points-entry">
          <div class="min-w-0 text-sm">
            <p class="text-ink">{{ entryText(e) }}</p>
            <p class="text-xs text-ink-muted">{{ formatMoment(e.createdAtUtc) }}</p>
          </div>
          <span class="flex-shrink-0 font-semibold"
            :class="entryAdds(e) ? 'text-emerald-700 dark:text-emerald-400' : 'text-ink-soft'">
            {{ entryPoints(e) }}
          </span>
        </li>
      </ul>
    </template>
  </div>
</template>
