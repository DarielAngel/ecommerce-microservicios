import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useThemeStore } from './theme'

function systemPrefersDark(value) {
  window.matchMedia = vi.fn().mockReturnValue({
    matches: value,
    addEventListener: vi.fn(),
    removeEventListener: vi.fn()
  })
}

describe('theme store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.documentElement.classList.remove('dark')
    systemPrefersDark(false)
  })

  it('arranca en modo claro si no hay preferencia guardada ni del sistema', () => {
    const theme = useThemeStore()
    theme.init()

    expect(theme.mode).toBe('light')
    expect(document.documentElement.classList.contains('dark')).toBe(false)
  })

  it('usa el modo oscuro si el sistema lo prefiere y el usuario nunca eligió', () => {
    systemPrefersDark(true)
    const theme = useThemeStore()
    theme.init()

    expect(theme.mode).toBe('dark')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
  })

  it('respeta la elección guardada aunque el sistema prefiera lo contrario', () => {
    systemPrefersDark(true)
    localStorage.setItem('storefront-theme', 'light')
    const theme = useThemeStore()
    theme.init()

    expect(theme.mode).toBe('light')
  })

  it('toggle alterna el modo, lo guarda y actualiza la clase dark del <html>', () => {
    const theme = useThemeStore()
    theme.init()

    theme.toggle()
    expect(theme.mode).toBe('dark')
    expect(localStorage.getItem('storefront-theme')).toBe('dark')
    expect(document.documentElement.classList.contains('dark')).toBe(true)

    theme.toggle()
    expect(theme.mode).toBe('light')
    expect(localStorage.getItem('storefront-theme')).toBe('light')
    expect(document.documentElement.classList.contains('dark')).toBe(false)
  })

  it('ignora valores inválidos guardados en localStorage', () => {
    localStorage.setItem('storefront-theme', 'azul-marino')
    const theme = useThemeStore()
    theme.init()

    expect(theme.mode).toBe('light')
  })
})
