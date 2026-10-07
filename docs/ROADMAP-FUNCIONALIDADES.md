# Roadmap de funcionalidades (derivado del análisis de mercado)

Ver el análisis completo en [ANALISIS-MERCADO.md](ANALISIS-MERCADO.md).

## Cómo se priorizó

Cada idea se puntuó por **impacto en la conversión/retención**, **relevancia para esta tienda**,
**encaje con la arquitectura actual** y **esfuerzo**. Las que puntuaron alto y bajo en esfuerzo van primero.

## Reglas de arquitectura (no se negocian)

1. **Un bounded context = un microservicio**, con su propia base de datos (Database per Service) y
   proyectos `Domain → Application → Infrastructure → Api`. Nada de tablas compartidas entre servicios.
2. **Comunicación entre servicios**: HTTP síncrono solo cuando la respuesta es necesaria en ese instante
   (ej. validar stock); todo lo demás se propaga por **eventos en RabbitMQ** (MassTransit). Los contratos
   viven en `src/Shared/Contracts` y solo se **extienden** (campos opcionales al final), nunca se rompen.
3. **Todo pasa por el Gateway** (YARP). El navegador conoce una sola URL.
4. **Seguridad**: JWT compartido; los roles se validan en cada servicio, no solo en el frontend.
5. **Definition of Done de cada tarea**:
   - Pruebas escritas **antes** de la implementación (unitarias + integración en .NET; Vitest en frontend;
     Playwright para el flujo de usuario).
   - La suite completa pasa en local y en GitHub Actions.
   - Documentado en el README y reflejado en los issues.
   - Si agrega un servicio: Dockerfile, entrada en `docker-compose.yml`, ruta en el Gateway, healthcheck.

## Fases

### Fase 0 — Identidad visual y sistema de diseño (storefront)
Inspiración: Squarespace, Shopify, Shopee (móvil primero).
- T0.1 Tokens de diseño semánticos (colores, tipografía Inter, radios, sombras)
- T0.2 Modo oscuro persistente (respeta la preferencia del sistema)
- T0.3 Encabezado renovado con buscador global y menú de usuario
- T0.4 Portada con hero y tarjeta de producto unificada
- T0.5 Pie de página, estados de carga (skeletons) y notificaciones (toasts)
- T0.6 Responsive / móvil primero
- **Aceptación**: el modo se conserva al recargar; el buscador del encabezado filtra el catálogo.

### Fase 1 — Reseñas y calificaciones (servicio `Reviews`)
Inspiración: Amazon, Walmart, Etsy, Shein.
- T1.1 Dominio: una reseña por usuario y producto, calificación 1–5, título y comentario con límites
- T1.2 API: crear, editar, borrar, listar (paginado y ordenable), resumen por producto y en lote
- T1.3 "Compra verificada": `OrderPaidEvent` pasa a incluir `ProductIds`; Reviews lo consume
- T1.4 Moderación: un Admin puede eliminar cualquier reseña
- T1.5 UI: estrellas en tarjetas y detalle, distribución por estrellas, formulario y lista
- **Aceptación**: un cliente no puede reseñar dos veces el mismo producto; el promedio y el conteo se
  actualizan; "Compra verificada" aparece solo si realmente compró.

### Fase 2 — Favoritos / lista de deseos (servicio `Wishlist`)
Inspiración: Amazon, Etsy, Zalando.
- T2.1 Agregar / quitar favoritos (idempotente) y listar
- T2.2 Corazón en tarjetas y detalle; página "Mis favoritos"
- T2.3 Mover un favorito al carrito
- **Aceptación**: agregar dos veces no duplica; quitar algo que no está no falla; solo el dueño ve
  sus favoritos; mover al carrito respeta el stock (si no alcanza, el favorito se queda).

### Fase 3 — Cupones y promociones (servicio `Promotions`)
Inspiración: Temu, Shein, Walmart, Shopify.
- T3.1 Cupones de porcentaje y monto fijo, vigencia, mínimo de compra, límite de usos
- T3.2 Validación desde el carrito y canje atómico en el checkout (saga de Órdenes)
- T3.3 Administración de cupones en el panel de Admin
- **Aceptación**: un cupón limitado nunca se usa de más aunque haya checkouts simultáneos; si el pago
  falla, el uso vuelve a estar disponible; el cliente ve el motivo exacto cuando un cupón no aplica.

### Fase 4 — Descubrimiento (servicio `Catalog`)
Inspiración: Amazon, Adobe Commerce (recomendaciones), Walmart.
- T4.1 Sugerencias de búsqueda mientras se escribe
- T4.2 "También te puede interesar" (misma categoría)
- T4.3 "Vistos recientemente" (cliente)
- T4.4 Insignia "Quedan pocas unidades" calculada con stock real
- **Aceptación**: las sugerencias no distinguen tildes ni mayúsculas; los relacionados nunca incluyen el
  propio producto ni productos desactivados; la insignia solo aparece con stock real ≤ 5.

### Fase 5 — Checkout rápido y seguimiento
Inspiración: Shopify (Shop Pay), Coupang, JD.com, Mercado Libre.
- T5.1 Libreta de direcciones (servicio `Users`) y prefill en el checkout
- T5.2 Línea de tiempo del pedido (Pagada → Enviada) y fecha estimada de entrega
- T5.3 "Comprar de nuevo"

### Fase 6 — Lealtad y recuperación (servicios `Loyalty` y `Notifications`)
Inspiración: Mercado Libre (MELI+), Rakuten, Coupang (WOW), Shopify.
- T6.1 Puntos por compra (consume `OrderPaidEvent`) y canje como descuento
- T6.2 Correo de recuperación de carritos abandonados

### Futuro (fuera de este ciclo)
Devoluciones y reembolsos · multi-vendedor · multimoneda e i18n · PWA instalable · asistente de compras
con IA · suscripciones · pago en cuotas · B2B (cotizaciones, precios por volumen) · omnicanal.

## Estado

| Fase | Estado |
|---|---|
| 0 — Identidad visual | Hecha |
| 1 — Reseñas | Hecha (pendiente: pantalla de moderación en el panel de Admin; el endpoint ya existe) |
| 2 — Favoritos | Hecha |
| 3 — Cupones | Hecha |
| 4 — Descubrimiento | Hecha |
| 5 — Checkout rápido y seguimiento | Hecha |
| 6 | Pendiente |
