<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import {
  addLocalRepository,
  fetchBranchStatus,
  fetchLocalRepositories,
  type BranchStatus,
  type LocalRepository,
  type LocalRepositoryScan,
} from '@/api/repositories'
import { formatDateTime } from '@/shared/format'
import { useCatalog } from '@/stores/catalog'
import { useNotifications } from '@/stores/notifications'

import HealthCell from './HealthCell.vue'

/**
 * Lokalne kopie repozytoriów z folderu ustawionego w Ustawieniach (np. C:\tfs):
 * branch, ostatnie commity oraz — dla repo z katalogu — PR, build i wdrożenia tego brancha.
 */
const catalog = useCatalog()
const notifications = useNotifications()

const scan = ref<LocalRepositoryScan | null>(null)
const statuses = ref<Record<string, BranchStatus>>({})
const loading = ref(false)
const expanded = ref<string | null>(null)

const environments = computed(() => Object.values(statuses.value)[0]?.environments.map((cell) => cell.environment) ?? [])
const unknown = { level: 'Unknown' as const, text: '—', url: null }
const environmentCell = (path: string, environment: string) =>
  statuses.value[path]?.environments.find((cell) => cell.environment === environment)?.status ?? unknown

async function load() {
  loading.value = true
  try {
    scan.value = await fetchLocalRepositories()
    statuses.value = {}
    // Statusy doczytujemy osobno dla każdego repo — jedno wolne połączenie nie blokuje całej listy.
    await Promise.all(scan.value.repositories.filter((repository) => repository.catalogName).map(loadStatus))
  } catch (error) {
    notifications.error(error)
  } finally {
    loading.value = false
  }
}

async function loadStatus(repository: LocalRepository) {
  try {
    statuses.value[repository.path] = await fetchBranchStatus(repository.catalogName!, repository.branch)
  } catch (error) {
    notifications.error(error)
  }
}

async function addToCatalog(repository: LocalRepository) {
  try {
    const added = await addLocalRepository(repository.path)
    notifications.success(`Dodano ${added.name} do repos.yaml`)
    await Promise.all([catalog.load(true), load()])
  } catch (error) {
    notifications.error(error)
  }
}

const vsCodeLink = (path: string) => `vscode://file/${path.replace(/\\/g, '/')}`

onMounted(load)
</script>

