<script setup>
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const router = useRouter()

const siteKey = ref('')
const siteKeyError = ref('')

const email = ref('')
const password = ref('')

const showLoginForm = computed(() => auth.siteUnlocked)

function submitSiteKey() {
  siteKeyError.value = ''
  if (!auth.unlockSite(siteKey.value)) {
    siteKeyError.value = 'Clave incorrecta.'
  }
}

async function submitLogin() {
  const ok = await auth.login(email.value, password.value)
  if (ok) router.push({ name: 'dashboard' })
}
</script>

<template>
  <div class="min-h-screen flex items-center justify-center bg-gray-100 px-4">
    <div class="w-full max-w-sm bg-white rounded-xl shadow-sm border border-gray-200 p-8">
      <h1 class="text-xl font-semibold text-gray-900 mb-1">Panel de Administración</h1>
      <p class="text-sm text-gray-500 mb-6">Ecommerce Microservicios</p>

      <form v-if="!showLoginForm" @submit.prevent="submitSiteKey" class="space-y-4">
        <p class="text-sm text-gray-600">Ingresa la clave de acceso al panel.</p>
        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">Clave de acceso</label>
          <input v-model="siteKey" type="password" required autofocus
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500" />
        </div>
        <p v-if="siteKeyError" class="text-sm text-red-600">{{ siteKeyError }}</p>
        <button type="submit"
          class="w-full bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium rounded-lg py-2 transition-colors">
          Continuar
        </button>
      </form>

      <form v-else @submit.prevent="submitLogin" class="space-y-4">
        <p class="text-sm text-gray-600">Inicia sesión con tu cuenta de Administrador.</p>
        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">Email</label>
          <input v-model="email" type="email" required autofocus
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500" />
        </div>
        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">Contraseña</label>
          <input v-model="password" type="password" required
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500" />
        </div>
        <p v-if="auth.error" class="text-sm text-red-600">{{ auth.error }}</p>
        <button type="submit" :disabled="auth.loading"
          class="w-full bg-brand-600 hover:bg-brand-700 disabled:opacity-60 text-white text-sm font-medium rounded-lg py-2 transition-colors">
          {{ auth.loading ? 'Ingresando...' : 'Iniciar sesión' }}
        </button>
      </form>
    </div>
  </div>
</template>
