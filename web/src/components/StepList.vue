<script setup lang="ts">
import type { StepRun } from '@/api/types'
import { stepKindPresentation, stepStatusPresentation } from '@/shared/stepPresentation'

defineProps<{ steps: StepRun[]; selectedId: string | null }>()
defineEmits<{ select: [step: StepRun] }>()
</script>

<template>
  <v-list density="compact" class="step-list" nav>
    <v-list-item
      v-for="step in steps"
      :key="step.id"
      :active="step.id === selectedId"
      color="primary"
      rounded="lg"
      @click="$emit('select', step)"
    >
      <template #prepend>
        <v-icon
          :icon="stepStatusPresentation[step.status].icon"
          :color="stepStatusPresentation[step.status].color"
          :title="stepStatusPresentation[step.status].label"
        />
      </template>

      <v-list-item-title :class="{ waits: step.status === 'Proposed' }">{{ step.order }}. {{ step.name }}</v-list-item-title>
      <v-list-item-subtitle>
        <v-icon :icon="stepKindPresentation[step.kind].icon" size="14" />
        {{ stepKindPresentation[step.kind].label }}
        <template v-if="step.repoName"> · {{ step.repoName }}</template>
        · {{ stepStatusPresentation[step.status].label }}
      </v-list-item-subtitle>
    </v-list-item>
  </v-list>
</template>

<style scoped>
.step-list {
  background: transparent;
}

.waits {
  font-weight: 700;
}
</style>
