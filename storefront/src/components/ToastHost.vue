<script setup>
import { useToastStore } from '../stores/toast'

const toast = useToastStore()

const accent = {
  success: 'border-l-brand-500',
  error: 'border-l-red-500',
  info: 'border-l-sky-500'
}
</script>

<template>
  <div class="pointer-events-none fixed bottom-4 right-4 z-50 flex w-[min(22rem,calc(100vw-2rem))] flex-col gap-2" aria-live="polite">
    <transition-group name="toast">
      <div v-for="t in toast.toasts" :key="t.id" role="status"
        class="pointer-events-auto flex items-start gap-3 rounded-xl border border-line border-l-4 bg-surface p-3.5 text-sm shadow-card-hover"
        :class="accent[t.type] || accent.info">
        <p class="flex-1 text-ink">{{ t.message }}</p>
        <button type="button" class="text-ink-muted hover:text-ink" aria-label="Cerrar aviso" @click="toast.dismiss(t.id)">✕</button>
      </div>
    </transition-group>
  </div>
</template>

<style scoped>
.toast-enter-active,
.toast-leave-active {
  transition: all 0.2s ease;
}
.toast-enter-from,
.toast-leave-to {
  opacity: 0;
  transform: translateY(8px);
}
</style>
