<script setup>
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useApi } from '../../api/useApi'
import { useAuthStore } from '../../stores/auth'

const props = defineProps({ id: { type: String, default: null } })
const isEdit = computed(() => !!props.id)

const apiClient = useApi()
const router = useRouter()
const auth = useAuthStore()

const categories = ref([])
const name = ref('')
const description = ref('')
const categoryId = ref('')
const isActive = ref(true)

// Solo aplica al crear: lista de variantes, cada una con SKU/precio/atributos libres.
const variants = ref([{ sku: '', price: '', attributes: [{ key: '', value: '' }] }])
const existingVariants = ref([])
const existingImages = ref([])

const saving = ref(false)
const error = ref('')
const loading = ref(isEdit.value)

const imageFile = ref(null)
const isPrimaryImage = ref(false)
const uploadingImage = ref(false)
const uploadError = ref('')

function addVariant() {
  variants.value.push({ sku: '', price: '', attributes: [{ key: '', value: '' }] })
}
function removeVariant(index) {
  variants.value.splice(index, 1)
}
function addAttribute(variant) {
  variant.attributes.push({ key: '', value: '' })
}
function removeAttribute(variant, index) {
  variant.attributes.splice(index, 1)
}

async function loadCategories() {
  categories.value = await apiClient.get('/api/categories')
}

async function loadProduct() {
  const product = await apiClient.get(`/api/products/${props.id}`)
  name.value = product.name
  description.value = product.description
  categoryId.value = product.categoryId
  isActive.value = product.isActive
  existingVariants.value = product.variants
  existingImages.value = product.images
}

onMounted(async () => {
  try {
    await loadCategories()
    if (isEdit.value) await loadProduct()
  } catch (err) {
    error.value = err.message
  } finally {
    loading.value = false
  }
})

function buildVariantPayload() {
  return variants.value
    .filter(v => v.sku && v.price)
    .map(v => ({
      sku: v.sku,
      price: Number(v.price),
      attributes: Object.fromEntries(
        v.attributes.filter(a => a.key).map(a => [a.key, a.value])
      )
    }))
}

async function submit() {
  error.value = ''
  saving.value = true
  try {
    if (isEdit.value) {
      await apiClient.put(`/api/products/${props.id}`, {
        name: name.value,
        description: description.value,
        categoryId: categoryId.value,
        isActive: isActive.value
      })
    } else {
      const payload = buildVariantPayload()
      if (payload.length === 0) {
        error.value = 'Agrega al menos una variante con SKU y precio.'
        saving.value = false
        return
      }
      const created = await apiClient.post('/api/products', {
        name: name.value,
        description: description.value,
        categoryId: categoryId.value,
        variants: payload
      })
      router.push({ name: 'product-edit', params: { id: created.id } })
      return
    }
    router.push({ name: 'products' })
  } catch (err) {
    error.value = err.message
  } finally {
    saving.value = false
  }
}

function onFileChange(event) {
  imageFile.value = event.target.files[0] ?? null
}

