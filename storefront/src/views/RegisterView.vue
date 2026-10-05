<script setup>
import { ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

const fullName = ref('')
const email = ref('')
const password = ref('')

async function submit() {
  const ok = await auth.register(email.value, password.value, fullName.value)
  if (ok) router.push(route.query.redirect || { name: 'home' })
}
</script>

<template>
  <div class="max-w-sm mx-auto mt-8">
    <h1 class="text-xl font-semibold text-ink mb-6">Crear cuenta</h1>
    <form @submit.prevent="submit" class="bg-surface border border-line rounded-xl p-6 space-y-4">
      <div>
        <label for="register-fullname" class="block text-sm font-medium text-ink-soft mb-1">Nombre completo</label>
        <input id="register-fullname" v-model="fullName" required autofocus
          class="w-full rounded-lg border border-line px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500" />
      </div>
      <div>
        <label for="register-email" class="block text-sm font-medium text-ink-soft mb-1">Email</label>
        <input id="register-email" v-model="email" type="email" required
          class="w-full rounded-lg border border-line px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500" />
      </div>
      <div>
        <label for="register-password" class="block text-sm font-medium text-ink-soft mb-1">Contraseña</label>
        <input id="register-password" v-model="password" type="password" required minlength="8"
          class="w-full rounded-lg border border-line px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500" />
      </div>
      <p v-if="auth.error" class="text-sm text-red-600 dark:text-red-400">{{ auth.error }}</p>
      <button type="submit" :disabled="auth.loading"
        class="w-full bg-brand-600 hover:bg-brand-700 disabled:opacity-60 text-white text-sm font-medium rounded-lg py-2.5">
        {{ auth.loading ? 'Creando...' : 'Crear cuenta' }}
      </button>
      <p class="text-sm text-ink-muted text-center">
        ¿Ya tienes cuenta? <router-link :to="{ name: 'login' }" class="text-brand-ink font-medium">Inicia sesión</router-link>
      </p>
    </form>
  </div>
</template>
