<script setup lang="ts">
import { computed } from 'vue'

import type { BlockFile, StepDocument } from '@/api/flowEditor'
import { stepKindPresentation } from '@/shared/stepPresentation'

/** Formularz jednego kroku flow. Pokazuje tylko pola, które mają sens dla danego rodzaju kroku. */
const step = defineModel<StepDocument>({ required: true })
const props = defineProps<{ blocks: BlockFile[] }>()

const paths = (kind: BlockFile['kind']) => props.blocks.filter((block) => block.kind === kind).map((block) => block.path)
const agentNames = computed(() => props.blocks.filter((block) => block.kind === 'agents').map((block) => block.name))

const kinds = Object.entries(stepKindPresentation).map(([value, kind]) => ({ value, title: kind.label }))
const checks = ['pull-request', 'build', 'deployment']
const tiers = ['tier:cheap', 'tier:standard', 'tier:premium']
const commonTools = ['read', 'write', 'shell(git:*)', 'shell(dotnet:*)', 'shell(npm:*)']

/** Listy w YAML edytujemy jako tekst: jedna pozycja w linii. */
const lines = (key: 'checklist' | 'inputs' | 'outputs') =>
  computed({
    get: () => step.value[key].join('\n'),
    set: (text: string) => (step.value[key] = text.split('\n').map((line) => line.trim()).filter(Boolean)),
  })

const checklist = lines('checklist')
const inputs = lines('inputs')
const outputs = lines('outputs')
</script>

<template>
  <div class="editor">
    <div class="row">
      <v-text-field v-model="step.id" label="Id (małe litery i myślniki)" hide-details />
      <v-text-field v-model="step.name" label="Nazwa" hide-details />
      <v-select v-model="step.kind" :items="kinds" label="Rodzaj" hide-details />
    </div>

    <v-combobox v-if="step.kind === 'Script'" v-model="step.script" :items="paths('scripts')" label="Skrypt (blocks/…)" hide-details />

    <template v-if="step.kind === 'Agent'">
      <div class="row">
        <v-combobox v-model="step.agent" :items="agentNames" label="Agent" hide-details />
        <v-combobox v-model="step.prompt" :items="paths('prompts')" label="Szablon promptu" hide-details />
      </div>
      <div class="row">
        <v-combobox v-model="step.model" :items="tiers" label="Model" hide-details />
        <v-text-field v-model.number="step.tokenBudget" label="Budżet tokenów" type="number" min="0" hide-details />
      </div>
      <v-combobox v-model="step.allowedTools" :items="commonTools" label="Dozwolone narzędzia" multiple chips closable-chips hide-details />
      <div class="row">
        <v-textarea v-model="inputs" label="Pliki wejściowe (jeden w linii)" rows="3" auto-grow hide-details />
        <v-textarea v-model="outputs" label="Pliki wynikowe (jeden w linii)" rows="3" auto-grow hide-details />
      </div>
    </template>

    <v-textarea v-if="step.kind === 'Manual'" v-model="checklist" label="Checklista (jeden punkt w linii)" rows="3" auto-grow hide-details />
    <v-select v-if="step.kind === 'Check'" v-model="step.check" :items="checks" label="Sprawdzenie" hide-details />

    <div class="switches">
      <v-switch v-model="step.requiresApproval" label="Wymaga mojej akceptacji" color="primary" density="compact" hide-details />
      <v-switch v-model="step.perRepo" label="Osobno dla każdego repo" color="primary" density="compact" hide-details />
    </div>
  </div>
</template>

<style scoped>
.editor {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  padding: 0.5rem 0 1rem;
}

.row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
}

.switches {
  display: flex;
  gap: 2rem;
  flex-wrap: wrap;
}
</style>
