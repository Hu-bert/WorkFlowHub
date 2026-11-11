<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { addLocalRepository, type LocalRepository } from '@/api/repositories'
import { formatDateTime } from '@/shared/format'
import { healthPresentation } from '@/shared/healthPresentation'
import { useCatalog } from '@/stores/catalog'
import { useLocalRepositories } from '@/stores/localRepositories'
import { useNotifications } from '@/stores/notifications'

import DiagnosisDialog from './DiagnosisDialog.vue'
import HealthCell from './HealthCell.vue'
import SystemLinks from './SystemLinks.vue'

/**
 * Lokalne kopie repozytoriów jako drzewo — tak jak leżą na dysku: folder (np. branch albo zadanie), a w nim repo.
 * Przy każdym repo: branch, ostatni commit, PR, build, release i środowiska oraz skróty do TFS / TeamCity / Octopus.
 */
const catalog = useCatalog()
const local = useLocalRepositories()
const notifications = useNotifications()

const collapsed = ref<Set<string>>(new Set())
const expanded = ref<string | null>(null)
const diagnosed = ref<{ name: string; branch: string } | null>(null)

interface Folder {
  /** Pusty tekst = repo leżące bezpośrednio w folderze głównym. */
  name: string
  repositories: LocalRepository[]
}

/** Grupujemy po pierwszym poziomie ścieżki: „ZR-3925\Billing.Api” → folder „ZR-3925”. */
const folders = computed<Folder[]>(() => {
  const groups = new Map<string, LocalRepository[]>()
  for (const repository of local.scan?.repositories ?? []) {
    const parts = repository.folder.split(/[\\/]/)
    const folder = parts.length > 1 ? parts[0]! : ''
    groups.set(folder, [...(groups.get(folder) ?? []), repository])
  }
  return [...groups.entries()].map(([name, repositories]) => ({ name, repositories })).sort((a, b) => a.name.localeCompare(b.name))
})

const statusOf = (repository: LocalRepository) => local.statuses[repository.path]?.status ?? null

/** Najgorszy poziom w folderze — widać z daleka, w którym folderze coś wymaga uwagi. */
function folderLevel(folder: Folder) {
  const order = ['Ok', 'Unknown', 'Pending', 'Warning', 'Failed'] as const
  const levels = folder.repositories.map((repository) => statusOf(repository)?.level ?? 'Unknown')
  return levels.reduce((worst, level) => (order.indexOf(level) > order.indexOf(worst) ? level : worst), 'Ok' as (typeof order)[number])
}

function toggle(folder: string) {
  const next = new Set(collapsed.value)
  if (!next.delete(folder)) next.add(folder)
  collapsed.value = next
}

async function addToCatalog(repository: LocalRepository) {
  try {
    const added = await addLocalRepository(repository.path)
    notifications.success(`Dodano ${added.name} do katalogu repo`)
    await catalog.load(true)
    await local.load(true)
  } catch (error) {
    notifications.error(error)
  }
}

const vsCodeLink = (path: string) => `vscode://file/${path.replace(/\\/g, '/')}`

onMounted(() => local.load())
</script>

