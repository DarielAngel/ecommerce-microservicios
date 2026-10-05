import { describe, it, expect } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import ProductCard from './ProductCard.vue'

const product = {
  id: 'p-1',
  name: 'Abrigo de invierno',
  categoryName: 'Ropa',
  minPrice: 12.3,
  primaryImageFileName: 'abrigo.jpg'
}

function mountCard(overrides = {}) {
  return mount(ProductCard, {
    props: { product: { ...product, ...overrides } },
    global: { stubs: { RouterLink: RouterLinkStub } }
  })
}

describe('ProductCard', () => {
  it('muestra nombre, categoría y precio formateado', () => {
    const wrapper = mountCard()

    expect(wrapper.text()).toContain('Abrigo de invierno')
    expect(wrapper.text()).toContain('Ropa')
    expect(wrapper.text()).toContain('$12.30')
  })

  it('muestra la imagen real cuando el producto tiene una', () => {
    const wrapper = mountCard()
    const img = wrapper.get('img')

    expect(img.attributes('src')).toBe('http://localhost:5000/images/abrigo.jpg')
    expect(img.attributes('alt')).toBe('Abrigo de invierno')
  })

  it('muestra un respaldo (sin <img>) cuando no hay imagen', () => {
    const wrapper = mountCard({ primaryImageFileName: null })

    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.text()).toContain('🛍️')
  })

  it('muestra "Consultar" si el producto no tiene precio', () => {
    const wrapper = mountCard({ minPrice: null })

    expect(wrapper.text()).toContain('Consultar')
  })

  it('enlaza al detalle del producto', () => {
    const wrapper = mountCard()

    expect(wrapper.getComponent(RouterLinkStub).props('to')).toEqual({
      name: 'product-detail',
      params: { id: 'p-1' }
    })
  })

  it('muestra las estrellas y la cantidad de reseñas cuando las hay', () => {
    const wrapper = mount(ProductCard, {
      props: { product, rating: { average: 4.5, count: 12 } },
      global: { stubs: { RouterLink: RouterLinkStub } }
    })

    expect(wrapper.get('[role="img"]').attributes('aria-label')).toBe('Calificación: 4.5 de 5')
    expect(wrapper.text()).toContain('(12)')
  })

  it('no muestra estrellas si el producto aún no tiene reseñas', () => {
    const wrapper = mount(ProductCard, {
      props: { product, rating: { average: 0, count: 0 } },
      global: { stubs: { RouterLink: RouterLinkStub } }
    })

    expect(wrapper.find('[role="img"]').exists()).toBe(false)
  })

  it('tampoco muestra estrellas si no se le pasa rating', () => {
    expect(mountCard().find('[role="img"]').exists()).toBe(false)
  })
})
