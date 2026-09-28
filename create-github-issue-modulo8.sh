#!/usr/bin/env bash
# Crea el issue del Módulo 8 (ABIERTO: ciérralo tú cuando verifiques los tests y el checklist).
# Requiere GitHub CLI autenticado (gh auth login) y correrse dentro del repo.
set -e
gh label create "modulo" --color "0E8A16" --description "Modulo del ecommerce" 2>/dev/null || true
gh issue create --label "modulo" \
  --title "Modulo 8: Notificaciones (emails)" \
  --body "Microservicio que consume eventos de RabbitMQ (UserRegistered, OrderPaid, OrderShipped) y envia emails via Resend. Idempotente (sin emails duplicados), con reintentos, y HTML-encode de datos de usuario. Incluye nuevo estado Shipped en Ordenes (POST /api/orders/{id}/ship, solo Admin).

Pendiente de verificar: tests unitarios/integracion (Notifications, Users, Ordenes) y checklist manual del README. Cerrar con: gh issue close <numero>"
