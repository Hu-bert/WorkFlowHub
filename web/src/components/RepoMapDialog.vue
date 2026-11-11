<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import { deleteRepoMap, fetchRepoMap, saveRepoMap, type RepoMap } from '@/api/repositories'
import { ApiError } from '@/api/http'
import { formatDateTime } from '@/shared/format'
import { useNotifications } from '@/stores/notifications'

/**
 * Mapa repo: zapamiętany wynik analizy technologicznej. Kolejne zadania zaczynają od niej i tylko ją aktualizują,
 * więc nie płacisz tokenami za analizę od zera. Możesz ją tu poprawić albo usunąć (wtedy następna analiza będzie pełna).
 */
const props = defineProps<{ repository: string | null }>()
const emit = defineEmits<{ close: [] }>()

const notifications = useNotifications()
const map = ref<RepoMap | null>(null)
const missing = ref(false)
const draft = ref('')
const saving = ref(false)
const dirty = computed(() => map.value !== null && draft.value !== map.value.content)

watch(
  () => props.repository,
  async (repository) => {
    map.value = null
    missing.value = false
    if (!repository) return

    try {
      map.value = await fetchRepoMap(repository)
      draft.value = map.value.content
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) missing.value = true
      else notifications.error(error)
    }
  },
)

async function save() {
  if (!props.repository) return
  saving.value = true
  try {
    map.value = await saveRepoMap(props.repository, draft.value)
    notifications.success('Zapisano mapę repo')
  } catch (error) {
    notifications.error(error)
  } finally {
    saving.value = false
  }
}

async function remove() {
  if (!props.repository || !window.confirm('Usunąć mapę? Następna analiza tego repo zrobi ją od zera.')) return
  try {
    await deleteRepoMap(props.repository)
    notifications.success('Usunięto mapę repo')
    emit('close')
  } catch (error) {
    notifications.error(error)
  }
}
</script>

<template>
  <v-dialog :model-value="repository !== null" max-width="960" scrollable @update:model-value="emit('close')">
    <v-card v-if="repository" :title="`Mapa repo: ${repository}`" prepend-icon="mdi-map-outline">
      <v-card-text>
        <p v-if="missing" class="fh-muted">
          To repo nie ma jeszcze mapy. Powstanie sama po pierwszym kroku „Analiza technologiczna” w zadaniu z tym repo — a kolejne zadania będą ją tylko
          aktualizować.
        </p>
        <template v-else-if="map">
          <p class="fh-muted meta">
            Zapisana {{ formatDateTime(map.savedAt) }}<template v-if="map.taskKey"> · z zadania {{ map.taskKey }}</template
            ><template v-if="map.commit"> · stan repo: commit <code>{{ map.commit }}</code></template>
          </p>
          <v-textarea v-model="draft" rows="22" hide-details spellcheck="false" class="editor" />
        </template>
      </v-card-text>
      <v-card-actions>
        <v-btn v-if="map" variant="text" color="error" prepend-icon="mdi-delete-outline" @click="remove">Usuń mapę</v-btn>
        <v-spacer />
        <v-btn variant="text" @click="emit('close')">Zamknij</v-btn>
        <v-btn v-if="map" color="primary" variant="flat" prepend-icon="mdi-content-save-outline" :disabled="!dirty" :loading="saving" @click="save">Zapisz</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.meta {
  margin: 0 0 0.75rem;
  font-size: 0.85rem;
}

.editor :deep(textarea) {
  font-family: 'Cascadia Code', Consolas, monospace;
  font-size: 0.9rem;
  line-height: 1.55;
}
</style>