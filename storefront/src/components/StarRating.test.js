import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import StarRating from './StarRating.vue'

describe('StarRating (solo lectura)', () => {
  it('describe la calificación para lectores de pantalla', () => {
    const wrapper = mount(StarRating, { props: { value: 4 } })

    expect(wrapper.get('[role="img"]').attributes('aria-label')).toBe('Calificación: 4.0 de 5')
  })

  it('rellena las estrellas en proporción al valor', () => {
    const wrapper = mount(StarRating, { props: { value: 4.5 } })

    expect(wrapper.get('[data-testid="stars-fill"]').attributes('style')).toContain('width: 90%')
  })

  it('recorta valores fuera de rango', () => {
    const over = mount(StarRating, { props: { value: 7 } })
    const under = mount(StarRating, { props: { value: -2 } })

    expect(over.get('[data-testid="stars-fill"]').attributes('style')).toContain('width: 100%')
    expect(under.get('[data-testid="stars-fill"]').attributes('style')).toContain('width: 0%')
  })

  it('no ofrece botones cuando no es interactiva', () => {
    const wrapper = mount(StarRating, { props: { value: 3 } })

    expect(wrapper.findAll('button')).toHaveLength(0)
  })
})

describe('StarRating (interactiva)', () => {
  const mountInteractive = (modelValue = 0) =>
    mount(StarRating, { props: { modelValue, interactive: true } })

  it('ofrece cinco opciones accesibles', () => {
    const radios = mountInteractive().findAll('[role="radio"]')

    expect(radios).toHaveLength(5)
    expect(radios.map((r) => r.attributes('aria-label'))).toEqual([
      '1 estrella', '2 estrellas', '3 estrellas', '4 estrellas', '5 estrellas'
    ])
  })

  it('al hacer clic emite update:modelValue con esa calificación', async () => {
    const wrapper = mountInteractive()

    await wrapper.findAll('[role="radio"]')[3].trigger('click')

    expect(wrapper.emitted('update:modelValue')[0]).toEqual([4])
  })

  it('marca como seleccionada la calificación actual', () => {
    const radios = mountInteractive(3).findAll('[role="radio"]')

    expect(radios.map((r) => r.attributes('aria-checked'))).toEqual(['false', 'false', 'true', 'false', 'false'])
  })

  it('las flechas del teclado cambian la calificación dentro de 1..5', async () => {
    const wrapper = mountInteractive(3)
    const group = wrapper.get('[role="radiogroup"]')

    await group.trigger('keydown', { key: 'ArrowRight' })
    await group.trigger('keydown', { key: 'ArrowLeft' })

    expect(wrapper.emitted('update:modelValue')).toEqual([[4], [2]])
  })

  it('no se pasa de 5 ni baja de 1 con el teclado', async () => {
    const top = mountInteractive(5)
    const bottom = mountInteractive(1)

    await top.get('[role="radiogroup"]').trigger('keydown', { key: 'ArrowRight' })
    await bottom.get('[role="radiogroup"]').trigger('keydown', { key: 'ArrowLeft' })

    expect(top.emitted('update:modelValue')).toBeUndefined()
    expect(bottom.emitted('update:modelValue')).toBeUndefined()
  })
})