<template>
  <div class="local">
    <div class="toolbar">
      <div class="root">
        <v-icon icon="mdi-folder-open-outline" />
        <code v-if="local.scan?.root">{{ local.scan.root }}</code>
        <span v-if="local.scan?.scannedAt" class="fh-muted">· sprawdzono {{ formatDateTime(local.scan.scannedAt) }}</span>
      </div>
      <v-btn variant="tonal" color="primary" prepend-icon="mdi-refresh" :loading="local.loading" @click="local.load(true)">Odśwież teraz</v-btn>
    </div>

    <v-card v-if="local.scan && !local.scan.root" class="empty fh-muted">
      Wskaż folder z repozytoriami (np. <code>C:\tfs</code>) w
      <RouterLink to="/settings">Ustawieniach → Repozytoria lokalne</RouterLink>, a zobaczysz tu swoje kopie z branchem, commitami i statusami.
    </v-card>
    <v-card v-else-if="local.scan && local.scan.repositories.length === 0" class="empty fh-muted">W folderze nie znalazłem repozytoriów Git.</v-card>
    <v-progress-linear v-else-if="!local.scan" indeterminate color="primary" />

    <div v-for="folder in folders" :key="folder.name" class="folder">
      <button v-if="folder.name" type="button" class="folder-head" @click="toggle(folder.name)">
        <v-icon :icon="collapsed.has(folder.name) ? 'mdi-chevron-right' : 'mdi-chevron-down'" />
        <v-icon :icon="collapsed.has(folder.name) ? 'mdi-folder-outline' : 'mdi-folder-open-outline'" color="primary" />
        <strong>{{ folder.name }}</strong>
        <span class="fh-muted">{{ folder.repositories.length }} repo</span>
        <v-icon :icon="healthPresentation[folderLevel(folder)].icon" :color="healthPresentation[folderLevel(folder)].color" size="18" />
      </button>

      <div v-if="!folder.name || !collapsed.has(folder.name)" :class="{ children: !!folder.name }">
        <v-card v-for="repository in folder.repositories" :key="repository.path" class="repo">
          <div class="row" @click="expanded = expanded === repository.path ? null : repository.path">
            <div class="identity">
              <div class="name">
                <v-icon
                  :icon="healthPresentation[statusOf(repository)?.level ?? 'Unknown'].icon"
                  :color="healthPresentation[statusOf(repository)?.level ?? 'Unknown'].color"
                />
                {{ repository.name }}
              </div>
              <div class="branch">
                <v-chip size="small" variant="tonal" color="primary" prepend-icon="mdi-source-branch">{{ repository.branch }}</v-chip>
                <v-chip v-if="repository.uncommittedFiles" size="small" color="warning" variant="tonal">
                  {{ repository.uncommittedFiles }} niezacommitowanych
                </v-chip>
              </div>
              <div v-if="repository.recentCommits[0]" class="commit fh-muted">
                {{ repository.recentCommits[0].subject }} — {{ repository.recentCommits[0].author }}, {{ formatDateTime(repository.recentCommits[0].date) }}
              </div>
            </div>

            <div v-if="statusOf(repository)" class="cells">
              <div class="cell"><span class="label">PR</span><HealthCell :cell="statusOf(repository)!.pullRequest" /></div>
              <div class="cell"><span class="label">Build</span><HealthCell :cell="statusOf(repository)!.build" /></div>
              <div class="cell"><span class="label">Release</span>{{ statusOf(repository)!.release ?? '—' }}</div>
              <div v-for="environment in statusOf(repository)!.environments" :key="environment.environment" class="cell">
                <span class="label">{{ environment.environment }}</span><HealthCell :cell="environment.status" />
              </div>
            </div>
            <div v-else class="cells fh-muted">
              <template v-if="repository.catalogName">sprawdzam statusy…</template>
              <template v-else>
                Nie ma w katalogu repo — bez tego FlowHub nie wie, gdzie szukać PR i buildów.
                <v-btn v-if="repository.remoteUrl" size="small" variant="tonal" color="primary" class="ml-2" @click.stop="addToCatalog(repository)">
                  Dodaj do katalogu
                </v-btn>
              </template>
            </div>

            <div class="actions" @click.stop>
              <SystemLinks :links="statusOf(repository)?.links" />
              <v-btn :href="vsCodeLink(repository.path)" size="x-small" variant="tonal" prepend-icon="mdi-microsoft-visual-studio-code">VS Code</v-btn>
            </div>
          </div>

          <div v-if="expanded === repository.path" class="details">
            <div>
              <h4>Ostatnie commity (lokalnie)</h4>
              <div v-for="commit in repository.recentCommits" :key="commit.hash">
                <code>{{ commit.hash }}</code> {{ commit.subject }}
                <span class="fh-muted">— {{ commit.author }}, {{ formatDateTime(commit.date) }}</span>
              </div>
              <p v-if="repository.recentCommits.length === 0" class="fh-muted">brak commitów</p>
            </div>
            <div v-if="statusOf(repository)?.builds.length">
              <h4>Buildy</h4>
              <div v-for="build in statusOf(repository)!.builds" :key="build.configuration + build.number" class="build">
                <a :href="build.url" target="_blank" rel="noopener">{{ build.configuration }} #{{ build.number }}</a>
                <span v-if="build.branch" class="fh-muted"> · {{ build.branch }}</span>
                <div v-if="build.statusText">{{ build.statusText }}</div>
                <div v-for="test in build.failedTests" :key="test" class="text-error">✗ {{ test }}</div>
              </div>
            </div>
            <div v-if="statusOf(repository)?.changes.length">
              <h4>Zmiany widoczne na serwerze</h4>
              <div v-for="change in statusOf(repository)!.changes" :key="change.revision">
                <code>{{ change.revision }}</code> {{ change.comment }} <span class="fh-muted">— {{ change.author }}</span>
              </div>
            </div>
            <div class="fh-muted">
              Ścieżka: {{ repository.path }}<br />Origin: {{ repository.remoteUrl ?? 'brak' }}
              <template v-if="local.statuses[repository.path]"><br />Statusy sprawdzono: {{ formatDateTime(local.statuses[repository.path]!.checkedAt) }}</template>
              <div v-if="repository.catalogName" class="mt-2">
                <v-btn size="small" variant="tonal" prepend-icon="mdi-stethoscope" @click="diagnosed = { name: repository.catalogName, branch: repository.branch }">
                  Diagnostyka połączeń
                </v-btn>
              </div>
            </div>
          </div>
        </v-card>
      </div>
    </div>

    <DiagnosisDialog :target="diagnosed" @close="diagnosed = null" />
  </div>
