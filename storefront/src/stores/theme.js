import { defineStore } from 'pinia'

const STORAGE_KEY = 'storefront-theme'
const VALID_MODES = ['light', 'dark']

function systemPrefersDark() {
  return typeof window !== 'undefined' && !!window.matchMedia?.('(prefers-color-scheme: dark)')?.matches
}

export const useThemeStore = defineStore('theme', {
  state: () => ({ mode: 'light' }),

  actions: {
    // Prioridad: lo que el usuario eligió alguna vez > la preferencia del sistema > claro.
    init() {
      const saved = localStorage.getItem(STORAGE_KEY)
      this.mode = VALID_MODES.includes(saved) ? saved : systemPrefersDark() ? 'dark' : 'light'
      this.apply()
    },

    toggle() {
      this.mode = this.mode === 'dark' ? 'light' : 'dark'
      localStorage.setItem(STORAGE_KEY, this.mode)
      this.apply()
    },

    apply() {
      document.documentElement.classList.toggle('dark', this.mode === 'dark')
    }
  }
})
