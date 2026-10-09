import { describe, it, expect } from 'vitest'
import { catalogPattern, imagesPattern } from './pwa-cache-rules'

const API = 'http://localhost:5000'
const catalog = catalogPattern(API)

describe('reglas de caché de la PWA', () => {
  it.each([
    '/api/products',
    '/api/products?searchTerm=abrigo&page=2',
    '/api/products/0b6c2c1e-1111-4bbb-8888-123456789abc',
    '/api/products/suggestions?q=gal&limit=6',
    '/api/products/p1/related?limit=8',
    '/api/categories',
    '/api/reviews/summaries?productIds=a&productIds=b',
    '/api/reviews/products/p1/summary',
    '/api/reviews/products/p1?sort=recent&page=1'
  ])('guarda el catálogo público: %s', (path) => {
    expect(catalog.test(API + path)).toBe(true)
  })

  it.each([
    '/api/cart',
    '/api/orders',
    '/api/orders/o1',
    '/api/loyalty/me',
    '/api/addresses',
    '/api/wishlist',
    '/api/coupons/validate?code=X&subtotal=10',
    '/api/stock/availability?variantIds=v1',
    '/api/reviews/products/p1/mine',
    '/api/auth/login',
    '/api/productsfake',
    '/api/payments/o1'
  ])('NUNCA guarda datos del cliente ni nada fuera del catálogo: %s', (path) => {
    expect(catalog.test(API + path)).toBe(false)
  })

  it('solo mira el Gateway configurado, no otros sitios con rutas parecidas', () => {
    expect(catalog.test('https://otro-sitio.com/api/products')).toBe(false)
    expect(catalog.test('http://localhost:5000.evil.com/api/products')).toBe(false)
    expect(catalogPattern('https://api.mitienda.com/').test('https://api.mitienda.com/api/categories')).toBe(true)
  })

  it('las fotos van por su propia regla', () => {
    const images = imagesPattern(API)
    expect(images.test(`${API}/images/abc123.jpg`)).toBe(true)
    expect(images.test(`${API}/api/products`)).toBe(false)
    expect(images.test('https://cdn.otro.com/images/x.jpg')).toBe(false)
  })
})
