<script setup lang="ts">
import { ref, watch } from 'vue'

import { fetchFile, fetchFiles } from '@/api/agent'
import type { FileContent, WorkItemFile } from '@/api/types'
import { useNotifications } from '@/stores/notifications'

/** `refreshKey` zmienia się, gdy kończy się krok — wtedy lista plików jest czytana od nowa. */
const props = defineProps<{ workItemId: string; refreshKey: number }>()

const notifications = useNotifications()
const files = ref<WorkItemFile[]>([])
const opened = ref<FileContent | null>(null)

watch(
  () => [props.workItemId, props.refreshKey],
  async () => {
    try {
      files.value = await fetchFiles(props.workItemId)
    } catch (error) {
      notifications.error(error)
    }
  },
  { immediate: true },
)

async function open(file: WorkItemFile) {
  try {
    opened.value = await fetchFile(props.workItemId, file.name)
  } catch (error) {
    notifications.error(error)
  }
}
</script>

<template>
  <v-card title="Pliki zadania" prepend-icon="mdi-file-document-multiple-outline">
    <v-card-text>
      <p v-if="files.length === 0" class="fh-muted mb-0">Pliki pojawią się po pierwszych krokach (task.md, context, plan…).</p>
      <div v-else class="files">
        <v-chip
          v-for="file in files"
          :key="file.name"
          prepend-icon="mdi-file-document-outline"
          variant="tonal"
          color="primary"
          @click="open(file)"
        >
          {{ file.name }}
        </v-chip>
      </div>
    </v-card-text>

    <v-dialog :model-value="opened !== null" max-width="960" scrollable @update:model-value="opened = null">
      <v-card v-if="opened" :title="opened.name" prepend-icon="mdi-file-document-outline">
        <v-card-text><pre class="content">{{ opened.content }}</pre></v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="opened = null">Zamknij</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-card>
</template>

<style scoped>
.files {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.content {
  margin: 0;
  white-space: pre-wrap;
  word-break: break-word;
  font-family: 'Cascadia Code', Consolas, monospace;
  font-size: 0.9rem;
  line-height: 1.55;
}
</style>
