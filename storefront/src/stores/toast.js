import { defineStore } from 'pinia'

let nextId = 1

export const useToastStore = defineStore('toast', {
  state: () => ({ toasts: [] }),

  actions: {
    /// duration en ms; 0 deja el aviso fijo hasta que el usuario lo cierre.
    push({ type = 'info', message, duration = 4000 }) {
      const id = nextId++
      this.toasts.push({ id, type, message })
      if (duration > 0) setTimeout(() => this.dismiss(id), duration)
      return id
    },

    dismiss(id) {
      this.toasts = this.toasts.filter((t) => t.id !== id)
    }
  }
})
