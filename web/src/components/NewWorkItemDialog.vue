<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'

import { suggestRepositories } from '@/api/catalog'
import { addRemoteRepository, fetchRemoteRepositories, type RemoteRepository } from '@/api/repositories'
import { fetchTaskFromTracker } from '@/api/integrations'
import type { CreateWorkItemRequest, WorkItemDetails } from '@/api/types'
import { useCatalog } from '@/stores/catalog'
import { useNotifications } from '@/stores/notifications'
import { useWorkItems } from '@/stores/workItems'

const open = defineModel<boolean>({ required: true })
const emit = defineEmits<{ created: [item: WorkItemDetails] }>()

const catalog = useCatalog()
const workItems = useWorkItems()
const notifications = useNotifications()

const emptyForm = (): CreateWorkItemRequest => ({
  key: '',
  title: '',
  description: '',
  type: catalog.flows[0]?.type ?? '',
  source: 'Manual',
  sourceUrl: null,
  repositories: [],
})

const form = reactive<CreateWorkItemRequest>(emptyForm())
const saving = ref(false)
const suggestionNote = ref('')

const flowOptions = computed(() => catalog.flows.map((flow) => ({ title: flow.name, value: flow.type })))
const repositoryOptions = computed(() => catalog.repositories.map((repository) => repository.name))
const canSave = computed(() => !!form.key.trim() && !!form.title.trim() && !!form.type && form.repositories.length > 0)

/** Repozytoria z serwera kodu (TFS), których nie ma jeszcze w katalogu — do dodania jednym wyborem. */
const remote = ref<RemoteRepository[]>([])
const remoteNote = ref('')
const remoteOptions = computed(() =>
  remote.value.filter((repository) => !catalog.repositories.some((known) => known.name === repository.name)).map((repository) => repository.name),
)

async function loadRemote() {
  try {
    remote.value = await fetchRemoteRepositories()
    remoteNote.value = ''
  } catch (error) {
    remote.value = []
    remoteNote.value = `Lista z TFS niedostępna: ${error instanceof Error ? error.message : error}`
  }
}

/** Wybrane repo z TFS trafia do katalogu (repos.yaml) i od razu do zadania. */
async function pickRemote(name: string | null) {
  if (!name) return
  try {
    const added = await addRemoteRepository(name)
    await catalog.load(true)
    form.repositories = [...new Set([...form.repositories, added.name])]
    notifications.success(`Dodano ${added.name} z TFS do katalogu repo`)
  } catch (error) {
    notifications.error(error)
  }
}

watch(open, (isOpen) => {
  if (isOpen) {
    Object.assign(form, emptyForm())
    suggestionNote.value = ''
    void loadRemote()
  }
})

const fetching = ref(false)

/** Nowa Jira ma API — tytuł, treść i link wypełniają się same. Zadania ze starej Jiry wpisujesz ręcznie. */
async function fetchFromTracker() {
  fetching.value = true
  try {
    const task = await fetchTaskFromTracker(form.key.trim())
    Object.assign(form, { title: task.title, description: task.description, sourceUrl: task.url, source: 'Tracker' })
  } catch (error) {
    notifications.error(error)
  } finally {
    fetching.value = false
  }
}

/** Rozpoznanie repo po słowach kluczowych z katalogu — bez modelu, 0 tokenów. */
async function suggest() {
  try {
    const suggestions = await suggestRepositories(`${form.title} ${form.description}`)
    form.repositories = [...new Set([...form.repositories, ...suggestions.map((suggestion) => suggestion.name)])]
    suggestionNote.value = suggestions.length
      ? suggestions.map((s) => `${s.name} (${s.matchedKeywords.join(', ')})`).join(' · ')
      : 'Nie znalazłem słów kluczowych z katalogu repo — wybierz ręcznie.'
  } catch (error) {
    notifications.error(error)
  }
}

async function save() {
  saving.value = true
  try {
    const created = await workItems.create({ ...form, sourceUrl: form.sourceUrl?.trim() || null })
    notifications.success(`Utworzono zadanie ${created.summary.key}`)
    open.value = false
    emit('created', created)
  } catch (error) {
    notifications.error(error)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <v-dialog v-model="open" max-width="720">
    <v-card title="Nowe zadanie" prepend-icon="mdi-plus-circle-outline">
      <v-card-text class="form">
        <div class="row key-row">
          <v-text-field v-model="form.key" label="Klucz (np. PROJ-123)" autofocus hide-details />
          <v-btn
            variant="tonal"
            color="primary"
            prepend-icon="mdi-cloud-download-outline"
            :disabled="!form.key.trim()"
            :loading="fetching"
            title="Nowa Jira: pobierz tytuł i treść przez API. Stara Jira: wklej treść ręcznie."
            @click="fetchFromTracker"
          >
            Pobierz
          </v-btn>
          <v-select v-model="form.type" :items="flowOptions" label="Typ zadania" hide-details />
        </div>
        <v-text-field v-model="form.title" label="Tytuł" hide-details />
        <v-textarea
          v-model="form.description"
          label="Treść zadania (wklej z Jiry)"
          rows="5"
          auto-grow
          hide-details
        />
        <v-text-field v-model="form.sourceUrl" label="Link do zadania (opcjonalnie)" hide-details />

        <div class="row repos">
          <v-select
            v-model="form.repositories"
            :items="repositoryOptions"
            label="Repozytoria (pierwsze = główne)"
            multiple
            chips
            closable-chips
            hide-details
          />
          <v-btn variant="tonal" color="primary" prepend-icon="mdi-auto-fix" @click="suggest">Zaproponuj</v-btn>
        </div>
        <div v-if="suggestionNote" class="fh-muted note">{{ suggestionNote }}</div>

        <v-autocomplete
          :model-value="null"
          :items="remoteOptions"
          label="Dodaj repo z TFS (spoza katalogu)"
          prepend-inner-icon="mdi-source-pull"
          :hint="remoteNote || 'Wybrane repo zostanie dopisane do repos.yaml i dodane do zadania.'"
          persistent-hint
          :no-data-text="remoteNote ? 'Brak połączenia z TFS' : 'Wszystkie repo z TFS są już w katalogu'"
          @update:model-value="pickRemote"
        />
      </v-card-text>

      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="open = false">Anuluj</v-btn>
        <v-btn color="primary" variant="flat" :disabled="!canSave" :loading="saving" @click="save">Utwórz</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
}

.row.key-row {
  grid-template-columns: 1fr auto 1fr;
  align-items: center;
}

.row.repos {
  grid-template-columns: 1fr auto;
  align-items: center;
}

.note {
  font-size: 0.8rem;
  margin-top: -0.5rem;
}
</style>
