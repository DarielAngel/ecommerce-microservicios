import '@fontsource-variable/inter'
import './style.css'
import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { STORE } from './config'
import { useThemeStore } from './stores/theme'
import { usePwaStore } from './stores/pwa'

const app = createApp(App)
const pinia = createPinia()
app.use(pinia)
app.use(router)

useThemeStore(pinia).init()
usePwaStore(pinia).init()
document.title = STORE.name

app.mount('#app')
