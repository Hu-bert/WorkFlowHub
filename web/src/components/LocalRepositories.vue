<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { addLocalRepository, type LocalRepository } from '@/api/repositories'
import { formatDateTime } from '@/shared/format'
import { healthPresentation } from '@/shared/healthPresentation'
import { useCatalog } from '@/stores/catalog'
import { useLocalRepositories } from '@/stores/localRepositories'
import { useNotifications } from '@/stores/notifications'

import DiagnosisDialog from './DiagnosisDialog.vue'
import RepoDetails, { type RepoDetailsTab } from './RepoDetails.vue'

/**
 * Lokalne kopie repozytoriów jako drzewo — tak jak leżą na dysku: folder (np. branch albo zadanie), a w nim repo.
 * Wiersz jest zwarty: PR, build i wersje na środowiskach. Kliknięcie w PR / build / środowisko rozwija szczegóły
 * od razu na właściwej zakładce.
 */
const catalog = useCatalog()
const local = useLocalRepositories()
const notifications = useNotifications()

const collapsed = ref<Set<string>>(new Set())
const opened = ref<{ path: string; tab: RepoDetailsTab } | null>(null)
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
const levelOf = (repository: LocalRepository) => statusOf(repository)?.level ?? 'Unknown'

/** Najgorszy poziom w folderze — widać z daleka, w którym folderze coś wymaga uwagi. */
function folderLevel(folder: Folder) {
  const order = ['Ok', 'Unknown', 'Pending', 'Warning', 'Failed'] as const
  return folder.repositories.map(levelOf).reduce((worst, level) => (order.indexOf(level) > order.indexOf(worst) ? level : worst), 'Ok' as (typeof order)[number])
}

function toggleFolder(folder: string) {
  const next = new Set(collapsed.value)
  if (!next.delete(folder)) next.add(folder)
  collapsed.value = next
}

