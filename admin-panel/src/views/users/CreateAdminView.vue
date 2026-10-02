<script setup>
import { ref } from 'vue'

const email = ref('')
const password = ref('')
const fullName = ref('')
const provisioningKey = ref('')

const submitting = ref(false)
const error = ref('')
const success = ref('')

async function submit() {
  error.value = ''
  success.value = ''
  submitting.value = true
  try {
    const baseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000'
    const response = await fetch(`${baseUrl}/api/admins`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-Admin-Provisioning-Key': provisioningKey.value },
      body: JSON.stringify({ email: email.value, password: password.value, fullName: fullName.value })
    })

    const data = await response.json().catch(() => null)
    if (!response.ok) throw new Error(data?.message || `Error ${response.status}`)

    success.value = `Admin "${data.fullName}" creado correctamente.`
    email.value = ''
    password.value = ''
    fullName.value = ''
  } catch (err) {
    error.value = err.message
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="max-w-md">
    <h1 class="text-2xl font-semibold text-gray-900 mb-2">Nuevo administrador</h1>
    <p class="text-sm text-gray-500 mb-6">
      Necesitas la <b>API key de aprovisionamiento</b> del servidor (variable <code class="text-xs bg-gray-100 px-1 rounded">ADMIN_PROVISIONING_KEY</code>) — no se guarda en el panel, hay que escribirla cada vez.
    </p>

    <form @submit.prevent="submit" class="bg-white border border-gray-200 rounded-xl p-5 space-y-4">
      <div>
        <label for="admin-fullname" class="block text-sm font-medium text-gray-700 mb-1">Nombre completo</label>
        <input id="admin-fullname" v-model="fullName" required class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm" />
      </div>
      <div>
        <label for="admin-email" class="block text-sm font-medium text-gray-700 mb-1">Email</label>
        <input id="admin-email" v-model="email" type="email" required class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm" />
      </div>
      <div>
        <label for="admin-password" class="block text-sm font-medium text-gray-700 mb-1">Contraseña</label>
        <input id="admin-password" v-model="password" type="password" required class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm" />
      </div>
      <div>
        <label for="admin-provisioning-key" class="block text-sm font-medium text-gray-700 mb-1">API key de aprovisionamiento</label>
        <input id="admin-provisioning-key" v-model="provisioningKey" type="password" required
          class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm font-mono" />
      </div>

      <p v-if="error" class="text-sm text-red-600">{{ error }}</p>
      <p v-if="success" class="text-sm text-green-600">{{ success }}</p>

      <button type="submit" :disabled="submitting"
        class="w-full bg-brand-600 hover:bg-brand-700 disabled:opacity-60 text-white text-sm font-medium rounded-lg py-2">
        {{ submitting ? 'Creando...' : 'Crear administrador' }}
      </button>
    </form>
  </div>
</template>
