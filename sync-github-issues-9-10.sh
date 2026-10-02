#!/usr/bin/env bash
# ============================================================================
# Completa el historial de issues desde el Módulo 9 en adelante. Los Módulos
# 1-8 ya deberían estar creados por create-github-issues.sh y
# create-github-issue-modulo8.sh — este script NO los repite, para no
# duplicar.
#
# REQUISITOS (en TU máquina, no en el sandbox de Claude):
#   1. GitHub CLI instalado y autenticado: gh auth login
#   2. Correr este script DESDE DENTRO del repo ya clonado/conectado a GitHub.
# ============================================================================

set -e

echo "Sincronizando issues desde el Módulo 9 en adelante..."
echo ""

gh label create "modulo" --color "0E8A16" --description "Modulo del ecommerce" 2>/dev/null || true
gh label create "bug" --color "D73A4A" --description "Bug real encontrado y corregido" 2>/dev/null || true
gh label create "mejora-futura" --color "FBCA04" --description "Pendiente, fuera del alcance actual" 2>/dev/null || true

create_and_close () {
  local title="$1"
  local body="$2"
  local label="$3"
  echo "-> [cerrado] $title"
  issue_url=$(gh issue create --title "$title" --body "$body" --label "$label")
  issue_number=$(echo "$issue_url" | grep -oE '[0-9]+$')
  gh issue close "$issue_number" --comment "Completado y verificado."
}

create_open () {
  local title="$1"
  local body="$2"
  local label="$3"
  echo "-> [abierto] $title"
  gh issue create --title "$title" --body "$body" --label "$label" > /dev/null
}

# ---------------------------------------------------------------------------
# Módulos completados
# ---------------------------------------------------------------------------

create_and_close \
  "Modulo 9: Panel de Administracion" \
  "SPA de Vue 3 + Tailwind + Pinia + pnpm, servida por nginx en Docker (puerto 8081). Login en dos capas (clave de sitio + login real contra Users con rol Admin). Gestion de productos, categorias, inventario, ordenes (ver todas + marcar enviadas), creacion de administradores, y auditoria de notificaciones enviadas." \
  "modulo"

create_and_close \
  "Modulo 10: Frontend de clientes" \
  "SPA de Vue 3 + Tailwind + Pinia + pnpm, servida por nginx en Docker (puerto 5173). Catalogo con busqueda/filtros, registro/login con renovacion automatica de token, carrito, checkout con PayPal, e historial de pedidos. Verificado de punta a punta en vivo, incluido el flujo completo de checkout con PayPal simulado." \
  "modulo"

# ---------------------------------------------------------------------------
# Bugs reales encontrados y corregidos en el camino
# ---------------------------------------------------------------------------

create_and_close \
  "Bug: .dockerignore ubicado en subcarpetas, Docker solo lee el de la raiz" \
  "Docker Compose usa 'context: .' (raiz del repo) para todos los servicios. El .dockerignore dentro de admin-panel/ y storefront/ se ignoraba en silencio, causando que node_modules local (con symlinks de Windows) se colara al build y pisara el node_modules recien instalado dentro del contenedor. Fix: un unico .dockerignore en la raiz, cubriendo .NET (bin/obj) y Node (node_modules/dist) a la vez." \
  "bug"

create_and_close \
  "Bug: symlinks de pnpm no se resuelven en node:20-alpine" \
  "'Cannot find module vite/bin/vite.js' al construir admin-panel/storefront en Docker, aunque pnpm install terminaba sin error. Causa: los symlinks por defecto de pnpm no se resuelven de forma confiable en Alpine. Fix: node-linker=hoisted via .npmrc en ambos proyectos (node_modules plano, sin symlinks)." \
  "bug"

create_and_close \
  "Bug: pnpm exige 'packages:' en pnpm-workspace.yaml incluso para un solo paquete" \
  "pnpm approve-builds genera pnpm-workspace.yaml con la clave allowBuilds, pero sin 'packages:' pnpm lo trata como workspace invalido ('packages field missing or empty'). Fix: agregar 'packages: - .' preservando allowBuilds." \
  "bug"

create_and_close \
  "Bug: esquema de Postgres desactualizado tras agregar columnas nuevas" \
  "EnsureCreatedAsync() solo crea las tablas la primera vez que la base no existe - nunca las actualiza. Al agregar user_email/user_full_name a Order (Modulo 8), postgres-orders ya existia desde el Modulo 7 y nunca se entero, causando 'column o.user_email does not exist'. Documentado en el README con el fix (ALTER TABLE manual o reset del volumen)." \
  "bug"

