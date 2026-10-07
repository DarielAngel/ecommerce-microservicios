<script setup>
import { ref, computed, onMounted } from 'vue'
import { useApi } from '../../api/useApi'
import { emptyCouponForm, toPayload, fromCoupon, describeDiscount, couponStatus, usageLabel } from '../../utils/coupons'

const apiClient = useApi()

const coupons = ref([])
const loading = ref(true)
const error = ref('')

const form = ref(emptyCouponForm())
const editingId = ref(null) // null = creando uno nuevo
const saving = ref(false)
const formError = ref('')
const success = ref('')

const isEditing = computed(() => editingId.value !== null)

const statusStyles = {
  active: 'bg-green-100 text-green-700',
  scheduled: 'bg-blue-100 text-blue-700',
  expired: 'bg-gray-100 text-gray-500',
  exhausted: 'bg-amber-100 text-amber-700',
  inactive: 'bg-gray-100 text-gray-500'
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    coupons.value = await apiClient.get('/api/coupons')
  } catch (err) {
    error.value = err.message
  } finally {
    loading.value = false
  }
}

function startEdit(coupon) {
  editingId.value = coupon.id
  form.value = fromCoupon(coupon)
  formError.value = ''
  success.value = ''
}

function resetForm() {
  editingId.value = null
  form.value = emptyCouponForm()
  formError.value = ''
}

async function save() {
  formError.value = ''
  success.value = ''
  saving.value = true
  try {
    const payload = toPayload(form.value)
    if (isEditing.value) {
      await apiClient.put(`/api/coupons/${editingId.value}`, payload)
      success.value = `Cupón ${payload.code} actualizado.`
    } else {
      await apiClient.post('/api/coupons', payload)
      success.value = `Cupón ${payload.code} creado.`
    }
    resetForm()
    await load()
  } catch (err) {
    formError.value = err.message
  } finally {
    saving.value = false
  }
}

async function toggleActive(coupon) {
  try {
    await apiClient.put(`/api/coupons/${coupon.id}`, toPayload({ ...fromCoupon(coupon), isActive: !coupon.isActive }))
    await load()
  } catch (err) {
    error.value = err.message
  }
}

const fmtDate = (iso) => (iso ? new Date(iso).toLocaleString('es', { dateStyle: 'short', timeStyle: 'short' }) : '—')

onMounted(load)
</script>

