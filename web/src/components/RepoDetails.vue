<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import type { BuildStatus, CiChange, PullRequestStatus, RepositoryLinks, StatusCell } from '@/api/integrations'
import { fetchProcesses, type GitCommit, type RemoteProcessState } from '@/api/repositories'
import { formatDateTime } from '@/shared/format'
import { healthPresentation } from '@/shared/healthPresentation'

import HealthCell from './HealthCell.vue'
import SystemLinks from './SystemLinks.vue'

export type RepoDetailsTab = 'pr' | 'build' | 'deploy' | 'processes' | 'commits' | 'info'

/**
 * Szczegóły repo w zakładkach — wspólne dla lokalnych kopii i tabeli repo w zadaniu.
 * Każda zakładka pokazuje jedną rzecz (PR, build, wdrożenia, commity), więc nic się nie rozjeżdża przy długich listach.
 */
const tab = defineModel<RepoDetailsTab>('tab', { required: true })

const props = defineProps<{
  pullRequest: StatusCell
  pullRequestDetails: PullRequestStatus | null
  build: StatusCell
  builds: BuildStatus[]
  changes: CiChange[]
  environments: { environment: string; status: StatusCell }[]
  release: string | null
  links: RepositoryLinks | null
  localCommits?: GitCommit[]
  /** Nazwa repo w katalogu — gdy podana i repo ma przypisane procesy, pojawia się zakładka „Procesy”. */
  catalogName?: string | null
  hasProcesses?: boolean
}>()

/** Procesy sprawdzamy dopiero po wejściu na zakładkę — to zapytanie do zdalnych maszyn, bywa wolne. */
const processes = ref<RemoteProcessState[] | null>(null)
const processesLoading = ref(false)
const processesError = ref('')

async function loadProcesses() {
  if (!props.catalogName) return
  processesLoading.value = true
  processesError.value = ''
  try {
    processes.value = await fetchProcesses(props.catalogName)
  } catch (error) {
    processesError.value = error instanceof Error ? error.message : String(error)
  } finally {
    processesLoading.value = false
  }
}

watch(
  tab,
  (current) => {
    if (current === 'processes' && processes.value === null) void loadProcesses()
  },
  { immediate: true },
)

const voteLabel = { Waiting: 'czeka', Approved: 'zaakceptował', ApprovedWithSuggestions: 'zaakceptował z uwagami', Rejected: 'odrzucił' }
const voteIcon = {
  Waiting: { icon: 'mdi-timer-sand', color: 'grey' },
  Approved: { icon: 'mdi-check-circle', color: 'success' },
  ApprovedWithSuggestions: { icon: 'mdi-check-circle-outline', color: 'success' },
  Rejected: { icon: 'mdi-close-circle', color: 'error' },
}
const buildLabel = { Queued: 'w kolejce', Running: 'trwa', Success: 'przeszedł', Failed: 'nie przeszedł' }
const buildColor = { Queued: 'warning', Running: 'warning', Success: 'success', Failed: 'error' }

const failedTests = computed(() => props.builds.reduce((sum, build) => sum + build.failedTests.length, 0))
const commitCount = computed(() => (props.localCommits?.length ?? 0) || props.changes.length)

/** Z długiej nazwy testu (Przestrzeń.Klasa.Metoda(parametry)) wyróżniamy końcówkę — to ona mówi, co nie przeszło. */
function testParts(name: string) {
  const end = name.indexOf('(') >= 0 ? name.indexOf('(') : name.length
  const dot = name.lastIndexOf('.', end - 1)
  const classDot = dot > 0 ? name.lastIndexOf('.', dot - 1) : -1
  return { prefix: name.slice(0, classDot + 1), name: name.slice(classDot + 1) }
}

/** Zielone = wersja z tego brancha; szare z numerem = na środowisku jest inna wersja. */
const deploymentNote = (cell: StatusCell) =>
  cell.text === '—' ? 'nic nie wdrożono' : cell.level === 'Unknown' ? 'inna wersja niż z tego brancha' : healthPresentation[cell.level].label
</script>