create_and_close \
  "Bug: CORS del Gateway no incluia todos los puertos de desarrollo de los frontends" \
  "El panel de Admin y el storefront corren en puertos distintos segun el modo (pnpm run dev vs Docker/nginx). Cada puerto nuevo (5174, 5173, 8081) tuvo que agregarse explicitamente a la whitelist de CORS del Gateway." \
  "bug"

create_and_close \
  "Bug: input de cantidad del carrito se quedaba 'pegado' al exceder el stock" \
  "El input de cantidad en CartView no tenia v-model (binding de solo escritura hacia el DOM) - al fallar la actualizacion en el servidor (stock insuficiente), Vue no tenia motivo para redibujar el input, quedando mostrado un valor invalido indefinidamente. Fix: v-model.number reactivo real, que revierte al ultimo valor confirmado por el servidor ante cualquier fallo, mas visibilidad del stock disponible por linea." \
  "bug"

create_and_close \
  "Bug: el pop-up de PayPal no se abria en el checkout" \
  "window.open() se llamaba despues de un await, momento en el que el navegador ya no reconoce la accion como iniciada directamente por el usuario y bloquea el pop-up en silencio. Fix: abrir la ventana (en blanco) de forma sincrona antes del await, navegandola a la URL real cuando llega la respuesta; con fallback a un link manual si el navegador bloquea incluso la ventana en blanco." \
  "bug"

# ---------------------------------------------------------------------------
# Mejoras agregadas durante la verificacion
# ---------------------------------------------------------------------------

create_and_close \
  "Mejora: modo PayPal simulado para pruebas sin cuenta real" \
  "IPayPalClient simulado (FakePayPalClient) activable por configuracion (PayPal:Provider=Fake), misma interfaz que ya usaban los tests automatizados. Por defecto siempre usa el PayPal real - nunca se activa solo. Permite probar el flujo completo de checkout (pagar -> Paid -> Shipped -> emails) sin necesitar una cuenta de PayPal." \
  "mejora-futura"

create_and_close \
  "Mejora: configuracion persistente via .env en vez de 'set' por terminal" \
  "Las variables definidas con 'set VARIABLE=valor' en cmd.exe solo duran mientras esa ventana siga abierta, causando que servicios se reconstruyan sin credenciales (ej. RESEND_API_KEY) al abrir una terminal nueva. Fix: .env.example documentado, leido automaticamente por Docker Compose en cada 'up', sin intervencion manual repetida." \
  "mejora-futura"

# ---------------------------------------------------------------------------
# Pendiente (abiertos a proposito, sin cerrar)
# ---------------------------------------------------------------------------

create_open \
  "Pendiente: verificar el flujo de pago con PayPal real" \
  "El checkout completo (crear orden, aprobar, capturar) esta cubierto por tests automatizados con fakes y verificado en vivo con el modo simulado (PayPal:Provider=Fake), pero nunca contra la API real de PayPal por no tener acceso a una cuenta de sandbox. Retomar si se consigue acceso (otra region, VPN, etc.)." \
  "mejora-futura"

create_open \
  "Pendiente: tests automatizados para los frontends (admin-panel y storefront)" \
  "Ambos frontends se validaron con 'pnpm run build' (compilacion real, cero errores) y QA manual exhaustivo en vivo, pero no tienen suite de tests propia (Vitest para unidad/componentes, Playwright o Cypress para end-to-end). El resto del proyecto (los 8 microservicios) si tiene cobertura de tests unitarios e integracion." \
  "mejora-futura"

create_open \
  "Pendiente: probar el webhook real de PayPal en vivo" \
  "El endpoint /api/payments/webhook esta implementado (verificacion de firma, manejo idempotente de PAYMENT.CAPTURE.COMPLETED/DENIED) pero nunca se probo contra un webhook real de PayPal, porque requiere una URL publica (ngrok) - documentado como paso opcional/avanzado en el Modulo 6. La captura manual ya cubre el flujo principal sin esto." \
  "mejora-futura"

create_open \
  "Pendiente: migrar de EnsureCreated a migraciones reales de EF Core" \
  "Todo el proyecto usa EnsureCreatedAsync() en vez de migraciones versionadas, una decision consciente de velocidad de desarrollo (documentada en el README junto con su principal consecuencia: hay que actualizar el esquema a mano cuando se agregan columnas a una entidad existente). Para un entorno de produccion real, convendria migrar a EF Core Migrations." \
  "mejora-futura"

echo ""
echo "Listo. Revisa la pestaña Issues de tu repo: 9 cerrados, 4 abiertos."
