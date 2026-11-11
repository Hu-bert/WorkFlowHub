<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'

import { saveSettings, testSettings, type ConnectionStatus, type ConnectionTest, type SettingsModule } from '@/api/settings'
import { formatDateTime } from '@/shared/format'
import { connectionPresentationOf } from '@/stores/connections'
import { useNotifications } from '@/stores/notifications'

/**
 * Formularz jednego modułu ustawień. Pola rysują się z opisu modułu z serwera — nowy moduł nie wymaga zmian w UI.
 * Na górze zawsze widać stan: atrapa / działa / nie działa oraz czy token jest zapisany.
 */
const props = defineProps<{ module: SettingsModule; status: ConnectionStatus | null }>()
const emit = defineEmits<{ saved: [module: SettingsModule]; tested: [] }>()

const notifications = useNotifications()
const values = reactive<Record<string, string>>({})
const saving = ref(false)
const testing = ref(false)
const test = ref<ConnectionTest | null>(null)

watch(
  () => props.module,
  (module, previous) => {
    for (const item of module.fields) {
      values[item.field.key] = item.field.kind === 'Secret' ? '' : (item.value ?? '')
    }
    if (module.id !== previous?.id) test.value = null
  },
  { immediate: true },
)

const dirty = computed(() =>
  props.module.fields.some((item) =>
    item.field.kind === 'Secret' ? values[item.field.key] !== '' : values[item.field.key] !== (item.value ?? ''),
  ),
)

/** Wynik do pokazania: świeży test z tej strony, a gdy go nie ma — ostatni znany stan z serwera. */
const shown = computed(() => {
  if (test.value) return { ok: test.value.ok, message: test.value.message, at: null as string | null, fake: test.value.configured === false }
  if (!props.status) return null
  const neutral = props.status.state === 'Fake' || props.status.state === 'NotConfigured'
  return { ok: props.status.state === 'Ok', message: props.status.message, at: props.status.checkedAt, fake: neutral }
})

/** Wysyłamy tylko zmienione pola; pusty sekret = „bez zmian”. */
function changes() {
  const changed: Record<string, string | null> = {}
  for (const item of props.module.fields) {
    const value = values[item.field.key] ?? ''
    if (item.field.kind === 'Secret') {
      if (value !== '') changed[item.field.key] = value
    } else if (value !== (item.value ?? '')) {
      changed[item.field.key] = String(value)
    }
  }
  return changed
}

async function save() {
  saving.value = true
  try {
    emit('saved', await saveSettings(props.module.id, changes()))
    notifications.success(`Zapisano: ${props.module.title}`)
    return true
  } catch (error) {
    notifications.error(error)
    return false
  } finally {
    saving.value = false
  }
}

/** Po zapisie od razu sprawdzamy połączenie — żeby było widać, czy nowe dane działają. */
async function saveAndTest() {
  if (!(await save())) return
  if (props.module.canTest) await executeTest()
}

async function clearSecret(key: string) {
  try {
    emit('saved', await saveSettings(props.module.id, { [key]: '' }))
    notifications.success('Usunięto zapisany token')
  } catch (error) {
    notifications.error(error)
  }
}

/** Test sprawdza zapisane ustawienia, więc niezapisane zmiany najpierw zapisujemy. */
async function runTest() {
  if (dirty.value && !(await save())) return
  await executeTest()
}

async function executeTest() {
  testing.value = true
  try {
    test.value = await testSettings(props.module.id)
    emit('tested')
  } catch (error) {
    notifications.error(error)
  } finally {
    testing.value = false
  }
}
</script>

