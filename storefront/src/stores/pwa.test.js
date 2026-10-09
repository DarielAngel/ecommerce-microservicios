import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'

const { registerSW, applyUpdate } = vi.hoisted(() => {
  const applyUpdate = vi.fn()
  return { applyUpdate, registerSW: vi.fn(() => applyUpdate) }
})
vi.mock('../pwa/registerSW', () => ({ registerSW }))

import { usePwaStore, isIosSafari } from './pwa'

function installEvent(outcome = 'accepted') {
  const event = new Event('beforeinstallprompt')
  event.prompt = vi.fn(async () => {})
  event.userChoice = Promise.resolve({ outcome })
  return event
}

describe('store de la PWA', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    registerSW.mockClear()
    applyUpdate.mockClear()
    localStorage.clear()
  })

  it('registra el service worker y avisa cuando hay una versión nueva', async () => {
    const pwa = usePwaStore()
    pwa.init({ register: true })

    expect(registerSW).toHaveBeenCalledTimes(1)
    registerSW.mock.calls[0][0].onNeedRefresh()
    expect(pwa.needRefresh).toBe(true)

    await pwa.update()
    expect(applyUpdate).toHaveBeenCalledWith(true)
    expect(pwa.needRefresh).toBe(false)
  })

  it('cada hora pregunta si hay una versión nueva', () => {
    vi.useFakeTimers()
    const pwa = usePwaStore()
    pwa.init({ register: true })
    const registration = { update: vi.fn(async () => {}) }

    registerSW.mock.calls[0][0].onRegisteredSW('/sw.js', registration)
    vi.advanceTimersByTime(60 * 60 * 1000)

    expect(registration.update).toHaveBeenCalledTimes(1)
    vi.useRealTimers()
  })

  it('sigue la conexión del navegador', () => {
    const pwa = usePwaStore()
    pwa.init({ register: false })

    window.dispatchEvent(new Event('offline'))
    expect(pwa.online).toBe(false)
    window.dispatchEvent(new Event('online'))
    expect(pwa.online).toBe(true)
  })

  it('guarda el aviso de instalación y lo muestra desde nuestro botón', async () => {
    const pwa = usePwaStore()
    pwa.init({ register: false })
    const event = installEvent('accepted')
    const prevent = vi.spyOn(event, 'preventDefault')

    window.dispatchEvent(event)
    expect(prevent).toHaveBeenCalled()
    expect(pwa.canInstall).toBe(true)
    expect(pwa.showInstall).toBe(true)

    expect(await pwa.install()).toBe(true)
    expect(event.prompt).toHaveBeenCalled()
    expect(pwa.installed).toBe(true)
    expect(pwa.showInstall).toBe(false)
  })

  it('si el cliente cancela la instalación, el mismo aviso no se puede volver a usar', async () => {
    const pwa = usePwaStore()
    pwa.init({ register: false })
    window.dispatchEvent(installEvent('dismissed'))

    expect(await pwa.install()).toBe(false)
    expect(pwa.canInstall).toBe(false)
    expect(pwa.installed).toBe(false)
  })

  it('"No, gracias" se recuerda para la próxima visita', () => {
    const pwa = usePwaStore()
    pwa.init({ register: false })
    window.dispatchEvent(installEvent())

    pwa.dismissInstall()
    expect(pwa.showInstall).toBe(false)

    setActivePinia(createPinia())
    const again = usePwaStore()
    again.init({ register: false })
    window.dispatchEvent(installEvent())
    expect(again.showInstall).toBe(false)
  })

  it('reconoce Safari en iPhone/iPad, pero no Chrome en iPhone', () => {
    const iphoneSafari = 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1'
    const iphoneChrome = 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/126.0 Mobile/15E148 Safari/604.1'
    const ipadDesktopMode = 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Safari/605.1.15'
    const android = 'Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Mobile Safari/537.36'

    expect(isIosSafari(iphoneSafari, 5)).toBe(true)
    expect(isIosSafari(iphoneChrome, 5)).toBe(false)
    expect(isIosSafari(ipadDesktopMode, 5)).toBe(true)
    expect(isIosSafari(ipadDesktopMode, 0)).toBe(false) // una Mac de verdad
    expect(isIosSafari(android, 5)).toBe(false)
  })
})
