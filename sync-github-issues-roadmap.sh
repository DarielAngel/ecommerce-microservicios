#!/usr/bin/env bash
# ============================================================================
# Crea las tareas (issues) del ROADMAP DE FUNCIONALIDADES derivado del análisis de
# 25 plataformas de e-commerce (ver docs/ANALISIS-MERCADO.md y
# docs/ROADMAP-FUNCIONALIDADES.md).
#
#   - Fases 0 y 1 ya están implementadas y probadas  -> se crean y se CIERRAN.
#   - Fases 2 a 6 y "Futuro"                         -> se crean ABIERTAS.
#
# REQUISITOS (en TU máquina, no en el sandbox de Claude):
#   1. GitHub CLI instalado y autenticado: gh auth login
#   2. Correr este script DESDE DENTRO del repo ya clonado/conectado a GitHub.
#   3. Es IDEMPOTENTE: si un issue con el mismo título ya existe (abierto o cerrado) lo
#      salta, así que correrlo dos veces no crea duplicados.
# ============================================================================

set -e

echo "Creando las tareas del roadmap de funcionalidades..."
echo ""

gh label create "roadmap" --color "1D76DB" --description "Funcionalidad del roadmap de mercado" 2>/dev/null || true
gh label create "fase-0" --color "BFD4F2" --description "Identidad visual" 2>/dev/null || true
gh label create "fase-1" --color "BFD4F2" --description "Reseñas y calificaciones" 2>/dev/null || true
gh label create "fase-2" --color "BFD4F2" --description "Favoritos" 2>/dev/null || true
gh label create "fase-3" --color "BFD4F2" --description "Cupones y promociones" 2>/dev/null || true
gh label create "fase-4" --color "BFD4F2" --description "Descubrimiento" 2>/dev/null || true
gh label create "fase-5" --color "BFD4F2" --description "Checkout rápido y seguimiento" 2>/dev/null || true
gh label create "fase-6" --color "BFD4F2" --description "Lealtad y recuperación" 2>/dev/null || true
gh label create "mejora-futura" --color "FBCA04" --description "Pendiente, fuera del alcance actual" 2>/dev/null || true

# Títulos que ya existen en el repo (abiertos Y cerrados), consultados UNA vez al inicio.
EXISTING_TITLES=$(gh issue list --state all --limit 1000 --json title --jq '.[].title')

issue_exists () {
  printf '%s\n' "$EXISTING_TITLES" | grep -Fxq -- "$1"
}

create_and_close () {
  local title="$1" body="$2" labels="$3"
  if issue_exists "$title"; then echo "-> [ya existe, se omite] $title"; return; fi
  echo "-> [cerrado] $title"
  local url number
  url=$(gh issue create --title "$title" --body "$body" --label "$labels")
  number=$(echo "$url" | grep -oE '[0-9]+$')
  gh issue close "$number" --comment "Completado y verificado (pruebas escritas primero)."
}

create_open () {
  local title="$1" body="$2" labels="$3"
  if issue_exists "$title"; then echo "-> [ya existe, se omite] $title"; return; fi
  echo "-> [abierto] $title"
  gh issue create --title "$title" --body "$body" --label "$labels" > /dev/null
}

# ---------------------------------------------------------------------------
# FASE 0 — Identidad visual y sistema de diseño (inspiración: Squarespace, Shopify, Shopee)
# ---------------------------------------------------------------------------
create_and_close "T0.1 Tokens de diseño semánticos (colores, tipografía Inter, sombras)" \
  "Variables CSS + Tailwind: bg-surface, text-ink, border-line, text-brand-ink. Cambian solas entre claro/oscuro." "roadmap,fase-0"
create_and_close "T0.2 Modo oscuro persistente" \
  "Botón en el encabezado, respeta la preferencia del sistema la primera vez, recuerda la elección y se aplica antes de pintar (sin parpadeo). Tests: store theme, ThemeToggle, e2e." "roadmap,fase-0"
create_and_close "T0.3 Encabezado con buscador global y menú de usuario" \
  "Búsqueda por ?q= desde cualquier pantalla, contador de carrito, sesión. Tests: AppHeader (8) + e2e." "roadmap,fase-0"