<template>
  <div class="repo-details">
    <v-tabs v-model="tab" density="compact" color="primary" class="tabs">
      <v-tab value="pr">
        <v-icon :icon="healthPresentation[pullRequest.level].icon" :color="healthPresentation[pullRequest.level].color" size="16" start />
        Pull request
      </v-tab>
      <v-tab value="build">
        <v-icon :icon="healthPresentation[build.level].icon" :color="healthPresentation[build.level].color" size="16" start />
        Build
        <v-chip v-if="failedTests" size="x-small" color="error" variant="flat" class="ml-2">{{ failedTests }}</v-chip>
      </v-tab>
      <v-tab value="deploy"><v-icon icon="mdi-rocket-launch-outline" size="16" start />Wdrożenia</v-tab>
      <v-tab v-if="hasProcesses" value="processes"><v-icon icon="mdi-server-network" size="16" start />Procesy</v-tab>
      <v-tab value="commits">
        <v-icon icon="mdi-source-commit" size="16" start />Commity
        <span v-if="commitCount" class="count">{{ commitCount }}</span>
      </v-tab>
      <v-tab value="info"><v-icon icon="mdi-information-outline" size="16" start />Informacje</v-tab>
      <v-spacer />
      <SystemLinks :links="links" class="links" />
    </v-tabs>

    <div class="panel">
      <!-- Pull request -->
      <template v-if="tab === 'pr'">
        <p v-if="!pullRequestDetails" class="fh-muted empty">Brak pull requesta dla tego brancha.</p>
        <template v-else>
          <div class="headline">
            <a :href="pullRequestDetails.url" target="_blank" rel="noopener">#{{ pullRequestDetails.id }} {{ pullRequestDetails.title }}</a>
            <v-chip size="small" variant="tonal" :color="healthPresentation[pullRequest.level].color">{{ pullRequestDetails.state }}</v-chip>
            <v-chip v-if="pullRequestDetails.hasConflicts" size="small" color="error" variant="flat" prepend-icon="mdi-alert">Konflikty scalania</v-chip>
            <v-chip v-if="pullRequestDetails.unresolvedComments" size="small" color="warning" variant="tonal" prepend-icon="mdi-comment-alert-outline">
              {{ pullRequestDetails.unresolvedComments }} nierozwiązanych komentarzy
            </v-chip>
          </div>
          <div class="columns">
            <div>
              <h4>Recenzenci</h4>
              <div v-for="reviewer in pullRequestDetails.reviewers" :key="reviewer.name" class="line">
                <v-icon :icon="voteIcon[reviewer.vote].icon" :color="voteIcon[reviewer.vote].color" size="16" />
                {{ reviewer.name }} <span class="fh-muted">— {{ voteLabel[reviewer.vote] }}</span>
              </div>
              <p v-if="pullRequestDetails.reviewers.length === 0" class="fh-muted">Brak recenzentów.</p>
            </div>
            <div>
              <h4>Polityki brancha</h4>
              <div v-for="policy in pullRequestDetails.policies" :key="policy.name" class="line">
                <v-icon :icon="policy.passed ? 'mdi-check-circle' : 'mdi-close-circle'" :color="policy.passed ? 'success' : 'error'" size="16" />
                {{ policy.name }}
              </div>
              <p v-if="pullRequestDetails.policies.length === 0" class="fh-muted">Brak danych o politykach.</p>
            </div>
          </div>
        </template>
      </template>

      <!-- Build -->
      <template v-else-if="tab === 'build'">
        <p v-if="builds.length === 0" class="fh-muted empty">Brak buildów dla tego brancha.</p>
        <div v-for="item in builds" :key="item.configuration + item.number" class="build">
          <div class="headline">
            <v-chip size="small" variant="flat" :color="buildColor[item.state]">{{ buildLabel[item.state] }}</v-chip>
            <a :href="item.url" target="_blank" rel="noopener">{{ item.configuration }} #{{ item.number }}</a>
            <v-chip v-if="item.branch" size="x-small" variant="tonal" prepend-icon="mdi-source-branch">{{ item.branch }}</v-chip>
            <span v-if="item.statusText" class="fh-muted">{{ item.statusText }}</span>
          </div>
          <div v-if="item.failedTests.length" class="tests">
            <div v-for="test in item.failedTests" :key="test" class="test" :title="test">
              <v-icon icon="mdi-close" color="error" size="14" />
              <span><span class="fh-muted">{{ testParts(test).prefix }}</span>{{ testParts(test).name }}</span>
            </div>
          </div>
        </div>
      </template>

      <!-- Wdrożenia -->
      <template v-else-if="tab === 'deploy'">
        <p class="headline">
          Release z tego brancha: <strong>{{ release ?? 'brak' }}</strong>
          <span class="fh-muted">Poniżej wersja wdrożona teraz na każdym środowisku.</span>
        </p>
        <p v-if="environments.length === 0" class="fh-muted empty">Brak danych o wdrożeniach.</p>
        <table v-else class="deployments">
          <tbody>
            <tr v-for="environment in environments" :key="environment.environment">
              <th>{{ environment.environment }}</th>
              <td><HealthCell :cell="environment.status" /></td>
              <td class="fh-muted">{{ deploymentNote(environment.status) }}</td>
            </tr>
          </tbody>
        </table>
      </template>

      <!-- Procesy na maszynach -->
      <template v-else-if="tab === 'processes'">
        <div class="headline">
          <span class="fh-muted">Czy serwis działa na maszynie (procesy i usługi Windows, tylko odczyt).</span>
          <v-btn size="x-small" variant="tonal" prepend-icon="mdi-refresh" :loading="processesLoading" @click="loadProcesses">Sprawdź teraz</v-btn>
        </div>
        <p v-if="processesError" class="text-error">{{ processesError }}</p>
        <table v-if="processes" class="deployments">
          <tbody>
            <tr v-for="state in processes" :key="state.host + state.name">
              <th>
                <v-icon :icon="state.running ? 'mdi-check-circle' : 'mdi-close-circle'" :color="state.running ? 'success' : 'error'" size="16" />
                {{ state.host }}<span v-if="state.label" class="fh-muted"> · {{ state.label }}</span>
              </th>
              <td>
                <div v-if="state.error" class="text-error">{{ state.error }}</div>
                <div v-else-if="state.found.length === 0">Nie znaleziono procesu ani usługi „{{ state.name }}”.</div>
                <div v-for="item in state.found" :key="item.kind + item.name">
                  {{ item.name }}
                  <span class="fh-muted">
                    — {{ item.kind === 'service' ? `usługa: ${item.state}` : 'proces działa' }}<template v-if="item.startedAt">, od {{ formatDateTime(item.startedAt) }}</template>
                  </span>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </template>

      <!-- Commity -->
      <template v-else-if="tab === 'commits'">
        <div class="columns">
          <div v-if="localCommits">
            <h4>Lokalnie</h4>
            <div v-for="commit in localCommits" :key="commit.hash" class="line commit">
              <code>{{ commit.hash }}</code>
              <span>{{ commit.subject }} <span class="fh-muted">— {{ commit.author }}, {{ formatDateTime(commit.date) }}</span></span>
            </div>
            <p v-if="localCommits.length === 0" class="fh-muted">Brak commitów.</p>
          </div>
          <div>
            <h4>Na serwerze (w buildzie)</h4>
            <div v-for="change in changes" :key="change.revision" class="line commit">
              <code>{{ change.revision }}</code>
              <span>{{ change.comment }} <span class="fh-muted">— {{ change.author }}</span></span>
            </div>
            <p v-if="changes.length === 0" class="fh-muted">Brak zmian widocznych na serwerze.</p>
          </div>
        </div>
      </template>

      <!-- Informacje -->
      <slot v-else name="info" />
    </div>
  </div>
