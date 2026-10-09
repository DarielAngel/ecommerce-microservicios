// Único punto que toca el módulo virtual de vite-plugin-pwa: así las pruebas lo reemplazan sin depender del plugin.
import { registerSW as register } from 'virtual:pwa-register'

export function registerSW(options) {
  return register(options)
}