create_and_close "T0.4 Portada con banner y tarjeta de producto unificada" \
  "Hero configurable (STORE_NAME / STORE_TAGLINE), chips de categoría, filtros, estados vacíos, ProductCard reutilizable." "roadmap,fase-0"
create_and_close "T0.5 Pie de página, esqueletos de carga y avisos (toasts)" \
  "SkeletonCard, ToastHost + store con auto-descarte. Tests del store de toasts." "roadmap,fase-0"
create_and_close "T0.6 Responsive / móvil primero" \
  "Grillas 2/3/4 columnas, buscador en segunda fila en móvil, navegación adaptable." "roadmap,fase-0"

# ---------------------------------------------------------------------------
# FASE 1 — Reseñas y calificaciones (inspiración: Amazon, Walmart, Etsy, Shein)
# ---------------------------------------------------------------------------
create_and_close "T1.1 Dominio de reseñas: una por usuario y producto, calificación 1-5" \
  "Review + RatingSummary + nombre protegido ('Ana P.'). Índice UNIQUE (user_id, product_id) como garantía real ante carreras." "roadmap,fase-1"
create_and_close "T1.2 API de reseñas: crear, editar, borrar, listar, resumen y resumen en lote" \
  "Servicio Reviews (puerto 5008) con su propia base de datos, ruta en el Gateway, 20 contenedores en total." "roadmap,fase-1"
create_and_close "T1.3 Compra verificada vía OrderPaidEvent" \
  "El contrato se extiende con ProductIds opcional (compatible). Reviews lo consume con cola propia 'reviews-order-paid' para no competir con Notificaciones." "roadmap,fase-1"
create_and_close "T1.4 Moderación: un Admin puede eliminar cualquier reseña (endpoint)" \
  "DELETE /api/reviews/{id} con rol Admin. Probado en unitarias e integración." "roadmap,fase-1"
create_and_close "T1.5 UI de reseñas en el storefront" \
  "StarRating accesible (teclado + lector de pantalla), ReviewsSection (resumen, distribución, formulario, edición, borrado con confirmación, orden y 'Cargar más'), estrellas en las tarjetas del catálogo. Vitest (81) + Playwright." "roadmap,fase-1"
create_open "T1.6 Pantalla de moderación de reseñas en el panel de Admin" \
  "El endpoint DELETE ya existe; falta una vista en el admin-panel para listar las reseñas recientes y eliminarlas. Requiere un endpoint de listado para Admin (GET /api/reviews/admin/latest)." "roadmap,fase-1,mejora-futura"
create_open "T1.7 Reseñas con fotos y votos de 'útil'" \
  "Inspiración: Amazon / Shein. Votos de utilidad (1 por usuario) y orden 'Más útiles'; fotos requieren almacenamiento de archivos." "roadmap,fase-1,mejora-futura"

# ---------------------------------------------------------------------------
# FASE 2 — Favoritos (inspiración: Amazon, Etsy, Zalando)
# ---------------------------------------------------------------------------
create_open "T2.1 Servicio Wishlist: agregar / quitar favoritos (idempotente) y listar" \
  "Nuevo microservicio con su base de datos. Aceptación: agregar dos veces no duplica; quitar algo que no está no falla; solo el dueño ve sus favoritos." "roadmap,fase-2"
create_open "T2.2 Corazón en tarjetas y detalle + página 'Mis favoritos'" \
  "Usa el slot 'corner' de ProductCard. Estimación de impacto citada en el análisis: ~10% de mejora de conversión a largo plazo." "roadmap,fase-2"
create_open "T2.3 Mover un favorito al carrito" "Un clic desde 'Mis favoritos' hacia el carrito respetando el stock." "roadmap,fase-2"

# ---------------------------------------------------------------------------
# FASE 3 — Cupones y promociones (inspiración: Temu, Shein, Walmart, Shopify)
# ---------------------------------------------------------------------------
create_open "T3.1 Servicio Promotions: cupones de porcentaje y monto fijo" \
  "Vigencia, compra mínima, límite de usos totales y por usuario. Códigos no sensibles a mayúsculas." "roadmap,fase-3"