<template>
  <v-card class="module">
    <header class="head">
      <span class="icon"><v-icon :icon="module.icon" /></span>
      <div class="title">
        <h2>{{ module.title }}</h2>
        <p class="fh-muted">{{ module.description }}</p>
      </div>
      <v-chip v-if="status" :color="connectionPresentationOf[status.state].color" :prepend-icon="connectionPresentationOf[status.state].icon" variant="tonal">
        {{ connectionPresentationOf[status.state].label }}
      </v-chip>
    </header>

    <v-alert
      v-if="shown"
      :type="shown.fake ? 'info' : shown.ok ? 'success' : 'error'"
      variant="tonal"
      density="compact"
      :title="shown.fake ? (status?.state === 'Fake' ? 'Dane przykładowe (atrapa)' : 'Jeszcze nie ustawiono') : shown.ok ? 'Połączenie działa' : 'Połączenie nie działa'"
    >
      {{ shown.message }}
      <span v-if="shown.at" class="checked"> · sprawdzono {{ formatDateTime(shown.at) }}</span>
    </v-alert>

    <div class="fields">
      <template v-for="item in module.fields" :key="item.field.key">
        <v-select
          v-if="item.field.kind === 'Choice'"
          v-model="values[item.field.key]"
          :items="item.field.choices ?? []"
          item-title="label"
          item-value="value"
          :label="item.field.label"
          :hint="item.field.hint ?? undefined"
          :persistent-hint="!!item.field.hint"
        />

        <div v-else-if="item.field.kind === 'Secret'" class="secret">
          <v-text-field
            v-model="values[item.field.key]"
            type="password"
            autocomplete="new-password"
            :label="item.field.label"
            :placeholder="item.hasValue ? 'Wpisz nowy, żeby zmienić' : 'Wklej token'"
            persistent-placeholder
            hint="Zapisywany zaszyfrowany na tym komputerze, poza repozytorium. Nigdy nie jest pokazywany."
            persistent-hint
          />
          <v-chip :color="item.hasValue ? 'success' : 'warning'" :prepend-icon="item.hasValue ? 'mdi-lock-check' : 'mdi-lock-open-alert-outline'" variant="tonal" class="secret-state">
            {{ item.hasValue ? 'Token zapisany' : 'Brak tokenu' }}
          </v-chip>
          <v-btn v-if="item.hasValue" variant="text" size="small" prepend-icon="mdi-delete-outline" @click="clearSecret(item.field.key)">Usuń</v-btn>
        </div>

        <v-text-field
          v-else
          v-model="values[item.field.key]"
          :type="item.field.kind === 'Number' ? 'number' : 'text'"
          :min="item.field.kind === 'Number' ? 0 : undefined"
          :label="item.field.label"
          :hint="item.field.hint ?? undefined"
          :persistent-hint="!!item.field.hint"
          :prepend-inner-icon="item.field.kind === 'Folder' ? 'mdi-folder-outline' : undefined"
        />
      </template>
    </div>

    <div class="actions">
      <v-btn color="primary" variant="flat" prepend-icon="mdi-content-save-outline" :disabled="!dirty" :loading="saving" @click="saveAndTest">
        {{ module.canTest ? 'Zapisz i sprawdź' : 'Zapisz' }}
      </v-btn>
      <v-btn v-if="module.canTest" variant="tonal" prepend-icon="mdi-connection" :loading="testing" @click="runTest">Sprawdź połączenie</v-btn>
      <span v-if="dirty" class="fh-muted unsaved">Masz niezapisane zmiany.</span>
    </div>
  </v-card>
</template>

<style scoped>
.module {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
  padding: 1.5rem;
}

.head {
  display: flex;
  gap: 0.9rem;
  align-items: flex-start;
}

.title {
  flex: 1;
  min-width: 0;
}

.icon {
  display: grid;
  place-items: center;
  flex: none;
  width: 40px;
  height: 40px;
  border-radius: 11px;
  background: rgba(139, 92, 246, 0.18);
}

h2 {
  margin: 0;
  font-size: 1.2rem;
}

p {
  margin: 0.25rem 0 0;
  font-size: 0.875rem;
}

/* Stała siatka: każde pole w swoim wierszu, podpowiedź pod spodem — nic się nie przesuwa po teście ani zapisie. */
.fields {
  display: flex;
  flex-direction: column;
  gap: 1.1rem;
}

.secret {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto auto;
  gap: 0.75rem;
  align-items: start;
}

.secret-state {
  margin-top: 0.6rem;
}

.secret .v-btn {
  margin-top: 0.7rem;
}

.checked,
.unsaved {
  font-size: 0.8rem;
  opacity: 0.85;
}

.actions {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}
</style>
