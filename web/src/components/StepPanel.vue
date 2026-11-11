<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import type { StepRun } from '@/api/types'
import { formatCost, formatDuration, formatTokens } from '@/shared/format'
import { stepKindPresentation, stepStatusPresentation } from '@/shared/stepPresentation'
import { useWorkItemDetail } from '@/stores/workItemDetail'

import ConsoleOutput from './ConsoleOutput.vue'
import StatusBadge from './StatusBadge.vue'

const props = defineProps<{ step: StepRun }>()
const detail = useWorkItemDetail()

const proposal = ref('')
const model = ref('')
const tokenBudget = ref(0)
const remark = ref('')

// Formularz zaczyna od stanu kroku; odświeża się, gdy krok zmieni się na serwerze.
watch(
  () => [props.step.id, props.step.status, props.step.proposal, props.step.model, props.step.tokenBudget],
  () => {
    proposal.value = props.step.proposal ?? ''
    model.value = props.step.model ?? ''
    tokenBudget.value = props.step.tokenBudget
    remark.value = ''
  },
  { immediate: true },
)

const isAgent = computed(() => props.step.kind === 'Agent')
const isProposed = computed(() => props.step.status === 'Proposed')
const isActive = computed(() => props.step.status === 'Approved' || props.step.status === 'Running')
const isDone = computed(() => ['Succeeded', 'Failed', 'Skipped'].includes(props.step.status))
const output = computed(() => detail.outputOf(props.step))
const permission = computed(() => detail.permissionFor(props.step))

const dirty = computed(
  () =>
    proposal.value !== (props.step.proposal ?? '') ||
    model.value !== (props.step.model ?? '') ||
    tokenBudget.value !== props.step.tokenBudget,
)

const editRequest = () => ({
  proposal: proposal.value,
  model: model.value.trim() || null,
  tokenBudget: Number(tokenBudget.value) || 0,
})

const save = () => detail.edit(props.step, editRequest())

/** Zatwierdzenie zapisuje najpierw niezapisane poprawki — uruchamiamy dokładnie to, co widać. */
async function approve() {
  if (dirty.value) await save()
  await detail.approve(props.step)
}

const approveLabel = computed(() => (props.step.kind === 'Manual' ? 'Potwierdzam — zrobione' : 'Zatwierdź i uruchom'))
</script>

<template>
  <v-card class="step-panel">
    <header class="head">
      <div>
        <h2>{{ step.order }}. {{ step.name }}</h2>
        <div class="fh-muted meta">
          <v-icon :icon="stepKindPresentation[step.kind].icon" size="16" />
          {{ stepKindPresentation[step.kind].label }}
          <template v-if="step.repoName"> · repo {{ step.repoName }}</template>
          <template v-if="step.attempt > 1"> · próba {{ step.attempt }}</template>
        </div>
      </div>
      <StatusBadge v-bind="stepStatusPresentation[step.status]" size="large" />
    </header>

    <v-alert v-if="step.remark" type="info" variant="tonal" density="compact" title="Uwaga do tej próby">
      {{ step.remark }}
    </v-alert>

    <!-- Czeka na wcześniejsze kroki -->
    <p v-if="step.status === 'Pending'" class="fh-muted">
      Ten krok poczeka, aż skończą się wcześniejsze. Propozycja pojawi się tutaj.
    </p>

    <!-- Propozycja do decyzji -->
    <template v-if="isProposed">
      <v-textarea
        v-model="proposal"
        :label="isAgent ? 'Prompt dla agenta (możesz edytować)' : 'Co zrobi ten krok'"
        :readonly="!isAgent"
        rows="6"
        auto-grow
        hide-details
        class="proposal"
      />

      <div v-if="isAgent" class="row">
        <v-text-field v-model="model" label="Model (np. tier:cheap albo id modelu)" hide-details />
        <v-text-field v-model.number="tokenBudget" label="Budżet tokenów (0 = bez limitu)" type="number" min="0" hide-details />
      </div>

      <div class="actions">
        <v-btn color="primary" variant="flat" prepend-icon="mdi-check" :loading="detail.busy" @click="approve">
          {{ approveLabel }}
        </v-btn>
        <v-btn v-if="dirty" variant="tonal" prepend-icon="mdi-content-save-outline" :disabled="detail.busy" @click="save">
          Zapisz zmiany
        </v-btn>
        <v-spacer />
        <v-btn variant="text" prepend-icon="mdi-debug-step-over" :disabled="detail.busy" @click="detail.skip(step)">
          Pomiń
        </v-btn>
      </div>
    </template>

    <!-- W trakcie -->
    <template v-if="isActive">
      <v-alert v-if="permission" type="warning" variant="tonal" prominent title="Agent prosi o zgodę" class="permission">
        <div>
          Narzędzie: <strong>{{ permission.tool }}</strong>
        </div>
        <div class="pre">{{ permission.detail }}</div>
        <div class="actions mt-3">
          <v-btn color="success" variant="flat" prepend-icon="mdi-check" @click="detail.decide(permission, true)">Zezwól</v-btn>
          <v-btn color="error" variant="tonal" prepend-icon="mdi-close" @click="detail.decide(permission, false)">Odmów</v-btn>
        </div>
      </v-alert>

      <ConsoleOutput :text="output" running />
      <div class="actions">
        <v-btn color="error" variant="tonal" prepend-icon="mdi-stop" @click="detail.cancel(step)">Przerwij</v-btn>
      </div>
    </template>

    <!-- Wynik -->
    <template v-if="isDone">
      <v-alert v-if="step.status === 'Failed'" type="error" variant="tonal" title="Krok się nie udał">
        <div class="pre">{{ step.error }}</div>
      </v-alert>
      <v-alert v-else-if="step.status === 'Succeeded'" type="success" variant="tonal" title="Wynik">
        <div class="pre">{{ step.result }}</div>
      </v-alert>
      <v-alert v-else type="info" variant="tonal">Krok pominięty.</v-alert>

      <div v-if="step.startedAt" class="fh-muted meta">
        Czas: {{ formatDuration(step.startedAt, step.finishedAt) }}
        <template v-if="isAgent">
          · tokeny: {{ formatTokens(step.usage.totalTokens) }} · koszt: {{ formatCost(step.usage.cost) }}
          <template v-if="step.usage.model"> · model: {{ step.usage.model }}</template>
        </template>
      </div>

      <v-expansion-panels v-if="output" variant="accordion">
        <v-expansion-panel title="Pełne wyjście kroku">
          <v-expansion-panel-text><ConsoleOutput :text="output" /></v-expansion-panel-text>
        </v-expansion-panel>
      </v-expansion-panels>

      <div class="retry">
        <v-text-field v-model="remark" label="Uwaga do powtórki (opcjonalnie)" hide-details />
        <v-btn variant="tonal" color="primary" prepend-icon="mdi-replay" :disabled="detail.busy" @click="detail.retry(step, remark)">
          Powtórz
        </v-btn>
      </div>
    </template>
  </v-card>
</template>

<style scoped>
.step-panel {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  padding: 1.5rem;
}

.head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
}

h2 {
  margin: 0;
  font-size: 1.3rem;
}

.meta {
  font-size: 0.85rem;
  margin-top: 0.2rem;
}

.proposal :deep(textarea) {
  font-family: 'Cascadia Code', Consolas, monospace;
  font-size: 0.9rem;
}

.row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
}

.actions,
.retry {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.pre {
  white-space: pre-wrap;
  word-break: break-word;
}
</style>
