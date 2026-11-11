<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'

import { fetchRepoStatus, type RepoStatus, type WorkItemRepoStatus } from '@/api/integrations'
import { liveConnection } from '@/api/live'
import { formatDateTime } from '@/shared/format'
import { healthPresentation } from '@/shared/healthPresentation'
import { useNotifications } from '@/stores/notifications'

import DiffDialog from './DiffDialog.vue'
import HealthCell from './HealthCell.vue'
import StatusBadge from './StatusBadge.vue'
import SystemLinks from './SystemLinks.vue'

/** Najważniejszy widok zadania: jeden wiersz = jedno repo (testy, PR, build, release, środowiska). */
const props = defineProps<{ workItemId: string; refreshKey: number }>()

const notifications = useNotifications()
const status = ref<WorkItemRepoStatus | null>(null)
const loading = ref(false)
const expanded = ref<string | null>(null)
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

const vsCodeLink = (path: string) => `vscode://file/${path.replace(/\\/g, '/')}`
const voteLabel = { Waiting: 'czeka', Approved: 'zaakceptował', ApprovedWithSuggestions: 'zaakceptował z uwagami', Rejected: 'odrzucił' }
</script>

<template>
  <v-card>
    <div class="head">
      <div class="title">
        <v-icon icon="mdi-source-repository-multiple" />
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

    <v-table v-if="status">
      <thead>
        <tr>
          <th>Repo</th>
          <th>Branch</th>
          <th>Testy</th>
          <th>PR (TFS)</th>
          <th>Build (TC)</th>
          <th>Release</th>
          <th v-for="environment in environments" :key="environment">{{ environment }}</th>
          <th>Akcje</th>
        </tr>
      </thead>
      <tbody>
        <template v-for="repo in status.repos" :key="repo.repo">
          <tr class="repo-row" @click="expanded = expanded === repo.repo ? null : repo.repo">
            <td class="font-weight-bold">
              <v-icon :icon="healthPresentation[repo.level].icon" :color="healthPresentation[repo.level].color" size="18" />
              {{ repo.repo }}
            </td>
            <td><code>{{ repo.branch }}</code></td>
            <td><HealthCell :cell="repo.tests" /></td>
            <td><HealthCell :cell="repo.pullRequest" /></td>
            <td><HealthCell :cell="repo.build" /></td>
            <td>{{ repo.release ?? '—' }}</td>
            <td v-for="environment in environments" :key="environment">
              <HealthCell :cell="environmentCell(repo, environment)" />
            </td>
            <td class="actions" @click.stop>
              <a href="#" @click.prevent="diffRepo = repo.repo">Diff</a> ·
              <a :href="vsCodeLink(repo.localPath)">VS Code</a>
              <SystemLinks :links="repo.links" class="ml-2" />
            </td>
          </tr>

          <tr v-if="expanded === repo.repo" class="details-row">
            <td :colspan="7 + environments.length">
              <div class="details">
                <div v-if="repo.pullRequestDetails">
                  <h4>Pull request #{{ repo.pullRequestDetails.id }} — {{ repo.pullRequestDetails.state }}</h4>
                  <div v-for="reviewer in repo.pullRequestDetails.reviewers" :key="reviewer.name">
                    {{ reviewer.name }}: {{ voteLabel[reviewer.vote] }}
                  </div>
                  <div>Nierozwiązane komentarze: {{ repo.pullRequestDetails.unresolvedComments }}</div>
                  <div v-for="policy in repo.pullRequestDetails.policies" :key="policy.name">
                    <v-icon :icon="policy.passed ? 'mdi-check' : 'mdi-close'" :color="policy.passed ? 'success' : 'error'" size="16" />
                    {{ policy.name }}: {{ policy.passed ? 'spełniona' : 'niespełniona' }}
                  </div>
                  <div v-if="repo.pullRequestDetails.hasConflicts" class="text-error">Konflikty scalania</div>
                </div>

                <div>
                  <h4>TeamCity</h4>
                  <div v-for="build in repo.builds" :key="build.configuration">
                    <a :href="build.url" target="_blank" rel="noopener">{{ build.configuration }} #{{ build.number }}</a> — {{ build.state }}
                    <div v-for="test in build.failedTests" :key="test" class="text-error failed-test">✗ {{ test }}</div>
                  </div>
                  <div v-if="repo.builds.length === 0" class="fh-muted">Brak buildów.</div>
                </div>

                <div>
                  <h4>Zmiany widziane przez TeamCity</h4>
                  <div v-for="change in repo.changes" :key="change.revision">
                    <code>{{ change.revision }}</code> {{ change.comment }}
                  </div>
                  <div v-if="repo.changes.length === 0" class="fh-muted">Brak zmian.</div>
                </div>
              </div>
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
  padding: 1rem 1.25rem 0.5rem;
}

.title {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  font-size: 1.15rem;
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

.actions a {
  color: #c4b5fd;
  text-decoration: none;
}

.details-row td {
  background: rgba(15, 12, 29, 0.6);
}

.details {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
  gap: 1.5rem;
  padding: 1rem 0;
  font-size: 0.875rem;
}

.details h4 {
  margin: 0 0 0.4rem;
}

.details a {
  color: #c4b5fd;
}

.failed-test {
  padding-left: 1rem;
  font-family: Consolas, monospace;
  font-size: 0.8rem;
}
</style>