async function uploadImage() {
  if (!imageFile.value) return
  uploadError.value = ''
  uploadingImage.value = true
  try {
    const form = new FormData()
    form.append('file', imageFile.value)
    form.append('isPrimary', isPrimaryImage.value)

    const base = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000'
    const response = await fetch(`${base}/api/products/${props.id}/images`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${auth.accessToken}` },
      body: form
    })
    if (!response.ok) {
      const body = await response.json().catch(() => null)
      throw new Error(body?.message || `Error ${response.status} al subir la imagen`)
    }
    const image = await response.json()
    existingImages.value.push(image)
    imageFile.value = null
    isPrimaryImage.value = false
  } catch (err) {
    uploadError.value = err.message
  } finally {
    uploadingImage.value = false
  }
}
</script>

<template>
  <div class="max-w-2xl">
    <h1 class="text-2xl font-semibold text-gray-900 mb-6">
      {{ isEdit ? 'Editar producto' : 'Nuevo producto' }}
    </h1>

    <div v-if="loading" class="text-sm text-gray-500">Cargando...</div>

    <form v-else @submit.prevent="submit" class="space-y-5">
      <div class="bg-white border border-gray-200 rounded-xl p-5 space-y-4">
        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">Nombre</label>
          <input v-model="name" required
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500" />
        </div>
        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">Descripción</label>
          <textarea v-model="description" required rows="3"
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"></textarea>
        </div>
        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">Categoría</label>
          <select v-model="categoryId" required
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500">
            <option value="" disabled>Selecciona una categoría</option>
            <option v-for="c in categories" :key="c.id" :value="c.id">{{ c.name }}</option>
          </select>
        </div>
        <label v-if="isEdit" class="flex items-center gap-2 text-sm text-gray-700">
          <input v-model="isActive" type="checkbox" class="rounded border-gray-300" />
          Producto activo (visible en el catálogo)
        </label>
      </div>

      <!-- Variantes: solo editables al crear -->
      <div v-if="!isEdit" class="bg-white border border-gray-200 rounded-xl p-5">
        <h2 class="text-sm font-medium text-gray-900 mb-3">Variantes</h2>
        <div v-for="(v, i) in variants" :key="i" class="border border-gray-100 rounded-lg p-3 mb-3">
          <div class="flex gap-3 mb-2">
            <input v-model="v.sku" placeholder="SKU" required
              class="flex-1 rounded-lg border border-gray-300 px-3 py-1.5 text-sm" />
            <input v-model="v.price" type="number" step="0.01" min="0" placeholder="Precio" required
              class="w-32 rounded-lg border border-gray-300 px-3 py-1.5 text-sm" />
            <button v-if="variants.length > 1" type="button" @click="removeVariant(i)"
              class="text-red-500 text-sm px-2">✕</button>
          </div>
          <p class="text-xs text-gray-500 mb-1">Atributos (ej. Talla, Color)</p>
          <div v-for="(a, ai) in v.attributes" :key="ai" class="flex gap-2 mb-1">
            <input v-model="a.key" placeholder="Nombre" class="flex-1 rounded-lg border border-gray-300 px-2 py-1 text-xs" />
            <input v-model="a.value" placeholder="Valor" class="flex-1 rounded-lg border border-gray-300 px-2 py-1 text-xs" />
            <button type="button" @click="removeAttribute(v, ai)" class="text-red-500 text-xs px-1">✕</button>
          </div>
          <button type="button" @click="addAttribute(v)" class="text-xs text-brand-600 mt-1">+ atributo</button>
        </div>
        <button type="button" @click="addVariant" class="text-sm text-brand-600 font-medium">+ Agregar variante</button>
      </div>

      <!-- Variantes existentes: solo lectura al editar -->
      <div v-if="isEdit" class="bg-white border border-gray-200 rounded-xl p-5">
        <h2 class="text-sm font-medium text-gray-900 mb-3">Variantes</h2>
        <table class="w-full text-sm">
          <thead class="text-gray-500 text-xs uppercase">
            <tr><th class="text-left py-1">SKU</th><th class="text-left py-1">Precio</th><th class="text-left py-1">Atributos</th></tr>
          </thead>
          <tbody class="divide-y divide-gray-100">
            <tr v-for="v in existingVariants" :key="v.id">
              <td class="py-1.5">{{ v.sku }}</td>
              <td class="py-1.5">${{ v.price.toFixed(2) }}</td>
              <td class="py-1.5 text-gray-500">
                {{ Object.entries(v.attributes).map(([k, val]) => `${k}: ${val}`).join(', ') || '—' }}
              </td>
            </tr>
          </tbody>
        </table>
        <p class="text-xs text-gray-400 mt-2">Las variantes no se pueden editar ni agregar después de crear el producto.</p>
      </div>

      <!-- Imágenes: solo al editar (necesita el Id ya creado) -->
      <div v-if="isEdit" class="bg-white border border-gray-200 rounded-xl p-5">
        <h2 class="text-sm font-medium text-gray-900 mb-3">Imágenes</h2>
        <div class="flex flex-wrap gap-2 mb-3">
          <span v-for="img in existingImages" :key="img.id"
            class="text-xs px-2 py-1 rounded bg-gray-100 text-gray-600">
            {{ img.fileName }} <span v-if="img.isPrimary" class="text-brand-600 font-medium">(principal)</span>
          </span>
          <span v-if="existingImages.length === 0" class="text-xs text-gray-400">Sin imágenes todavía.</span>
        </div>
        <div class="flex items-center gap-3">
          <input type="file" accept="image/*" @change="onFileChange" class="text-sm" />
          <label class="flex items-center gap-1.5 text-xs text-gray-600">
            <input v-model="isPrimaryImage" type="checkbox" class="rounded border-gray-300" /> Principal
          </label>
          <button type="button" @click="uploadImage" :disabled="!imageFile || uploadingImage"
            class="text-sm bg-gray-800 hover:bg-gray-900 disabled:opacity-50 text-white rounded-lg px-3 py-1.5">
            {{ uploadingImage ? 'Subiendo...' : 'Subir imagen' }}
          </button>
        </div>
        <p v-if="uploadError" class="text-sm text-red-600 mt-2">{{ uploadError }}</p>
      </div>

      <p v-if="error" class="text-sm text-red-600">{{ error }}</p>

      <div class="flex gap-3">
        <button type="submit" :disabled="saving"
          class="bg-brand-600 hover:bg-brand-700 disabled:opacity-60 text-white text-sm font-medium rounded-lg px-5 py-2">
          {{ saving ? 'Guardando...' : 'Guardar' }}
        </button>
        <router-link :to="{ name: 'products' }" class="text-sm text-gray-600 hover:text-gray-900 px-3 py-2">
          Cancelar
        </router-link>
      </div>
    </form>
  </div>
</template>
