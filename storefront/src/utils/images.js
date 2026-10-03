import { BASE_URL } from '../api/client'

// Catálogo sirve las imágenes subidas en /images/{fileName} — una ruta estática, fuera de
// /api/, pero igual pasa por el Gateway (ver catalog-images-route en su configuración YARP).
export function imageUrl(fileName) {
  if (!fileName) return null
  return `${BASE_URL}/images/${fileName}`
}
