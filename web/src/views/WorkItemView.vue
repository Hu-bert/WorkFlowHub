<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'

import type { WorkItemDetails } from '@/api/types'
import { fetchWorkItem } from '@/api/workItems'
import StatusBadge from '@/components/StatusBadge.vue'
import { stepKindPresentation, stepStatusPresentation } from '@/shared/stepPresentation'
import { useCatalog } from '@/stores/catalog'
import { useNotifications } from '@/stores/notifications'
import { useWorkItems } from '@/stores/workItems'

const props = defineProps<{ id: string }>()

const catalog = useCatalog()
const workItems = useWorkItems()
const notifications = useNotifications()
const item = ref<WorkItemDetails | null>(null)

async function load() {
  try {
    await catalog.load()
    item.value = await fetchWorkItem(props.id)
  } catch (error) {
    notifications.error(error)
  }
}

async function moveTo(column: string) {
  if (!item.value) return
  try {
    await workItems.load()
    await workItems.move(item.value.summary.id, column)
    item.value.summary.boardColumn = column
  } catch (error) {
    notifications.error(error)
  }
}

onMounted(load)
watch(() => props.id, load)
</script>

<template>
  <section v-if="item" class="fh-page">
    <header class="fh-page-header">
      <div>
        <div class="crumbs fh-muted">
          <RouterLink :to="{ name: 'kanban' }">Kanban</RouterLink> / {{ item.summary.key }}
        </div>
        <h1>{{ item.summary.title }}</h1>
        <p>
          {{ catalog.flowName(item.summary.type) }} · {{ item.summary.stepsDone }}/{{ item.summary.stepsTotal }} kroków
        </p>
      </div>
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
    </header>

    <v-card v-if="item.description" title="Treść zadania">
      <v-card-text class="description">{{ item.description }}</v-card-text>
    </v-card>

    <v-card title="Repozytoria">
      <v-table>
        <thead>
          <tr>
            <th>Repo</th>
            <th>Rola</th>
            <th>Branch</th>
            <th>Katalog</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="repo in item.repos" :key="repo.name">
            <td class="font-weight-bold">{{ repo.name }}</td>
            <td>{{ repo.role === 'Main' ? 'główne' : 'zależne' }}</td>
            <td><code>{{ repo.branch }}</code></td>
            <td class="fh-muted">{{ repo.localPath }}</td>
          </tr>
        </tbody>
      </v-table>
    </v-card>

    <v-card title="Kroki flow">
      <v-list lines="one">
        <v-list-item v-for="step in item.steps" :key="step.id">
          <template #prepend>
            <span class="order">{{ step.order }}</span>
            <v-icon :icon="stepKindPresentation[step.kind].icon" :title="stepKindPresentation[step.kind].label" />
          </template>
          <v-list-item-title>
            {{ step.name }}
            <v-chip v-if="step.repoName" size="x-small" variant="tonal" class="ml-2">{{ step.repoName }}</v-chip>
          </v-list-item-title>
          <template #append>
            <StatusBadge v-bind="stepStatusPresentation[step.status]" size="small" />
          </template>
        </v-list-item>
      </v-list>
    </v-card>
  </section>
</template>

<style scoped>
.crumbs {
  font-size: 0.85rem;
  margin-bottom: 0.25rem;
}

.crumbs a {
  color: #c4b5fd;
  text-decoration: none;
}

.column-select {
  max-width: 260px;
  min-width: 220px;
}

.description {
  white-space: pre-wrap;
}

.order {
  width: 1.75rem;
  color: var(--fh-muted);
  font-variant-numeric: tabular-nums;
}
</style>
