<script setup>
import { ref, reactive, computed, watch, onMounted } from 'vue'
import { api } from '../api/client'
import { useApi } from '../api/useApi'
import { useAuthStore } from '../stores/auth'
import { useToastStore } from '../stores/toast'
import { formatDate } from '../utils/format'
import StarRating from './StarRating.vue'

const props = defineProps({ productId: { type: String, required: true } })
const emit = defineEmits(['summary'])

const PAGE_SIZE = 5
const STARS_DESC = [5, 4, 3, 2, 1]

const auth = useAuthStore()
const toast = useToastStore()
const apiClient = useApi() // llamadas que necesitan el token del cliente

const summary = ref(null)
const reviews = ref([])
const sort = ref('newest')
const page = ref(1)
const totalPages = ref(1)
const loading = ref(true)
const loadError = ref('')

const mine = ref(null)
const editing = ref(false)
const confirmingDelete = ref(false)
const submitting = ref(false)
const formError = ref('')
const form = reactive({ rating: 0, title: '', comment: '' })

const showForm = computed(() => auth.isAuthenticated && (!mine.value || editing.value))
const hasReviews = computed(() => (summary.value?.count ?? 0) > 0)
const reviewsLabel = computed(() => {
  const count = summary.value?.count ?? 0
  return count === 1 ? '1 reseña' : `${count} reseñas`
})

function percentOf(star) {
  const total = summary.value?.count ?? 0
  return total === 0 ? 0 : Math.round(((summary.value.distribution[star] ?? 0) / total) * 100)
}

// ---- Lectura pública ----
async function loadSummary() {
  summary.value = await api.get(`/api/reviews/products/${props.productId}/summary`)
  emit('summary', summary.value)
}

async function loadList({ append = false } = {}) {
  const nextPage = append ? page.value + 1 : 1
  const result = await api.get(`/api/reviews/products/${props.productId}`, {
    params: { sort: sort.value, page: nextPage, pageSize: PAGE_SIZE }
  })
  reviews.value = append ? [...reviews.value, ...result.items] : result.items
  page.value = nextPage
  totalPages.value = result.totalPages ?? 1
}

async function reload() {
  loadError.value = ''
  try {
    await Promise.all([loadSummary(), loadList()])
  } catch {
    loadError.value = 'No pudimos cargar las reseñas en este momento.'
  }
}

async function loadMore() {
  try {
    await loadList({ append: true })
  } catch {
    toast.push({ type: 'error', message: 'No pudimos cargar más reseñas.' })
  }
}

async function loadMine() {
  if (!auth.isAuthenticated) {
    mine.value = null
    return
  }
  try {
    mine.value = await apiClient.get(`/api/reviews/products/${props.productId}/mine`)
  } catch {
    mine.value = null // 404 = todavía no reseñó este producto
  }
}

async function init() {
  loading.value = true
  await Promise.all([reload(), loadMine()])
  loading.value = false
}

// ---- Escribir / editar / eliminar ----
function resetForm() {
  form.rating = 0
  form.title = ''
  form.comment = ''
  formError.value = ''
}

function startEdit() {
  form.rating = mine.value.rating
  form.title = mine.value.title
  form.comment = mine.value.comment
  formError.value = ''
  editing.value = true
}

function cancelEdit() {
  editing.value = false
  resetForm()
}

async function submit() {
  formError.value = ''
  if (!form.rating) {
    formError.value = 'Elige una calificación de 1 a 5 estrellas.'
    return
  }
  if (!form.title.trim()) {
    formError.value = 'Escribe un título para tu reseña.'
    return
  }

  submitting.value = true
  try {
    const body = { rating: form.rating, title: form.title.trim(), comment: form.comment.trim() }

    if (editing.value) {
      mine.value = await apiClient.put(`/api/reviews/${mine.value.id}`, body)
      editing.value = false
      toast.push({ type: 'success', message: 'Tu reseña se actualizó.' })
    } else {
      mine.value = await apiClient.post(`/api/reviews/products/${props.productId}`, body)
      toast.push({ type: 'success', message: '¡Gracias por tu reseña!' })
    }

    resetForm()
    await reload()
  } catch (err) {
    formError.value = err.message || 'No pudimos guardar tu reseña. Intenta de nuevo.'
  } finally {
    submitting.value = false
  }
}

async function removeMine() {
  try {
    await apiClient.delete(`/api/reviews/${mine.value.id}`)
    mine.value = null
    confirmingDelete.value = false
    resetForm()
    toast.push({ type: 'info', message: 'Tu reseña se eliminó.' })
    await reload()
  } catch (err) {
    toast.push({ type: 'error', message: err.message || 'No pudimos eliminar tu reseña.' })
  }
}

