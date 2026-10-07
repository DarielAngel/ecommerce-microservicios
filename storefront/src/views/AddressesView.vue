<script setup>
import { ref, onMounted } from 'vue'
import { useApi } from '../api/useApi'
import { addressesApi } from '../api/addresses'
import { useAuthStore } from '../stores/auth'
import { useToastStore } from '../stores/toast'
import { emptyAddress, toForm, isComplete, errorText } from '../utils/addresses'
import AddressForm from '../components/AddressForm.vue'

const MAX_ADDRESSES = 10

const apiClient = useApi()
const auth = useAuthStore()
const toast = useToastStore()

const addresses = ref([])
const loading = ref(true)
const error = ref('')

// Formulario abierto: null (cerrado), 'new' o el id de la dirección que se edita.
const editing = ref(null)
const form = ref(emptyAddress())
const saving = ref(false)
const formError = ref('')

// Borrar pide confirmación en la misma tarjeta (sin diálogos del navegador).
const confirmingDelete = ref(null)
const busyId = ref(null)

async function load() {
  loading.value = true
  error.value = ''
  try {
    addresses.value = await addressesApi.list(apiClient)
  } catch (err) {
    error.value = errorText(err)
  } finally {
    loading.value = false
  }
}

function startNew() {
  editing.value = 'new'
  form.value = emptyAddress(auth.fullName ?? '')
  form.value.makeDefault = addresses.value.length === 0
  formError.value = ''
}

function startEdit(address) {
  editing.value = address.id
  form.value = toForm(address)
  formError.value = ''
}

function cancel() {
  editing.value = null
  formError.value = ''
}

async function save() {
  if (!isComplete(form.value)) {
    formError.value = 'Completa quién recibe, la calle y el número, la ciudad y el país.'
    return
  }
  saving.value = true
  formError.value = ''
  try {
    if (editing.value === 'new') {
      await addressesApi.create(apiClient, form.value)
      toast.push({ type: 'success', message: 'Dirección guardada.' })
    } else {
      await addressesApi.update(apiClient, editing.value, form.value)
      toast.push({ type: 'success', message: 'Dirección actualizada.' })
    }
    editing.value = null
    // Volvemos a pedir la lista: si cambió la predeterminada, el orden y las marcas cambian.
    addresses.value = await addressesApi.list(apiClient)
  } catch (err) {
    formError.value = errorText(err)
  } finally {
    saving.value = false
  }
}

async function makeDefault(address) {
  busyId.value = address.id
  try {
    addresses.value = await addressesApi.setDefault(apiClient, address.id)
    toast.push({ type: 'success', message: `"${address.label}" es ahora tu dirección predeterminada.` })
  } catch (err) {
    toast.push({ type: 'error', message: errorText(err) })
  } finally {
    busyId.value = null
  }
}

async function remove(address) {
  busyId.value = address.id
  try {
    addresses.value = await addressesApi.remove(apiClient, address.id)
    confirmingDelete.value = null
    toast.push({ type: 'success', message: `Borraste "${address.label}".` })
  } catch (err) {
    toast.push({ type: 'error', message: errorText(err) })
  } finally {
    busyId.value = null
  }
}

onMounted(load)
</script>

<template>
  <div class="mx-auto max-w-2xl">
    <div class="mb-6 flex items-center justify-between gap-3">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-ink">Mis direcciones</h1>
        <p class="text-sm text-ink-muted">La predeterminada aparece elegida al pagar.</p>
      </div>
      <button v-if="!loading && !error && editing === null && addresses.length < MAX_ADDRESSES" type="button"
        data-testid="address-new" @click="startNew"
        class="rounded-full bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700">
        Agregar dirección
      </button>
    </div>

    <form v-if="editing !== null" @submit.prevent="save" data-testid="address-form"
      class="mb-6 space-y-4 rounded-xl border border-line bg-surface p-5">
      <h2 class="text-sm font-semibold text-ink">{{ editing === 'new' ? 'Nueva dirección' : 'Editar dirección' }}</h2>
      <AddressForm v-model="form" id-prefix="book" />
      <p v-if="formError" class="text-sm text-red-600 dark:text-red-400" data-testid="address-form-error">{{ formError }}</p>
      <div class="flex justify-end gap-2">
        <button type="button" @click="cancel" class="rounded-full px-4 py-2 text-sm text-ink-soft hover:bg-surface-muted">Cancelar</button>
        <button type="submit" :disabled="saving"
          class="rounded-full bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-60">
          {{ saving ? 'Guardando…' : 'Guardar' }}
        </button>
      </div>
    </form>

    <div v-if="loading" class="py-16 text-center text-ink-muted">Cargando...</div>
    <div v-else-if="error" class="py-16 text-center text-red-600 dark:text-red-400">{{ error }}</div>
    <div v-else-if="addresses.length === 0 && editing === null" class="rounded-xl border border-dashed border-line py-12 text-center"
      data-testid="addresses-empty">
      <p class="text-ink-soft">Todavía no guardaste direcciones.</p>
      <p class="text-sm text-ink-muted">Guárdalas acá o al pagar, y la próxima compra no tendrás que escribirla.</p>
    </div>

    <ul v-else class="space-y-3">
      <li v-for="a in addresses" :key="a.id" data-testid="address-item"
        class="rounded-xl border bg-surface p-4" :class="a.isDefault ? 'border-brand-500' : 'border-line'">
        <div class="flex items-start justify-between gap-3">
          <div class="min-w-0">
            <p class="flex items-center gap-2 text-sm font-semibold text-ink">
              {{ a.label }}
              <span v-if="a.isDefault" data-testid="address-default-badge"
                class="rounded-full bg-brand-soft px-2 py-0.5 text-xs font-medium text-brand-ink">Predeterminada</span>
            </p>
            <p class="mt-1 text-sm text-ink-soft">{{ a.formatted }}</p>
          </div>
        </div>

        <div v-if="confirmingDelete === a.id" class="mt-3 flex flex-wrap items-center gap-2 text-sm">
          <span class="text-ink-soft">¿Borrar esta dirección?</span>
          <button type="button" :disabled="busyId === a.id" @click="remove(a)" data-testid="address-confirm-delete"
            class="rounded-full bg-red-600 px-3 py-1 text-white hover:bg-red-700 disabled:opacity-60">Sí, borrar</button>
          <button type="button" @click="confirmingDelete = null" class="rounded-full px-3 py-1 text-ink-soft hover:bg-surface-muted">No</button>
        </div>
        <div v-else class="mt-3 flex flex-wrap gap-2 text-sm">
          <button type="button" @click="startEdit(a)" data-testid="address-edit"
            class="rounded-full border border-line px-3 py-1 text-ink-soft hover:bg-surface-muted">Editar</button>
          <button v-if="!a.isDefault" type="button" :disabled="busyId === a.id" @click="makeDefault(a)" data-testid="address-make-default"
            class="rounded-full border border-line px-3 py-1 text-ink-soft hover:bg-surface-muted disabled:opacity-60">Usar como predeterminada</button>
          <button type="button" @click="confirmingDelete = a.id" data-testid="address-delete"
            class="rounded-full px-3 py-1 text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-500/10">Borrar</button>
        </div>
      </li>
    </ul>

    <p v-if="!loading && addresses.length >= MAX_ADDRESSES" class="mt-4 text-center text-xs text-ink-muted">
      Llegaste al máximo de {{ MAX_ADDRESSES }} direcciones.
    </p>
  </div>
</template>