<template>
  <v-card>
    <div class="head">
      <div class="title">
        <v-icon icon="mdi-folder-search-outline" />
        <span>Lokalne kopie</span>
        <code v-if="scan?.root" class="fh-muted">{{ scan.root }}</code>
      </div>
      <v-btn variant="tonal" size="small" prepend-icon="mdi-refresh" :loading="loading" @click="load">Odśwież</v-btn>
    </div>

    <p v-if="scan && !scan.root" class="empty fh-muted">
      Wskaż folder z repozytoriami (np. <code>C:\tfs</code>) w
      <RouterLink to="/settings">Ustawieniach → Repozytoria lokalne</RouterLink>, a zobaczysz tu swoje kopie z branchem, commitami i statusami.
    </p>
    <p v-else-if="scan && scan.repositories.length === 0" class="empty fh-muted">W folderze nie znalazłem repozytoriów Git.</p>

    <v-table v-else-if="scan">
      <thead>
        <tr>
          <th>Folder</th>
          <th>Repo</th>
          <th>Branch</th>
          <th>Ostatni commit</th>
          <th>PR (TFS)</th>
          <th>Build (TC)</th>
          <th>Release</th>
          <th v-for="environment in environments" :key="environment">{{ environment }}</th>
          <th>Akcje</th>
        </tr>
      </thead>
      <tbody>
        <template v-for="repository in scan.repositories" :key="repository.path">
          <tr class="repo-row" @click="expanded = expanded === repository.path ? null : repository.path">
            <td><code>{{ repository.folder }}</code></td>
            <td class="font-weight-bold">{{ repository.name }}</td>
            <td>
              <code>{{ repository.branch }}</code>
              <v-chip v-if="repository.uncommittedFiles" size="x-small" color="warning" variant="tonal" class="ml-1">
                {{ repository.uncommittedFiles }} niezacommitowanych
              </v-chip>
            </td>
            <td class="commit">
              <template v-if="repository.recentCommits[0]">
                {{ repository.recentCommits[0].subject }}
                <div class="fh-muted">{{ repository.recentCommits[0].author }} · {{ formatDateTime(repository.recentCommits[0].date) }}</div>
              </template>
              <span v-else class="fh-muted">brak commitów</span>
            </td>
            <template v-if="statuses[repository.path]">
              <td><HealthCell :cell="statuses[repository.path]!.pullRequest" /></td>
              <td><HealthCell :cell="statuses[repository.path]!.build" /></td>
              <td>{{ statuses[repository.path]!.release ?? '—' }}</td>
              <td v-for="environment in environments" :key="environment">
                <HealthCell :cell="environmentCell(repository.path, environment)" />
              </td>
            </template>
            <td v-else :colspan="3 + environments.length" class="fh-muted">
              <template v-if="repository.catalogName">sprawdzam…</template>
              <template v-else>nie ma w katalogu repo — statusy niedostępne</template>
            </td>
            <td class="actions" @click.stop>
              <a :href="vsCodeLink(repository.path)">VS Code</a>
              <template v-if="!repository.catalogName && repository.remoteUrl">
                · <a href="#" @click.prevent="addToCatalog(repository)">Dodaj do katalogu</a>
              </template>
            </td>
          </tr>

          <tr v-if="expanded === repository.path" class="details-row">
            <td :colspan="8 + environments.length">
              <div class="details">
                <div>
                  <h4>Ostatnie commity (lokalnie)</h4>
                  <div v-for="commit in repository.recentCommits" :key="commit.hash">
                    <code>{{ commit.hash }}</code> {{ commit.subject }}
                    <span class="fh-muted">— {{ commit.author }}, {{ formatDateTime(commit.date) }}</span>
                  </div>
                </div>
                <div v-if="statuses[repository.path]?.changes.length">
                  <h4>Zmiany widoczne w TeamCity</h4>
                  <div v-for="change in statuses[repository.path]!.changes" :key="change.revision">
                    <code>{{ change.revision }}</code> {{ change.comment }} <span class="fh-muted">— {{ change.author }}</span>
                  </div>
                </div>
                <div v-if="statuses[repository.path]?.builds.length">
                  <h4>Buildy</h4>
                  <div v-for="build in statuses[repository.path]!.builds" :key="build.configuration">
                    <a :href="build.url" target="_blank" rel="noopener">{{ build.configuration }} #{{ build.number }}</a> — {{ build.state }}
                    <div v-for="test in build.failedTests" :key="test" class="text-error">✗ {{ test }}</div>
                  </div>
                </div>
                <div class="fh-muted">Ścieżka: {{ repository.path }}<br />Origin: {{ repository.remoteUrl ?? 'brak' }}</div>
              </div>
            </td>
          </tr>
        </template>
      </tbody>
    </v-table>
  </v-card>
</template>

<style scoped>
.head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  padding: 1rem 1.25rem;
}

.title {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  font-size: 1.1rem;
  font-weight: 600;
}

.title code {
  font-size: 0.85rem;
  font-weight: 400;
}

.empty {
  padding: 0 1.25rem 1.25rem;
  margin: 0;
}

.repo-row {
  cursor: pointer;
}

.commit {
  max-width: 320px;
  font-size: 0.875rem;
}

.commit .fh-muted {
  font-size: 0.78rem;
}

.actions a,
.empty a,
.details a {
  color: #c4b5fd;
}

.details {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 1.5rem;
  padding: 1rem 0;
  font-size: 0.875rem;
}

.details h4 {
  margin: 0 0 0.4rem;
}
</style>
