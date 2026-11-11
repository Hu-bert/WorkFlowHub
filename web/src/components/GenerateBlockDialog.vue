<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import { generateBlock, type BlockDraft, type BlockFile, type GeneratedKind } from '@/api/flowEditor'
import { useNotifications } from '@/stores/notifications'

/**
 * Generator bloczków: opisujesz, czego oczekujesz, a agent pisze nowy plik na wzór istniejącego.
 * Wynik to szkic — trafia do edytora i zapisuje się dopiero po Twojej akceptacji.
 */
const open = defineModel<boolean>({ required: true })
const props = defineProps<{ blocks: BlockFile[] }>()
const emit = defineEmits<{ generated: [draft: BlockDraft] }>()

const notifications = useNotifications()

const kinds: { value: GeneratedKind; title: string; file: (name: string) => string }[] = [
  { value: 'skills', title: 'Skill', file: (name) => `skills/${name}/SKILL.md` },
  { value: 'agents', title: 'Agent', file: (name) => `agents/${name}.agent.md` },
  { value: 'prompts', title: 'Prompt kroku', file: (name) => `prompts/${name}.md` },
  { value: 'scripts', title: 'Skrypt PowerShell', file: (name) => `scripts/${name}.ps1` },
]

const kind = ref<GeneratedKind>('skills')
const name = ref('')
const description = ref('')
const examplePath = ref<string | null>(null)
const busy = ref(false)

const examples = computed(() =>
  props.blocks.filter((block) => block.kind === kind.value).map((block) => ({ title: block.name, value: block.path })),
)

// Wzorzec musi być tego samego rodzaju — po zmianie rodzaju wybieramy pierwszy z listy.
watch([kind, examples], () => (examplePath.value = examples.value[0]?.value ?? null), { immediate: true })

const nameValid = computed(() => /^[a-z0-9]+(-[a-z0-9]+)*$/.test(name.value))
const target = computed(() => kinds.find((item) => item.value === kind.value)!.file(name.value || 'nazwa'))
const ready = computed(() => nameValid.value && description.value.trim().length > 0 && examplePath.value !== null)

async function generate() {
  busy.value = true
  try {
    emit('generated', await generateBlock({ kind: kind.value, name: name.value, description: description.value, examplePath: examplePath.value }))
    name.value = ''
    description.value = ''
  } catch (error) {
    notifications.error(error)
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <v-dialog v-model="open" max-width="720" :persistent="busy">
    <v-card title="Generuj nowy bloczek" prepend-icon="mdi-auto-fix">
      <v-card-text class="form">
        <div class="row">
          <v-select v-model="kind" :items="kinds" label="Rodzaj" hide-details />
          <v-text-field
            v-model="name"
            label="Nazwa"
            placeholder="np. przeglad-bezpieczenstwa"
            :error-messages="name && !nameValid ? ['Małe litery, cyfry i myślniki'] : []"
            :hint="`Plik: blocks/${target}`"
            persistent-hint
          />
        </div>

        <v-textarea
          v-model="description"
          label="Czego oczekujesz?"
          placeholder="Np. skill, który sprawdza zmianę pod kątem bezpieczeństwa: sekrety w kodzie, walidacja wejścia, uprawnienia. Wynik jako krótka lista ryzyk z plikami."
          rows="5"
          auto-grow
          hide-details
        />

        <v-select
          v-model="examplePath"
          :items="examples"
          label="Wzoruj się na"
          hint="Nowy bloczek dostanie tę samą strukturę i styl."
          persistent-hint
          :no-data-text="'Brak bloczków tego rodzaju'"
        />
      </v-card-text>
      <v-card-actions>
        <span class="fh-muted note">Powstanie szkic w edytorze. Nic nie zapisuje się bez Twojego „Zapisz”.</span>
        <v-spacer />
        <v-btn variant="text" :disabled="busy" @click="open = false">Anuluj</v-btn>
        <v-btn color="primary" variant="flat" prepend-icon="mdi-auto-fix" :disabled="!ready" :loading="busy" @click="generate">Generuj szkic</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.form {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.row {
  display: grid;
  grid-template-columns: 200px 1fr;
  gap: 1rem;
  align-items: start;
}

.note {
  font-size: 0.8rem;
  padding-left: 0.5rem;
}
</style>
