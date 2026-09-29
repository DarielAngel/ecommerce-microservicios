import { defineStore } from 'pinia'
import { api } from '../api/client'

const STORAGE_KEY = 'admin-panel-auth'
const SITE_KEY_STORAGE = 'admin-panel-site-unlocked'

function loadPersisted() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? JSON.parse(raw) : null
  } catch {
    return null
  }
}

export const useAuthStore = defineStore('auth', {
  state: () => ({
    siteUnlocked: localStorage.getItem(SITE_KEY_STORAGE) === 'true',
    user: loadPersisted()?.user ?? null,
    accessToken: loadPersisted()?.accessToken ?? null,
    refreshToken: loadPersisted()?.refreshToken ?? null,
    error: null,
    loading: false
  }),

  getters: {
    isAuthenticated: (state) => !!state.accessToken && state.user?.role === 'Admin',
    fullName: (state) => state.user?.fullName ?? ''
  },

  actions: {
    // Primer candado: una clave fija de sitio, solo para que no cualquiera vea la
    // pantalla de login. NO es autenticación real — eso lo hace login() contra Users.
    unlockSite(key) {
      const expected = import.meta.env.VITE_ADMIN_SITE_KEY
      if (key === expected) {
        this.siteUnlocked = true
        localStorage.setItem(SITE_KEY_STORAGE, 'true')
        return true
      }
      return false
    },

    async login(email, password) {
      this.loading = true
      this.error = null
      try {
        const result = await api.post('/api/auth/login', { email, password })

        if (result.role !== 'Admin') {
          this.error = 'Esta cuenta no tiene rol de Administrador.'
          return false
        }

        this.user = { userId: result.userId, email: result.email, fullName: result.fullName, role: result.role }
        this.accessToken = result.accessToken
        this.refreshToken = result.refreshToken
        this.persist()
        return true
      } catch (err) {
        this.error = err.message || 'No se pudo iniciar sesión.'
        return false
      } finally {
        this.loading = false
      }
    },

    persist() {
      localStorage.setItem(STORAGE_KEY, JSON.stringify({
        user: this.user, accessToken: this.accessToken, refreshToken: this.refreshToken
      }))
    },

    logout() {
      this.user = null
      this.accessToken = null
      this.refreshToken = null
      localStorage.removeItem(STORAGE_KEY)
    }
  }
})
