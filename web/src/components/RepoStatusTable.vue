<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'

import { fetchRepoStatus, type RepoStatus, type WorkItemRepoStatus } from '@/api/integrations'
import { liveConnection } from '@/api/live'
import { formatDateTime } from '@/shared/format'
import { healthPresentation } from '@/shared/healthPresentation'
import { useNotifications } from '@/stores/notifications'

import DiffDialog from './DiffDialog.vue'
import HealthCell from './HealthCell.vue'
import RepoDetails, { type RepoDetailsTab } from './RepoDetails.vue'
import StatusBadge from './StatusBadge.vue'

/** Najważniejszy widok zadania: jeden wiersz = jedno repo (testy, PR, build, wersje na środowiskach). */
const props = defineProps<{ workItemId: string; refreshKey: number }>()

const notifications = useNotifications()
const status = ref<WorkItemRepoStatus | null>(null)
const loading = ref(false)
const opened = ref<{ repo: string; tab: RepoDetailsTab } | null>(null)
const diffRepo = ref<string | null>(null)

const environments = computed(() => status.value?.repos[0]?.environments.map((cell) => cell.environment) ?? [])
const environmentCell = (repo: RepoStatus, environment: string) =>
  repo.environments.find((cell) => cell.environment === environment)?.status ?? { level: 'Unknown' as const, text: '—', url: null }

async function load(refresh = false) {
  loading.value = true
  try {
    status.value = await fetchRepoStatus(props.workItemId, refresh)
  } catch (error) {
    notifications.error(error)
  } finally {
    loading.value = false
  }
}

// Po każdym zakończonym kroku czytamy statusy od nowa (testy mogły się zmienić).
watch(() => [props.workItemId, props.refreshKey], () => load(props.refreshKey > 0), { immediate: true })

// Odświeżenia z usługi w tle przychodzą przez SignalR.
const onRefreshed = (fresh: WorkItemRepoStatus) => {
  if (fresh.workItemId === props.workItemId) status.value = fresh
}
liveConnection().on('repoStatusChanged', onRefreshed)
onUnmounted(() => liveConnection().off('repoStatusChanged', onRefreshed))

/** Klik w wiersz zwija / rozwija; klik w komórkę (PR, build, środowisko) otwiera od razu jej zakładkę. */
function open(repo: RepoStatus, tab?: RepoDetailsTab) {
  const same = opened.value?.repo === repo.repo
  if (same && (!tab || opened.value?.tab === tab)) opened.value = null
  else opened.value = { repo: repo.repo, tab: tab ?? (same ? opened.value!.tab : 'pr') }
}

const vsCodeLink = (path: string) => `vscode://file/${path.replace(/\\/g, '/')}`
</script>

<template>
  <v-card>
    <div class="head">
      <div class="title">
        <v-icon icon="mdi-source-repository-multiple" size="20" />
        <span>Repozytoria</span>
        <StatusBadge
          v-if="status"
          :label="status.summary"
          :icon="healthPresentation[status.level].icon"
          :color="healthPresentation[status.level].color"
        />
      </div>
      <div class="refresh fh-muted">
        <span v-if="status">sprawdzono {{ formatDateTime(status.checkedAt) }}</span>
        <v-btn variant="tonal" size="small" prepend-icon="mdi-refresh" :loading="loading" @click="load(true)">Odśwież teraz</v-btn>
      </div>
    </div>

    <v-table v-if="status" density="compact">
      <thead>
        <tr>
          <th>Repo</th>
          <th>Branch</th>
          <th>Testy</th>
          <th>PR</th>
          <th>Build</th>
          <th v-for="environment in environments" :key="environment">{{ environment }}</th>
          <th>Akcje</th>
        </tr>
      </thead>
      <tbody>
        <template v-for="repo in status.repos" :key="repo.repo">
          <tr class="repo-row" :class="{ open: opened?.repo === repo.repo }" @click="open(repo)">
            <td class="font-weight-bold">
              <v-icon :icon="healthPresentation[repo.level].icon" :color="healthPresentation[repo.level].color" size="18" />
              {{ repo.repo }}
            </td>
            <td><code>{{ repo.branch }}</code></td>
            <td><HealthCell :cell="{ ...repo.tests, url: null }" /></td>
            <td class="clickable" title="Kliknij po szczegóły pull requesta" @click.stop="open(repo, 'pr')">
              <HealthCell :cell="{ ...repo.pullRequest, url: null }" />
            </td>
            <td class="clickable" title="Kliknij po szczegóły builda i testy" @click.stop="open(repo, 'build')">
              <HealthCell :cell="{ ...repo.build, url: null }" />
            </td>
            <td
              v-for="environment in environments"
              :key="environment"
              class="clickable"
              title="Wersja wdrożona na środowisku — kliknij po szczegóły"
              @click.stop="open(repo, 'deploy')"
            >
              <HealthCell :cell="{ ...environmentCell(repo, environment), url: null }" />
            </td>
            <td class="actions" @click.stop>
              <a href="#" @click.prevent="diffRepo = repo.repo">Diff</a> ·
              <a :href="vsCodeLink(repo.localPath)">VS Code</a>
            </td>
          </tr>

          <tr v-if="opened?.repo === repo.repo" class="details-row">
            <td :colspan="6 + environments.length">
              <RepoDetails
                v-model:tab="opened.tab"
                :pull-request="repo.pullRequest"
                :pull-request-details="repo.pullRequestDetails"
                :build="repo.build"
                :builds="repo.builds"
                :changes="repo.changes"
                :environments="repo.environments"
                :release="repo.release"
                :links="repo.links"
              >
                <template #info>
                  <dl class="info">
                    <dt>Katalog</dt>
                    <dd>{{ repo.localPath }}</dd>
                    <dt>Branch zadania</dt>
                    <dd>{{ repo.branch }}</dd>
                  </dl>
                </template>
              </RepoDetails>
            </td>
          </tr>
        </template>
      </tbody>
    </v-table>

    <DiffDialog :work-item-id="workItemId" :repo="diffRepo" @close="diffRepo = null" />
  </v-card>
</template>

<style scoped>
.head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  flex-wrap: wrap;
  padding: 0.75rem 1rem 0.4rem;
}

.title {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  font-size: 1rem;
  font-weight: 600;
}

.refresh {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  font-size: 0.8rem;
}

.repo-row {
  cursor: pointer;
}

.clickable:hover {
  background: rgba(139, 92, 246, 0.12);
}

.actions {
  white-space: nowrap;
}

.actions a {
  color: #c4b5fd;
  text-decoration: none;
}

.details-row > td {
  padding: 0 !important;
  background: rgba(15, 12, 29, 0.6);
}

.info {
  display: grid;
  grid-template-columns: max-content 1fr;
  gap: 0.25rem 1.25rem;
  margin: 0;
}

.info dt {
  color: var(--fh-muted);
}

.info dd {
  margin: 0;
  word-break: break-all;
}
</style>
