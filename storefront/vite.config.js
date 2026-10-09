import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'
import { VitePWA } from 'vite-plugin-pwa'
import { catalogPattern, imagesPattern } from './pwa-cache-rules.js'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd())
  const storeName = env.VITE_STORE_NAME || 'Tienda'
  const tagline = env.VITE_STORE_TAGLINE || 'Todo lo que necesitas, en un solo lugar'
  // El Gateway vive en otro origen (otro puerto): las reglas de caché lo apuntan con su origen exacto.
  const apiBaseUrl = env.VITE_API_BASE_URL || 'http://localhost:5000'

  return {
    plugins: [
      vue(),
      // PWA: la tienda se puede instalar como app y abre sin conexión (ver README, "PWA").
      VitePWA({
        // "prompt": una versión nueva NO se activa sola a mitad de una compra; la tienda avisa y el cliente
        // elige cuándo recargar (PwaPrompts.vue registra el service worker).
        registerType: 'prompt',
        injectRegister: false,
        includeAssets: ['favicon.svg', 'icons/apple-touch-icon.png'],
        manifest: {
          name: storeName,
          short_name: storeName.length > 12 ? storeName.slice(0, 12) : storeName,
          description: tagline,
          lang: 'es',
          dir: 'ltr',
          start_url: '/',
          scope: '/',
          display: 'standalone',
          theme_color: '#059669',
          background_color: '#ffffff',
          categories: ['shopping'],
          icons: [
            { src: '/icons/icon-192.png', sizes: '192x192', type: 'image/png' },
            { src: '/icons/icon-512.png', sizes: '512x512', type: 'image/png' },
            { src: '/icons/maskable-512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' }
          ],
          // Accesos directos al mantener presionado el ícono (Android / escritorio).
          shortcuts: [
            { name: 'Mis pedidos', url: '/orders', icons: [{ src: '/icons/icon-192.png', sizes: '192x192' }] },
            { name: 'Carrito', url: '/cart', icons: [{ src: '/icons/icon-192.png', sizes: '192x192' }] },
            { name: 'Favoritos', url: '/favorites', icons: [{ src: '/icons/icon-192.png', sizes: '192x192' }] }
          ]
        },
        workbox: {
          // La "cáscara" de la app (HTML, JS, CSS, fuentes, íconos) queda guardada: la tienda abre sin red.
          globPatterns: ['**/*.{js,css,html,svg,png,woff2}'],
          navigateFallback: '/index.html',
          cleanupOutdatedCaches: true,
          runtimeCaching: [
            {
              // Catálogo público (productos, categorías, reseñas): primero la red; sin red (o si tarda más de
              // 4 s), lo último que se vio. NUNCA se guarda nada del cliente: carrito, pedidos, puntos,
              // direcciones, favoritos, cupones o pagos siempre van a la red.
              urlPattern: catalogPattern(apiBaseUrl),
              handler: 'NetworkFirst',
              method: 'GET',
              options: {
                cacheName: 'catalogo',
                networkTimeoutSeconds: 4,
                expiration: { maxEntries: 150, maxAgeSeconds: 60 * 60 * 24 * 3 },
                cacheableResponse: { statuses: [200] }
              }
            },
            {
              // Fotos de productos: no cambian (cada subida tiene su propio nombre), así que primero la caché.
              urlPattern: imagesPattern(apiBaseUrl),
              handler: 'CacheFirst',
              method: 'GET',
              options: {
                cacheName: 'fotos',
                expiration: { maxEntries: 300, maxAgeSeconds: 60 * 60 * 24 * 30 },
                cacheableResponse: { statuses: [0, 200] }
              }
            }
          ]
        }
      })
    ],
    server: { port: 5173 },
    test: {
      environment: 'happy-dom',
      globals: true
    }
  }
})
