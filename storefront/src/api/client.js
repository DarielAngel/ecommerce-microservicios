// Igual que el panel de Admin: todo pasa por el Gateway, nunca directo a un microservicio.
const BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000'

class ApiError extends Error {
  constructor(message, status, body) {
    super(message)
    this.status = status
    this.body = body
  }
}

async function request(method, path, { body, token, params } = {}) {
  const url = new URL(BASE_URL + path)
  if (params) {
    Object.entries(params).forEach(([key, value]) => {
      if (Array.isArray(value)) {
        // ASP.NET enlaza Guid[] desde parámetros repetidos (ids=a&ids=b), no desde "a,b".
        value.forEach((item) => url.searchParams.append(key, item))
      } else if (value !== undefined && value !== null && value !== '') {
        url.searchParams.set(key, value)
      }
    })
  }

  const headers = { 'Content-Type': 'application/json' }
  if (token) headers.Authorization = `Bearer ${token}`

  let response
  try {
    response = await fetch(url, {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined
    })
  } catch {
    // fetch solo falla así cuando no hubo respuesta (sin red, servidor caído, CORS): el "Failed to fetch" del
    // navegador no le dice nada al cliente.
    const offline = typeof navigator !== 'undefined' && navigator.onLine === false
    throw new ApiError(offline
      ? 'Sin conexión a internet. Revisa tu red e intenta de nuevo.'
      : 'No pudimos conectar con la tienda. Intenta de nuevo en un momento.', 0, null)
  }

  const contentType = response.headers.get('content-type') || ''
  const data = contentType.includes('application/json') ? await response.json().catch(() => null) : null

  if (!response.ok) {
    const message = data?.message || `Error ${response.status} al llamar a ${path}`
    throw new ApiError(message, response.status, data)
  }

  return data
}

export const api = {
  get: (path, opts) => request('GET', path, opts),
  post: (path, body, opts = {}) => request('POST', path, { ...opts, body }),
  put: (path, body, opts = {}) => request('PUT', path, { ...opts, body }),
  delete: (path, opts) => request('DELETE', path, opts)
}

export { ApiError, BASE_URL }
