<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import type { StepRun } from '@/api/types'
import BudgetBar from '@/components/BudgetBar.vue'
import RepoStatusTable from '@/components/RepoStatusTable.vue'
import StepList from '@/components/StepList.vue'
import StepPanel from '@/components/StepPanel.vue'
import TaskFiles from '@/components/TaskFiles.vue'
import TerminalDialog from '@/components/TerminalDialog.vue'
import type { TerminalTarget } from '@/api/terminal'
import { useCatalog } from '@/stores/catalog'
import { useNotifications } from '@/stores/notifications'
import { useWorkItemDetail } from '@/stores/workItemDetail'
import { useWorkItems } from '@/stores/workItems'

const props = defineProps<{ id: string }>()

const catalog = useCatalog()
const workItems = useWorkItems()
const detail = useWorkItemDetail()
const notifications = useNotifications()

/** Krok wybrany ręcznie; bez wyboru pokazujemy bieżący krok flow. */
const pickedStepId = ref<string | null>(null)

const item = computed(() => detail.item)
const selectedStep = computed<StepRun | null>(() => {
  const steps = item.value?.steps ?? []
  return steps.find((step) => step.id === pickedStepId.value) ?? detail.currentStep ?? steps.at(-1) ?? null
})

watch(
  () => props.id,
  async (id) => {
    pickedStepId.value = null
    try {
      await Promise.all([catalog.load(), detail.load(id)])
    } catch (error) {
      notifications.error(error)
    }
  },
  { immediate: true },
)

// Gdy flow przejdzie do następnego kroku, widok idzie za nim.
watch(
  () => detail.currentStep?.id,
  () => (pickedStepId.value = null),
)

/** Otwarty terminal Copilota: repo zadania albo kontynuacja sesji kroku. */
const terminal = ref<{ target: TerminalTarget; title: string } | null>(null)

function openTerminal(repo: string) {
  if (!item.value) return
  terminal.value = {
    target: { workItemId: item.value.summary.id, repo },
    title: `Copilot — ${item.value.summary.key} · ${repo || 'katalog zadania'}`,
  }
}

function resumeInTerminal(step: StepRun) {
  if (!item.value) return
  terminal.value = {
    target: { workItemId: item.value.summary.id, stepId: step.id },
    title: `Copilot — ${item.value.summary.key} · kontynuacja: ${step.name}`,
  }
}

async function moveTo(column: string) {
  if (!item.value) return
  try {
    if (workItems.items.length === 0) await workItems.load()
    await workItems.move(item.value.summary.id, column)
    item.value.summary.boardColumn = column
  } catch (error) {
    notifications.error(error)
  }
}
</script>

<template>
  <section v-if="item" class="fh-page work-item">
    <header class="fh-page-header">
      <div>
        <div class="crumbs fh-muted">
          <RouterLink :to="{ name: 'kanban' }">Kanban</RouterLink> / {{ item.summary.key }}
        </div>
        <h1>{{ item.summary.title }}</h1>
        <p>
          {{ catalog.flowName(item.summary.type) }} · {{ item.summary.stepsDone }}/{{ item.summary.stepsTotal }} kroków ·
          {{ item.repos.map((repo) => repo.name).join(', ') }}
        </p>
      </div>
      <div class="header-actions">
        <v-btn
          v-if="detail.notStarted"
          color="primary"
          size="large"
          prepend-icon="mdi-play"
          :loading="detail.busy"
          @click="detail.start()"
        >
          Uruchom flow
        </v-btn>
        <v-menu>
          <template #activator="{ props: menu }">
            <v-btn v-bind="menu" variant="tonal" prepend-icon="mdi-console">Terminal</v-btn>
          </template>
          <v-list density="compact">
            <v-list-subheader>Copilot CLI w katalogu…</v-list-subheader>
            <v-list-item
              v-for="repo in item.repos"
              :key="repo.name"
              prepend-icon="mdi-source-repository"
              :title="repo.name"
              @click="openTerminal(repo.name)"
            />
            <v-list-item prepend-icon="mdi-folder-outline" title="Katalog zadania" @click="openTerminal('')" />
          </v-list>
        </v-menu>
        <v-select
          :model-value="item.summary.boardColumn"
          :items="catalog.columns"
          item-title="label"
          item-value="key"
          label="Etap pracy"
          hide-details
          class="column-select"
          @update:model-value="moveTo"
        />
      </div>
    </header>

    <v-progress-linear
      :model-value="(item.summary.stepsDone / Math.max(1, item.summary.stepsTotal)) * 100"
      color="primary"
      height="6"
      rounded
    />

    <BudgetBar
      :used="item.summary.usage.totalTokens"
      :limit="catalog.models?.taskTokenBudget ?? 0"
      label="Budżet tokenów zadania"
    />

    <RepoStatusTable :work-item-id="item.summary.id" :refresh-key="item.summary.stepsDone" />

    <div class="layout">
      <v-card class="steps">
        <StepList :steps="item.steps" :selected-id="selectedStep?.id ?? null" @select="pickedStepId = $event.id" />
      </v-card>

      <div class="main">
        <StepPanel v-if="selectedStep" :step="selectedStep" @terminal="resumeInTerminal" />
        <TaskFiles :work-item-id="item.summary.id" :refresh-key="item.summary.stepsDone" />

        <v-expansion-panels variant="accordion">
          <v-expansion-panel v-if="item.description" title="Treść zadania">
            <v-expansion-panel-text class="description">{{ item.description }}</v-expansion-panel-text>
          </v-expansion-panel>
          <v-expansion-panel title="Katalogi na dysku">
            <v-expansion-panel-text>
              <p class="fh-muted">Katalog zadania: {{ item.workDirectory }}</p>
              <p v-for="repo in item.repos" :key="repo.name" class="fh-muted mb-1">
                {{ repo.name }} ({{ repo.role === 'Main' ? 'główne' : 'zależne' }}): {{ repo.localPath }}
              </p>
            </v-expansion-panel-text>
          </v-expansion-panel>
        </v-expansion-panels>
      </div>
    </div>

    <TerminalDialog :target="terminal?.target ?? null" :title="terminal?.title ?? ''" @close="terminal = null" />
  </section>
</template>

<style scoped>
.work-item {
  max-width: none;
}

.crumbs {
  font-size: 0.85rem;
  margin-bottom: 0.25rem;
}

.crumbs a {
  color: #c4b5fd;
  text-decoration: none;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.column-select {
  min-width: 220px;
}

.layout {
  display: grid;
  grid-template-columns: minmax(280px, 360px) 1fr;
  gap: 1.5rem;
  align-items: start;
}

.steps {
  padding: 0.5rem;
  max-height: calc(100vh - 220px);
  overflow-y: auto;
}

.main {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  min-width: 0;
}

.description {
  white-space: pre-wrap;
}

@media (max-width: 1000px) {
  .layout {
    grid-template-columns: 1fr;
  }
}
</style>