create_open "T3.2 Validar cupón en el carrito y canjearlo de forma atómica en el checkout" \
  "Se integra con la saga de Órdenes: canjear al crear la orden y liberar si el pago falla. Pruebas de concurrencia sobre el límite de usos." "roadmap,fase-3"
create_open "T3.3 Administración de cupones en el panel de Admin" "Crear, pausar y ver usos de cada cupón." "roadmap,fase-3"

# ---------------------------------------------------------------------------
# FASE 4 — Descubrimiento (inspiración: Amazon, Adobe Commerce, Walmart)
# ---------------------------------------------------------------------------
create_open "T4.1 Sugerencias de búsqueda mientras se escribe" "GET /api/products/suggestions?q= en Catálogo; menú desplegable accesible en el buscador del encabezado." "roadmap,fase-4"
create_open "T4.2 'También te puede interesar' (misma categoría)" "Endpoint de relacionados en Catálogo y carrusel en el detalle de producto." "roadmap,fase-4"
create_open "T4.3 'Vistos recientemente' (cliente)" "Persistido en el navegador; sin backend." "roadmap,fase-4"
create_open "T4.4 Insignia 'Quedan pocas unidades' con stock real" "Calculada SIEMPRE con datos reales de Inventario; nunca con presión artificial." "roadmap,fase-4"

# ---------------------------------------------------------------------------
# FASE 5 — Checkout rápido y seguimiento (inspiración: Shopify Shop Pay, Coupang, JD.com)
# ---------------------------------------------------------------------------
create_open "T5.1 Libreta de direcciones (servicio Users) y prefill en el checkout" "Varias direcciones, una predeterminada. Reduce fricción del cliente recurrente." "roadmap,fase-5"
create_open "T5.2 Línea de tiempo del pedido y fecha estimada de entrega" "Pagada -> Enviada -> Entregada; la promesa de entrega es lo que compiten Coupang, JD.com y Mercado Libre." "roadmap,fase-5"
create_open "T5.3 'Comprar de nuevo'" "Desde Mis pedidos, volver a poner los ítems en el carrito respetando stock y precios actuales." "roadmap,fase-5"

# ---------------------------------------------------------------------------
# FASE 6 — Lealtad y recuperación (inspiración: Mercado Libre MELI+, Rakuten, Coupang WOW, Shopify)
# ---------------------------------------------------------------------------
create_open "T6.1 Servicio Loyalty: puntos por compra y canje como descuento" "Consume OrderPaidEvent con cola propia. Puntos idempotentes por orden." "roadmap,fase-6"
create_open "T6.2 Correo de recuperación de carritos abandonados" "Un correo dentro de las primeras 24 h; los estudios citados en el análisis estiman recuperar entre 10% y 15% de las ventas perdidas." "roadmap,fase-6"

# ---------------------------------------------------------------------------
# Futuro (fuera de este ciclo)
# ---------------------------------------------------------------------------
create_open "Futuro: devoluciones y reembolsos" "Inspiración: Amazon, eBay, Etsy (protección al comprador)." "roadmap,mejora-futura"
create_open "Futuro: marketplace multi-vendedor" "Inspiración: Amazon, Taobao/Tmall, Mercado Libre." "roadmap,mejora-futura"
create_open "Futuro: multimoneda e i18n" "Inspiración: PrestaShop, Shopify." "roadmap,mejora-futura"
create_open "Futuro: PWA instalable" "Inspiración: Shopee, Flipkart (móvil primero)." "roadmap,mejora-futura"
create_open "Futuro: asistente de compras con IA" "Inspiración: Amazon Rufus, Shopify Sidekick." "roadmap,mejora-futura"
create_open "Futuro: pago en cuotas / B2B (cotizaciones, precios por volumen)" "Inspiración: Flipkart, Adobe Commerce." "roadmap,mejora-futura"

echo ""
echo "Listo. Revisa los issues en GitHub (filtra por la etiqueta 'roadmap')."
