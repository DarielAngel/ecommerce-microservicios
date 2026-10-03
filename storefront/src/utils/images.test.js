import { describe, it, expect } from 'vitest'
import { imageUrl } from './images'

describe('imageUrl', () => {
  it('construye la URL bajo /images/ a partir del nombre de archivo', () => {
    expect(imageUrl('abc123.jpg')).toBe('http://localhost:5000/images/abc123.jpg')
  })

  it('devuelve null si no hay nombre de archivo (producto sin imagen)', () => {
    expect(imageUrl(null)).toBeNull()
    expect(imageUrl(undefined)).toBeNull()
    expect(imageUrl('')).toBeNull()
  })
})