/** Klik w wiersz zwija / rozwija; klik w komórkę (PR, build, środowisko) otwiera od razu jej zakładkę. */
function open(repository: LocalRepository, tab?: RepoDetailsTab) {
  const same = opened.value?.path === repository.path
  if (same && (!tab || opened.value?.tab === tab)) opened.value = null
  else opened.value = { path: repository.path, tab: tab ?? (same ? opened.value!.tab : statusOf(repository) ? 'pr' : 'commits') }
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
        <v-icon icon="mdi-folder-open-outline" size="20" />
        <code v-if="local.scan?.root">{{ local.scan.root }}</code>
        <span v-if="local.scan?.scannedAt" class="fh-muted">sprawdzono {{ formatDateTime(local.scan.scannedAt) }}</span>
      </div>
      <v-btn variant="tonal" color="primary" size="small" prepend-icon="mdi-refresh" :loading="local.loading" @click="local.load(true)">Odśwież teraz</v-btn>
    </div>

    <v-card v-if="local.scan && !local.scan.root" class="empty fh-muted">
      Wskaż folder z repozytoriami (np. <code>C:\tfs</code>) w
      <RouterLink to="/settings">Ustawieniach → Repozytoria lokalne</RouterLink>, a zobaczysz tu swoje kopie z branchem, commitami i statusami.
    </v-card>
    <v-card v-else-if="local.scan && local.scan.repositories.length === 0" class="empty fh-muted">W folderze nie znalazłem repozytoriów Git.</v-card>
    <v-progress-linear v-else-if="!local.scan" indeterminate color="primary" />

    <div v-for="folder in folders" :key="folder.name" class="folder">
      <button v-if="folder.name" type="button" class="folder-head" @click="toggleFolder(folder.name)">
        <v-icon :icon="collapsed.has(folder.name) ? 'mdi-chevron-right' : 'mdi-chevron-down'" size="20" />
        <v-icon :icon="collapsed.has(folder.name) ? 'mdi-folder-outline' : 'mdi-folder-open-outline'" color="primary" size="20" />
        <strong>{{ folder.name }}</strong>
        <span class="fh-muted">{{ folder.repositories.length }} repo</span>
        <v-icon :icon="healthPresentation[folderLevel(folder)].icon" :color="healthPresentation[folderLevel(folder)].color" size="16" />
      </button>

      <div v-if="!folder.name || !collapsed.has(folder.name)" :class="{ children: !!folder.name }">
        <v-card v-for="repository in folder.repositories" :key="repository.path" class="repo" :class="{ open: opened?.path === repository.path }">
          <div class="row" @click="open(repository)">
            <div class="identity">
              <div class="name">
                <v-icon :icon="healthPresentation[levelOf(repository)].icon" :color="healthPresentation[levelOf(repository)].color" size="20" />
                <span>{{ repository.name }}</span>
              </div>
              <div class="sub">
                <v-chip size="x-small" variant="tonal" color="primary" prepend-icon="mdi-source-branch">{{ repository.branch }}</v-chip>
                <v-chip v-if="repository.uncommittedFiles" size="x-small" color="warning" variant="tonal" title="Niezacommitowane pliki">
                  {{ repository.uncommittedFiles }} zmian
                </v-chip>
              </div>
            </div>

            <template v-if="statusOf(repository)">
              <button type="button" class="cell" title="Pull request — kliknij po szczegóły" @click.stop="open(repository, 'pr')">
                <span class="label">PR</span>
                <span class="value">
                  <v-icon :icon="healthPresentation[statusOf(repository)!.pullRequest.level].icon" :color="healthPresentation[statusOf(repository)!.pullRequest.level].color" size="16" />
                  {{ statusOf(repository)!.pullRequest.text }}
                </span>
              </button>
              <button type="button" class="cell" title="Build — kliknij po szczegóły i testy" @click.stop="open(repository, 'build')">
                <span class="label">Build</span>
                <span class="value">
                  <v-icon :icon="healthPresentation[statusOf(repository)!.build.level].icon" :color="healthPresentation[statusOf(repository)!.build.level].color" size="16" />
                  {{ statusOf(repository)!.build.text }}
                </span>
              </button>
              <button type="button" class="cell environments" title="Wersja wdrożona na środowiskach — kliknij po szczegóły" @click.stop="open(repository, 'deploy')">
                <span class="label">Wdrożone</span>
                <span class="value">
                  <span
                    v-for="environment in statusOf(repository)!.environments"
                    :key="environment.environment"
                    class="environment"
                    :class="{ mine: environment.status.level === 'Ok', problem: environment.status.level === 'Failed' }"
                  >
                    <span class="env-name">{{ environment.environment }}</span>{{ environment.status.text }}
                  </span>
                  <span v-if="statusOf(repository)!.environments.length === 0" class="fh-muted">—</span>
                </span>
              </button>
            </template>
            <div v-else class="pending fh-muted">
              <template v-if="repository.catalogName">sprawdzam statusy…</template>
              <template v-else>
                Nie ma w katalogu repo — FlowHub nie wie, gdzie szukać PR i buildów.
                <v-btn v-if="repository.remoteUrl" size="x-small" variant="tonal" color="primary" class="ml-2" @click.stop="addToCatalog(repository)">Dodaj do katalogu</v-btn>
              </template>
            </div>

            <v-btn :href="vsCodeLink(repository.path)" size="x-small" variant="text" icon="mdi-microsoft-visual-studio-code" title="Otwórz w VS Code" @click.stop />
            <v-icon :icon="opened?.path === repository.path ? 'mdi-chevron-up' : 'mdi-chevron-down'" size="20" class="fh-muted" />
          </div>

          <RepoDetails
            v-if="opened?.path === repository.path"
            v-model:tab="opened.tab"
            :pull-request="statusOf(repository)?.pullRequest ?? { level: 'Unknown', text: '—', url: null }"
            :pull-request-details="statusOf(repository)?.pullRequestDetails ?? null"
            :build="statusOf(repository)?.build ?? { level: 'Unknown', text: '—', url: null }"
            :builds="statusOf(repository)?.builds ?? []"
            :changes="statusOf(repository)?.changes ?? []"
            :environments="statusOf(repository)?.environments ?? []"
            :release="statusOf(repository)?.release ?? null"
            :links="statusOf(repository)?.links ?? null"
            :local-commits="repository.recentCommits"
          >
            <template #info>
              <dl class="info">
                <dt>Ścieżka</dt>
                <dd>{{ repository.path }}</dd>
                <dt>Origin</dt>
                <dd>{{ repository.remoteUrl ?? 'brak' }}</dd>
                <dt>W katalogu repo</dt>
                <dd>{{ repository.catalogName ?? 'nie' }}</dd>
                <template v-if="local.statuses[repository.path]">
                  <dt>Statusy sprawdzono</dt>
                  <dd>{{ formatDateTime(local.statuses[repository.path]!.checkedAt) }}</dd>
                </template>
              </dl>
              <v-btn
                v-if="repository.catalogName"
                size="small"
                variant="tonal"
                prepend-icon="mdi-stethoscope"
                @click="diagnosed = { name: repository.catalogName, branch: repository.branch }"
              >
                Diagnostyka połączeń
              </v-btn>
            </template>
          </RepoDetails>
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
  gap: 0.6rem;
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
  font-size: 0.9rem;
}

