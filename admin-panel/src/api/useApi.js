import { useAuthStore } from '../stores/auth'
import { api } from './client'

// Wrapper delgado: agrega el token del Admin logueado automáticamente a cada llamada.
export function useApi() {
  const auth = useAuthStore()
  const token = () => auth.accessToken

  return {
    get: (path, opts) => api.get(path, { ...opts, token: token() }),
    post: (path, body, opts) => api.post(path, body, { ...opts, token: token() }),
    put: (path, body, opts) => api.put(path, body, { ...opts, token: token() }),
    delete: (path, opts) => api.delete(path, { ...opts, token: token() })
  }
}
