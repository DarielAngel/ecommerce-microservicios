import { describe, it, expect } from 'vitest'
import { emptyAddress, toForm, isComplete, formatAddress, errorText } from './addresses'

const full = {
  label: 'Casa', recipientName: 'Ana Martínez', phone: '+54 351 555-1234', street: 'Av. Siempre Viva 742',
  details: 'Piso 3, depto B', city: 'Córdoba', region: 'Córdoba', postalCode: '5000', country: 'Argentina'
}

describe('utils/addresses', () => {
  it('formatea igual que el servicio Users (mismos casos que AddressBookTests)', () => {
    expect(formatAddress(full)).toBe(
      'Ana Martínez, Av. Siempre Viva 742, Piso 3, depto B, 5000 Córdoba, Argentina · Tel. +54 351 555-1234')
    expect(formatAddress({ ...emptyAddress('Luis Pérez'), street: 'Calle 9 #12', city: 'Medellín', region: 'Antioquia', country: 'Colombia' }))
      .toBe('Luis Pérez, Calle 9 #12, Medellín, Antioquia, Colombia')
  })

  it('recorta espacios y omite los opcionales vacíos', () => {
    expect(formatAddress({ recipientName: ' Ana ', street: ' Calle 1 ', details: '  ', city: ' Lima ', country: ' Perú ' }))
      .toBe('Ana, Calle 1, Lima, Perú')
  })

  it('isComplete exige quién recibe, calle, ciudad y país', () => {
    expect(isComplete(full)).toBe(true)
    expect(isComplete({ ...full, street: '  ' })).toBe(false)
    expect(isComplete(emptyAddress('Ana'))).toBe(false)
  })

  it('toForm copia una dirección guardada (y su "predeterminada")', () => {
    const form = toForm({ ...full, id: 'a1', isDefault: true, details: null, formatted: 'x' })
    expect(form.details).toBe('')
    expect(form.makeDefault).toBe(true)
    expect(form).not.toHaveProperty('id')
  })

  it('errorText lista los errores por campo del servidor, o el mensaje general', () => {
    const validation = { message: 'Se encontraron errores de validación.', body: { errors: { 'Input.Street': ['Escribe la calle y el número.'], 'Input.City': ['Escribe la ciudad.'] } } }
    expect(errorText(validation)).toBe('Escribe la calle y el número. Escribe la ciudad.')
    expect(errorText({ message: 'La dirección no existe.' })).toBe('La dirección no existe.')
  })
})
