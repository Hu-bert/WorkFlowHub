import { createPinia } from 'pinia'
import PrimeVue from 'primevue/config'
import { createApp } from 'vue'

import 'primeicons/primeicons.css'
import '@/styles/main.css'

import App from './App.vue'
import { router } from './router'
import { flowHubTheme } from './theme/flowHubTheme'

createApp(App).use(createPinia()).use(router).use(PrimeVue, { theme: flowHubTheme }).mount('#app')
