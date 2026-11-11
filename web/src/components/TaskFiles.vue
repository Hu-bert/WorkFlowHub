<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import { fetchFile, fetchFiles, saveFile } from '@/api/agent'
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

/** Tekst w edytorze; null = tryb podglądu. */
const draft = ref<string | null>(null)
const saving = ref(false)
const dirty = computed(() => draft.value !== null && draft.value !== opened.value?.content)

async function open(file: WorkItemFile) {
  try {
    opened.value = await fetchFile(props.workItemId, file.name)
    draft.value = null
  } catch (error) {
    notifications.error(error)
  }
}

/** Kolejne kroki agenta czytają te pliki z dysku, więc poprawka (np. w plan.md) działa od następnego kroku. */
async function save() {
  if (!opened.value || draft.value === null) return
  saving.value = true
  try {
    await saveFile(props.workItemId, opened.value.name, draft.value)
    opened.value = { ...opened.value, content: draft.value }
    draft.value = null
    notifications.success(`Zapisano ${opened.value.name}`)
  } catch (error) {
    notifications.error(error)
  } finally {
    saving.value = false
  }
}

function close() {
  if (dirty.value && !window.confirm('Masz niezapisane zmiany w pliku. Zamknąć bez zapisu?')) return
  opened.value = null
  draft.value = null
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

    <v-dialog :model-value="opened !== null" max-width="960" scrollable :persistent="dirty" @update:model-value="close">
      <v-card v-if="opened" :title="opened.name" prepend-icon="mdi-file-document-outline">
        <v-card-text>
          <v-textarea v-if="draft !== null" v-model="draft" rows="22" hide-details spellcheck="false" class="editor" />
          <pre v-else class="content">{{ opened.content }}</pre>
        </v-card-text>
        <v-card-actions>
          <span v-if="draft !== null" class="fh-muted note">Kolejne kroki agenta przeczytają poprawioną wersję.</span>
          <v-spacer />
          <template v-if="draft !== null">
            <v-btn variant="text" :disabled="saving" @click="draft = null">Anuluj</v-btn>
            <v-btn color="primary" variant="flat" prepend-icon="mdi-content-save-outline" :disabled="!dirty" :loading="saving" @click="save">
              Zapisz
            </v-btn>
          </template>
          <template v-else>
            <v-btn variant="tonal" prepend-icon="mdi-pencil-outline" @click="draft = opened.content">Edytuj</v-btn>
            <v-btn variant="text" @click="close">Zamknij</v-btn>
          </template>
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

.editor :deep(textarea) {
  font-family: 'Cascadia Code', Consolas, monospace;
  font-size: 0.9rem;
  line-height: 1.55;
}

.note {
  font-size: 0.8rem;
  padding-left: 0.5rem;
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
