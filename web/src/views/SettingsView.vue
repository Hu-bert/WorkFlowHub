<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { fetchSettings, type SettingsModule } from '@/api/settings'
import SettingsModuleCard from '@/components/SettingsModuleCard.vue'
import { useCatalog } from '@/stores/catalog'
import { connectionPresentationOf, useConnections } from '@/stores/connections'
import { useNotifications } from '@/stores/notifications'

const catalog = useCatalog()
const connections = useConnections()
const notifications = useNotifications()
const modules = ref<SettingsModule[]>([])
const selectedId = ref<string | null>(null)

const selected = computed(() => modules.value.find((module) => module.id === selectedId.value) ?? modules.value[0] ?? null)

onMounted(async () => {
  try {
    modules.value = await fetchSettings()
    await connections.load()
  } catch (error) {
    notifications.error(error)
  }
})

/** Zapis działa od razu (bez restartu) — odświeżamy też dane słownikowe, np. plan i routing modeli. */
function replace(saved: SettingsModule) {
  modules.value = modules.value.map((module) => (module.id === saved.id ? saved : module))
  catalog.load(true).catch(notifications.error)
  void connections.load()
}
</script>

<template>
  <section class="fh-page settings">
    <header class="fh-page-header">
      <div>
        <h1>Ustawienia</h1>
        <p>Połączenia z Copilotem i systemami firmowymi. Zmiany działają od razu; ustawienia i tokeny zostają na tym komputerze.</p>
      </div>
      <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="connections.loading" @click="connections.load(true)">
        Sprawdź wszystkie połączenia
      </v-btn>
    </header>

    <div class="layout">
      <v-card class="list">
        <v-list nav density="comfortable" color="primary">
          <v-list-item
            v-for="module in modules"
            :key="module.id"
            :active="selected?.id === module.id"
            :prepend-icon="module.icon"
            :title="module.title"
            rounded="lg"
            @click="selectedId = module.id"
          >
            <template v-if="connections.byId.get(module.id)" #append>
              <v-chip size="x-small" variant="tonal" :color="connectionPresentationOf[connections.byId.get(module.id)!.state].color">
                {{ connectionPresentationOf[connections.byId.get(module.id)!.state].label }}
              </v-chip>
            </template>
          </v-list-item>
        </v-list>
      </v-card>

      <SettingsModuleCard
        v-if="selected"
        :key="selected.id"
        :module="selected"
        :status="connections.byId.get(selected.id) ?? null"
        @saved="replace"
        @tested="connections.load()"
      />
    </div>
  </section>
</template>

<style scoped>
.settings {
  max-width: 1200px;
}

/* Lista modułów po lewej, jeden formularz po prawej — karty nie zmieniają rozmiaru i nic się nie „rozjeżdża”. */
.layout {
  display: grid;
  grid-template-columns: 320px minmax(0, 1fr);
  gap: 1.5rem;
  align-items: start;
}

.list {
  padding: 0.5rem;
  position: sticky;
  top: 1rem;
}

@media (max-width: 900px) {
  .layout {
    grid-template-columns: 1fr;
  }

  .list {
    position: static;
  }
}
</style>
