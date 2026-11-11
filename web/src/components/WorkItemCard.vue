<script setup lang="ts">
import { computed } from 'vue'

import type { WorkItemSummary } from '@/api/types'
import { formatCost, formatTokens } from '@/shared/format'
import { stepStatusPresentation } from '@/shared/stepPresentation'
import { healthPresentation } from '@/shared/healthPresentation'
import { useCatalog } from '@/stores/catalog'
import { useWorkItems } from '@/stores/workItems'

import StatusBadge from './StatusBadge.vue'

const props = defineProps<{ item: WorkItemSummary }>()

const catalog = useCatalog()
const workItems = useWorkItems()

/** Kropka per repo — bez wchodzenia w zadanie widać, które repo blokuje. */
const repoLevels = computed(() => workItems.repoLevels[props.item.id] ?? [])
const progress = computed(() => (props.item.stepsTotal ? (props.item.stepsDone / props.item.stepsTotal) * 100 : 0))
const finished = computed(() => props.item.stepsTotal > 0 && props.item.stepsDone === props.item.stepsTotal)
</script>

<template>
  <v-card
    class="work-item-card"
    :class="{ waits: item.waitsForUser }"
    :to="{ name: 'work-item', params: { id: item.id } }"
    draggable="true"
    @dragstart="$event.dataTransfer?.setData('text/plain', item.id)"
  >
    <div class="top">
      <span class="key">{{ item.key }}</span>
      <v-chip size="x-small" color="primary" variant="tonal">{{ catalog.flowName(item.type) }}</v-chip>
    </div>

    <div class="title">{{ item.title }}</div>

    <div v-if="finished" class="step">
      <StatusBadge label="Zakończone" icon="mdi-flag-checkered" color="success" size="small" />
    </div>
    <div v-else-if="item.currentStepStatus" class="step">
      <StatusBadge v-bind="stepStatusPresentation[item.currentStepStatus]" size="small" />
      <span class="fh-muted step-name">{{ item.currentStepName }}</span>
    </div>

    <v-progress-linear :model-value="progress" color="primary" height="5" rounded />

    <div class="bottom fh-muted">
      <span v-if="repoLevels.length" class="dots">
        <v-icon
          v-for="repo in repoLevels"
          :key="repo.repo"
          :icon="healthPresentation[repo.level].icon"
          :color="healthPresentation[repo.level].color"
          :title="`${repo.repo}: ${healthPresentation[repo.level].label}`"
          size="16"
        />
      </span>
      <span v-else :title="item.repositories.join(', ')">
        <v-icon icon="mdi-source-repository" size="16" /> {{ item.repositories.length }}
      </span>
      <span>{{ item.stepsDone }}/{{ item.stepsTotal }} kroków</span>
      <span :title="formatCost(item.usage.cost)">
        <v-icon icon="mdi-lightning-bolt-outline" size="16" /> {{ formatTokens(item.usage.totalTokens) }}
      </span>
    </div>
  </v-card>
</template>

<style scoped>
.work-item-card {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
  padding: 0.9rem;
  cursor: grab;
  border-radius: 14px !important;
}

.work-item-card.waits {
  border-color: rgba(245, 158, 11, 0.7) !important;
  box-shadow: 0 0 0 1px rgba(245, 158, 11, 0.25);
}

.top,
.bottom {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}

.key {
  font-weight: 700;
  font-size: 0.8rem;
  color: #c4b5fd;
}

.title {
  font-weight: 600;
  line-height: 1.3;
}

.step {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  min-width: 0;
}

.step-name {
  font-size: 0.8rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.bottom {
  font-size: 0.75rem;
}
</style>