</template>

<style scoped>
.local {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  flex-wrap: wrap;
}

.root {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 1.05rem;
}

.empty {
  padding: 1.5rem;
}

.empty a,
.details a {
  color: #c4b5fd;
}

.folder-head {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  width: 100%;
  padding: 0.5rem 0.25rem;
  background: none;
  border: 0;
  color: inherit;
  font: inherit;
  font-size: 1.05rem;
  text-align: left;
  cursor: pointer;
}

/* Linia drzewa: repo w folderze są wcięte i połączone pionową kreską. */
.children {
  margin-left: 0.85rem;
  padding-left: 1.25rem;
  border-left: 2px solid var(--fh-border);
}

.repo {
  margin-bottom: 0.75rem;
}

.row {
  display: grid;
  grid-template-columns: minmax(240px, 1.1fr) minmax(0, 2fr) auto;
  gap: 1.25rem;
  align-items: center;
  padding: 1rem 1.25rem;
  cursor: pointer;
}

.name {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 1.1rem;
  font-weight: 700;
}

.branch {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
  margin-top: 0.4rem;
}

.commit {
  margin-top: 0.4rem;
  font-size: 0.8rem;
}

.cells {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem 1.5rem;
  align-items: center;
}

.cell {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  min-width: 80px;
}

.label {
  font-size: 0.7rem;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--fh-muted);
}

.actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 0.35rem;
  max-width: 260px;
}

.details {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 1.5rem;
  padding: 1rem 1.25rem 1.25rem;
  border-top: 1px solid var(--fh-border);
  font-size: 0.875rem;
}

.details h4 {
  margin: 0 0 0.4rem;
}

.build {
  margin-bottom: 0.5rem;
}

@media (max-width: 1100px) {
  .row {
    grid-template-columns: 1fr;
  }

  .actions {
    justify-content: flex-start;
    max-width: none;
  }
}
</style>
