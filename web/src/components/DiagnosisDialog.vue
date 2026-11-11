<script setup lang="ts">
import { ref, watch } from 'vue'

import { diagnose, type Diagnosis } from '@/api/repositories'
import { useNotifications } from '@/stores/notifications'

/**
 * Diagnostyka połączeń dla jednego repo i brancha: jakie adresy FlowHub zapytał i co odpowiedział serwer.
 * Pomaga ustalić, dlaczego np. nie widać PR albo builda (zła nazwa repo, projekt, branch, token).
 */
const props = defineProps<{ target: { name: string; branch: string } | null }>()
const emit = defineEmits<{ close: [] }>()

const notifications = useNotifications()
const result = ref<Diagnosis | null>(null)
const loading = ref(false)

watch(
  () => props.target,
  async (target) => {
    result.value = null
    if (!target) return

    loading.value = true
    try {
      result.value = await diagnose(target.name, target.branch)
    } catch (error) {
      notifications.error(error)
      emit('close')
    } finally {
      loading.value = false
    }
  },
)

const copy = () =>
  navigator.clipboard.writeText(
    [
      `Repo: ${props.target?.name}, branch: ${props.target?.branch}`,
      `PR: ${result.value?.pullRequest ?? '—'} | Build: ${result.value?.build ?? '—'} | Release: ${result.value?.release ?? '—'}`,
      result.value?.error ? `Błąd: ${result.value.error}` : '',
      ...(result.value?.calls.map((call) => `${call.result} (${call.milliseconds} ms)  ${call.url}`) ?? []),
    ]
      .filter(Boolean)
      .join('\n'),
  )
</script>

<template>
  <v-dialog :model-value="target !== null" max-width="1000" scrollable @update:model-value="emit('close')">
    <v-card v-if="target" :title="`Diagnostyka: ${target.name} · ${target.branch}`" prepend-icon="mdi-stethoscope">
      <v-card-text>
        <v-progress-linear v-if="loading" indeterminate color="primary" />

        <template v-if="result">
          <v-alert v-if="result.error" type="error" variant="tonal" density="compact" class="mb-3" title="Zapytanie się nie powiodło">
            {{ result.error }}
          </v-alert>
          <p class="summary">
            PR: <strong>{{ result.pullRequest ?? '—' }}</strong> · Build: <strong>{{ result.build ?? '—' }}</strong> · Release:
            <strong>{{ result.release ?? '—' }}</strong>
          </p>

          <p v-if="result.calls.length === 0" class="fh-muted">
            Nie było żadnych zapytań do serwerów — wszystkie źródła danych dla tego repo działają na atrapie (zmienisz to w Ustawieniach).
          </p>
          <v-table v-else density="compact">
            <thead>
              <tr>
                <th>Odpowiedź</th>
                <th>Czas</th>
                <th>Adres (bez tokenu)</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(call, index) in result.calls" :key="index">
                <td :class="call.result.startsWith('200') ? 'text-success' : 'text-warning'">{{ call.result }}</td>
                <td class="fh-muted">{{ call.milliseconds }} ms</td>
                <td><code class="url">{{ call.url }}</code></td>
              </tr>
            </tbody>
          </v-table>
          <p class="fh-muted hint">
            „404 — nie znaleziono” przy pull requestach zwykle oznacza inną nazwę repo albo projekt w TFS niż w katalogu repo. Brak builda: sprawdź
            id build configuration przy repo albo ustaw źródło buildów „Statusy z TFS”.
          </p>
        </template>
      </v-card-text>
      <v-card-actions>
        <v-btn v-if="result" variant="tonal" prepend-icon="mdi-content-copy" @click="copy">Kopiuj wynik</v-btn>
        <v-spacer />
        <v-btn variant="text" @click="emit('close')">Zamknij</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.summary {
  margin: 0 0 1rem;
}

.url {
  font-size: 0.78rem;
  word-break: break-all;
}

.hint {
  margin: 1rem 0 0;
  font-size: 0.8rem;
}
</style>