<template>
  <div>
    <h1 class="text-2xl font-semibold text-gray-900 mb-6">Cupones</h1>

    <div class="bg-white border border-gray-200 rounded-xl p-5 mb-6">
      <h2 class="text-sm font-medium text-gray-900 mb-4">
        {{ isEditing ? `Editar cupón ${form.code}` : 'Nuevo cupón' }}
      </h2>
      <form @submit.prevent="save" class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div>
          <label for="coupon-code" class="block text-xs font-medium text-gray-500 mb-1">Código</label>
          <input id="coupon-code" v-model="form.code" required :disabled="isEditing" maxlength="30" placeholder="VERANO25"
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm uppercase disabled:bg-gray-100 focus:outline-none focus:ring-2 focus:ring-brand-500" />
        </div>
        <div class="md:col-span-3">
          <label for="coupon-description" class="block text-xs font-medium text-gray-500 mb-1">Descripción (la ve el cliente)</label>
          <input id="coupon-description" v-model="form.description" required maxlength="200" placeholder="25 % de descuento en toda la tienda"
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500" />
        </div>

        <div>
          <label for="coupon-type" class="block text-xs font-medium text-gray-500 mb-1">Tipo</label>
          <select id="coupon-type" v-model="form.type"
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500">
            <option value="Percentage">Porcentaje (%)</option>
            <option value="FixedAmount">Monto fijo ($)</option>
          </select>
        </div>
        <div>
          <label for="coupon-value" class="block text-xs font-medium text-gray-500 mb-1">
            {{ form.type === 'Percentage' ? 'Porcentaje (1–90)' : 'Monto ($)' }}
          </label>
          <input id="coupon-value" v-model="form.value" type="number" step="0.01" min="0.01" required
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500" />
        </div>
        <div v-if="form.type === 'Percentage'">
          <label for="coupon-max" class="block text-xs font-medium text-gray-500 mb-1">Tope de descuento ($, opcional)</label>
          <input id="coupon-max" v-model="form.maxDiscountAmount" type="number" step="0.01" min="0.01"
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500" />
        </div>
        <div>
          <label for="coupon-minimum" class="block text-xs font-medium text-gray-500 mb-1">Compra mínima ($, opcional)</label>
          <input id="coupon-minimum" v-model="form.minimumSubtotal" type="number" step="0.01" min="0"
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500" />
        </div>

        <div>
          <label for="coupon-starts" class="block text-xs font-medium text-gray-500 mb-1">Empieza (opcional)</label>
          <input id="coupon-starts" v-model="form.startsAt" type="datetime-local"
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500" />
        </div>
        <div>
          <label for="coupon-ends" class="block text-xs font-medium text-gray-500 mb-1">Termina (opcional)</label>
          <input id="coupon-ends" v-model="form.endsAt" type="datetime-local"
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500" />
        </div>
        <div>
          <label for="coupon-limit" class="block text-xs font-medium text-gray-500 mb-1">Usos totales (vacío = ilimitado)</label>
          <input id="coupon-limit" v-model="form.usageLimit" type="number" min="1" step="1"
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500" />
        </div>
        <div class="flex flex-col justify-end gap-2 text-sm text-gray-700">
          <label class="inline-flex items-center gap-2">
            <input v-model="form.oncePerCustomer" type="checkbox" class="rounded border-gray-300" /> Un uso por cliente
          </label>
          <label class="inline-flex items-center gap-2">
            <input v-model="form.isActive" type="checkbox" class="rounded border-gray-300" /> Activo
          </label>
        </div>

        <div class="md:col-span-4 flex items-center gap-3">
          <button type="submit" :disabled="saving"
            class="bg-brand-600 hover:bg-brand-700 disabled:opacity-60 text-white text-sm font-medium rounded-lg px-4 py-2">
            {{ saving ? 'Guardando...' : isEditing ? 'Guardar cambios' : 'Crear cupón' }}
          </button>
          <button v-if="isEditing" type="button" @click="resetForm" class="text-sm text-gray-600 hover:text-gray-900">Cancelar</button>
          <p v-if="formError" role="alert" class="text-sm text-red-600">{{ formError }}</p>
          <p v-if="success" class="text-sm text-green-700">{{ success }}</p>
        </div>
      </form>
    </div>

    <div class="bg-white border border-gray-200 rounded-xl overflow-x-auto">
      <div v-if="loading" class="p-6 text-sm text-gray-500">Cargando...</div>
      <div v-else-if="error" class="p-6 text-sm text-red-600">{{ error }}</div>
      <div v-else-if="coupons.length === 0" class="p-6 text-sm text-gray-500">Todavía no hay cupones.</div>
      <table v-else class="w-full text-sm">
        <thead class="bg-gray-50 text-gray-500 text-xs uppercase">
          <tr>
            <th class="text-left px-4 py-2 font-medium">Código</th>
            <th class="text-left px-4 py-2 font-medium">Descuento</th>
            <th class="text-left px-4 py-2 font-medium">Mínimo</th>
            <th class="text-left px-4 py-2 font-medium">Vigencia</th>
            <th class="text-left px-4 py-2 font-medium">Usos</th>
            <th class="text-left px-4 py-2 font-medium">Estado</th>
            <th class="px-4 py-2"></th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-100">
          <tr v-for="c in coupons" :key="c.id" data-testid="coupon-row">
            <td class="px-4 py-2.5">
              <p class="font-mono font-semibold text-gray-900">{{ c.code }}</p>
              <p class="text-xs text-gray-500 max-w-xs truncate">{{ c.description }}</p>
            </td>
            <td class="px-4 py-2.5 text-gray-900">{{ describeDiscount(c) }}</td>
            <td class="px-4 py-2.5 text-gray-500">{{ c.minimumSubtotal > 0 ? `$${c.minimumSubtotal.toFixed(2)}` : '—' }}</td>
            <td class="px-4 py-2.5 text-xs text-gray-500">{{ fmtDate(c.startsAtUtc) }} → {{ fmtDate(c.endsAtUtc) }}</td>
            <td class="px-4 py-2.5 text-gray-700">
              {{ usageLabel(c) }}
              <span v-if="c.oncePerCustomer" class="block text-xs text-gray-400">1 por cliente</span>
            </td>
            <td class="px-4 py-2.5">
              <span class="text-xs px-2 py-0.5 rounded-full" :class="statusStyles[couponStatus(c).key]">{{ couponStatus(c).label }}</span>
            </td>
            <td class="px-4 py-2.5 text-right whitespace-nowrap">
              <button type="button" @click="startEdit(c)" class="text-xs font-medium text-brand-700 hover:underline mr-3">Editar</button>
              <button type="button" @click="toggleActive(c)" class="text-xs font-medium text-gray-600 hover:underline">
                {{ c.isActive ? 'Pausar' : 'Reactivar' }}
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
