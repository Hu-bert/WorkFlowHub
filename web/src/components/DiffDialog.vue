<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import { fetchDiff, type RepoDiff } from '@/api/integrations'
import { useNotifications } from '@/stores/notifications'

/** Podgląd zmian w repo zadania względem brancha domyślnego (razem z niezacommitowanymi). */
const props = defineProps<{ workItemId: string; repo: string | null }>()
const emit = defineEmits<{ close: [] }>()

const notifications = useNotifications()
const diff = ref<RepoDiff | null>(null)

watch(
  () => props.repo,
  async (repo) => {
    diff.value = null
    if (!repo) return

    try {
      diff.value = await fetchDiff(props.workItemId, repo)
    } catch (error) {
      notifications.error(error)
      emit('close')
    }
  },
)

const lineClass = (line: string) => {
  if (line.startsWith('+++') || line.startsWith('---') || line.startsWith('diff ')) return 'file'
  if (line.startsWith('@@')) return 'hunk'
  if (line.startsWith('+')) return 'added'
  if (line.startsWith('-')) return 'removed'
  return ''
}

const lines = computed(() => diff.value?.patch.split('\n') ?? [])
</script>

<template>
  <v-dialog :model-value="repo !== null" max-width="1200" scrollable @update:model-value="$emit('close')">
    <v-card :title="`Zmiany: ${repo}`" prepend-icon="mdi-file-compare">
      <v-card-text v-if="diff">
        <p class="fh-muted">Względem brancha <code>{{ diff.baseBranch }}</code>, razem ze zmianami niezacommitowanymi.</p>

        <pre v-if="diff.stat" class="stat">{{ diff.stat }}</pre>
        <p v-else class="fh-muted">Brak zmian w śledzonych plikach.</p>

        <v-alert v-if="diff.untrackedFiles.length" type="info" variant="tonal" density="compact" title="Nowe pliki (nieśledzone)">
          <div v-for="file in diff.untrackedFiles" :key="file">{{ file }}</div>
        </v-alert>

        <v-alert v-if="diff.truncated" type="warning" variant="tonal" density="compact" class="mt-3">
          Diff jest bardzo duży — pokazuję początek. Całość zobaczysz w VS Code.
        </v-alert>

        <div v-if="diff.patch" class="patch">
          <div v-for="(line, index) in lines" :key="index" class="line" :class="lineClass(line)">{{ line || ' ' }}</div>
        </div>
      </v-card-text>
      <v-card-text v-else><v-progress-linear indeterminate color="primary" /></v-card-text>

      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="$emit('close')">Zamknij</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.stat,
.patch {
  font-family: 'Cascadia Code', Consolas, monospace;
  font-size: 0.82rem;
  line-height: 1.5;
}

.stat {
  margin: 0 0 1rem;
  white-space: pre-wrap;
}

.patch {
  margin-top: 1rem;
  border: 1px solid var(--fh-border);
  border-radius: 12px;
  background: #0b0916;
  overflow-x: auto;
  padding: 0.5rem 0;
}

.line {
  white-space: pre;
  padding: 0 1rem;
}

.added {
  background: rgba(34, 197, 94, 0.14);
  color: #86efac;
}

.removed {
  background: rgba(244, 63, 94, 0.14);
  color: #fda4af;
}

.hunk {
  color: #a78bfa;
}

.file {
  color: #e9d5ff;
  font-weight: 700;
}
</style>