.empty {
  padding: 1.25rem;
}

.empty a {
  color: #c4b5fd;
}

.folder-head {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  width: 100%;
  padding: 0.35rem 0.1rem;
  background: none;
  border: 0;
  color: inherit;
  font: inherit;
  text-align: left;
  cursor: pointer;
}

/* Linia drzewa: repo w folderze są wcięte i połączone pionową kreską. */
.children {
  margin-left: 0.6rem;
  padding-left: 1rem;
  border-left: 2px solid var(--fh-border);
}

.repo {
  margin-bottom: 0.5rem;
}

.repo.open {
  border-color: rgba(167, 139, 250, 0.45) !important;
}

/* Stałe kolumny: nazwy i statusy stoją równo w całym drzewie. */
.row {
  display: grid;
  grid-template-columns: minmax(220px, 1.4fr) minmax(130px, 0.9fr) minmax(170px, 1.1fr) minmax(200px, 2fr) auto auto;
  gap: 0.75rem;
  align-items: center;
  padding: 0.6rem 0.9rem;
  cursor: pointer;
}

.name {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  font-weight: 700;
  min-width: 0;
}

.name span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.sub {
  display: flex;
  flex-wrap: wrap;
  gap: 0.3rem;
  margin-top: 0.3rem;
  padding-left: 1.7rem;
}

.cell {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
  min-width: 0;
  padding: 0.3rem 0.5rem;
  border: 0;
  border-radius: 8px;
  background: none;
  color: inherit;
  font: inherit;
  font-size: 0.85rem;
  text-align: left;
  cursor: pointer;
}

.cell:hover {
  background: rgba(139, 92, 246, 0.12);
}

.label {
  font-size: 0.65rem;
  letter-spacing: 0.07em;
  text-transform: uppercase;
  color: var(--fh-muted);
}

.value {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.3rem 0.5rem;
}

.environment {
  display: inline-flex;
  align-items: baseline;
  gap: 0.3rem;
  padding: 0.05rem 0.45rem;
  border: 1px solid var(--fh-border);
  border-radius: 999px;
  font-size: 0.78rem;
  white-space: nowrap;
}

.environment.mine {
  border-color: rgba(34, 197, 94, 0.6);
  color: #86efac;
}

.environment.problem {
  border-color: rgba(244, 63, 94, 0.6);
  color: #fda4af;
}

.env-name {
  font-size: 0.65rem;
  letter-spacing: 0.05em;
  color: var(--fh-muted);
}

.pending {
  grid-column: span 3;
  font-size: 0.85rem;
}

.info {
  display: grid;
  grid-template-columns: max-content 1fr;
  gap: 0.25rem 1.25rem;
  margin: 0 0 0.9rem;
}

.info dt {
  color: var(--fh-muted);
}

.info dd {
  margin: 0;
  word-break: break-all;
}

@media (max-width: 1100px) {
  .row {
    grid-template-columns: 1fr 1fr;
  }

  .pending {
    grid-column: span 2;
  }
}
</style>
