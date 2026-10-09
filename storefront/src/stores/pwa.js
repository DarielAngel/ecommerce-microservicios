import { defineStore } from 'pinia'
import { markRaw } from 'vue'
import { registerSW } from '../pwa/registerSW'

// Función para activar la versión nueva del service worker (la da registerSW). Fuera del estado: no es un dato.
let applyUpdate = null

const DISMISS_KEY = 'pwa-install-dismissed'
const UPDATE_CHECK_MS = 60 * 60 * 1000

function isStandalone() {
  try {
    return window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true
  } catch {
    return false
  }
}

/** iPhone/iPad con Safari: no tiene botón de "Instalar"; se agrega desde Compartir → "Agregar a inicio". */
export function isIosSafari(userAgent = navigator.userAgent, maxTouchPoints = navigator.maxTouchPoints ?? 0) {
  const ios = /iPad|iPhone|iPod/.test(userAgent) || (/Macintosh/.test(userAgent) && maxTouchPoints > 1)
  const otherBrowser = /CriOS|FxiOS|EdgiOS|OPiOS/.test(userAgent)
  return ios && !otherBrowser
}

function readDismissed() {
  try {
    return localStorage.getItem(DISMISS_KEY) === '1'
  } catch {
    return false
  }
}

/**
 * Estado de la PWA (Fase 8): conexión, instalación y versión nueva.
 * - online: si el navegador tiene red (para avisar y bloquear el pago sin conexión).
 * - installEvent: el aviso de instalación de Chrome/Edge/Android, guardado para mostrarlo desde nuestro botón.
 * - needRefresh: hay una versión nueva de la tienda esperando; el cliente decide cuándo recargar.
 */
export const usePwaStore = defineStore('pwa', {
  state: () => ({
    online: typeof navigator === 'undefined' ? true : navigator.onLine,
    installEvent: null,
    installed: false,
    iosHint: false,
    installDismissed: false,
    needRefresh: false,
    offlineReady: false
  }),

  getters: {
    canInstall: (s) => Boolean(s.installEvent) && !s.installed,
    /** Mostrar la invitación a instalar (botón o, en iPhone, la explicación). */
    showInstall: (s) => !s.installed && !s.installDismissed && (Boolean(s.installEvent) || s.iosHint)
  },

  actions: {
    init({ register = 'serviceWorker' in navigator } = {}) {
      this.installed = isStandalone()
      this.iosHint = isIosSafari()
      this.installDismissed = readDismissed()
      this.online = navigator.onLine

      window.addEventListener('online', () => { this.online = true })
      window.addEventListener('offline', () => { this.online = false })
      window.addEventListener('beforeinstallprompt', (event) => {
        // Chrome mostraría su propia barra; la guardamos para ofrecerla desde la tienda, en el momento justo.
        event.preventDefault()
        this.installEvent = markRaw(event)
      })
      window.addEventListener('appinstalled', () => {
        this.installed = true
        this.installEvent = null
      })

      if (register) {
        applyUpdate = registerSW({
          onNeedRefresh: () => { this.needRefresh = true },
          onOfflineReady: () => { this.offlineReady = true },
          // Una app instalada puede quedar abierta días: cada hora pregunta si hay una versión nueva.
          onRegisteredSW: (_url, registration) => {
            if (registration) setInterval(() => registration.update().catch(() => {}), UPDATE_CHECK_MS)
          }
        })
      }
    },

    /** Abre el diálogo de instalación del navegador. Devuelve true si el cliente aceptó. */
    async install() {
      const event = this.installEvent
      if (!event) return false
      this.installEvent = null // el mismo aviso no se puede mostrar dos veces
      await event.prompt()
      const choice = await event.userChoice
      if (choice?.outcome === 'accepted') this.installed = true
      return choice?.outcome === 'accepted'
    },

    dismissInstall() {
      this.installDismissed = true
      try {
        localStorage.setItem(DISMISS_KEY, '1')
      } catch {
        // Sin almacenamiento (modo privado): solo se oculta por esta visita.
      }
    },

    /** Activa la versión nueva y recarga la página. */
    async update() {
      this.needRefresh = false
      if (applyUpdate) await applyUpdate(true)
    },

    dismissUpdate() {
      this.needRefresh = false
    },

    dismissOfflineReady() {
      this.offlineReady = false
    }
  }
})
