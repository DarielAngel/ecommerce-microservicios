<script setup>
import { STORE } from '../config'

const year = new Date().getFullYear()
</script>

<template>
  <footer class="mt-12 border-t border-line bg-surface">
    <div class="mx-auto grid max-w-6xl gap-8 px-4 py-10 sm:grid-cols-3">
      <div>
        <p class="flex items-center gap-2 text-lg font-bold text-ink">
          <span class="flex h-8 w-8 items-center justify-center rounded-xl bg-brand-600 text-base text-white" aria-hidden="true">🛍️</span>
          {{ STORE.name }}
        </p>
        <p class="mt-3 text-sm text-ink-muted">{{ STORE.tagline }}</p>
      </div>

      <div class="text-sm">
        <p class="font-semibold text-ink">Comprar</p>
        <ul class="mt-3 space-y-2 text-ink-muted">
          <li><router-link :to="{ name: 'home' }" class="hover:text-brand-ink">Catálogo</router-link></li>
          <li><router-link :to="{ name: 'cart' }" class="hover:text-brand-ink">Mi carrito</router-link></li>
          <li><router-link :to="{ name: 'orders' }" class="hover:text-brand-ink">Mis pedidos</router-link></li>
        </ul>
      </div>

      <div class="text-sm">
        <p class="font-semibold text-ink">Compra tranquilo</p>
        <ul class="mt-3 space-y-2 text-ink-muted">
          <li>Pago seguro con PayPal</li>
          <li>Disponibilidad verificada en tiempo real</li>
          <li>Avisos por correo de tu pedido</li>
        </ul>
      </div>
    </div>

    <div class="border-t border-line py-4 text-center text-xs text-ink-muted">
      © {{ year }} {{ STORE.name }}. Proyecto de demostración — Ecommerce Microservicios.
    </div>
  </footer>
</template>
