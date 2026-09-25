#!/usr/bin/env bash
# ============================================================================
# Crea un issue por módulo del proyecto en tu repo de GitHub, y cierra
# automáticamente los que ya están completados (todos, hasta el Módulo 7).
#
# REQUISITOS (en TU máquina, no en el sandbox de Claude):
#   1. GitHub CLI instalado: https://cli.github.com/
#   2. Haber corrido: gh auth login   (una sola vez, con tu propia cuenta)
#   3. Correr este script DESDE DENTRO del repo ya clonado/con el remoto
#      configurado (ver INSTRUCCIONES-GITHUB.md).
# ============================================================================

set -e

echo "Creando issues en el repositorio de GitHub actual..."
echo ""

create_and_close () {
  local title="$1"
  local body="$2"
  echo "-> $title"
  issue_url=$(gh issue create --title "$title" --body "$body" --label "modulo")
  issue_number=$(echo "$issue_url" | grep -oE '[0-9]+$')
  gh issue close "$issue_number" --comment "Completado y verificado (unitarios + integracion en verde, checklist manual pasado)."
}

gh label create "modulo" --color "0E8A16" --description "Modulo del ecommerce" 2>/dev/null || true

create_and_close \
  "Modulo 1: Infraestructura base" \
  "Docker Compose con Postgres por servicio, RabbitMQ, y el Gateway (YARP) como unico punto de entrada."

create_and_close \
  "Modulo 2: Users/Auth" \
  "Registro, login, JWT (access + refresh token con rotacion), roles Cliente/Admin, endpoint protegido de creacion de Admins. Clean Architecture completa con tests unitarios e integracion (Testcontainers)."

create_and_close \
  "Modulo 3: Catalogo de productos" \
  "CRUD de productos con variantes (SKU, precio, atributos libres), categorias, busqueda con filtros (texto parcial, categoria, rango de precio), subida de imagenes. Integracion con Users via JWT compartido."

create_and_close \
  "Modulo 4: Inventario" \
  "Stock por variante con reservas atomicas (ExecuteUpdateAsync, sin condiciones de carrera). Se entera de nuevas variantes automaticamente via evento VariantCreated (RabbitMQ, MassTransit) publicado por Catalogo."

create_and_close \
  "Modulo 5: Carrito de compras" \
  "Un carrito por usuario logueado. Valida stock disponible en tiempo real contra Inventario al agregar/actualizar items. Precio de cada linea queda congelado desde que se agrega."

create_and_close \
  "Modulo 6: Pagos (PayPal)" \
  "Integracion real con la API de PayPal (sandbox): OAuth2, creacion de orden, captura, verificacion de firma de webhooks. Idempotente ante reintentos y webhooks duplicados."

create_and_close \
  "Modulo 7: Ordenes/Checkout (saga)" \
  "Saga orquestada: Carrito -> Inventario (reserva) -> Pagos (crea orden) -> confirmacion tras aprobacion del comprador -> captura + confirmar stock, o liberar stock si el pago falla. Checkout parcial del carrito, dirección de envio."

echo ""
echo "Listo. Revisa los issues (todos cerrados) en la pestaña Issues de tu repo."
