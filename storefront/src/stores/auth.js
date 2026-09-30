import { defineStore } from 'pinia'
import { api } from '../api/client'

const STORAGE_KEY = 'storefront-auth'

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
    user: loadPersisted()?.user ?? null,
    accessToken: loadPersisted()?.accessToken ?? null,
    refreshToken: loadPersisted()?.refreshToken ?? null,
    error: null,
    loading: false
  }),

  getters: {
    isAuthenticated: (state) => !!state.accessToken,
    fullName: (state) => state.user?.fullName ?? ''
  },

  actions: {
    setSession(result) {
      this.user = { userId: result.userId, email: result.email, fullName: result.fullName, role: result.role }
      this.accessToken = result.accessToken
      this.refreshToken = result.refreshToken
      this.persist()
    },

    async register(email, password, fullName) {
      this.loading = true
      this.error = null
      try {
        const result = await api.post('/api/auth/register', { email, password, fullName })
        this.setSession(result)
        return true
      } catch (err) {
        this.error = err.message
        return false
      } finally {
        this.loading = false
      }
    },

    async login(email, password) {
      this.loading = true
      this.error = null
      try {
        const result = await api.post('/api/auth/login', { email, password })
        this.setSession(result)
        return true
      } catch (err) {
        this.error = err.message
        return false
      } finally {
        this.loading = false
      }
    },

    /// Intenta renovar el access token con el refresh token. Si también falla, cierra sesión.
    async tryRefresh() {
      if (!this.refreshToken) return false
      try {
        const result = await api.post('/api/auth/refresh', { refreshToken: this.refreshToken })
        this.setSession(result)
        return true
      } catch {
        this.logout()
        return false
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
