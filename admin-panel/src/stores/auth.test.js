import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from './auth'
import { api } from '../api/client'

// El store llama directamente a `api.post` (no al wrapper useApi, porque el login todavía
// no tiene token) — mockeamos ese módulo en vez de hacer peticiones de red reales.
vi.mock('../api/client', () => ({
  api: { post: vi.fn() }
}))

describe('useAuthStore (admin-panel)', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.clearAllMocks()
    vi.stubEnv('VITE_ADMIN_SITE_KEY', 'clave-de-test')
  })

  describe('unlockSite', () => {
    it('desbloquea el sitio con la clave correcta y la persiste', () => {
      const auth = useAuthStore()

      const result = auth.unlockSite('clave-de-test')

      expect(result).toBe(true)
      expect(auth.siteUnlocked).toBe(true)
      expect(localStorage.getItem('admin-panel-site-unlocked')).toBe('true')
    })

    it('rechaza una clave incorrecta sin desbloquear nada', () => {
      const auth = useAuthStore()

      const result = auth.unlockSite('clave-incorrecta')

      expect(result).toBe(false)
      expect(auth.siteUnlocked).toBe(false)
    })
  })

  describe('login', () => {
    it('con credenciales válidas de un Admin, guarda la sesión', async () => {
      api.post.mockResolvedValue({
        userId: 'u1', email: 'admin@test.com', fullName: 'Admin Prueba',
        role: 'Admin', accessToken: 'token-123', refreshToken: 'refresh-123'
      })
      const auth = useAuthStore()

      const ok = await auth.login('admin@test.com', 'Password123')

      expect(ok).toBe(true)
      expect(auth.isAuthenticated).toBe(true)
      expect(auth.fullName).toBe('Admin Prueba')
      expect(JSON.parse(localStorage.getItem('admin-panel-auth')).accessToken).toBe('token-123')
    })

    it('rechaza el login si la cuenta no tiene rol Admin, sin guardar sesión', async () => {
      api.post.mockResolvedValue({
        userId: 'u2', email: 'cliente@test.com', fullName: 'Cliente Prueba',
        role: 'Cliente', accessToken: 'token-456', refreshToken: 'refresh-456'
      })
      const auth = useAuthStore()

      const ok = await auth.login('cliente@test.com', 'Password123')

      expect(ok).toBe(false)
      expect(auth.isAuthenticated).toBe(false)
      expect(auth.error).toContain('Administrador')
    })

    it('si la API falla, expone el mensaje de error y no queda autenticado', async () => {
      api.post.mockRejectedValue(new Error('Credenciales inválidas.'))
      const auth = useAuthStore()

      const ok = await auth.login('admin@test.com', 'clave-mala')

      expect(ok).toBe(false)
      expect(auth.isAuthenticated).toBe(false)
      expect(auth.error).toBe('Credenciales inválidas.')
    })
  })

  describe('logout', () => {
    it('limpia la sesión y el localStorage', async () => {
      api.post.mockResolvedValue({
        userId: 'u1', email: 'admin@test.com', fullName: 'Admin Prueba',
        role: 'Admin', accessToken: 'token-123', refreshToken: 'refresh-123'
      })
      const auth = useAuthStore()
      await auth.login('admin@test.com', 'Password123')

      auth.logout()

      expect(auth.isAuthenticated).toBe(false)
      expect(auth.user).toBeNull()
      expect(localStorage.getItem('admin-panel-auth')).toBeNull()
    })
  })
})
