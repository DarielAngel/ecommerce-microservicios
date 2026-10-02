import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from './auth'
import { api } from '../api/client'

vi.mock('../api/client', () => ({
  api: { post: vi.fn() }
}))

const sampleSession = {
  userId: 'u1', email: 'cliente@test.com', fullName: 'Cliente Prueba', role: 'Cliente',
  accessToken: 'token-1', refreshToken: 'refresh-1'
}

describe('useAuthStore (storefront)', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.clearAllMocks()
  })

  it('register guarda la sesión al tener éxito', async () => {
    api.post.mockResolvedValue(sampleSession)
    const auth = useAuthStore()

    const ok = await auth.register('cliente@test.com', 'Password123', 'Cliente Prueba')

    expect(ok).toBe(true)
    expect(auth.isAuthenticated).toBe(true)
    expect(auth.fullName).toBe('Cliente Prueba')
  })

  it('register expone el mensaje de error si el email ya existe', async () => {
    api.post.mockRejectedValue(new Error("Ya existe una cuenta con el email 'cliente@test.com'."))
    const auth = useAuthStore()

    const ok = await auth.register('cliente@test.com', 'Password123', 'Cliente Prueba')

    expect(ok).toBe(false)
    expect(auth.error).toContain('Ya existe')
  })

  it('login guarda la sesión al tener éxito (sin exigir un rol en particular, a diferencia del panel de Admin)', async () => {
    api.post.mockResolvedValue(sampleSession)
    const auth = useAuthStore()

    const ok = await auth.login('cliente@test.com', 'Password123')

    expect(ok).toBe(true)
    expect(auth.isAuthenticated).toBe(true)
  })

  describe('tryRefresh', () => {
    it('sin refreshToken guardado, no intenta nada y devuelve false', async () => {
      const auth = useAuthStore()

      const ok = await auth.tryRefresh()

      expect(ok).toBe(false)
      expect(api.post).not.toHaveBeenCalled()
    })

    it('con un refreshToken válido, renueva la sesión', async () => {
      api.post.mockResolvedValueOnce(sampleSession) // login inicial
      const auth = useAuthStore()
      await auth.login('cliente@test.com', 'Password123')

      api.post.mockResolvedValueOnce({ ...sampleSession, accessToken: 'token-2' }) // refresh
      const ok = await auth.tryRefresh()

      expect(ok).toBe(true)
      expect(auth.accessToken).toBe('token-2')
      expect(api.post).toHaveBeenLastCalledWith('/api/auth/refresh', { refreshToken: 'refresh-1' })
    })

    it('si el refresh también falla, cierra la sesión por completo', async () => {
      api.post.mockResolvedValueOnce(sampleSession) // login inicial
      const auth = useAuthStore()
      await auth.login('cliente@test.com', 'Password123')

      api.post.mockRejectedValueOnce(new Error('Refresh token inválido o expirado.'))
      const ok = await auth.tryRefresh()

      expect(ok).toBe(false)
      expect(auth.isAuthenticated).toBe(false)
      expect(auth.accessToken).toBeNull()
    })
  })

  it('logout limpia la sesión y el localStorage', async () => {
    api.post.mockResolvedValue(sampleSession)
    const auth = useAuthStore()
    await auth.login('cliente@test.com', 'Password123')

    auth.logout()

    expect(auth.isAuthenticated).toBe(false)
    expect(localStorage.getItem('storefront-auth')).toBeNull()
  })
})
