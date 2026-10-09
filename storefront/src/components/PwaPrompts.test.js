import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('../pwa/registerSW', () => ({ registerSW: vi.fn(() => vi.fn()) }))

import PwaPrompts from './PwaPrompts.vue'
import InstallAppCard from './InstallAppCard.vue'
import { usePwaStore } from '../stores/pwa'
import { useToastStore } from '../stores/toast'

let pinia
beforeEach(() => {
  pinia = createPinia()
  setActivePinia(pinia)
  localStorage.clear()
})
const mountIt = (component) => mount(component, { global: { plugins: [pinia] } })

describe('PwaPrompts', () => {
  it('sin conexión muestra el aviso, y lo quita al volver la red', async () => {
    const pwa = usePwaStore()
    pwa.online = false
    const wrapper = mountIt(PwaPrompts)

    expect(wrapper.get('[data-testid="offline-banner"]').text()).toContain('Sin conexión')

    pwa.online = true
    await wrapper.vm.$nextTick()
    expect(wrapper.find('[data-testid="offline-banner"]').exists()).toBe(false)
  })

  it('con una versión nueva ofrece actualizar, y "Después" la deja para más tarde', async () => {
    const pwa = usePwaStore()
    const update = vi.spyOn(pwa, 'update').mockResolvedValue()
    pwa.needRefresh = true
    const wrapper = mountIt(PwaPrompts)

    await wrapper.get('[data-testid="update-apply"]').trigger('click')
    expect(update).toHaveBeenCalled()

    pwa.needRefresh = true
    await wrapper.vm.$nextTick()
    await wrapper.findAll('button').find((b) => b.text() === 'Después').trigger('click')
    expect(pwa.needRefresh).toBe(false)
  })

  it('avisa una sola vez que ya funciona sin conexión', async () => {
    const pwa = usePwaStore()
    const toast = useToastStore()
    mountIt(PwaPrompts)

    pwa.offlineReady = true
    await new Promise((r) => setTimeout(r))

    expect(toast.toasts.map((t) => t.message)).toEqual(['Listo: la tienda ya abre aunque no tengas conexión.'])
    expect(pwa.offlineReady).toBe(false)
  })
})

describe('InstallAppCard', () => {
  it('no aparece si el navegador no ofrece instalar', () => {
    expect(mountIt(InstallAppCard).find('[data-testid="install-card"]').exists()).toBe(false)
  })

  it('con el aviso del navegador muestra el botón e instala', async () => {
    const pwa = usePwaStore()
    pwa.installEvent = { prompt: vi.fn(async () => {}), userChoice: Promise.resolve({ outcome: 'accepted' }) }
    const wrapper = mountIt(InstallAppCard)

    await wrapper.get('[data-testid="install-app"]').trigger('click')
    await new Promise((r) => setTimeout(r))

    expect(pwa.installed).toBe(true)
    expect(useToastStore().toasts[0].message).toContain('ya está entre tus apps')
    expect(wrapper.find('[data-testid="install-card"]').exists()).toBe(false)
  })

  it('en iPhone explica cómo agregarla a inicio', () => {
    const pwa = usePwaStore()
    pwa.iosHint = true
    const wrapper = mountIt(InstallAppCard)

    expect(wrapper.get('[data-testid="install-ios"]').text()).toContain('Agregar a inicio')
    expect(wrapper.find('[data-testid="install-app"]').exists()).toBe(false)
  })

  it('"No, gracias" la oculta', async () => {
    const pwa = usePwaStore()
    pwa.installEvent = { prompt: vi.fn(), userChoice: Promise.resolve({ outcome: 'dismissed' }) }
    const wrapper = mountIt(InstallAppCard)

    await wrapper.findAll('button').find((b) => b.text() === 'No, gracias').trigger('click')

    expect(wrapper.find('[data-testid="install-card"]').exists()).toBe(false)
    expect(localStorage.getItem('pwa-install-dismissed')).toBe('1')
  })
})
