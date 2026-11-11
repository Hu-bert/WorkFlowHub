<script setup lang="ts">
import { onMounted, ref } from 'vue'

import { fetchSettings, type SettingsModule } from '@/api/settings'
import SettingsModuleCard from '@/components/SettingsModuleCard.vue'
import { useCatalog } from '@/stores/catalog'
import { useNotifications } from '@/stores/notifications'

const catalog = useCatalog()
const notifications = useNotifications()
const modules = ref<SettingsModule[]>([])

onMounted(async () => {
  try {
    modules.value = await fetchSettings()
  } catch (error) {
    notifications.error(error)
  }
})

/** Zapis działa od razu (bez restartu) — odświeżamy też dane słownikowe, np. plan i routing modeli. */
function replace(saved: SettingsModule) {
  modules.value = modules.value.map((module) => (module.id === saved.id ? saved : module))
  catalog.load(true).catch(notifications.error)
}
</script>

<template>
  <section class="fh-page">
    <header class="fh-page-header">
      <div>
        <h1>Ustawienia</h1>
        <p>
          Połączenia z Copilotem i systemami firmowymi. Zmiany działają od razu. Ustawienia i tokeny są zapisywane na tym
          komputerze, poza repozytorium.
        </p>
      </div>
    </header>

    <div class="modules">
      <SettingsModuleCard v-for="module in modules" :key="module.id" :module="module" @saved="replace" />
    </div>
  </section>
</template>

<style scoped>
.modules {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(420px, 1fr));
  gap: 1.5rem;
  align-items: stretch;
}
</style>
