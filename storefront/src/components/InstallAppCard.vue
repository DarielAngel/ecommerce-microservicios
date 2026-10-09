<script setup>
// Invitación a instalar la tienda como app (PWA). Chrome, Edge y Android: botón que abre el diálogo del
// navegador. iPhone/iPad (Safari): no hay botón posible, así que explicamos los dos toques.
import { usePwaStore } from '../stores/pwa'
import { useToastStore } from '../stores/toast'
import { STORE } from '../config'

const pwa = usePwaStore()
const toast = useToastStore()

async function install() {
  if (await pwa.install()) toast.push({ type: 'success', message: `¡Listo! ${STORE.name} ya está entre tus apps.` })
}
</script>

<template>
  <div v-if="pwa.showInstall" class="mt-4 rounded-xl border border-line bg-canvas p-3 text-sm" data-testid="install-card">
    <p class="font-medium text-ink">📲 Lleva la tienda en tu teléfono</p>
    <template v-if="pwa.canInstall">
      <p class="mt-1 text-ink-muted">Instálala como app: abre más rápido, con su propio ícono, y funciona aunque se corte la conexión.</p>
      <div class="mt-2 flex flex-wrap gap-2">
        <button type="button" @click="install" data-testid="install-app"
          class="rounded-full bg-brand-600 px-3 py-1.5 font-medium text-white hover:bg-brand-700">Instalar la app</button>
        <button type="button" @click="pwa.dismissInstall()" class="px-2 py-1.5 text-ink-soft hover:text-ink">No, gracias</button>
      </div>
    </template>
    <template v-else>
      <p class="mt-1 text-ink-muted" data-testid="install-ios">
        En Safari toca <b>Compartir</b> (el cuadrado con la flecha) y luego <b>“Agregar a inicio”</b>.
      </p>
      <button type="button" @click="pwa.dismissInstall()" class="mt-2 px-0 py-1 text-ink-soft hover:text-ink">Entendido</button>
    </template>
  </div>
</template>
