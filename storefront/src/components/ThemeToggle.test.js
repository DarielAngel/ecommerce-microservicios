import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import ThemeToggle from './ThemeToggle.vue'
import { useThemeStore } from '../stores/theme'

describe('ThemeToggle', () => {
  beforeEach(() => {
    localStorage.clear()
    document.documentElement.classList.remove('dark')
    window.matchMedia = vi.fn().mockReturnValue({ matches: false, addEventListener: vi.fn(), removeEventListener: vi.fn() })
  })

  it('ofrece cambiar a modo oscuro cuando está en claro, y al revés', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    useThemeStore().init()
    const wrapper = mount(ThemeToggle, { global: { plugins: [pinia] } })

    const button = wrapper.get('button')
    expect(button.attributes('aria-label')).toBe('Cambiar a modo oscuro')

    await button.trigger('click')

    expect(useThemeStore().mode).toBe('dark')
    expect(wrapper.get('button').attributes('aria-label')).toBe('Cambiar a modo claro')
  })
})
