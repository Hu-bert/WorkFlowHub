<script setup lang="ts">
import { computed } from 'vue'

import { formatTokens } from '@/shared/format'

/** Zużycie budżetu tokenów: żółty od 80%, czerwony od 100% — zawsze z tekstem, nie tylko kolorem. */
const props = defineProps<{ used: number; limit: number; label: string }>()

const percent = computed(() => (props.limit > 0 ? Math.round((props.used / props.limit) * 100) : 0))
const color = computed(() => (percent.value >= 100 ? 'error' : percent.value >= 80 ? 'warning' : 'primary'))
const note = computed(() => (percent.value >= 100 ? ' — budżet przekroczony' : percent.value >= 80 ? ' — uwaga, ponad 80%' : ''))
</script>

<template>
  <div v-if="limit > 0" class="budget">
    <div class="text">
      <span>{{ label }}: {{ formatTokens(used) }} / {{ formatTokens(limit) }} ({{ percent }}%){{ note }}</span>
    </div>
    <v-progress-linear :model-value="Math.min(100, percent)" :color="color" height="6" rounded />
  </div>
</template>

<style scoped>
.budget {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}

.text {
  font-size: 0.8rem;
  color: var(--fh-muted);
}
</style>
