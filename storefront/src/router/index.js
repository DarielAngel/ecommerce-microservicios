import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const routes = [
  {
    path: '/',
    component: () => import('../components/AppLayout.vue'),
    children: [
      { path: '', name: 'home', component: () => import('../views/HomeView.vue') },
      { path: 'products/:id', name: 'product-detail', component: () => import('../views/ProductDetailView.vue'), props: true },
      { path: 'login', name: 'login', component: () => import('../views/LoginView.vue') },
      { path: 'register', name: 'register', component: () => import('../views/RegisterView.vue') },
      { path: 'cart', name: 'cart', component: () => import('../views/CartView.vue'), meta: { requiresAuth: true } },
      { path: 'checkout', name: 'checkout', component: () => import('../views/CheckoutView.vue'), meta: { requiresAuth: true } },
      { path: 'orders/:id/pending', name: 'order-pending', component: () => import('../views/OrderPendingView.vue'), props: true, meta: { requiresAuth: true } },
      { path: 'favorites', name: 'wishlist', component: () => import('../views/WishlistView.vue'), meta: { requiresAuth: true } },
      { path: 'orders', name: 'orders', component: () => import('../views/OrdersHistoryView.vue'), meta: { requiresAuth: true } }
    ]
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

router.beforeEach((to) => {
  const auth = useAuthStore()

  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }

  return true
})

export default router