</template>

<style scoped>
.repo-details {
  border-top: 1px solid var(--fh-border);
  font-size: 0.875rem;
}

.tabs {
  padding: 0 0.75rem;
  border-bottom: 1px solid var(--fh-border);
}

.tabs :deep(.v-tab) {
  min-width: 0;
  padding: 0 0.85rem;
  font-size: 0.82rem;
}

.count {
  margin-left: 0.4rem;
  color: var(--fh-muted);
}

.links {
  align-self: center;
}

/* Stała wysokość z przewijaniem: długa lista testów nie rozpycha strony. */
.panel {
  max-height: 340px;
  overflow-y: auto;
  padding: 0.9rem 1.25rem 1.1rem;
}

.empty {
  margin: 0;
}

.headline {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem 0.75rem;
  margin: 0 0 0.75rem;
}

.headline a {
  font-weight: 600;
}

a {
  color: #c4b5fd;
  text-decoration: none;
}

a:hover {
  text-decoration: underline;
}

.columns {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 1rem 2rem;
}

h4 {
  margin: 0 0 0.35rem;
  font-size: 0.72rem;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--fh-muted);
}

.line {
  display: flex;
  align-items: baseline;
  gap: 0.45rem;
  padding: 0.15rem 0;
}

.line .v-icon {
  align-self: center;
}

.commit code {
  flex: none;
  color: #c4b5fd;
}

.build + .build {
  margin-top: 1rem;
  padding-top: 1rem;
  border-top: 1px dashed var(--fh-border);
}

.tests {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.test {
  display: flex;
  align-items: baseline;
  gap: 0.4rem;
  font-family: 'Cascadia Code', Consolas, monospace;
  font-size: 0.78rem;
  word-break: break-word;
}

.test .v-icon {
  flex: none;
  align-self: center;
}

.deployments {
  border-collapse: collapse;
}

.deployments th {
  padding: 0.3rem 1.5rem 0.3rem 0;
  text-align: left;
  font-weight: 600;
  white-space: nowrap;
}

.deployments td {
  padding: 0.3rem 1.5rem 0.3rem 0;
}

.deployments th,
.deployments td {
  vertical-align: top;
}
</style>
