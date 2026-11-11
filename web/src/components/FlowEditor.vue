<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import { emptyStep, fetchFlowDocument, saveFlow, type BlockFile, type FlowDocument } from '@/api/flowEditor'
import type { StepKind } from '@/api/types'
import { stepKindPresentation } from '@/shared/stepPresentation'
import { useCatalog } from '@/stores/catalog'
import { useNotifications } from '@/stores/notifications'

import StepEditor from './StepEditor.vue'

/** Edytor flow: kolejność kroków strzałkami, edycja kroku w miejscu, zapis do YAML po walidacji. */
const props = defineProps<{ blocks: BlockFile[] }>()

const catalog = useCatalog()
const notifications = useNotifications()

const selectedType = ref<string | null>(null)
const document = ref<FlowDocument | null>(null)
const isNew = ref(false)
const openStep = ref<number | null>(null)
const errors = ref<string[]>([])
const saving = ref(false)

const flowOptions = computed(() => catalog.flows.map((flow) => ({ title: `${flow.name} (${flow.type})`, value: flow.type })))
const kinds = Object.keys(stepKindPresentation) as StepKind[]

watch(
  () => catalog.flows.length,
  () => (selectedType.value ??= catalog.flows[0]?.type ?? null),
  { immediate: true },
)

watch(selectedType, async (type) => {
  if (!type || isNew.value) return
  try {
    document.value = await fetchFlowDocument(type)
    errors.value = []
    openStep.value = null
  } catch (error) {
    notifications.error(error)
  }
})

function startNew() {
  isNew.value = true
  selectedType.value = null
  document.value = { type: '', name: '', version: 1, flow: [] }
  errors.value = []
}

function move(index: number, offset: -1 | 1) {
  const steps = document.value!.flow
  const target = index + offset
  if (target < 0 || target >= steps.length) return

  ;[steps[index], steps[target]] = [steps[target]!, steps[index]!]
  openStep.value = null
}

function remove(index: number) {
  document.value!.flow.splice(index, 1)
  openStep.value = null
}

function add(kind: StepKind) {
  document.value!.flow.push(emptyStep(kind))
  openStep.value = document.value!.flow.length - 1
}

async function save() {
  if (!document.value) return
  saving.value = true
  try {
    const response = await saveFlow(document.value)
    errors.value = response.errors

    if (response.saved) {
      await catalog.load(true)
      isNew.value = false
      selectedType.value = document.value.type
      notifications.success(`Zapisano flow „${document.value.name}” do flows/`)
    }
  } catch (error) {
    notifications.error(error)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="flow-editor">
    <div class="toolbar">
      <v-select v-if="!isNew" v-model="selectedType" :items="flowOptions" label="Typ zadania" hide-details class="flow-select" />
      <v-btn variant="tonal" prepend-icon="mdi-plus" @click="startNew">Nowy typ zadania</v-btn>
      <v-spacer />
      <v-btn color="primary" prepend-icon="mdi-content-save-outline" :loading="saving" :disabled="!document" @click="save">
        Zapisz do YAML
      </v-btn>
    </div>

    <v-alert v-if="errors.length" type="error" variant="tonal" title="Flow nie został zapisany — popraw błędy">
      <div v-for="error in errors" :key="error">{{ error }}</div>
    </v-alert>

    <template v-if="document">
      <div class="meta">
        <v-text-field v-model="document.type" label="Klucz typu (np. Bug)" :readonly="!isNew" hide-details />
        <v-text-field v-model="document.name" label="Nazwa" hide-details />
      </div>

      <v-card v-for="(step, index) in document.flow" :key="index" class="step" :class="{ open: openStep === index }">
        <div class="step-head" @click="openStep = openStep === index ? null : index">
          <span class="order">{{ index + 1 }}</span>
          <v-icon :icon="stepKindPresentation[step.kind].icon" />
          <div class="step-title">
            <strong>{{ step.name || step.id || '(nowy krok)' }}</strong>
            <span class="fh-muted">
              {{ stepKindPresentation[step.kind].label }}
              <template v-if="step.perRepo"> · per repo</template>
              <template v-if="!step.requiresApproval"> · bez akceptacji</template>
              <template v-if="step.model"> · {{ step.model }}</template>
            </span>
          </div>
          <v-btn icon="mdi-arrow-up" variant="text" size="small" title="W górę" :disabled="index === 0" @click.stop="move(index, -1)" />
          <v-btn
            icon="mdi-arrow-down"
            variant="text"
            size="small"
            title="W dół"
            :disabled="index === document.flow.length - 1"
            @click.stop="move(index, 1)"
          />
          <v-btn icon="mdi-delete-outline" variant="text" size="small" color="error" title="Usuń krok" @click.stop="remove(index)" />
        </div>

        <StepEditor v-if="openStep === index" v-model="document.flow[index]!" :blocks="props.blocks" />
      </v-card>

      <div class="add">
        <span class="fh-muted">Dodaj krok:</span>
        <v-btn v-for="kind in kinds" :key="kind" variant="tonal" size="small" :prepend-icon="stepKindPresentation[kind].icon" @click="add(kind)">
          {{ stepKindPresentation[kind].label }}
        </v-btn>
      </div>
    </template>
  </div>
</template>

<style scoped>
.flow-editor {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.toolbar,
.add {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.flow-select {
  max-width: 360px;
}

.meta {
  display: grid;
  grid-template-columns: 1fr 2fr;
  gap: 1rem;
}

.step {
  padding: 0.5rem 1rem;
}

.step.open {
  border-color: rgb(var(--v-theme-primary)) !important;
}

.step-head {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  cursor: pointer;
}

.step-title {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
  font-size: 0.9rem;
}

.order {
  width: 1.5rem;
  color: var(--fh-muted);
  text-align: right;
}
</style>
