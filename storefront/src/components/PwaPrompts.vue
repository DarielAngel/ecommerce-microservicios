<script setup>
// Avisos de la PWA: sin conexión, versión nueva disponible y "ya funciona sin conexión".
import { watch } from 'vue'
import { usePwaStore } from '../stores/pwa'
import { useToastStore } from '../stores/toast'

const pwa = usePwaStore()
const toast = useToastStore()

watch(() => pwa.offlineReady, (ready) => {
  if (!ready) return
  toast.push({ type: 'success', message: 'Listo: la tienda ya abre aunque no tengas conexión.' })
  pwa.dismissOfflineReady()
}, { immediate: true })
</script>

<template>
  <div v-if="!pwa.online" role="status" data-testid="offline-banner"
    class="sticky top-0 z-40 border-b border-amber-300 bg-amber-50 px-4 py-2 text-center text-sm text-amber-900 dark:border-amber-700 dark:bg-amber-950 dark:text-amber-100">
    <b>Sin conexión.</b> Puedes ver los productos que ya visitaste; para comprar, ver tu carrito o tus pedidos
    necesitas internet.
  </div>

  <div v-if="pwa.needRefresh" role="alert" data-testid="update-banner"
    class="fixed inset-x-3 bottom-3 z-50 mx-auto flex max-w-md flex-wrap items-center gap-3 rounded-xl border border-line bg-surface p-3 text-sm text-ink shadow-card-hover">
    <span class="flex-1">Hay una versión nueva de la tienda.</span>
    <button type="button" @click="pwa.update()" data-testid="update-apply"
      class="rounded-full bg-brand-600 px-3 py-1.5 font-medium text-white hover:bg-brand-700">Actualizar</button>
    <button type="button" @click="pwa.dismissUpdate()" class="px-2 py-1.5 text-ink-soft hover:text-ink">Después</button>
  </div>
</template>
