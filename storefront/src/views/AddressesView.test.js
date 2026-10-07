import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises, RouterLinkStub } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

const { client } = vi.hoisted(() => ({ client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() } }))
vi.mock('../api/useApi', () => ({ useApi: () => client }))

import AddressesView from './AddressesView.vue'
import { useAuthStore } from '../stores/auth'
import { useToastStore } from '../stores/toast'

const casa = { id: 'a1', label: 'Casa', recipientName: 'Ana', street: 'Calle 1', city: 'Lima', country: 'Perú', isDefault: true, formatted: 'Ana, Calle 1, Lima, Perú' }
const oficina = { id: 'a2', label: 'Oficina', recipientName: 'Ana', street: 'Av. 2', city: 'Lima', country: 'Perú', isDefault: false, formatted: 'Ana, Av. 2, Lima, Perú' }

async function mountView(list) {
  client.get.mockResolvedValue(list)
  const pinia = createPinia()
  setActivePinia(pinia)
  useAuthStore().$patch({ user: { fullName: 'Ana Martínez' } })
  const wrapper = mount(AddressesView, { global: { plugins: [pinia], stubs: { RouterLink: RouterLinkStub } } })
  await flushPromises()
  return wrapper
}

const items = (w) => w.findAll('[data-testid="address-item"]')

describe('AddressesView', () => {
  beforeEach(() => {
    for (const fn of [client.get, client.post, client.put, client.delete]) fn.mockReset()
  })

  it('sin direcciones muestra el estado vacío y el botón para agregar', async () => {
    const wrapper = await mountView([])

    expect(wrapper.find('[data-testid="addresses-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="address-new"]').exists()).toBe(true)
  })

  it('lista las direcciones y marca la predeterminada', async () => {
    const wrapper = await mountView([casa, oficina])

    expect(items(wrapper)).toHaveLength(2)
    expect(items(wrapper)[0].find('[data-testid="address-default-badge"]').exists()).toBe(true)
    expect(items(wrapper)[1].find('[data-testid="address-default-badge"]').exists()).toBe(false)
    // La predeterminada no ofrece "Usar como predeterminada".
    expect(items(wrapper)[0].find('[data-testid="address-make-default"]').exists()).toBe(false)
  })

  it('agregar: precarga quién recibe, la primera queda predeterminada y recarga la lista', async () => {
    const wrapper = await mountView([])
    await wrapper.get('[data-testid="address-new"]').trigger('click')

    expect(wrapper.get('#book-recipient').element.value).toBe('Ana Martínez')
    await wrapper.get('#book-street').setValue('Calle 1')
    await wrapper.get('#book-city').setValue('Lima')
    await wrapper.get('#book-country').setValue('Perú')
    client.post.mockResolvedValue(casa)
    client.get.mockResolvedValue([casa])
    await wrapper.get('[data-testid="address-form"]').trigger('submit')
    await flushPromises()

    expect(client.post).toHaveBeenCalledWith('/api/addresses', expect.objectContaining({ street: 'Calle 1', makeDefault: true }))
    expect(items(wrapper)).toHaveLength(1)
    expect(wrapper.find('[data-testid="address-form"]').exists()).toBe(false)
  })

  it('un error de validación del servidor se muestra con el detalle del campo', async () => {
    const wrapper = await mountView([])
    await wrapper.get('[data-testid="address-new"]').trigger('click')
    await wrapper.get('#book-street').setValue('Calle 1')
    await wrapper.get('#book-city').setValue('Lima')
    await wrapper.get('#book-country').setValue('Perú')
    client.post.mockRejectedValue(Object.assign(new Error('Se encontraron errores de validación.'), {
      body: { errors: { 'Input.Phone': ['El teléfono solo puede tener números, espacios y + ( ) -.'] } }
    }))
    await wrapper.get('[data-testid="address-form"]').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[data-testid="address-form-error"]').text()).toBe('El teléfono solo puede tener números, espacios y + ( ) -.')
  })

  it('cambiar la predeterminada usa la lista que devuelve el servidor', async () => {
    const wrapper = await mountView([casa, oficina])
    client.post.mockResolvedValue([{ ...oficina, isDefault: true }, { ...casa, isDefault: false }])

    await items(wrapper)[1].get('[data-testid="address-make-default"]').trigger('click')
    await flushPromises()

    expect(client.post).toHaveBeenCalledWith('/api/addresses/a2/default')
    expect(items(wrapper)[0].text()).toContain('Oficina')
    expect(useToastStore().toasts.at(-1).message).toContain('"Oficina" es ahora tu dirección predeterminada')
  })

  it('borrar pide confirmación en la tarjeta antes de llamar al servidor', async () => {
    const wrapper = await mountView([casa, oficina])
    client.delete.mockResolvedValue([{ ...oficina, isDefault: true }])

    await items(wrapper)[0].get('[data-testid="address-delete"]').trigger('click')
    expect(client.delete).not.toHaveBeenCalled()
    await items(wrapper)[0].get('[data-testid="address-confirm-delete"]').trigger('click')
    await flushPromises()

    expect(client.delete).toHaveBeenCalledWith('/api/addresses/a1')
    expect(items(wrapper)).toHaveLength(1)
  })
})
