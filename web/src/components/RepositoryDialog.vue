<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'

import {
  createRepository,
  fetchCiConfigurations,
  fetchDeployProjects,
  updateRepository,
  type CiConfiguration,
  type DeployProject,
} from '@/api/repositories'
import type { CodeRepository } from '@/api/types'
import { useNotifications } from '@/stores/notifications'

/**
 * Wpis katalogu repo: skąd klonować i jak repo nazywa się w TeamCity i Octopus.
 * Listy podpowiedzi pochodzą z serwerów (gdy są podłączone); zawsze można też wpisać wartość ręcznie.
 */
const props = defineProps<{ repository: CodeRepository | null; isNew: boolean }>()
const emit = defineEmits<{ close: []; saved: [repository: CodeRepository] }>()

const notifications = useNotifications()
const form = reactive<CodeRepository>({ name: '', url: '', defaultBranch: 'main', ciBuildConfigurations: [], deployProject: null, keywords: [], processes: [] })
const saving = ref(false)
const configurations = ref<CiConfiguration[]>([])
const projects = ref<DeployProject[]>([])

const configurationItems = computed(() =>
  configurations.value.map((item) => ({ title: item.project ? `${item.id} — ${item.project} / ${item.name}` : item.id, value: item.id })),
)
const projectItems = computed(() => projects.value.map((item) => ({ title: item.name === item.slug ? item.slug : `${item.name} (${item.slug})`, value: item.slug })))

watch(
  () => props.repository,
  async (repository) => {
    if (!repository) return
    Object.assign(form, JSON.parse(JSON.stringify(repository)) as CodeRepository)

    // Podpowiedzi są dodatkiem — brak połączenia z serwerem nie blokuje edycji.
    const [ci, deploy] = await Promise.allSettled([fetchCiConfigurations(), fetchDeployProjects()])
    configurations.value = ci.status === 'fulfilled' ? ci.value : []
    projects.value = deploy.status === 'fulfilled' ? deploy.value : []
  },
  { immediate: true },
)

const valid = computed(() => form.name.trim().length > 0 && form.url.trim().length > 0)

async function save() {
  if (!props.repository) return
  saving.value = true
  try {
    const processes = form.processes
      .map((process) => ({ host: process.host.trim(), name: process.name.trim(), label: process.label?.trim() || null }))
      .filter((process) => process.host && process.name)
    const payload = { ...form, deployProject: form.deployProject?.trim() || null, processes }
    const saved = props.isNew ? await createRepository(payload) : await updateRepository(props.repository.name, payload)
    notifications.success(`Zapisano repo ${saved.name}`)
    emit('saved', saved)
  } catch (error) {
    notifications.error(error)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <v-dialog :model-value="repository !== null" max-width="820" scrollable @update:model-value="emit('close')">
    <v-card v-if="repository" :title="isNew ? 'Nowe repo w katalogu' : `Repo: ${repository.name}`" prepend-icon="mdi-source-repository">
      <v-card-text class="form">
        <div class="row">
          <v-text-field v-model="form.name" label="Nazwa" hint="Tak będzie się nazywał katalog klonu w zadaniu." persistent-hint />
          <v-text-field v-model="form.defaultBranch" label="Branch domyślny" hint="Od niego odchodzi branch zadania (np. master)." persistent-hint />
        </div>
        <v-text-field v-model="form.url" label="Adres repo w TFS" hint="Adres klonu, np. https://tfs.firma.pl/tfs/Kolekcja/Projekt/_git/Repo. Z niego FlowHub bierze też projekt TFS." persistent-hint />
        <v-combobox
          v-model="form.ciBuildConfigurations"
          :items="configurationItems"
          item-title="title"
          item-value="value"
          :return-object="false"
          label="Build configuration w TeamCity (id)"
          hint="Potrzebne tylko przy źródle buildów „TeamCity”. Przy „Statusach z TFS” zostaw puste."
          persistent-hint
          multiple
          chips
          closable-chips
        />
        <v-combobox
          v-model="form.deployProject"
          :items="projectItems"
          item-title="title"
          item-value="value"
          :return-object="false"
          label="Projekt w Octopus (slug)"
          hint="Puste = taki sam jak nazwa repo."
          persistent-hint
          clearable
        />
        <v-combobox
          v-model="form.keywords"
          label="Słowa kluczowe"
          hint="Po nich „Zaproponuj” w nowym zadaniu rozpoznaje repo z treści zadania. Enter dodaje słowo."
          persistent-hint
          multiple
          chips
          closable-chips
        />

        <div class="processes">
          <div class="processes-head">
            <strong>Procesy / usługi na maszynach</strong>
            <v-btn size="small" variant="tonal" prepend-icon="mdi-plus" @click="form.processes.push({ host: '', name: '', label: null })">Dodaj</v-btn>
          </div>
          <p class="fh-muted">
            Po wdrożeniu FlowHub sprawdzi, czy proces albo usługa o tej nazwie działa na maszynie (zakładka „Procesy”). Nazwa = początek nazwy z Menedżera
            zadań, bez .exe.
          </p>
          <div v-for="(process, index) in form.processes" :key="index" class="process">
            <v-text-field v-model="process.host" label="Maszyna" placeholder="v-app-02" hide-details />
            <v-text-field v-model="process.name" label="Nazwa procesu / usługi" hide-details />
            <v-text-field v-model="process.label" label="Etykieta (np. środowisko)" hide-details />
            <v-btn variant="text" size="small" icon="mdi-delete-outline" title="Usuń" @click="form.processes.splice(index, 1)" />
          </div>
        </div>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" :disabled="saving" @click="emit('close')">Anuluj</v-btn>
        <v-btn color="primary" variant="flat" prepend-icon="mdi-content-save-outline" :disabled="!valid" :loading="saving" @click="save">Zapisz</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.form {
  display: flex;
  flex-direction: column;
  gap: 1.1rem;
}

.row {
  display: grid;
  grid-template-columns: 2fr 1fr;
  gap: 1rem;
}

.processes-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.processes p {
  margin: 0.25rem 0 0.6rem;
  font-size: 0.8rem;
}

.process {
  display: grid;
  grid-template-columns: 1fr 1.4fr 1fr auto;
  gap: 0.6rem;
  align-items: center;
  margin-bottom: 0.5rem;
}
</style>
