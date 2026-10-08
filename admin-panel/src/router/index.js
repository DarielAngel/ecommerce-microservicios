import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const routes = [
  { path: '/login', name: 'login', component: () => import('../views/LoginView.vue') },
  {
    path: '/',
    component: () => import('../components/AppLayout.vue'),
    meta: { requiresAuth: true },
    children: [
      { path: '', name: 'dashboard', component: () => import('../views/DashboardView.vue') },
      { path: 'products', name: 'products', component: () => import('../views/products/ProductsListView.vue') },
      { path: 'products/new', name: 'product-new', component: () => import('../views/products/ProductFormView.vue') },
      { path: 'products/:id/edit', name: 'product-edit', component: () => import('../views/products/ProductFormView.vue'), props: true },
      { path: 'categories', name: 'categories', component: () => import('../views/categories/CategoriesView.vue') },
      { path: 'inventory', name: 'inventory', component: () => import('../views/inventory/StockView.vue') },
      { path: 'coupons', name: 'coupons', component: () => import('../views/coupons/CouponsView.vue') },
      { path: 'returns', name: 'returns', component: () => import('../views/returns/ReturnsView.vue') },
      { path: 'reviews', name: 'reviews', component: () => import('../views/reviews/ReviewsView.vue') },
      { path: 'orders', name: 'orders', component: () => import('../views/orders/OrdersView.vue') },
      { path: 'admins', name: 'admins', component: () => import('../views/users/CreateAdminView.vue') },
      { path: 'notifications', name: 'notifications', component: () => import('../views/notifications/NotificationsView.vue') }
    ]
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

router.beforeEach((to) => {
  const auth = useAuthStore()

  if (!auth.siteUnlocked && to.name !== 'login') {
    return { name: 'login' }
  }

  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    return { name: 'login' }
  }

  if (to.name === 'login' && auth.isAuthenticated) {
    return { name: 'dashboard' }
  }

  return true
})

export default router
