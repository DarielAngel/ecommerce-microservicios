import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useToastStore } from './toast'

describe('toast store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.useFakeTimers()
  })
  afterEach(() => vi.useRealTimers())

  it('push agrega un toast con id, tipo y mensaje', () => {
    const toast = useToastStore()
    const id = toast.push({ type: 'success', message: 'Listo' })

    expect(toast.toasts).toHaveLength(1)
    expect(toast.toasts[0]).toMatchObject({ id, type: 'success', message: 'Listo' })
  })

  it('el tipo por defecto es info', () => {
    const toast = useToastStore()
    toast.push({ message: 'Hola' })

    expect(toast.toasts[0].type).toBe('info')
  })

  it('se descarta solo después de la duración indicada', () => {
    const toast = useToastStore()
    toast.push({ message: 'Efímero', duration: 3000 })

    vi.advanceTimersByTime(2999)
    expect(toast.toasts).toHaveLength(1)
    vi.advanceTimersByTime(2)
    expect(toast.toasts).toHaveLength(0)
  })

  it('dismiss quita el toast indicado sin tocar los demás', () => {
    const toast = useToastStore()
    const first = toast.push({ message: 'Uno', duration: 0 })
    toast.push({ message: 'Dos', duration: 0 })

    toast.dismiss(first)

    expect(toast.toasts.map(t => t.message)).toEqual(['Dos'])
  })

  it('una duración 0 hace el toast persistente', () => {
    const toast = useToastStore()
    toast.push({ message: 'Fijo', duration: 0 })

    vi.advanceTimersByTime(60000)
    expect(toast.toasts).toHaveLength(1)
  })
})
