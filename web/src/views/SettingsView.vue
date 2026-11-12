<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { fetchSettings, fetchStorage, type SettingsModule, type StorageLocation } from '@/api/settings'
import SettingsModuleCard from '@/components/SettingsModuleCard.vue'
import { useCatalog } from '@/stores/catalog'
import { connectionPresentationOf, useConnections } from '@/stores/connections'
import { useNotifications } from '@/stores/notifications'

const catalog = useCatalog()
const connections = useConnections()
const notifications = useNotifications()
const modules = ref<SettingsModule[]>([])
const selectedId = ref<string | null>(null)
const storage = ref<StorageLocation[]>([])

const selected = computed(() => modules.value.find((module) => module.id === selectedId.value) ?? modules.value[0] ?? null)

onMounted(async () => {
  try {
    modules.value = await fetchSettings()
    storage.value = await fetchStorage()
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

      <div class="detail">
      <SettingsModuleCard
        v-if="selected"
        :key="selected.id"
        :module="selected"
        :status="connections.byId.get(selected.id) ?? null"
        @saved="replace"
        @tested="connections.load()"
      />

      <v-card class="storage">
        <h3><v-icon icon="mdi-harddisk" size="18" /> Gdzie są zapisane dane</h3>
        <p class="fh-muted">
          Ustawienia, tokeny, katalog repo i mapy repo leżą poza repozytorium FlowHub — aktualizacja aplikacji (<code>git pull</code>) ich nie rusza.
        </p>
        <dl>
          <template v-for="location in storage" :key="location.name">
            <dt :title="location.description">{{ location.name }}</dt>
            <dd><code>{{ location.path }}</code></dd>
          </template>
        </dl>
      </v-card>
      </div>
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

.detail {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  min-width: 0;
}

.storage {
  padding: 1rem 1.25rem;
  font-size: 0.85rem;
}

.storage h3 {
  margin: 0 0 0.25rem;
  font-size: 0.95rem;
}

.storage p {
  margin: 0 0 0.75rem;
}

.storage dl {
  display: grid;
  grid-template-columns: max-content 1fr;
  gap: 0.3rem 1.25rem;
  margin: 0;
}

.storage dt {
  color: var(--fh-muted);
}

.storage dd {
  margin: 0;
  word-break: break-all;
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
