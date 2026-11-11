<script setup lang="ts">
import { onMounted, ref } from 'vue'

import { removeRepository } from '@/api/repositories'
import type { CodeRepository } from '@/api/types'
import LocalRepositories from '@/components/LocalRepositories.vue'
import RepoMapDialog from '@/components/RepoMapDialog.vue'
import RepositoryDialog from '@/components/RepositoryDialog.vue'
import { useCatalog } from '@/stores/catalog'
import { useLocalRepositories } from '@/stores/localRepositories'
import { useNotifications } from '@/stores/notifications'

const catalog = useCatalog()
const local = useLocalRepositories()
const notifications = useNotifications()

const tab = ref<'local' | 'catalog'>('local')
const edited = ref<{ repository: CodeRepository; isNew: boolean } | null>(null)
const mapOf = ref<string | null>(null)

onMounted(() => catalog.load().catch(notifications.error))

const emptyRepository = (): CodeRepository => ({ name: '', url: '', defaultBranch: 'main', ciBuildConfigurations: [], deployProject: null, keywords: [] })

/** Po zmianie katalogu dopasowanie lokalnych kopii i ich statusy mogą być inne — czytamy od nowa. */
async function afterChange() {
  edited.value = null
  await catalog.load(true)
  await local.load(true)
}

async function remove(repository: CodeRepository) {
  if (!window.confirm(`Usunąć ${repository.name} z katalogu repo? Kod na dysku i w TFS zostaje nietknięty.`)) return
  try {
    await removeRepository(repository.name)
    notifications.success(`Usunięto ${repository.name} z katalogu`)
    await afterChange()
  } catch (error) {
    notifications.error(error)
  }
}
</script>

<template>
  <section class="fh-page">
    <header class="fh-page-header">
      <div>
        <h1>Repozytoria</h1>
        <p>Twoje kopie na dysku ze statusami oraz katalog repo, które FlowHub zna.</p>
      </div>
    </header>

    <v-tabs v-model="tab" color="primary">
      <v-tab value="local" prepend-icon="mdi-folder-search-outline">Moje repozytoria (na dysku)</v-tab>
      <v-tab value="catalog" prepend-icon="mdi-source-repository">Katalog repo ({{ catalog.repositories.length }})</v-tab>
    </v-tabs>

    <LocalRepositories v-if="tab === 'local'" />

    <template v-else>
      <v-alert type="info" variant="tonal" density="compact" title="Po co jest katalog?">
        To lista repo, z którymi FlowHub umie pracować: skąd je sklonować do zadania oraz jak nazywają się w TeamCity i Octopus (bez tego nie da się
        znaleźć buildów i wdrożeń). Edytujesz ją tutaj — zapis trafia do pliku <code>repos.yaml</code>, którego nie musisz dotykać ręcznie. Repo z dysku
        albo z TFS dodasz też jednym kliknięciem („Dodaj do katalogu”, „Dodaj repo z TFS” w nowym zadaniu).
      </v-alert>

      <v-card>
        <div class="catalog-head">
          <v-btn color="primary" variant="flat" prepend-icon="mdi-plus" @click="edited = { repository: emptyRepository(), isNew: true }">Dodaj repo</v-btn>
        </div>
        <v-table>
          <thead>
            <tr>
              <th>Repo</th>
              <th>Adres</th>
              <th>Branch</th>
              <th>TeamCity</th>
              <th>Octopus</th>
              <th>Słowa kluczowe</th>
              <th>Akcje</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="repository in catalog.repositories" :key="repository.name">
              <td class="font-weight-bold">{{ repository.name }}</td>
              <td class="fh-muted url">{{ repository.url }}</td>
              <td>{{ repository.defaultBranch }}</td>
              <td>{{ repository.ciBuildConfigurations.join(', ') || '—' }}</td>
              <td>{{ repository.deployProject ?? '= nazwa repo' }}</td>
              <td>
                <v-chip v-for="keyword in repository.keywords" :key="keyword" size="x-small" class="mr-1">{{ keyword }}</v-chip>
              </td>
              <td class="actions">
                <v-btn size="small" variant="tonal" prepend-icon="mdi-pencil-outline" @click="edited = { repository, isNew: false }">Edytuj</v-btn>
                <v-btn size="small" variant="tonal" prepend-icon="mdi-map-outline" @click="mapOf = repository.name">Mapa</v-btn>
                <v-btn size="small" variant="text" icon="mdi-delete-outline" title="Usuń z katalogu" @click="remove(repository)" />
              </td>
            </tr>
            <tr v-if="catalog.repositories.length === 0">
              <td colspan="7" class="text-center fh-muted py-6">Katalog jest pusty. Dodaj repo tutaj albo z zakładki „Moje repozytoria”.</td>
            </tr>
          </tbody>
        </v-table>
      </v-card>
    </template>

    <RepositoryDialog :repository="edited?.repository ?? null" :is-new="edited?.isNew ?? false" @close="edited = null" @saved="afterChange" />
    <RepoMapDialog :repository="mapOf" @close="mapOf = null" />
  </section>
</template>

<style scoped>
.catalog-head {
  display: flex;
  justify-content: flex-end;
  padding: 1rem 1.25rem 0.5rem;
}

.url {
  max-width: 360px;
  word-break: break-all;
  font-size: 0.85rem;
}

.actions {
  white-space: nowrap;
}

.actions .v-btn {
  margin-right: 0.35rem;
}
</style>
