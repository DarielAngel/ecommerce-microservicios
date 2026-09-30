import { useAuthStore } from '../stores/auth'
import { api, ApiError } from './client'

// Igual que el panel de Admin, pero además intenta refrescar el token una vez si
// la API responde 401 — los clientes van a tener sesiones largas navegando la tienda,
// y el access token dura solo 15 minutos.
export function useApi() {
  const auth = useAuthStore()

  async function call(fn) {
    try {
      return await fn(auth.accessToken)
    } catch (err) {
      if (err instanceof ApiError && err.status === 401 && auth.refreshToken) {
        const refreshed = await auth.tryRefresh()
        if (refreshed) return await fn(auth.accessToken)
      }
      throw err
    }
  }

  return {
    get: (path, opts) => call((token) => api.get(path, { ...opts, token })),
    post: (path, body, opts) => call((token) => api.post(path, body, { ...opts, token })),
    put: (path, body, opts) => call((token) => api.put(path, body, { ...opts, token })),
    delete: (path, opts) => call((token) => api.delete(path, { ...opts, token }))
  }
}