watch(sort, () => loadList().catch(() => { loadError.value = 'No pudimos cargar las reseñas en este momento.' }))
watch(() => props.productId, init)
// Si el cliente inicia o cierra sesión con la página abierta, su "reseña propia" cambia.
watch(() => auth.isAuthenticated, loadMine)

onMounted(init)
</script>

<template>
  <section id="resenas" class="scroll-mt-24" aria-labelledby="reviews-heading">
    <h2 id="reviews-heading" class="text-2xl font-bold tracking-tight text-ink">Opiniones de clientes</h2>

    <p v-if="loadError" class="mt-4 rounded-xl border border-line bg-surface p-4 text-sm text-red-600 dark:text-red-400">
      {{ loadError }}
    </p>

    <div class="mt-5 grid gap-8 lg:grid-cols-3">
      <!-- Columna izquierda: resumen + formulario -->
      <div class="space-y-6 lg:col-span-1">
        <div class="rounded-2xl border border-line bg-surface p-5 shadow-card">
          <template v-if="hasReviews">
            <div class="flex items-center gap-4">
              <p class="text-5xl font-bold text-ink">{{ summary.average.toFixed(1) }}</p>
              <div>
                <StarRating :value="summary.average" size="md" />
                <p class="mt-1 text-sm text-ink-muted">{{ reviewsLabel }}</p>
              </div>
            </div>

            <div class="mt-5 space-y-1.5">
              <div v-for="star in STARS_DESC" :key="star" :data-testid="`distribution-${star}`"
                class="flex items-center gap-2 text-sm text-ink-soft">
                <span class="w-8 shrink-0">{{ star }} ★</span>
                <div class="h-2 flex-1 overflow-hidden rounded-full bg-surface-muted">
                  <div class="h-full rounded-full bg-accent-500" :style="{ width: percentOf(star) + '%' }"></div>
                </div>
                <span class="w-6 shrink-0 text-right text-ink-muted">{{ summary.distribution[star] ?? 0 }}</span>
              </div>
            </div>
          </template>
          <div v-else-if="!loading" class="py-4 text-center">
            <p class="text-3xl" aria-hidden="true">💬</p>
            <p class="mt-2 font-medium text-ink">Todavía no hay reseñas</p>
            <p class="text-sm text-ink-muted">Sé el primero en compartir tu opinión.</p>
          </div>
        </div>

        <!-- Invitado -->
        <div v-if="!auth.isAuthenticated" class="rounded-2xl border border-dashed border-line bg-surface p-5 text-center text-sm">
          <p class="text-ink-soft">¿Compraste este producto?</p>
          <router-link :to="{ name: 'login', query: { redirect: `/products/${productId}` } }"
            class="mt-2 inline-block font-semibold text-brand-ink hover:underline">
            Inicia sesión para dejar tu reseña
          </router-link>
        </div>

        <!-- Mi reseña -->
        <div v-else-if="mine && !editing" class="rounded-2xl border border-brand-300 bg-brand-soft p-5">
          <p class="text-sm font-semibold text-brand-ink">Tu reseña</p>
          <div class="mt-2 flex items-center gap-2">
            <StarRating :value="mine.rating" size="sm" />
            <span class="text-sm font-medium text-ink">{{ mine.title }}</span>
          </div>
          <p v-if="mine.comment" class="mt-1 text-sm text-ink-soft">{{ mine.comment }}</p>

          <div v-if="!confirmingDelete" class="mt-4 flex gap-3 text-sm">
            <button type="button" data-testid="edit-review" class="font-medium text-brand-ink hover:underline" @click="startEdit">Editar</button>
            <button type="button" data-testid="delete-review" class="font-medium text-red-600 hover:underline dark:text-red-400"
              @click="confirmingDelete = true">Eliminar</button>
          </div>
          <div v-else class="mt-4 flex flex-wrap items-center gap-3 text-sm">
            <span class="text-ink-soft">¿Seguro que quieres eliminarla?</span>
            <button type="button" data-testid="confirm-delete"
              class="rounded-full bg-red-600 px-3 py-1 font-medium text-white hover:bg-red-700" @click="removeMine">Sí, eliminar</button>
            <button type="button" class="font-medium text-ink-soft hover:underline" @click="confirmingDelete = false">Cancelar</button>
          </div>
        </div>

        <!-- Formulario (nueva reseña o edición) -->
        <form v-if="showForm" data-testid="review-form" novalidate @submit.prevent="submit"
          class="space-y-4 rounded-2xl border border-line bg-surface p-5 shadow-card">
          <h3 class="font-semibold text-ink">{{ editing ? 'Editar tu reseña' : 'Escribe tu reseña' }}</h3>

          <div>
            <p class="mb-1 text-sm font-medium text-ink-soft">Tu calificación</p>
            <StarRating v-model="form.rating" interactive size="lg" />
          </div>

          <div>
            <label for="review-title" class="mb-1 block text-sm font-medium text-ink-soft">Título</label>
            <input id="review-title" v-model="form.title" maxlength="100" placeholder="Resume tu opinión en una frase"
              class="w-full rounded-lg border border-line px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>

          <div>
            <label for="review-comment" class="mb-1 block text-sm font-medium text-ink-soft">Comentario <span class="font-normal text-ink-muted">(opcional)</span></label>
            <textarea id="review-comment" v-model="form.comment" rows="4" maxlength="2000" placeholder="¿Qué te gustó o no te gustó?"
              class="w-full rounded-lg border border-line px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"></textarea>
          </div>

          <p v-if="formError" role="alert" class="text-sm text-red-600 dark:text-red-400">{{ formError }}</p>

          <div class="flex items-center gap-3">
            <button type="submit" :disabled="submitting"
              class="rounded-full bg-brand-600 px-5 py-2 text-sm font-semibold text-white transition-colors hover:bg-brand-700 disabled:opacity-60">
              {{ submitting ? 'Guardando...' : editing ? 'Guardar cambios' : 'Publicar reseña' }}
            </button>
            <button v-if="editing" type="button" data-testid="cancel-edit" class="text-sm font-medium text-ink-soft hover:underline"
              @click="cancelEdit">Cancelar</button>
          </div>
        </form>
      </div>

      <!-- Columna derecha: lista -->
      <div class="lg:col-span-2">
        <div v-if="hasReviews" class="mb-4 flex items-center justify-end gap-2 text-sm">
          <label for="review-sort" class="text-ink-muted">Ordenar por</label>
          <select id="review-sort" v-model="sort" class="rounded-lg border border-line px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-brand-500">
            <option value="newest">Más recientes</option>
            <option value="highest">Mejor calificadas</option>
            <option value="lowest">Peor calificadas</option>
          </select>
        </div>

        <ul class="space-y-4">
          <li v-for="review in reviews" :key="review.id" class="rounded-2xl border border-line bg-surface p-5">
            <div class="flex items-start gap-3">
              <span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-brand-soft text-sm font-bold text-brand-ink" aria-hidden="true">
                {{ review.authorName.charAt(0).toUpperCase() }}
              </span>
              <div class="min-w-0 flex-1">
                <div class="flex flex-wrap items-center gap-x-3 gap-y-1">
                  <span class="text-sm font-semibold text-ink">{{ review.authorName }}</span>
                  <span v-if="review.isVerifiedPurchase" data-testid="verified-badge"
                    class="inline-flex items-center gap-1 rounded-full bg-brand-soft px-2 py-0.5 text-xs font-medium text-brand-ink">
                    <svg viewBox="0 0 20 20" fill="currentColor" class="h-3.5 w-3.5" aria-hidden="true"><path fill-rule="evenodd" d="M16.704 4.153a.75.75 0 0 1 .143 1.052l-8 10.5a.75.75 0 0 1-1.127.075l-4.5-4.5a.75.75 0 0 1 1.06-1.06l3.894 3.893 7.48-9.817a.75.75 0 0 1 1.05-.143Z" clip-rule="evenodd" /></svg>
                    Compra verificada
                  </span>
                  <span class="text-xs text-ink-muted">{{ formatDate(review.createdAtUtc) }}</span>
                </div>
                <div class="mt-1"><StarRating :value="review.rating" size="sm" /></div>
                <p class="mt-2 font-medium text-ink">{{ review.title }}</p>
                <p v-if="review.comment" class="mt-1 whitespace-pre-line text-sm text-ink-soft">{{ review.comment }}</p>
              </div>
            </div>
          </li>
        </ul>

        <div v-if="page < totalPages" class="mt-5 text-center">
          <button type="button" data-testid="load-more"
            class="rounded-full border border-line bg-surface px-5 py-2 text-sm font-medium text-ink-soft transition-colors hover:border-brand-300"
            @click="loadMore">
            Cargar más reseñas
          </button>
        </div>
      </div>
    </div>
  </section>
</template>
