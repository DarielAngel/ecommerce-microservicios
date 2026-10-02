<script setup>
import { ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

const email = ref('')
const password = ref('')

async function submit() {
  const ok = await auth.login(email.value, password.value)
  if (ok) router.push(route.query.redirect || { name: 'home' })
}
</script>

<template>
  <div class="max-w-sm mx-auto mt-8">
    <h1 class="text-xl font-semibold text-gray-900 mb-6">Iniciar sesión</h1>
    <form @submit.prevent="submit" class="bg-white border border-gray-200 rounded-xl p-6 space-y-4">
      <div>
        <label for="login-email" class="block text-sm font-medium text-gray-700 mb-1">Email</label>
        <input id="login-email" v-model="email" type="email" required autofocus
          class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500" />
      </div>
      <div>
        <label for="login-password" class="block text-sm font-medium text-gray-700 mb-1">Contraseña</label>
        <input id="login-password" v-model="password" type="password" required
          class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500" />
      </div>
      <p v-if="auth.error" class="text-sm text-red-600">{{ auth.error }}</p>
      <button type="submit" :disabled="auth.loading"
        class="w-full bg-brand-600 hover:bg-brand-700 disabled:opacity-60 text-white text-sm font-medium rounded-lg py-2.5">
        {{ auth.loading ? 'Ingresando...' : 'Iniciar sesión' }}
      </button>
      <p class="text-sm text-gray-500 text-center">
        ¿No tienes cuenta? <router-link :to="{ name: 'register' }" class="text-brand-600 font-medium">Regístrate</router-link>
      </p>
    </form>
  </div>
</template>
